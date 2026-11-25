import { test as temel, expect, type BrowserContext, type Locator, type Page, type Response } from '@playwright/test';

// Arayüzün bugünkü dilindeki görünen metinler tek yerde.
export const METIN = {
  epostaEtiketi: 'E-POSTA / KULLANICI ADI',
  sifreEtiketi: 'ŞİFRE',
  girisDugmesi: 'Giriş Yap',
  yanlisParolaIletisi: 'Invalid email or password.',
  anaSayfa: 'Ana Sayfa',
  yonetimMenusu: 'Yönetim',
  ogrenciMenuleri: ['Hesabım', 'Ders ve Dönem İşlemleri'],
  yetkiYokBasligi: '403',
  yetkiYokIletisi: 'Erişim Yetkiniz Yok',
  kullaniciTanimlama: 'Kullanıcı Tanımlama',
  rolleriDuzenle: 'Rolleri Düzenle',
  transkript: 'Transkript',
  ogrenciNoSatiri: /Öğrenci No \d+/,
  transkriptBos: 'Bu öğrenci için henüz transkriptte gösterilecek harf notlu ders kaydı bulunmuyor.',
  dersKayit: 'Ders Kayıt',
  mufredatDersleri: 'Müfredat Dersleri',
  notListesi: 'Not Listesi',
  sinavIstatistigi: /sınav istatistiklerini aç$/,
  dersProgramim: 'Ders Programım',
  dersProgramiBos: 'Bu dönem için kayıtlı ders bulunmuyor.',
} as const;

export type Rol = 'YONETICI' | 'OGRENCI' | 'DANISMAN';

function ortam(ad: string): string {
  const deger = process.env[ad];
  if (!deger) throw new Error(`${ad} ortam değişkeni yok (kurulum adımı çalışmadı mı?)`);
  return deger;
}

export const parola = (): string => ortam('DUMAN_PAROLA');

// Alan değerini sayfa içinden yazar: fill() değeri rapordaki adım başlığına yazar.
const alanaYaz = (alan: Locator, deger: string) =>
  alan.evaluate((el: HTMLInputElement, d: string) => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set?.call(el, d);
    el.dispatchEvent(new Event('input', { bubbles: true }));
  }, deger);

// Arayüzden giriş formunu doldurur ve gönderir; sonucu çağıran iddia eder.
// Kırılan iddiaya Playwright sayfa dökümünü ekler; bu yüzden düğme görünür olmadan parola
// alana konmaz ve basıştan hemen sonra alanlar boşaltılır.
export async function formuGonder(page: Page, rol: Rol, sifre: string): Promise<void> {
  await page.goto('/login');
  const eposta = page.getByLabel(METIN.epostaEtiketi);
  const sifreAlani = page.getByLabel(METIN.sifreEtiketi);
  const dugme = page.getByRole('button', { name: METIN.girisDugmesi, exact: true });
  await expect(dugme).toBeVisible();
  await alanaYaz(eposta, ortam(`DUMAN_EPOSTA_${rol}`));
  await alanaYaz(sifreAlani, sifre);
  try {
    await dugme.click();
  } finally {
    // Gönderim yapıldı; sayfa gezinmişse alan zaten yoktur.
    await alanaYaz(sifreAlani, '').catch(() => undefined);
    await alanaYaz(eposta, '').catch(() => undefined);
  }
}
// Doğru parolayla giriş yapar ve ana sayfanın açıldığını bekler.
export async function girisYap(page: Page, rol: Rol): Promise<void> {
  await formuGonder(page, rol, parola());
  await expect(page).toHaveURL(/\/$/);
  await expect(ustBaslik(page)).toHaveText(METIN.anaSayfa);
}

export const ustBaslik = (page: Page) => page.getByRole('banner').getByRole('heading', { level: 2 });
export const menu = (page: Page) => page.getByRole('complementary').getByRole('navigation');

// Gezinmeden ÖNCE çağrılır: verilen yola giden ilk GET yanıtını bekler.
export function veriYaniti(page: Page, yol: string): Promise<Response> {
  return page.waitForResponse((r) => r.request().method() === 'GET' && new URL(r.url()).pathname === yol);
}

// Bağlamdaki /api/ yanıtlarından 5xx olanları toplar.
export function sunucuHatalariniIzle(context: BrowserContext): string[] {
  const hatalar: string[] = [];
  context.on('response', (r) => {
    const yol = new URL(r.url()).pathname;
    if (yol.startsWith('/api/') && r.status() >= 500) hatalar.push(`${r.status()} ${yol}`);
  });
  return hatalar;
}

// Her testte ortak koruma: testin bağlamında 5xx yanıt olmamalı.
export const test = temel.extend<{ sunucuHatalari: string[] }>({
  sunucuHatalari: [
    async ({ context }, use) => {
      const hatalar = sunucuHatalariniIzle(context);
      await use(hatalar);
      expect(hatalar, 'API 5xx yanıtı').toEqual([]);
    },
    { auto: true },
  ],
});

export { expect };
