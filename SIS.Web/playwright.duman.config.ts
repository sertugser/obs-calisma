import { defineConfig } from '@playwright/test';

// Duman takımı: ayrı yapılandırma; mevcut e2e takımına dokunmaz. Kurulum ve çalıştırma: e2e-duman/calistir.ps1.
const ci = !!process.env.CI;

export default defineConfig({
  testDir: './e2e-duman',
  globalSetup: './e2e-duman/kurulum.ts',
  fullyParallel: true,
  workers: ci ? 2 : 4,
  retries: 0,
  forbidOnly: ci,
  reporter: [['list'], ['html', { open: 'never' }]],
  timeout: 30_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: process.env.DUMAN_ADRES,
    locale: 'tr-TR',
    timezoneId: 'Europe/Istanbul',
    // İz ve video kapalı: doldurulan parola iz dosyasına yazılmasın.
    trace: 'off',
    video: 'off',
    screenshot: 'only-on-failure',
    viewport: { width: 1600, height: 900 },
  },
});
