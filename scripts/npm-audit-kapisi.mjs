#!/usr/bin/env node
// npm denetim kapısı: `npm audit --audit-level=high` yerine çalışır.
// Yüksek ya da kritik her açık kapıyı düşürür; yalnız ci/npm-audit-istisnalar.txt
// içinde kimliğiyle (GHSA + paket) ve süresi dolmamış olarak yazılı olanlar geçer.
//
// Kullanım (SIS.Web klasöründen):
//   node ../scripts/npm-audit-kapisi.mjs [--istisnalar <dosya>]
//
// Çıkış 1:
//   (a) listede olmayan yüksek/kritik açık;
//   (b) süresi dolmuş istisna;
//   (c) istisna paketinin npm'deki en son sürümü listedekinden farklı;
//   (d) npm audit çıktısı okunamıyor ya da npm view başarısız;
//   (e) istisna dosyasında biçimi bozuk satır.
// Çıkış 0 + uyarı: listede olup npm audit'te görünmeyen istisna.
// Orta ve düşük açıklar yalnız raporlanır.

import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const betikKlasoru = path.dirname(fileURLToPath(import.meta.url));
const varsayilanListe = path.join(betikKlasoru, '..', 'ci', 'npm-audit-istisnalar.txt');
const ghActions = process.env.GITHUB_ACTIONS === 'true';
const ENGELLEYICI_DEGIL = new Set(['info', 'low', 'moderate']);

const hatalar = [];
const uyarilar = [];

function hata(mesaj) {
  hatalar.push(mesaj);
  console.log(ghActions ? `::error::${mesaj}` : `HATA: ${mesaj}`);
}

function uyari(mesaj) {
  uyarilar.push(mesaj);
  console.log(ghActions ? `::warning::${mesaj}` : `UYARI: ${mesaj}`);
}

function bitir() {
  console.log('');
  if (hatalar.length > 0) {
    console.log(`SONUÇ: KALDI (${hatalar.length} hata, ${uyarilar.length} uyarı)`);
    process.exit(1);
  }
  console.log(`SONUÇ: GEÇTİ (${uyarilar.length} uyarı)`);
  process.exit(0);
}

function npm(argumanlar) {
  return spawnSync('npm', argumanlar, {
    cwd: process.cwd(),
    encoding: 'utf8',
    maxBuffer: 64 * 1024 * 1024,
    shell: process.platform === 'win32',
  });
}

function argumanAl(ad) {
  const i = process.argv.indexOf(ad);
  if (i === -1) return undefined;
  const deger = process.argv[i + 1];
  if (!deger || deger.startsWith('--')) {
    hata(`${ad} için değer verilmedi`);
    bitir();
  }
  return deger;
}

function gecerliTarih(metin) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(metin)) return false;
  const t = new Date(`${metin}T00:00:00Z`);
  return !Number.isNaN(t.getTime()) && t.toISOString().slice(0, 10) === metin;
}

// --- İstisna listesi ---------------------------------------------------------

const listeYolu = argumanAl('--istisnalar') ?? varsayilanListe;
let listeMetni;
try {
  listeMetni = readFileSync(listeYolu, 'utf8').replace(/^﻿/, '');
} catch (e) {
  hata(`istisna dosyası okunamadı: ${listeYolu} (${e.code ?? e.message})`);
  bitir();
}

const GHSA_DESENI = /^GHSA-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}$/;
const PAKET_DESENI = /^(@[a-z0-9~-][a-z0-9._~-]*\/)?[a-z0-9~-][a-z0-9._~-]*$/;
const SURUM_DESENI = /^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$/;

const istisnalar = new Map(); // GHSA -> { paket, surum, bitis, gerekce, satir }
listeMetni.split(/\r?\n/).forEach((hamSatir, i) => {
  const satir = hamSatir.trim();
  const no = i + 1;
  if (satir === '' || satir.startsWith('#')) return;
  const alanlar = satir.split('|').map((a) => a.trim());
  if (alanlar.length !== 5) {
    hata(`istisna dosyası satır ${no}: 5 alan bekleniyordu, ${alanlar.length} var`);
    return;
  }
  const [ghsa, paket, surum, bitis, gerekce] = alanlar;
  const sorunlar = [];
  if (!GHSA_DESENI.test(ghsa)) sorunlar.push(`GHSA kimliği geçersiz: "${ghsa}"`);
  if (!PAKET_DESENI.test(paket)) sorunlar.push(`paket adı geçersiz: "${paket}"`);
  if (!SURUM_DESENI.test(surum)) sorunlar.push(`sürüm geçersiz: "${surum}"`);
  if (!gecerliTarih(bitis)) sorunlar.push(`bitiş tarihi YYYY-AA-GG değil: "${bitis}"`);
  if (gerekce === '') sorunlar.push('gerekçe boş');
  if (istisnalar.has(ghsa)) sorunlar.push(`${ghsa} listede birden fazla kez geçiyor`);
  if (sorunlar.length > 0) {
    hata(`istisna dosyası satır ${no}: ${sorunlar.join('; ')}`);
    return;
  }
  istisnalar.set(ghsa, { paket, surum, bitis, gerekce, satir: no });
});

// --- npm audit ---------------------------------------------------------------

const denetim = npm(['audit', '--json']);
let rapor;
try {
  rapor = JSON.parse(denetim.stdout);
} catch {
  hata(`npm audit çıktısı JSON olarak okunamadı (çıkış kodu ${denetim.status})`);
  if (denetim.stderr) console.log(denetim.stderr.trim());
  bitir();
}
if (!rapor || rapor.error || typeof rapor.vulnerabilities !== 'object' || rapor.vulnerabilities === null) {
  hata(`npm audit hata döndürdü: ${JSON.stringify(rapor?.error ?? 'vulnerabilities alanı yok')}`);
  bitir();
}

// Kök duyuruları tekilleştir (via içindeki nesneler).
const duyurular = new Map(); // anahtar -> { ghsa, paket, onem, baslik, etkilenen:Set }
for (const [ad, acik] of Object.entries(rapor.vulnerabilities)) {
  for (const kaynak of acik.via ?? []) {
    if (typeof kaynak !== 'object' || kaynak === null) continue;
    const ghsa = (String(kaynak.url ?? '').match(/GHSA-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}/) ?? [])[0];
    const kimlik = ghsa ?? `kaynak-${kaynak.source ?? '?'}`;
    const anahtar = `${kimlik}|${kaynak.name}`;
    if (!duyurular.has(anahtar)) {
      duyurular.set(anahtar, {
        ghsa: kimlik,
        paket: kaynak.name,
        onem: String(kaynak.severity ?? 'bilinmiyor'),
        baslik: kaynak.title ?? '',
        etkilenen: new Set(),
      });
    }
    duyurular.get(anahtar).etkilenen.add(ad);
  }
}

const bugun = new Date().toISOString().slice(0, 10);
const kullanilan = new Set();
const sayac = rapor.metadata?.vulnerabilities ?? {};
console.log(
  `npm audit: ${sayac.total ?? '?'} açık (kritik ${sayac.critical ?? '?'}, yüksek ${sayac.high ?? '?'}, ` +
    `orta ${sayac.moderate ?? '?'}, düşük ${sayac.low ?? '?'}); ${duyurular.size} kök duyuru. Bugün: ${bugun}`,
);
console.log('');

const siraliDuyurular = [...duyurular.values()].sort((a, b) => a.paket.localeCompare(b.paket));
for (const d of siraliDuyurular) {
  const satirBasi = `${d.paket}  ${d.onem}  ${d.ghsa}`;
  if (ENGELLEYICI_DEGIL.has(d.onem)) {
    console.log(`${satirBasi}  rapor (engellemez)`);
    continue;
  }
  const ist = istisnalar.get(d.ghsa);
  if (!ist || ist.paket !== d.paket) {
    console.log(`${satirBasi}  ENGEL`);
    hata(`${d.ghsa} (${d.paket}, ${d.onem}) istisna listesinde yok: ${d.baslik}`);
    continue;
  }
  kullanilan.add(d.ghsa);
  if (bugun > ist.bitis) {
    console.log(`${satirBasi}  ENGEL (istisnanın süresi ${ist.bitis} tarihinde doldu)`);
    hata(`${d.ghsa} (${d.paket}) istisnasının süresi doldu (${ist.bitis}): yeniden değerlendir`);
    continue;
  }
  const gorunum = npm(['view', d.paket, 'version']);
  const enSon = (gorunum.stdout ?? '').trim();
  if (gorunum.status !== 0 || !SURUM_DESENI.test(enSon)) {
    console.log(`${satirBasi}  ENGEL (npm view başarısız)`);
    hata(`npm view ${d.paket} version başarısız (çıkış kodu ${gorunum.status})`);
    continue;
  }
  if (enSon !== ist.surum) {
    console.log(`${satirBasi}  ENGEL (yeni sürüm çıktı: ${enSon}, listede ${ist.surum})`);
    hata(`${d.paket} için yeni sürüm çıktı (${enSon}): yükselt ve istisnayı kaldır`);
    continue;
  }
  console.log(`${satirBasi}  istisna (bitiş ${ist.bitis}; en son sürüm ${enSon})`);
}

for (const [ghsa, ist] of istisnalar) {
  if (bugun > ist.bitis && !kullanilan.has(ghsa)) {
    hata(`istisna dosyası satır ${ist.satir}: ${ghsa} (${ist.paket}) süresi doldu (${ist.bitis})`);
  } else if (!kullanilan.has(ghsa)) {
    uyari(`${ghsa} (${ist.paket}) npm audit'te görünmüyor: listeden çıkar (satır ${ist.satir})`);
  }
}

bitir();
