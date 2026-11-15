#!/usr/bin/env bash
# Yasaklı kelime kontrolü: git archive HEAD içindeki dosyaların içeriğine ve yollarına
# ci/yasakli-kelimeler.txt desenlerini uygular, sonucu ci/yasakli-kelime-bilinen.txt ile
# karşılaştırır. Ayrıca izin listesinde olmayan export-ignore kuralına izin vermez.
#   Çıkış 1: yeni (dosya, desen), artan sayı, yeni yol eşleşmesi ya da izinsiz export-ignore.
#   Çıkış 0 + uyarı: bilinen bir eşleşme azaldıysa ("listeyi küçült").
# Listeyi küçültmek: ihlali temizle, commit et, betiği çalıştır; uyarıdaki satırları listeye
# uygula ya da elle "bash scripts/yasakli-kelime.sh --baseline" çalıştır. Liste yalnız küçülür;
# --baseline listeyi büyütmeyi reddeder. CI --baseline kullanmaz.
# Çıktıda yalnız dosya:satır ve desen adı yazılır; satırın içeriği yazılmaz.
set -euo pipefail
export LC_ALL=C.UTF-8

kok=$(git rev-parse --show-toplevel)
cd "$kok"
desen_dosyasi="ci/yasakli-kelimeler.txt"
bilinen="ci/yasakli-kelime-bilinen.txt"
# Taramadan hariç: uygulanmış migration'lar ve iki listenin kendisi.
haric='^(SIS\.Infrastructure/Migrations/|ci/yasakli-kelime(ler|-bilinen)\.txt$)'

mod=kontrol
case "${1:-}" in
  "") ;;
  --baseline) mod=baseline ;;
  *) echo "kullanım: $0 [--baseline]" >&2; exit 2 ;;
esac

gecici=$(mktemp -d)
trap 'rm -rf "$gecici"' EXIT
arsiv="$gecici/arsiv"
mkdir "$arsiv"
git archive --format=tar HEAD | tar -x -C "$arsiv"

# Desen dosyasını iki bölüme ayır: desenler (ad<TAB>desen) ve izinli export-ignore kuralları.
: > "$gecici/desenler"; : > "$gecici/izinli"
tr -d '\r' < "$desen_dosyasi" | awk -F'\t' -v d="$gecici/desenler" -v i="$gecici/izinli" '
  /^[[:space:]]*(#|$)/ { next }
  /^\[izinli-export-ignore\]$/ { bolum = "izinli"; next }
  NF != 2 { print "HATA: bozuk satır " NR | "cat 1>&2"; hata = 1; next }
  bolum == "izinli" { print $1 "\t" $2 > i; next }
  { print > d }
  END { exit hata }'
[[ -s "$gecici/desenler" ]] || { echo "HATA: desen bulunamadı" >&2; exit 2; }

# 1) export-ignore kuralları: HEAD'deki tüm .gitattributes dosyaları ve yerel info/attributes.
export_ignore_oku() {  # $1 = kaynak adı; stdin = öznitelik dosyası
  tr -d '\r' | awk -v k="$1" '
    /^[[:space:]]*(#|$)/ { next }
    { for (j = 2; j <= NF; j++) if ($j == "export-ignore") { print k "\t" $1; break } }'
}
{
  git ls-tree -r --name-only HEAD | { grep -E '(^|/)\.gitattributes$' || true; } |
    while IFS= read -r f; do git show "HEAD:$f" | export_ignore_oku "$f"; done
  ia=$(git rev-parse --git-path info/attributes)
  if [[ -f "$ia" ]]; then export_ignore_oku "info/attributes" < "$ia"; fi
} | LC_ALL=C sort -u > "$gecici/export"
LC_ALL=C sort -u "$gecici/izinli" -o "$gecici/izinli"
LC_ALL=C comm -23 "$gecici/export" "$gecici/izinli" > "$gecici/izinsiz"

# 2) Şimdiki eşleşmeler: dosya<TAB>desen<TAB>sayı (içerik) ve dosya<TAB>desen<TAB>ad (yol).
(cd "$arsiv" && find . \( -type f -o -type l \)) | sed 's|^\./||' |
  { grep -Ev "$haric" || true; } | LC_ALL=C sort > "$gecici/yollar"
: > "$gecici/simdi"
while IFS=$'\t' read -r ad desen; do
  rc=0
  (cd "$arsiv" && grep -rIcP -e "$desen" .) > "$gecici/sayim" || rc=$?
  if (( rc > 1 )); then echo "HATA: '$ad' deseni çalıştırılamadı" >&2; exit 2; fi
  sed 's|^\./||' "$gecici/sayim" |
    awk -v d="$ad" '{ n = $0; sub(/^.*:/, "", n); f = $0; sub(/:[0-9]+$/, "", f)
                      if (n + 0 > 0) print f "\t" d "\t" n }' |
    { grep -Ev "$haric" || true; } >> "$gecici/simdi"
  rc=0
  grep -P -e "$desen" "$gecici/yollar" > "$gecici/yol" || rc=$?
  if (( rc > 1 )); then echo "HATA: '$ad' deseni yollara uygulanamadı" >&2; exit 2; fi
  awk -v d="$ad" '{ print $0 "\t" d "\tad" }' "$gecici/yol" >> "$gecici/simdi"
done < "$gecici/desenler"
LC_ALL=C sort -u "$gecici/simdi" -o "$gecici/simdi"

# 3) Bilinen listeyle karşılaştır. Çıktı: tür<TAB>dosya<TAB>desen<TAB>tip<TAB>eski<TAB>yeni
karsilastir() {
  tr -d '\r' < "$bilinen" | awk -F'\t' '
    FNR == NR { if ($0 ~ /^#/ || NF != 3) next
                t = ($3 == "ad") ? "ad" : "sayi"; b[$1 FS $2 FS t] = $3; next }
    { t = ($3 == "ad") ? "ad" : "sayi"; k = $1 FS $2 FS t; s[k] = $3
      if (!(k in b)) print "YENI" FS k FS "-" FS $3
      else if (t == "sayi" && $3 + 0 > b[k] + 0) print "ARTIS" FS k FS b[k] FS $3
      else if (t == "sayi" && $3 + 0 < b[k] + 0) print "AZALIS" FS k FS b[k] FS $3 }
    END { for (k in b) if (!(k in s)) print "AZALIS" FS k FS b[k] FS "0" }' - "$gecici/simdi" |
    LC_ALL=C sort
}

hata=0
if [[ -s "$gecici/izinsiz" ]]; then
  hata=1
  while IFS=$'\t' read -r kaynak kural; do
    echo "İHLAL (izinsiz export-ignore) $kaynak: $kural"
  done < "$gecici/izinsiz"
fi

if [[ "$mod" == baseline ]]; then
  if [[ -f "$bilinen" ]] && karsilastir | grep -qE '^(YENI|ARTIS)'; then
    echo "HATA: bilinen listesi yalnız küçülür; yeni ya da artan eşleşme var (betiği --baseline olmadan çalıştır)." >&2
    exit 1
  fi
  if (( hata )); then echo "HATA: izinsiz export-ignore varken liste üretilmez." >&2; exit 1; fi
  {
    echo "# dosya<TAB>desen-adı<TAB>sayı (yol eşleşmesinde sayı yerine \"ad\"). scripts/yasakli-kelime.sh --baseline üretir; liste yalnız küçülür."
    cat "$gecici/simdi"
  } > "$bilinen"
  echo "Bilinen listesi yazıldı: $(wc -l < "$gecici/simdi") satır."
  exit 0
fi

[[ -f "$bilinen" ]] || { echo "HATA: $bilinen yok; elle --baseline ile üret." >&2; exit 2; }
karsilastir > "$gecici/fark"

: > "$gecici/kucult"
while IFS=$'\t' read -r tur dosya ad tip eski yeni; do
  case "$tur" in
    YENI|ARTIS)
      hata=1
      if [[ "$tip" == ad ]]; then
        echo "İHLAL (yeni dosya adı) $dosya  desen=$ad"
      else
        [[ "$tur" == YENI ]] && neden="yeni" || neden="artış: bilinen $eski, şimdi $yeni"
        echo "İHLAL ($neden) $dosya  desen=$ad"
        desen=$(awk -F'\t' -v a="$ad" '$1 == a { print $2; exit }' "$gecici/desenler")
        grep -nP -e "$desen" -- "$arsiv/$dosya" | cut -d: -f1 |
          while IFS= read -r n; do printf '  %s:%s\t%s\n' "$dosya" "$n" "$ad"; done
      fi ;;
    AZALIS)
      if [[ "$yeni" == 0 || "$tip" == ad ]]; then
        printf '  sil:       %s\t%s\t%s\n' "$dosya" "$ad" "$eski" >> "$gecici/kucult"
      else
        printf '  değiştir:  %s\t%s\t%s\n' "$dosya" "$ad" "$yeni" >> "$gecici/kucult"
      fi ;;
  esac
done < "$gecici/fark"

if [[ -s "$gecici/kucult" ]]; then
  echo "UYARI: bilinen eşleşmeler azaldı; listeyi küçült ($bilinen):"
  cat "$gecici/kucult"
  echo "  (ya da elle: bash scripts/yasakli-kelime.sh --baseline)"
fi

if (( hata )); then
  echo "Yasaklı kelime kontrolü: BAŞARISIZ."
  exit 1
fi
echo "Yasaklı kelime kontrolü: geçti ($(wc -l < "$gecici/simdi") bilinen eşleşme, $(wc -l < "$gecici/yollar") dosya)."
