using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Middleware;

/// <summary>
/// Tüm pipeline'da yakalanmayan exception'ları HTTP response'a anlamlı bir JSON gövde olarak
/// çevirir ve AuditLog'a tam bağlam (ex.Message + StackTrace + request meta + userId) yazar.
/// Dev/prod ayrımı sadece response'ta details alanını gizler — mesaj her durumda gönderilir.
///
/// Eşleme (Exception → HTTP status):
///   KeyNotFoundException      → 404
///   ArgumentException         → 400
///   InvalidOperationException → 400  (iş kuralı ihlali)
///   UnauthorizedAccessException → 403
///   DbUpdateException         → 500  (EF'in İngilizce iç mesajı yerine genel TR mesaj)
///   PostgresException 23P01/23514 → 409 (işlem sonunda düşen ertelemeli kısıt; TR mesaj)
///   diğerleri                 → 500
///
/// Response body: { error, status, type, traceId, details? }
/// Frontend extractError mevcut haliyle data.error'ı okuyor; başka değişiklik gerekmez.
/// Not: DbUpdateException dışında client'a gönderilen error = ex.Message; tam bağlam her durumda
/// AuditLog'a yazılır (DbUpdateException'da da ham mesaj + stack log'a gider, client'a gitmez).
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private const int StackTraceMaxLength = 4000;

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var status = MapStatusCode(ex);

        _logger.LogError(
            ex,
            "Unhandled exception for {Method} {Path}. Status={Status}. TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path.Value,
            status,
            context.TraceIdentifier);

        // Yalnızca gerçek sunucu hatalarını (5xx) "SystemError" olarak AuditLog'a yaz.
        // İş kuralı ihlalleri (InvalidOperationException/ArgumentException → 400),
        // bulunamayan kayıt (404) ve yetki reddi (403) beklenen 4xx akışlarıdır; bunları
        // SystemError saymak anomali alarmlarını ve hata istatistiklerini şişirir,
        // ayrıca her validasyon hatasında 4000 karakterlik stack trace yazılmasına yol açar.
        if (status >= StatusCodes.Status500InternalServerError)
            await TryWriteAuditLogAsync(context, ex, status);

        if (context.Response.HasStarted)
        {
            // Response already flushed — sadece logla, body yazma denemesi yapma.
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";

        // DbUpdateException'ın ex.Message'ı EF'in jenerik İngilizce metnidir ("An error occurred
        // while saving the entity changes...") — client'a anlamsız. Genel TR mesaja çevir; ham
        // detay zaten AuditLog'a + ILogger'a yazıldı.
        // Ertelemeli kısıt (ör. tek etkin dönem) işlem sonunda düşer; hata DbUpdateException'a sarılmadan
        // PostgresException olarak gelir. Ham PostgreSQL iletisi de client'a gönderilmez.
        var clientError = ex switch
        {
            Npgsql.PostgresException { SqlState: "23P01" or "23514" } =>
                "İşlem bir veritabanı kuralına aykırı olduğu için kaydedilmedi (örneğin aynı anda ikinci bir etkin dönem).",
            DbUpdateException or Npgsql.PostgresException =>
                "Veritabanı işlemi sırasında bir hata oluştu. Lütfen tekrar deneyin; sorun sürerse yöneticinize başvurun.",
            _ => ex.Message,
        };

        var body = new Dictionary<string, object?>
        {
            ["error"] = clientError,
            ["status"] = status,
            ["type"] = ex.GetType().Name,
            ["traceId"] = context.TraceIdentifier,
        };

        if (_environment.IsDevelopment())
        {
            var stackTrace = ex.StackTrace ?? string.Empty;
            body["details"] = stackTrace.Length > StackTraceMaxLength
                ? stackTrace[..StackTraceMaxLength]
                : stackTrace;
        }

        await context.Response.WriteAsJsonAsync(body, JsonOptions);
    }

    private static int MapStatusCode(Exception ex) => ex switch
    {
        KeyNotFoundException => StatusCodes.Status404NotFound,
        UnauthorizedAccessException => StatusCodes.Status403Forbidden,
        ArgumentException => StatusCodes.Status400BadRequest,
        InvalidOperationException => StatusCodes.Status400BadRequest,
        Npgsql.PostgresException { SqlState: "23P01" or "23514" } => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    private async Task TryWriteAuditLogAsync(HttpContext context, Exception ex, int status)
    {
        try
        {
            using var scope = context.RequestServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var stackTrace = ex.StackTrace ?? string.Empty;
            var truncatedStack = stackTrace.Length > StackTraceMaxLength
                ? stackTrace[..StackTraceMaxLength]
                : stackTrace;

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = GetUserId(context),
                Action = "SystemError",
                EntityType = "System",
                EntityId = null,
                OldValue = null,
                NewValue = JsonSerializer.Serialize(new
                {
                    exceptionType = ex.GetType().Name,
                    message = ex.Message,
                    stackTrace = truncatedStack,
                    status,
                    traceId = context.TraceIdentifier,
                    endpoint = context.Request.Path.Value,
                    method = context.Request.Method,
                    queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                }, JsonOptions),
                Timestamp = DateTime.UtcNow,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            });

            await dbContext.SaveChangesAsync();
        }
        catch (Exception logEx)
        {
            // Loglama hatası asıl response'u bozmasın; sadece ILogger'a düş.
            _logger.LogWarning(logEx, "AuditLog yazımı başarısız (asıl exception zaten loglandı).");
        }
    }

    private static int? GetUserId(HttpContext context)
    {
        var claim = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out var uid) ? uid : null;
    }
}
