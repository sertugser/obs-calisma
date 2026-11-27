using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// Faz 3a — öğrenci belge tipleri referans kataloğu seed'i.
/// Idempotent merge: <see cref="Defaults"/> içindeki Code'su tabloda olmayanları ekler,
/// mevcut kayıtlara dokunmaz. Bu katalog için admin CRUD yoktur (sadece seed).
/// </summary>
public static class DocumentTypeSeed
{
    public static readonly DocumentType[] Defaults =
    {
        new() { Code = "LISE_DIPLOMA",      Name = "Lise Diploması",            IsRequired = true,  SortOrder = 1 },
        new() { Code = "OSYS_SONUC",        Name = "ÖSYS/YKS Sonuç Belgesi",    IsRequired = true,  SortOrder = 2 },
        new() { Code = "NUFUS",             Name = "Nüfus Cüzdanı Fotokopisi",  IsRequired = true,  SortOrder = 3 },
        new() { Code = "FOTO",              Name = "Vesikalık Fotoğraf",        IsRequired = true,  SortOrder = 4 },
        new() { Code = "ASKERLIK",          Name = "Askerlik Durum Belgesi",    IsRequired = false, SortOrder = 5 },
        new() { Code = "SAGLIK",            Name = "Sağlık Raporu",             IsRequired = false, SortOrder = 6 },
        new() { Code = "DENKLIK",           Name = "Denklik Belgesi",           IsRequired = false, SortOrder = 7 },
        new() { Code = "ONCEKI_TRANSKRIPT", Name = "Önceki Kurum Transkripti",  IsRequired = false, SortOrder = 8 },
    };

    /// <summary>
    /// Idempotent merge — sadece Code'su tabloda olmayanları ekler.
    /// </summary>
    public static async Task SeedAsync(AppDbContext context, CancellationToken ct = default)
    {
        await Katalog.KatalogEsitleyici.EsitleAsync(context, new Katalog.KatalogTuru<DocumentType, DocumentType>
        {
            Ad = "belge-tipi",
            Kayitlar = Defaults,
            KayitAnahtari = d => d.Code,
            VarlikAnahtari = d => d.Code,
            Olustur = (d, _) => new DocumentType { Code = d.Code, Name = d.Name, IsRequired = d.IsRequired, SortOrder = d.SortOrder },
        }, DateTime.UtcNow, ct);
    }
}
