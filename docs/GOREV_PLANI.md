# Görev Planı

Bu plan yalnız iki referans Excel'in kanıtladığı işlevlerle sınırlıdır. Her görev, bağımlı olduğu önceki görev kabul edilmeden tamamlanmış sayılmaz.

## Görev 5 — Gerçek otel senaryoları

**Durum:** Tamamlandı. Operasyonel kod listesi veritabanı tanımlarına taşındı; keyfi vardiya kodları destekleniyor. “İşten Ayrıldı” açıklama-temelli özel durumu haftalık/aylık seçim ve Excel karartma akışına eklendi. 24/24 test ve tam solution build başarılıdır.

## Görev 4 — Gerçek derleme ve belge çıktıları

**Durum:** Tamamlandı. .NET 8 restore edildi; tam WindowsDesktop SDK ile solution 0 hata/0 uyarı derlendi; 22 test geçti. Haftalık tek sayfa ve aylık Excel örnekleri gerçek olarak üretildi ve yeniden açıldı. İki ekrana Excel/PDF/Yazdır düğmeleri, A4/PrintArea ayarları ve Windows Excel COM otomasyonu eklendi. PDF/yazdırmanın gerçek Windows + Microsoft Excel manuel kabulü bekliyor.

## 1. Excel analizi ve proje iskeleti

**Amaç:** Kaynak/çıktı şablonunu, kapsamı ve belirsizlikleri sabitlemek.

**Yapılacaklar:** İki XLSX'in yapısal envanteri; hücre/biçim/formül/baskı incelemesi; dönüşüm karar oturumu; .NET solution ve katman sınırlarının kararlardan sonra kurulması; referans dosyalar için salt-okunur bütünlük kontrolü.

**Dosyalar:** Mevcut `docs/*.md`; sonraki uygulama adımında `Puantaj.sln`, `src/`, `tests/` altındaki proje dosyaları, `.gitignore`.

**Test şartları:** Kaynak dosya hash'leri işlem öncesi/sonrası aynı; sayfa/formül/birleşim sayıları analiz raporuyla eş; tüm belirsizlikler karar kaydına bağlanmış.

**Tamamlanma kriteri:** Kullanıcı kod dönüşümü, tarih çakışması, personel kimliği ve şablon tutarsızlıkları için karar vermiş; mimari iskelet bu sınırlarla onaylanmış.

**Bağımlılık:** Başlangıç görevi; 2, 3, 5, 6 ve 7'nin girdisidir.

## 2. Lisans altyapısı

**Durum:** Uygulandı; kaynak ve otomatik testler hazır. Yerel ortamda .NET SDK bulunmadığı ve geliştirme macOS üzerinde olduğu için build/test ile WinForms manuel kabulü Windows/.NET ortamında bekliyor.

**Amaç:** Uygulamanın çevrimdışı ve cihaz kimliğine bağlı lisansla açılmasını sağlamak.

**Yapılanlar:** MachineGuid + sistem birimi + erişilebilen BIOS UUID + işlemci kaynağından cihaz kodu; RSA-PSS/SHA-256 imzalı lisans; public-key doğrulama; süreli/süresiz doğrulama; aktivasyon ve Generator ekranları; `%LocalAppData%` deposu; private key'in Git ve müşteri uygulaması dışında tutulması.

**Dosyalar:** `src/.../Licensing/` modelleri ve servisleri, lisans ekranı; `tests/.../Licensing/`; lisans biçimi belgesi.

**Test şartları:** Geçerli, bozuk, başka cihaza ait, süresi geçmiş (süreli seçilirse), saat geri alma ve kısmi donanım değişimi senaryoları; ağ olmadan test.

**Tamamlanma kriteri:** Kaynak düzeyinde karşılandı. Geçersiz lisans ana akışı açamaz; geçerli lisans çevrimdışı çalışır; özel anahtar müşteri EXE'sinde/depo takibinde değildir. Windows build ve manuel kabul sonrası görev tamamen doğrulanmış sayılır.

**Bağımlılık:** 1'e bağlı; 8'de paketlemeye bağlanır, 3–7 ile paralel geliştirilebilir.

## 3. Haftalık çalışma planı ekranı

**Durum:** Görev 3 MVP kapsamında uygulandı; Windows manuel UI doğrulaması bekliyor.

**Amaç:** Referanstaki haftalık temel alanları WinForms üzerinden girmek ve doğrulamak.

**Yapılacaklar:** Otel, departman, hafta başlangıcı; Pazartesi–Pazar tarihleri; dinamik personel satırları; sıra, ad-soyad, görev; günlük kod seçimi; yalnız onaylı kod listesi; haftalık imza/onay alanlarının kapsam kararına göre sunumu; bilinmeyen/eksik kod uyarıları.

**Dosyalar:** `src/.../WeeklyPlan/` form, view-model/presenter ve doğrulamalar; `tests/.../WeeklyPlan/`.

**Test şartları:** Tarihler ardışık; ay sınırı gösterimi; boş/çift personel; Türkçe karakterler; tüm onaylı kodlar; referans ekran alanlarının kapsanması.

**Tamamlanma kriteri:** Kullanıcı bir haftayı eksiksiz oluşturup düzenleyebilir; hatalı tarih/kod kaydedilemez; kaynak dışı alan eklenmemiştir.

**Bağımlılık:** 1'deki kod/tarih kararlarına ve 4'ün sözleşmelerine bağlı; 5'e veri sağlar.

## 4. Yerel veri kaydı

**Durum:** Görev 3 MVP kapsamında SQLite ile uygulandı; otomatik testler hazır, .NET SDK bulunan ortamda çalıştırılmayı bekliyor.

**Amaç:** Haftalık planları kayıpsız ve tekrar açılabilir biçimde SQLite'ta saklamak.

**Yapılacaklar:** Otel/departman/personel/hafta/gün şeması; uygulama içi personel kimliği; migration yaklaşımı; transaction; benzersiz tarih-personel kısıtı veya kontrollü çakışma; yedekleme kapsamı yalnız gerekiyorsa kullanıcı kararıyla.

**Dosyalar:** `src/.../Persistence/`, SQLite şeması/migration'ları, repository sözleşmeleri; `tests/.../Persistence/`.

**Test şartları:** CRUD, transaction rollback, yeniden açma, Türkçe metin, aynı tarih/personel çakışması, bozuk/veri sürümü senaryosu.

**Tamamlanma kriteri:** Ekranda kaydedilen plan birebir geri yüklenir; sessiz veri ezilmesi yoktur; tarih ve personel kimlikleri sabittir.

**Bağımlılık:** 1'e bağlı; 3 ve 5'in altyapısıdır.

## 5. Aylık birleştirme motoru

**Durum:** MVP için seçilen ayın tarih aralığındaki tekil personel/gün kayıtlarını okuyan basit akış uygulandı.

**Amaç:** Seçilen ayla kesişen haftalık kayıtları tek personel-gün matrisi haline getirmek.

**Yapılacaklar:** Tarih aralığı sorgusu; ay dışı günleri eleme; personel kimliğiyle gruplama; ad varyasyonu uyarıları; aynı gün için çift/çelişkili kayıt tespiti; 32 kişilik şablon kapasitesi kontrolü; eksik hafta/gün raporu.

**Dosyalar:** `src/.../MonthlyMerge/`; `tests/.../MonthlyMerge/` test verileri.

**Test şartları:** Ayın hafta ortasında başlaması/bitmesi; Şubat 28/29; 30/31 gün; yıl geçişi; aynı kişide yazım değişimi; çakışma; eksik hafta; kapasite aşımı.

**Tamamlanma kriteri:** Her hedef tarih-personel için en fazla bir onaylı kayıt oluşur; ay dışı kayıt yoktur; belirsizlikler sessizce çözülmez.

**Bağımlılık:** 1 ve 4'e bağlı; 6'ya matrisi verir.

## 6. Puantaj hesaplama motoru

**Durum:** MVP kod dönüşümü uygulandı: A/B/C/D/E → X; HT, RT, RP, ÜZ, Üİ, Yİ, Aİ ve G aynen korunur; Sİ reddedilir.

**Amaç:** Onaylanmış dönüşüm ve toplam kurallarını Excel'den bağımsız, test edilebilir biçimde uygulamak.

**Yapılacaklar:** Vardiya→aylık kod eşlemesi; HT/RT/izin kodları; X+G çalışma toplamı kararı; kişi toplamları; günlük kategori sayımları; bilinmeyen kod sonucu; R/Aİ/Sİ/Üİ/ÜZ kararlarının uygulanması.

**Dosyalar:** `src/.../AttendanceCalculation/` kurallar/modeller; `tests/.../AttendanceCalculation/` tablo güdümlü testler.

**Test şartları:** Her kod tek başına ve karışık ay; boş gün; ayın olmayan günü; referans Haziran satırlarının onaylanan kurala göre karşılaştırılması; tutarsız şablon formüllerine regresyon testleri.

**Tamamlanma kriteri:** Kişi ve günlük toplamlar aynı veri için deterministik; bilinmeyen kod açık hata; kullanıcıca onaylı örneklerin tamamı geçer.

**Bağımlılık:** 1 ve 5'e bağlı; 7'nin hesap girdisidir.

## 7. Excel çıktı üretimi

**Durum:** ClosedXML ile aylık şablon kopyası üzerinde personel/gün yerleşimi ve düzeltilmiş toplam formülleri uygulandı; gerçek Windows çalıştırma ve görsel kabul bekliyor.

**Amaç:** Hesaplanan ayı referans puantaj görünümünde yeni bir `.xlsx` olarak üretmek.

**Yapılacaklar:** ClosedXML ile B:AT yerleşimi; başlıklar, günler, personel ve toplamlar; formül/değer stratejisi; birleşimler; ölçüler; yazı tipi/dolgu/kenarlık/hizalama; logo/görseller; yatay A4 ve kenar boşlukları; kullanılmayan gün davranışı; hedef dosyayı yeni adla kaydetme.

**Dosyalar:** `src/.../ExcelExport/`; gerekiyorsa değiştirilmeden kullanılan şablon kopyası; `tests/.../ExcelExport/` altın dosya/yapısal testleri.

**Test şartları:** Open XML düzeyinde sayfa adı, aralık, birleşimler, değer/formüller, kolon/satır ölçüleri ve baskı ayarları; Excel/LibreOffice açılış testi; görsel baskı karşılaştırması; kaynak hash değişmezliği.

**Tamamlanma kriteri:** Çıktı uyarısız açılır, hesap sonuçları doğru, görünüm kabul edilmiş ve kaynak dosyalar değişmemiştir.

**Bağımlılık:** 1 ve 6'ya bağlı; 8'in uçtan uca test girdisidir.

## 8. Windows EXE ve testler

**Görev 6 durumu:** Son kullanıcı ayarları, ay kilitleme/kilit kaldırma ve lisans hariç ZIP yedekleme/geri yükleme tamamlandı. Core testleri macOS üzerinde geçmektedir. WinForms kaynakları WindowsDesktop SDK ile derlenmiştir; gerçek Excel COM, yazıcı ve UI kabul senaryoları Windows'ta manuel doğrulanacaktır.

**Amaç:** Özellikleri kurulabilir/taşınabilir Windows çıktısında bütünleştirmek ve kabul etmek.

**Yapılacaklar:** Release yapılandırması; x64/self-contained veya framework-dependent dağıtım kararı; uygulama veri yolu ve dosya izinleri; lisans, SQLite ve Excel akışını bağlama; hata günlüğü yalnız yerel; birim/entegrasyon/UI ve manuel kabul senaryoları; imzalama/installer ancak kullanıcı kararıyla.

**Dosyalar:** build/publish ayarları; `tests/.../EndToEnd/`; kullanıcı ve test yönergeleri; gerekiyorsa installer projesi (ayrı onayla).

**Test şartları:** Temiz Windows 10/11 x64; ağ kapalı; lisans açılışı; plan kaydet/yeniden aç; ay birleştir; Excel üret/aç/yazdır önizleme; Türkçe bölgesel ayarlar; veri klasörü izinleri; kaynak dosya bütünlüğü.

**Tamamlanma kriteri:** İlk sürüm kabul kriterleri temiz Windows ortamında geçer; EXE dışında geliştirme aracı gerekmez; sürüm ve bilinen sınırlamalar belgelenmiştir.

**Bağımlılık:** 2–7'nin tamamına bağlı; teslim görevidir.
