using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIS.Domain.Enums;

namespace SIS.Tests.Postgres.DersKaydi;

// C-04: kayıt işlemi içinde çakışma olmayan bir veritabanı hatası kontenjan hatasına çevrilmez; olduğu gibi yayılır.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class C04_CakismaOlmayanHataTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const RegistrationStatus Bekliyor = RegistrationStatus.PendingAdvisorApproval;

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Aynı öğrencinin aynı şubeye ikinci gönderimi doğrulamayı geçer; ikinci kayıt satırı tekil dizine takılır.
    [Fact]
    public async Task C04_AyniOgrenciAyniSubeyeIkinciGonderim_TekilDizinHatasiYayilir_MevcutDegismez()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");
        await using (var db = fixture.YeniContext())
            Assert.True((await veri.GonderAsync(db, bir, sube)).Success);

        await using (var db = fixture.YeniContext())
        {
            var hata = await Assert.ThrowsAsync<DbUpdateException>(() => veri.GonderAsync(db, bir, sube));
            var pg = Assert.IsType<PostgresException>(hata.InnerException);
            Assert.Equal("23505", pg.SqlState);
            Assert.Equal("IX_CourseRegistrations_StudentId_CourseSectionId", pg.ConstraintName);
        }
        Assert.Equal(1, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (bir, Bekliyor) }, await veri.KayitlarAsync(sube));

        // Karşı örnek: başka bir öğrencinin aynı şubeye gönderimi kabul edilir.
        await using (var db = fixture.YeniContext())
            Assert.True((await veri.GonderAsync(db, iki, sube)).Success);
        Assert.Equal(2, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (bir, Bekliyor), (iki, Bekliyor) }.OrderBy(k => k.Item1), await veri.KayitlarAsync(sube));
    }
}
