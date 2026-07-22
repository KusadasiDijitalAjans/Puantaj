# Proje Kapsamı

## Projenin amacı

Puantaj, otellerde haftalık çalışma planlarının yerel olarak kaydedilmesini, seçilen aya denk gelen haftaların birleştirilmesini ve `Haziran 2025 Puantaj` çalışma kitabının görünüm ve hesap mantığı esas alınarak aylık puantaj Excel'i üretilmesini amaçlayan basit bir Windows masaüstü uygulamasıdır.

## Kullanıcı akışı

1. Kullanıcı geçerli lisansla uygulamayı açar.
2. Otel, departman, hafta tarihleri, personel, görev ve günlük çalışma/izin kodlarını haftalık plan görünümünde girer.
3. Haftalık planı cihazdaki SQLite veritabanına kaydeder.
4. Ay ve yıl seçer; uygulama o ayın günleriyle kesişen kayıtlı haftaları toplar.
5. Kullanıcı eşleşme ve eksik/çakışan kayıt uyarılarını çözer.
6. Uygulama aylık toplamları hesaplar ve referans puantaj görünümünde `.xlsx` üretir.

## Projeye dahil olanlar

- Kaynak iki Excel'in kanıtladığı haftalık alanlar: otel, departman, hafta/tarih, sıra, ad-soyad, görev, yedi günlük çalışma kodu ve imza/onay alanları.
- Haftalık planların yerel kaydı ve seçilen aya göre birleştirilmesi.
- Aylık personel/gün matrisi ile referans dosyada bulunan toplam ve günlük özet alanları.
- Referans Excel'in sayfa düzeni, hücre biçimleri, kenarlıkları, dolguları, yazı tipleri, hizalamaları ve imza alanlarını koruyan çıktı.
- Windows EXE, SQLite yerel depolama, ClosedXML ile Excel üretimi.
- İnternet gerektirmeyen, cihaz kimliğine bağlı lisans doğrulaması.

## Projeye dahil olmayanlar

- Kaynak Excel'lerde bulunmayan bordro, ücret, fazla mesai, vardiya optimizasyonu, izin talep/onay iş akışı, bulut eşitleme, web/mobil uygulama, çok kullanıcılı sunucu, e-posta veya ERP entegrasyonu.
- Excel içe aktarma biçimlerinin iki referans dosya dışında genelleştirilmesi.
- Excel'de kanıtlanmayan insan kaynakları kurallarının otomatik uygulanması.
- İlk sürümde çevrimiçi lisans sunucusu veya abonelik yönetimi.

## Masaüstü uygulama yaklaşımı

.NET 8 WinForms ile tek cihazda çalışan, çevrimdışı bir Windows uygulaması planlanır. Ekranlar yalnız temel akışı destekler: lisans doğrulama, haftalık plan düzenleme/kaydetme, ay seçimi ve Excel çıktı alma. Alan ve kod doğrulamaları referans dosyalarda gözlenen değerlerle sınırlı tutulur; belirsiz kodlar yapılandırılmadan otomatik dönüştürülmez.

## Veri saklama yaklaşımı

SQLite içinde otel, departman, personel, haftalık plan, plan günü ve lisans durumu tutulur. Gün kaydı gerçek takvim tarihiyle saklanmalıdır; sayfa adı veya sütun konumuna tek başına güvenilmemelidir. Personel için sabit bir iç kimlik kullanılmalı; ad-soyad yalnız görüntü alanı olmalıdır. Veritabanı cihazda kalır ve ilk sürümde eşitleme yapılmaz.

## Excel çıktı yaklaşımı

ClosedXML ile yeni bir çıktı üretilir; kaynak Excel dosyaları değiştirilmez. Hedef düzen, `Haziran 2025 Puantaj` dosyasındaki B:AT görünür alanı, 1–31 gün sütunları, AJ:AS toplamları, 40–50 günlük özet alanı, imza satırı ve yazdırma ayarlarıdır. Seçilen ayın mevcut olmayan günleri boş bırakılır. Kaynak şablondaki bariz formül/kod tutarsızlıkları kullanıcı kararı olmadan çoğaltılmaz; karar verilene kadar çıktı engellenir veya açık uyarı verilir.

## Lisanslama yaklaşımı

Lisans çevrimdışı doğrulanır ve cihaz kimliğine bağlanır. Lisans verisi imzalı olmalı; uygulama içine yalnız doğrulama anahtarı konulmalı, lisans üreten gizli anahtar dağıtılmamalıdır. Cihaz kimliğinin hangi bileşenlerden üretileceği, donanım değişikliğinde yeniden etkinleştirme ve süreli/süresiz lisans kararı uygulamadan önce netleştirilmelidir.

## İlk sürüm kabul kriterleri

- Windows 10/11 üzerinde .NET 8 tabanlı EXE açılır ve geçerli çevrimdışı lisansı doğrular.
- Haftalık planda kaynak dosyadaki temel başlık/personel/görev/yedi gün alanları girilip yeniden açılabilecek biçimde SQLite'a kaydedilir.
- Ay sınırını aşan haftalarda yalnız hedef aya ait takvim tarihleri alınır.
- Aynı tarih-personel için çakışma sessizce ezilmez; kullanıcıya gösterilir.
- Onaylanan kod dönüşüm tablosu uygulanır ve bilinmeyen kod çıktıdan önce raporlanır.
- Aylık kişi toplamları ve günlük özetler onaylı kurallarla hesaplanır ve test örnekleriyle doğrulanır.
- Üretilen `.xlsx`, referans görünüm, birleştirmeler, kolon/satır ölçüleri, baskı yönü/ölçeği, kenar boşlukları ve imza alanları bakımından kabul testini geçer.
- Kaynak Excel dosyaları hiçbir işlemde değiştirilmez.
