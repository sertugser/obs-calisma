#!/usr/bin/env node
// Admin sayfa envanterini otomatik üretir.
// Çıktı: docs/CODEMAPS/admin-pages-inventory.md
//
// Çalıştırma: pnpm codemap:generate  (veya)  node scripts/generate-codemap.mjs
//
// Taranan kaynaklar:
//   - src/App.tsx                    → Route → Component eşlemesi + import'lar
//   - SIS.Infrastructure/Persistence/Katalog/MenuKatalogu.cs → modüller (gruplar) → sayfalar
//   - SIS.Infrastructure/Persistence/Seeds/RolePermissionSeed.cs → permission → roller
//   - src/pages/*.tsx                → sekmeler, dialog/sheet sayısı, hook kullanımı
//   - src/hooks/*.ts & src/api/*.ts  → API endpoint'leri
//
// Not: Menü artık DB-driven (MenuModules/MenuItems). Eski SidebarMenuGroups.ts kaldırıldığı
// için sidebar yapısı doğrudan seed dosyalarından okunur — tek gerçek kaynak budur.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const WEB_ROOT = path.resolve(__dirname, '..');
const WEB_SRC = path.join(WEB_ROOT, 'src');
const REPO_ROOT = path.resolve(WEB_ROOT, '..');
const SEEDS_DIR = path.join(REPO_ROOT, 'SIS.Infrastructure', 'Persistence', 'Seeds');
const OUTPUT = path.join(REPO_ROOT, 'docs', 'CODEMAPS', 'admin-pages-inventory.md');

const read = (p) => fs.readFileSync(p, 'utf8');

// RolePermissionSeed rol adı → codemap kısa adı
const ROLE_DISPLAY = {
  SystemAdministrator: 'systemAdmin',
  Dean: 'dean',
  DepartmentManager: 'departmentManager',
  Instructor: 'instructor',
  AcademicAdvisor: 'advisor',
  AdministrativeStaff: 'administrativeStaff',
  Student: 'student',
};

// ─── 1) App.tsx: import + Route eşlemesi ────────────────────────
function parseApp() {
  const content = read(path.join(WEB_SRC, 'App.tsx'));
  const componentToFile = {};
  // import CourseOpeningPage from './pages/CourseOpeningPage';
  const importRegex = /import\s+(\w+)\s+from\s+['"](\.\/pages\/[^'"]+)['"];?/g;
  let m;
  while ((m = importRegex.exec(content)) !== null) {
    componentToFile[m[1]] = m[2].replace(/^\.\//, '');
  }
  // <Route path="ders-acma-islemleri" element={<CourseOpeningPage ...
  // veya <Route path="/login" element={<LoginPage ...
  const pathToComponent = {}; // '/ders-acma-islemleri' → 'CourseOpeningPage'
  const routeRegex = /path=["']([^"']+)["']\s+element=\{<(\w+)/g;
  while ((m = routeRegex.exec(content)) !== null) {
    const rawPath = m[1];
    const normalized = rawPath.startsWith('/') ? rawPath : '/' + rawPath;
    pathToComponent[normalized] = m[2];
    // not-girisi/:sectionId gibi parametreli route'ların da temel yolunu ekle
    const basePath = normalized.split('/:')[0];
    if (basePath !== normalized && !pathToComponent[basePath]) {
      pathToComponent[basePath] = m[2];
    }
  }
  return { componentToFile, pathToComponent };
}

// ─── 3) Sidebar: role → gruplar → sayfalar ──────────────────────
function findMatchingBracket(text, startIdx, openCh, closeCh) {
  let depth = 1;
  let i = startIdx;
  while (i < text.length && depth > 0) {
    if (text[i] === openCh) depth++;
    else if (text[i] === closeCh) depth--;
    i++;
  }
  return i - 1; // index of matching close bracket
}

// RolePermissionSeed.cs: rol → sahip olduğu permission key kümesi
function parseRolePermissions() {
  const content = read(path.join(SEEDS_DIR, 'RolePermissionSeed.cs'));
  const roleToKeys = new Map(); // "Dean" → Set("menu.x.view", ...)
  const roleRe = /\["([A-Za-z]+)"\]\s*=\s*new\[\]\s*\{/g;
  let rm;
  while ((rm = roleRe.exec(content)) !== null) {
    const role = rm[1];
    const openIdx = rm.index + rm[0].length;
    const closeIdx = findMatchingBracket(content, openIdx, '{', '}');
    const block = content.slice(openIdx, closeIdx);
    const keys = new Set();
    for (const km of block.matchAll(/"(menu\.[^"]+|system\.[^"]+|lookup\.[^"]+)"/g)) {
      keys.add(km[1]);
    }
    roleToKeys.set(role, keys);
  }
  return roleToKeys;
}

// Bir permission key'e sahip rolleri çöz (SystemAdministrator her şeye sahiptir).
// Modülde AllowedRolesCsv varsa görünürlük o role listesiyle kesişir (sysadmin shortcut'ı bypass eder).
function resolveRoles(permKey, allowedRolesCsv, roleToKeys) {
  const granted = new Set();
  for (const [role, keys] of roleToKeys) {
    if (keys.has(permKey)) granted.add(role);
  }
  if (allowedRolesCsv) {
    const allowed = new Set(allowedRolesCsv.split(',').map(s => s.trim()).filter(Boolean));
    // AllowedRolesCsv modül görünürlüğünü kısar; sysadmin yalnızca CSV'de varsa görür.
    if (allowed.has('SystemAdministrator')) granted.add('SystemAdministrator');
    const intersected = new Set([...granted].filter(r => allowed.has(r)));
    return [...intersected].map(r => ROLE_DISPLAY[r] ?? r).sort();
  }
  // CSV yoksa: permission'a sahip roller + her zaman sysadmin
  granted.add('SystemAdministrator');
  return [...granted].map(r => ROLE_DISPLAY[r] ?? r).sort();
}

// MenuKatalogu.cs: modüller (gruplar) → sayfalar (route + label + permission)
function parseMenuStructure(roleToKeys) {
  const content = read(path.join(REPO_ROOT, 'SIS.Infrastructure', 'Persistence', 'Katalog', 'MenuKatalogu.cs'));
  const str = '(?:"([^"]*)"|null)';

  // 1) Modüller: new("slug", "ust"|null, "Etiket", "Simge"|null, sira, "Roller"|null)
  const modules = new Map(); // slug → { label, allowedRolesCsv, sortOrder }
  const moduleRe = new RegExp(String.raw`new\(\s*"([^"]+)"\s*,\s*${str}\s*,\s*"([^"]+)"\s*,\s*${str}\s*,\s*(\d+)\s*,\s*${str}\s*\)`, 'g');
  const modulBolumu = content.slice(content.indexOf('Moduller ='), content.indexOf('Ogeler ='));
  let mm;
  while ((mm = moduleRe.exec(modulBolumu)) !== null) {
    const [, slug, , label, , sortOrder, allowedRolesCsv] = mm;
    modules.set(slug, { label, allowedRolesCsv: allowedRolesCsv ?? null, sortOrder: parseInt(sortOrder, 10) });
  }

  // 2) Öğeler: new("modulSlug", "Etiket", "/yol", "slug", sira, "izin"[, true])
  const byRoute = new Map(); // route → { label, groupLabels:Set, roles:Set, sortOrder }
  const itemRe = /new\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*\d+\s*,\s*"([^"]+)"/g;
  const ogeBolumu = content.slice(content.indexOf('Ogeler ='));
  let im;
  while ((im = itemRe.exec(ogeBolumu)) !== null) {
    const [, moduleSlug, label, route, , permKey] = im;
    const mod = modules.get(moduleSlug);
    if (!mod) continue;
    const roles = resolveRoles(permKey, mod.allowedRolesCsv, roleToKeys);
    const info = byRoute.get(route) ?? {
      label,
      groupLabels: new Set(),
      roles: new Set(),
      sortOrder: mod.sortOrder,
    };
    info.groupLabels.add(mod.label);
    for (const r of roles) info.roles.add(r);
    info.sortOrder = Math.min(info.sortOrder, mod.sortOrder);
    byRoute.set(route, info);
  }
  return byRoute;
}

// ─── 4) Sayfa dosyası taraması ─────────────────────────────────
function scanPageFile(filePath) {
  if (!fs.existsSync(filePath)) return null;
  const content = read(filePath);

  const tabs = [...content.matchAll(/<TabsContent\s+value\s*=\s*["'`]([^"'`]+)["'`]/g)]
    .map(m => m[1]);

  const dialogs = [...content.matchAll(/<Dialog\s[^>]*open\s*=/g)].length;
  const sheets = [...content.matchAll(/<Sheet\s[^>]*open\s*=/g)].length;
  const alertDialogs = [...content.matchAll(/<AlertDialog\s[^>]*open\s*=/g)].length;

  // Import edilen hook'ları bul
  const importHookRe = /import\s+\{([^}]+)\}\s+from\s+['"]\.\.\/hooks\/([^'"]+)['"]/g;
  const importedHooks = new Set();
  const hookFileMap = {}; // hookName → hook file
  let m;
  while ((m = importHookRe.exec(content)) !== null) {
    const hookFile = m[2];
    const names = m[1].split(',').map(s => s.trim().split(' as ')[0].trim()).filter(Boolean);
    for (const n of names) {
      if (/^use[A-Z]/.test(n)) {
        importedHooks.add(n);
        hookFileMap[n] = hookFile;
      }
    }
  }

  // Import edilen api fonksiyonları
  const importApiRe = /import\s+\{([^}]+)\}\s+from\s+['"]\.\.\/api\/([^'"]+)['"]/g;
  const apiFileMap = {};
  while ((m = importApiRe.exec(content)) !== null) {
    const apiFile = m[2];
    const names = m[1].split(',').map(s => s.trim().split(' as ')[0].trim()).filter(Boolean);
    for (const n of names) apiFileMap[n] = apiFile;
  }

  return { tabs, dialogs, sheets, alertDialogs, importedHooks: [...importedHooks], hookFileMap, apiFileMap };
}

// ─── 5) Hook dosyalarından API endpoint'leri çıkar ──────────────
const hookEndpointCache = {}; // hookFile → Set of endpoints
function extractEndpointsFromHookFile(relativePath, searchDir) {
  const key = searchDir + '/' + relativePath;
  if (hookEndpointCache[key]) return hookEndpointCache[key];
  const candidates = [
    path.join(searchDir, relativePath + '.ts'),
    path.join(searchDir, relativePath + '.tsx'),
    path.join(searchDir, relativePath, 'index.ts'),
  ];
  const endpoints = new Set();
  for (const c of candidates) {
    if (!fs.existsSync(c)) continue;
    const content = read(c);
    const apiRe = /apiClient\.(post|get|put|delete|patch)\s*(?:<[^>]*>)?\s*\(\s*[`'"]([^`'"]+)[`'"]/g;
    let m;
    while ((m = apiRe.exec(content)) !== null) {
      endpoints.add(`${m[1].toUpperCase()} ${m[2]}`);
    }
    break;
  }
  hookEndpointCache[key] = endpoints;
  return endpoints;
}

function endpointsForPage(scan) {
  const endpoints = new Set();
  const hookDir = path.join(WEB_SRC, 'hooks');
  const apiDir = path.join(WEB_SRC, 'api');
  for (const hookName of scan.importedHooks) {
    const hookFile = scan.hookFileMap[hookName];
    if (!hookFile) continue;
    for (const e of extractEndpointsFromHookFile(hookFile, hookDir)) endpoints.add(e);
  }
  // Ayrıca api/* den import edilen fonksiyonlar da var
  for (const apiFile of new Set(Object.values(scan.apiFileMap))) {
    for (const e of extractEndpointsFromHookFile(apiFile, apiDir)) endpoints.add(e);
  }
  return [...endpoints].sort();
}

// ─── 6) Çıktıyı oluştur ────────────────────────────────────────
function buildMarkdown() {
  const { componentToFile, pathToComponent } = parseApp();
  const roleToKeys = parseRolePermissions();
  const byRoute = parseMenuStructure(roleToKeys); // route path → info

  // Sidebar grubu bazında gruplandır (birden fazla grupta olabilir — ilkini kullan)
  const byGroup = new Map();
  const groupSort = new Map(); // groupLabel → min sortOrder (modül SortOrder'ından)
  for (const [routePath, info] of byRoute) {
    const primaryGroup = [...info.groupLabels][0] ?? 'Diğer';
    if (!byGroup.has(primaryGroup)) byGroup.set(primaryGroup, []);
    byGroup.get(primaryGroup).push({ routePath, ...info });
    const prev = groupSort.get(primaryGroup) ?? Infinity;
    groupSort.set(primaryGroup, Math.min(prev, info.sortOrder));
  }

  // Grupları modül SortOrder'ına göre dizme (seed'deki menü sırası)
  const sortedGroups = [...byGroup.keys()].sort((a, b) => {
    const ai = groupSort.get(a) ?? 999;
    const bi = groupSort.get(b) ?? 999;
    if (ai !== bi) return ai - bi;
    return a.localeCompare(b, 'tr');
  });

  let md = `# Admin / Kullanıcı Sayfa Envanteri\n\n`;
  md += `**Üretim tarihi:** ${new Date().toISOString().slice(0, 16).replace('T', ' ')}\n`;
  md += `**Üreten:** \`SIS.Web/scripts/generate-codemap.mjs\`\n\n`;
  md += `> ⚠️ **OTOMATIK ÜRETILIR — elle düzenleme.** Script yeniden çalışınca üzerine yazılır.\n`;
  md += `> Kod değiştikten sonra: \`pnpm codemap:generate\` veya \`node scripts/generate-codemap.mjs\`\n\n`;
  md += `## Özet\n\n`;
  md += `- **Toplam sayfa:** ${byRoute.size}\n`;
  md += `- **Sidebar grubu:** ${byGroup.size}\n\n`;

  // İçindekiler
  md += `## İçindekiler\n\n`;
  for (const g of sortedGroups) {
    const slug = g.toLowerCase().replace(/\s/g, '-').replace(/[ç]/g, 'c').replace(/[ğ]/g, 'g').replace(/[ı]/g, 'i').replace(/[ö]/g, 'o').replace(/[ş]/g, 's').replace(/[ü]/g, 'u');
    md += `- [${g}](#${slug}) (${byGroup.get(g).length} sayfa)\n`;
  }
  md += '\n---\n';

  for (const groupLabel of sortedGroups) {
    md += `\n## ${groupLabel}\n\n`;
    const pages = byGroup.get(groupLabel);
    pages.sort((a, b) => a.label.localeCompare(b.label, 'tr'));

    for (const p of pages) {
      const routePath = p.routePath ?? '';
      const componentName = pathToComponent[routePath];
      const componentFile = componentName ? componentToFile[componentName] : null;
      md += `### ${p.label}\n\n`;
      md += `| | |\n|---|---|\n`;
      md += `| **Route** | \`${routePath}\` |\n`;
      md += `| **Dosya** | \`${componentFile ? 'SIS.Web/src/' + componentFile + '.tsx' : '(bulunamadı)'}\` |\n`;
      md += `| **Roller** | ${[...p.roles].join(', ')} |\n`;

      if (componentFile) {
        const filePath = path.join(WEB_SRC, componentFile + '.tsx');
        const scan = scanPageFile(filePath);
        if (scan) {
          const modalCount = scan.dialogs + scan.sheets + scan.alertDialogs;
          md += `| **Modal sayısı** | ${modalCount} (Dialog: ${scan.dialogs}, Sheet: ${scan.sheets}, AlertDialog: ${scan.alertDialogs}) |\n`;
          md += `\n`;

          if (scan.tabs.length > 0) {
            md += `**Sekmeler (${scan.tabs.length}):**\n`;
            for (const t of scan.tabs) md += `- \`${t}\`\n`;
            md += '\n';
          }

          if (scan.importedHooks.length > 0) {
            md += `**Veri kancaları (${scan.importedHooks.length}):**\n`;
            const shown = scan.importedHooks.slice(0, 25);
            for (const h of shown) md += `- \`${h}()\`\n`;
            if (scan.importedHooks.length > 25) md += `- _...ve ${scan.importedHooks.length - 25} hook daha_\n`;
            md += '\n';
          }

          const eps = endpointsForPage(scan);
          if (eps.length > 0) {
            md += `**API endpoint'leri (${eps.length}):**\n`;
            for (const e of eps.slice(0, 30)) md += `- \`${e}\`\n`;
            if (eps.length > 30) md += `- _...ve ${eps.length - 30} endpoint daha_\n`;
            md += '\n';
          }
        }
      }
      md += '---\n\n';
    }
  }

  return md;
}

// ─── 7) Yaz ────────────────────────────────────────────────────
const md = buildMarkdown();
fs.mkdirSync(path.dirname(OUTPUT), { recursive: true });
fs.writeFileSync(OUTPUT, md, 'utf8');
console.log(`OK written: ${OUTPUT}`);
console.log(`Bytes: ${Buffer.byteLength(md, 'utf8')}`);
