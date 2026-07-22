# Excel Analizi

## Yöntem ve kapsam

İki `.xlsx` dosyası yeniden kaydedilmeden ZIP/Open XML yapısı üzerinden programatik olarak incelendi. Sayfa XML'leri, paylaşılan metinler, biçimler, ilişkiler, çizimler ve yazıcı ayarları okundu. “Dolu hücre”, değeri veya formül kaydı bulunan hücredir. Excel'in `dimension` alanı biçimlendirilmiş boş hücreler nedeniyle gerçek içerikten daha geniş olabilir; bu nedenle bildirilen alan ile gerçek hücre sınırı birlikte verilmiştir.

## 1. Haftalık Çalışma Planı

**Dosya:** `HAFTALIK ÇALIŞMA PLANI 2026.xlsx`  
**Sayfa:** 26 (25 görünür, `Sayfa1` gizli)  
**Toplam formül kaydı:** 3  
**Toplam birleşik alan:** 822  
**Biçim kataloğu:** 34 yazı tipi, 28 dolgu, 83 kenarlık, 443 hücre biçimi, 2 özel sayı biçimi  
**Tanımlı ad / harici bağlantı / veri doğrulama / koşullu biçim:** yok  
**Dondurulmuş alan / üstbilgi-altbilgi / sayfa koruması:** yok

### Sayfa envanteri

| # | Sayfa adı | Bildirilen aralık | Dolu hücre | Formül | Birleşik |
|---:|---|---|---:|---:|---:|
| 1 | `13.19.05.2026. ` | A1:ALK32 | 221 | 0 | 25 |
| 2 | `20.26.05.2026` | A1:ALK32 | 221 | 0 | 25 |
| 3 | `27.03.05.2026 ` | A1:ALK32 | 221 | 0 | 25 |
| 4 | `04.10.05.2026 ` | A1:ALK38 | 277 | 0 | 25 |
| 5 | `11.17.05.2026  ` | A1:ALK41 | 306 | 0 | 25 |
| 6 | ` 18.24.05.2026.K` | A1:ALK36 | 228 | 0 | 28 |
| 7 | `11.24.05.2026.M` | A1:ALV34 | 166 | 1 | 36 |
| 8 | `25.31.05.2026k` | A1:ALK36 | 230 | 0 | 28 |
| 9 | `25.31.05.2026` | A1:S32 | 165 | 1 | 34 |
| 10 | `01.07.06.2026.M` | A1:S30 | 171 | 0 | 33 |
| 11 | `01.07.06.2026.k` | A1:ALK35 | 215 | 0 | 28 |
| 12 | `08.14.07.2026 M` | A1:S30 | 173 | 0 | 33 |
| 13 | `08.14.06.2026.K` | A1:ALK37 | 242 | 0 | 28 |
| 14 | `15.21.06.2026 K` | A1:S34 | 228 | 0 | 27 |
| 15 | `15.21.06.2026.M` | A1:S32 | 185 | 0 | 33 |
| 16 | `22.28..06.2026.M ()` | A1:S32 | 189 | 0 | 33 |
| 17 | `22.28.06.2026.k` | A1:U34 | 231 | 0 | 27 |
| 18 | `29.05.07.2026 k` | A1:U35 | 236 | 0 | 30 |
| 19 | `29.05.07.2026.M` | A1:S33 | 176 | 0 | 33 |
| 20 | `06.12.07.2026.M` | A1:S35 | 193 | 0 | 35 |
| 21 | `06.12.07.2026K` | A1:V37 | 246 | 0 | 33 |
| 22 | `13.19.07.2026.m` | A1:S35 | 185 | 0 | 33 |
| 23 | `13.19.07.2026. k` | A1:V37 | 261 | 0 | 36 |
| 24 | `20.26.07.2026 k` | A1:V38 | 259 | 0 | 52 |
| 25 | `20.26.07.2026 m` | A1:S35 | 186 | 0 | 33 |
| 26 | `Sayfa1` (gizli) | A2:AMJ39 | 178 | 1 | 44 |

`ALK` (999. sütun) gibi geniş bildirilen aralıklar içerik alanı değildir; biçimlendirilmiş boş hücrelerden kaynaklanır. Gerçek değer/formül sınırları çoğu sayfada A:S, bazı notlu sayfalarda A:V/ALV; satırlar 1–41 arasındadır.

### İşleyiş ve hücre alanları

İki yerleşim varyantı vardır. İlk sayfalarda başlık satırları 2–8, personel 9'dan itibaren; sonraki sayfalarda başlık 1–6, personel çoğunlukla 7/8'den itibaren; gizli `Sayfa1`de başlık 8–14, personel 15'ten itibaren başlar. Bu nedenle sabit satır numarasıyla okuma güvenli değildir.

- Başlıklar: `WEEKLY WORK PLAN` / `HAFTALIK ÇALIŞMA PLANI`.
- Otel: etiket A3/A5/A11; değer çoğunlukla C3/C5/C11 (`SEYA BEACH HOTEL`).
- Departman: etiket A4/A6/A12; değer C4/C6/C12 (`HOUSEKEEPING` veya `HK`).
- Hafta: R sütununda `WEEK :` ve tarih aralığı; sayfa adı da hafta aralığını taşır fakat çok sayıda yazım/tarih hatası vardır.
- Tarihler: D/F/H/J/L/N/P; seri Excel tarihi, düz tarih metni veya hatalı yıl/gün metni olabilir. Aradaki E/G/I/K/M/O/Q sütunları imzadır.
- Personel: A sıra, B ad-soyad, C görev/pozisyon; veri satırları değişken uzunluktadır ve vardiya başlığı/not satırlarıyla kesilebilir.
- Gün girişi: D/F/H/J/L/N/P. Bunlar kullanıcı verisidir. İmza hücreleri E/G/I/K/M/O/Q çoğunlukla boştur.
- Otomatik alan: yalnız üç formül vardır: `S6=+P6`, `S4=+P4`, `S12=+P12`; Pazar tarihini yardımcı hücreye kopyalar. Başka otomatik hesap yoktur.
- Alt bölüm: vardiya saat lejantı, HT/RT/RP/ÜZ/Üİ/Yİ açıklamaları, Department Head ve Human Resources Manager imza alanları; bazı sayfalarda yemek/çay molası notu.
- Birden fazla hafta/sayfa: Her sayfa bir departman/ekip ve bir haftayı temsil eder. `K/k` ve `M/m` ekli aynı hafta sayfaları farklı personel grupları/vardiya kümeleri gibi görünür; harflerin resmi anlamı dosyada açıklanmamıştır. Sayfalar arası formül veya bağlantı yoktur.

### Gözlenen kodlar

Gün hücrelerinde gözlenen anlamlı kodlar: **A, B, C, D, E, G, HT, RT, RP, Üİ, Aİ, R**. Lejant ayrıca **ÜZ** ve **Yİ** kodlarını tanımlar; veri hücrelerinde bu ikisi gözlenmemiştir. Ham kısa değer taramasında hatalı tarih/başlık parçaları dışarıda bırakılmıştır.

Lejantın daha yeni sayfalarında A=09:00–17:00, B=08:00–16:00, C=16:00–24:00, D=24:00–08:00, E=13:00–21:00 görünür. İlk sayfalarda saatler B–E satırlarına kaymış ve A'nın saati boş bırakılmıştır; tek doğru eşleme olarak kabul edilemez.

### Biçim, boyut ve baskı

- 431 farklı hücre biçimi fiilen kullanılmıştır; sayfalar kopyalanıp düzenlendikçe biçim çeşitliliği büyümüştür.
- Başlıca yazı tipleri Calibri ve Times New Roman'dır; 9–24 punto aralığı, kalın başlıklar ve ortalı hücreler yaygındır.
- Personel/gün tablolarında dört yönde ince kenarlık; bölüm ayrımlarında orta kalınlıkta kenarlık vardır. Beyaz, sarı ve tema vurgu dolguları görülür.
- Tarih hücrelerinde `dd.mm.yyyy` özel biçimi kullanılır. Metin ve tarihler çoğunlukla yatay ortalı; bazı başlıklar dikey ortalıdır.
- Satır yükseklikleri varyanta göre 15/15,75; başlıkta 19,5–30/48,75; personelde sıklıkla 17,25; not/ayırıcı satırlarda 1,5–14,25 aralığındadır.
- Tipik sütunlar: A yaklaşık 2,71–5,57; B 20–25,57; C 16,86–23,43; çalışma sütunları yaklaşık 4,43–7,86; imza sütunları yaklaşık 5–11,86. Sayfaya göre sapmalar bulunduğundan kesin üretimde referans varyant seçilmelidir.
- Sayfa 7 ve gizli Sayfa1'de bir gizli satır; eski geniş şablonlarda X sütunu, bazı sayfalarda S ve uzak yardımcı kolon kümeleri gizlidir. Diğer sayfalarda gizli satır yoktur.
- Tüm görünür haftalık sayfalar yataydır. İlk 4 sayfada A3 kağıt (`paperSize=12`); diğerlerinde çoğunlukla A4 (`paperSize=9`) ve `%60–70` ölçek vardır. Kenar boşlukları yaklaşık sol/sağ 0,7; üst/alt 0,75; header/footer 0,3 inçtir.
- XML tanımlı yazdırma alanı yoktur. Üstbilgi/altbilgi yoktur. Çizim ilişkileri logo/görsel içerir ve görsel kimlik korunmalıdır.

### Birleşik hücreler

Her sayfada 25–52 birleşik alan vardır. Tek tek 822 alanı listelemek yerine yapısal desen: büyük başlık satırları, A/B/C alanlarının dikey-yatay başlık birleşimleri, gün başlıklarının çalışma+imza çiftleri, lejant açıklamaları, onay/imza alanları ve alt not satırlarıdır. Çıktı üretim testinde seçilen baz sayfanın birleşim listesi hücre hücre karşılaştırılmalıdır.

## 2. Haziran 2025 Puantaj

**Dosya:** `HAZİRAN    2025 PUANTAJ -.xlsx`  
**Sayfa:** `HAZİRAN .2025 PUANTAJ`  
**Bildirilen/gerçek alan:** B2:AZ53  
**Dolu hücre:** 1.674  
**Formül kaydı:** 669 (32 paylaşımlı formül kökü, 587 paylaşımlı devam kaydı, 50 normal formül)  
**Birleşik alan:** 5  
**Biçim kataloğu:** 16 yazı tipi, 9 dolgu, 20 kenarlık, 57 hücre biçimi, 1 özel sayı biçimi  
**Tanımlı ad / harici bağlantı / veri doğrulama:** yok  
**Dondurulmuş alan / üstbilgi-altbilgi / sayfa koruması:** yok

### Alanlar ve işleyiş

- `AO2` Departman etiketi, `AP2:AS2` değer (`HAUSKEEPENG`).
- `AO3` Otel etiketi, `AP3:AS3` değer (`SEYA BEACH`).
- `E5:AI5` ay/yıl başlığı (`HAZİRAN 2025 PUANTAJ`); `AJ5:AT5` bilgi başlığı.
- `B6` sıra, `C6` ad-soyad, `D6` görev; `E6:AI6` 1–31; `AJ6:AS6` toplam kodları; `AT6` imza.
- Personel alanı `7:38` (32 satır kapasitesi); örnekte 7–35 dolu, 36–38 boş/toplam formüllü. 29 personel vardır.
- Haziran 30 gün olduğu için AI (31) boştur. Örnekte bazı kişilerin ay başı/sonu hücreleri işe giriş gibi nedenlerle boş bırakılmıştır; gerekçe hücrelerden kesin anlaşılamaz.
- `AJ:AS`: X, HT, RT, Mİ, Üİ, RP, Yİ, ÜZ, DZ, GR kişi toplamları.
- `D41:D50`: günlük kategori açıklamaları; `E:AI` günlük sayımlar. D47'de beklenen Yİ açıklaması yerine sayısal `202` bulunur.
- `40`: günlük toplamlar ve kişi toplamlarının toplulaştırılması.
- `E53`, `AC53`, `AS53`: Human Resources Manager, Department Head, General Manager imza alanları.

### Formüller

Kişi bazında `AJ = X adedi + G adedi`; diğer toplamlar `COUNTIF` ile ilgili kodu sayar. Satır 7 ve 8–38 için ayrı paylaşımlı formül grupları vardır. Günlük özetler de her gün sütununda `COUNTIF` kullanır. `E40=SUM(E41:E50)`, `AJ40=SUM(AJ8:AJ38)`, `AK40=SUM(AK7:AK38)` örnek toplamlardır; diğer hücrelerin çoğu paylaşımlı formül devam kaydıdır.

Tespit edilen yapısal sorunlar:

- AN başlığı Üİ olduğu halde kişi formülleri `Sİ` sayar.
- Günlük Üİ satırı da `Sİ` sayar.
- D48 etiketi ÜZ olduğu halde formüller `Üİ` sayar.
- R kodu veri alanında 5 kez vardır fakat hiçbir toplam sütununa dahil değildir.
- Toplam aralık başlangıçları bazı hücrelerde 7, bazılarında 8'dir. Bu farklılıklar iş kuralı kabul edilmeden doğrulanmalıdır.

### Birleşimler, biçim ve baskı

Birleşimler: `AP2:AS2`, `AP3:AS3`, `B5:D5`, `E5:AI5`, `AJ5:AT5`.

- Ana tablo Tahoma ağırlıklıdır: çoğu hücre 7 punto; bölüm başlıklarında 11 punto/kalın; ortalı ve bazı başlıklarda metin kaydırmalı hizalama vardır.
- Gün/personel tablosunda ince siyah çerçeveler; günlük özet alanında kırmızı hairline ve bölüm sınırlarında medium kenarlıklar bulunur.
- Sarı dolgu (özellikle toplam alanları), kırmızı indeksli dolgu ve beyaz/temasız alanlar kullanılır. Bazı metinler kırmızı/beyaz yazı rengindedir.
- Sütun genişlikleri: A 2,125; B 3,625; C 22,625; D 18,25; E:AH 2,25; AI 2,75; AJ:AS 3,125; AT 18,125; AU 14,625. AV:AZ (48–52) 8,125 genişliğinde ve gizlidir.
- Özel satır yükseklikleri: 4–5 ve 40/50 için 15; 6 için 27,75; 9–12 için 13,5. Diğerleri varsayılandır.
- Yatay A4, `%66` ölçek, genişliğe sığdırma kapalı (`fitToWidth=0`). Kenar boşlukları sol/sağ 0; üst/alt yaklaşık 0,748; header/footer yaklaşık 0,315 inçtir.
- XML yazdırma alanı, üstbilgi/altbilgi ve dondurma yoktur. Üç gömülü görsel/çizim ilişkisi vardır; logo ve imza yerleşimi görsel kabul testine dahil edilmelidir.
- 13.141 koşullu biçimlendirme bölümü vardır. Çok büyük bölümü yinelenmiş/şişmiş `cellIs = "O"` kurallarıdır; `styles.xml` içindeki diferansiyel biçimler de olağan dışı büyüktür. Bunları aynen yeniden üretmek yerine görünür sonuç kullanıcıyla doğrulanmalı; aksi halde dosya boyutu ve performans gereksiz büyür.

## Korunması gereken görsel ve işlevsel özellikler

- Aylıkta B:AT ana düzeni, başlık birleşimleri, dar 1–31 gün sütunları, kişi ve günlük toplam blokları.
- Otel/departman, ay-yıl, personel/görev ve imza alanlarının konumu.
- Kod toplamlarının onaylanan formül mantığı ve boş/gün olmayan hücrelerin davranışı.
- Tahoma tabanlı aylık görünüm, sınırlar, dolgu renkleri, hizalama ve baskıda tek yatay sayfa hedefi.
- Haftalık ekranda yedi çalışma + yedi imza sütunu semantiği, vardiya/izin lejantı ve onay alanları.
- Kaynak görsellerin/logo oranlarının korunması; kaynak dosyaların hiçbir zaman yeniden kaydedilmemesi.
