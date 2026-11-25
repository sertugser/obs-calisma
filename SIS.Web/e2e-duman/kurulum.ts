import { execFileSync } from 'node:child_process';
import { pbkdf2Sync, randomBytes } from 'node:crypto';

// Her rol için seed'deki tek rollü ilk hesap (Id sırasıyla) seçilir. Veritabanına yalnız bu hesapların
// parola özeti ile kilit ve sayaç alanları yazılır; e-postalar testlere ortam değişkeniyle geçer.
// Öğrenci ön koşulu: öğrenci kaydı ve aktif dönemde notlu en az bir ders kaydı.
const SECIM = `
WITH tek_rollu AS (
  SELECT ur."UserId" AS id, min(r."Name") AS rol
  FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
  GROUP BY ur."UserId" HAVING count(*) = 1
), aday AS (
  SELECT DISTINCT ON (t.rol) t.rol, t.id FROM tek_rollu t
  WHERE t.rol IN ('SystemAdministrator', 'AcademicAdvisor')
     OR (t.rol = 'Student' AND EXISTS (
       SELECT 1 FROM "Students" s
       JOIN "CourseRegistrations" cr ON cr."StudentId" = s."Id"
       JOIN "CourseSections" cs ON cs."Id" = cr."CourseSectionId"
       JOIN "AcademicTerms" at ON at."Id" = cs."TermId"
       WHERE s."UserId" = t.id AND cr."Grade" IS NOT NULL AND at."IsActivated" AND NOT at."IsCancelled"))
  ORDER BY t.rol, t.id
)
UPDATE "AspNetUsers" u
SET "PasswordHash" = :'ozet', "AccessFailedCount" = 0, "LockoutEnd" = NULL
FROM aday a WHERE u."Id" = a.id
RETURNING a.rol, u."Email";
`;

const ANAHTAR: Record<string, string> = {
  SystemAdministrator: 'YONETICI',
  Student: 'OGRENCI',
  AcademicAdvisor: 'DANISMAN',
};

// ASP.NET Core Identity V3 biçimi: 0x01, PBKDF2-HMACSHA512, 100.000 tur, 16 bayt tuz, 32 bayt alt anahtar.
function parolaOzeti(parola: string): string {
  const tuz = randomBytes(16);
  const bas = Buffer.alloc(13);
  bas.writeUInt8(1, 0);
  bas.writeUInt32BE(2, 1);
  bas.writeUInt32BE(100_000, 5);
  bas.writeUInt32BE(16, 9);
  return Buffer.concat([bas, tuz, pbkdf2Sync(parola, tuz, 100_000, 32, 'sha512')]).toString('base64');
}

function gerekli(ad: string): string {
  const deger = process.env[ad];
  if (!deger) throw new Error(`Duman kurulumu: ${ad} ortam değişkeni yok`);
  return deger;
}

export default function kurulum(): void {
  gerekli('DUMAN_ADRES');
  const ozet = parolaOzeti(gerekli('DUMAN_PAROLA'));
  const cikti = execFileSync(
    'docker',
    ['exec', '-i', gerekli('DUMAN_DB_KONTEYNER'), 'psql', '-U', 'postgres', '-d', 'sis', '-X', '-q', '-tA',
      '-F', '\t', '-v', 'ON_ERROR_STOP=1', '-v', `ozet=${ozet}`],
    { input: SECIM, encoding: 'utf8' },
  );
  const satirlar = cikti.trim().split('\n').map((s) => s.split('\t'));
  for (const [rol, anahtar] of Object.entries(ANAHTAR)) {
    const satir = satirlar.find(([r]) => r === rol);
    if (!satir) throw new Error(`Duman kurulumu: ${rol} rolünde ön koşulu sağlayan hesap yok`);
    process.env[`DUMAN_EPOSTA_${anahtar}`] = satir[1];
  }
}
