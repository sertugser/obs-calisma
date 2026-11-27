import { randomBytes } from 'node:crypto';
import { test, expect, METIN, formuGonder, girisYap, menu, ustBaslik, veriYaniti } from './yardimci';

// Uçtan uca duman takımı: salt okunur; her test kendi bağlamında arayüzden giriş yapar.

test('yönetici girişi ana sayfayı ve yönetim menüsünü gösterir', async ({ page }) => {
  await girisYap(page, 'YONETICI');
  await expect(menu(page).getByRole('button', { name: METIN.yonetimMenusu, exact: true })).toBeVisible();
});

test('öğrenci girişi öğrenci menülerini gösterir, yönetim menüsünü göstermez', async ({ page }) => {
  await girisYap(page, 'OGRENCI');
  for (const ad of METIN.ogrenciMenuleri) {
    await expect(menu(page).getByRole('button', { name: ad, exact: true })).toBeVisible();
  }
  await expect(menu(page).getByRole('button', { name: METIN.yonetimMenusu, exact: true })).toHaveCount(0);
});

test('yanlış parolayla giriş hata iletisi gösterir ve giriş sayfasında kalır', async ({ page }) => {
  const yanit = page.waitForResponse((r) => r.request().method() === 'POST' && new URL(r.url()).pathname === '/api/auth/login');
  yanit.catch(() => undefined); // yardımcı kırılırsa ikinci bir zaman aşımı hatası üretmesin
  await formuGonder(page, 'DANISMAN', `Yanlis1!${randomBytes(8).toString('hex')}`);
  const cevap = await yanit;
  // Sunucu boş parolaya da aynı 401'i verir; giden istekte parola alanı dolu olmalı (değer iletiye yazılmaz).
  const gonderilen = (cevap.request().postDataJSON() as { password?: unknown }).password;
  expect(typeof gonderilen === 'string' && gonderilen.length > 0, 'giriş isteğinde parola alanı dolu').toBe(true);
  expect(cevap.status()).toBe(401);
  await expect(page.getByRole('alert').filter({ hasText: METIN.yanlisParolaIletisi })).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
});

test('yönetim sayfası öğrenciye 403 gösterir, aynı adres yöneticiye açılır', async ({ page }) => {
  await girisYap(page, 'OGRENCI');
  const istekler: string[] = [];
  page.on('request', (r) => istekler.push(new URL(r.url()).pathname));
  await page.goto('/kullanici-yonetimi');
  await expect(page).toHaveURL(/\/forbidden$/);
  await expect(page.getByRole('heading', { name: METIN.yetkiYokBasligi, level: 1 })).toBeVisible();
  await expect(page.getByText(METIN.yetkiYokIletisi)).toBeVisible();
  expect(istekler, 'öğrenci yönlendirilirken kullanıcı listesi istenmemeli').not.toContain('/api/admin/users');

  await page.evaluate(() => localStorage.clear());
  await girisYap(page, 'YONETICI');
  const yanit = veriYaniti(page, '/api/admin/users');
  await page.goto('/kullanici-yonetimi');
  expect((await yanit).status()).toBe(200);
  await expect(page).toHaveURL(/\/kullanici-yonetimi$/);
  await expect(ustBaslik(page)).toHaveText(METIN.kullaniciTanimlama);
  await expect(page.getByRole('button', { name: METIN.rolleriDuzenle }).first()).toBeVisible();
});

test('öğrenci transkript sayfasını açar', async ({ page }) => {
  await girisYap(page, 'OGRENCI');
  const yanit = veriYaniti(page, '/api/student/transcript');
  await page.goto('/report/transkript');
  expect((await yanit).status()).toBe(200);
  await expect(page.getByRole('heading', { name: METIN.transkript, level: 1 })).toBeVisible();
  await expect(page.getByRole('row', { name: METIN.ogrenciNoSatiri })).toBeVisible();
  // Örnek veride notlar geçmiş dönemlerde (Adım 3.7): transkriptte ders listesi dolu, boş durum iletisi yok.
  await expect(page.getByText(METIN.transkriptBos)).toHaveCount(0);
});

test('öğrenci ders kayıt sayfasını açar', async ({ page }) => {
  await girisYap(page, 'OGRENCI');
  const yanit = veriYaniti(page, '/api/course/registration-catalog');
  await page.goto('/ders-kayit');
  expect((await yanit).status()).toBe(200);
  await expect(ustBaslik(page)).toHaveText(METIN.dersKayit);
  await expect(page.getByRole('heading', { name: METIN.mufredatDersleri, level: 3 })).toBeVisible();
  // "Müfredat Dersleri" başlığını ve tabloyu birlikte içeren en içteki kapsayıcı.
  const mufredat = page.locator('div')
    .filter({ has: page.getByRole('heading', { name: METIN.mufredatDersleri, level: 3 }) })
    .filter({ has: page.getByRole('table') })
    .last();
  await expect(mufredat.getByRole('row').filter({ has: page.getByRole('cell') }).first()).toBeVisible();
});

test('öğrenci not listesi sayfasını açar', async ({ page }) => {
  await girisYap(page, 'OGRENCI');
  const yanit = veriYaniti(page, '/api/registrations/history');
  await page.goto('/not-listesi');
  expect((await yanit).status()).toBe(200);
  await expect(ustBaslik(page)).toHaveText(METIN.notListesi);
  await expect(page.getByRole('button', { name: METIN.sinavIstatistigi }).first()).toBeVisible();
});

test('öğrenci ders programı sayfasını açar', async ({ page }) => {
  await girisYap(page, 'OGRENCI');
  const yanit = veriYaniti(page, '/api/scheduling/student/me/timetable/terms');
  await page.goto('/ders-programim');
  expect((await yanit).status()).toBe(200);
  await expect(ustBaslik(page)).toHaveText(METIN.dersProgramim);
  // Örnek veride öğrencinin etkin dönemde kaydı yok (kayıtlar geçmiş dönemlerde); boş durum gösterilir.
  await expect(page.getByText(METIN.dersProgramiBos)).toBeVisible();
});
