# Puantaj

Otellerde haftalık çalışma planlarını kaydedip seçilen ay için mevcut puantaj şablonunun görünümünde aylık Excel çıktısı üretmeyi amaçlayan Windows masaüstü uygulaması.

- Girdi: `templates/HAFTALIK ÇALIŞMA PLANI 2026.xlsx`
- Çıktı referansı: `templates/HAZİRAN 2025 PUANTAJ -.xlsx`
- Planlanan teknoloji: C#, .NET 8, WinForms, SQLite ve ClosedXML
- Lisanslama: çevrimdışı, çok bileşenli cihaz kimliğine bağlı, RSA-PSS imzalı lisans kodu
- Aktivasyon: uygulama cihaz kodunu gösterir; yazılım sahibindeki ayrı Generator bu cihaza özel süreli/süresiz lisans üretir. Public key uygulamada, private key yalnız sahibindedir.
- Haftalık referans sayfa: `25.31.05.2026k` (26 sayfa ayrı özellik değildir)
- Mevcut durum: Excel analizi, lisans altyapısı ve Görev 3 Puantaj MVP kaynakları hazır; Windows derleme ve manuel kabul bekliyor.
- MVP özellikleri: aktif/pasif personel listesi, haftalık hücre ve çoklu kod atama, düzenlenebilir A–E saatleri, SQLite yerel kayıt ve aylık Excel çıktısı.
- Belge çıktıları: haftalık çalışma planı ve aylık puantaj için Excel kaydetme, PDF kaydetme ve yazdırma.
- Excel çıktıları A4, yatay, bir sayfa genişliğine sığacak ve gerçek kullanılan alanı yazdıracak şekilde hazırlanır.
- PDF ve yazdırma işlemleri Windows'ta Microsoft Excel gerektirir; Excel yoksa oluşturulan `.xlsx` korunur ve kullanıcı bilgilendirilir.
- Yerel veritabanı: `%LocalAppData%\Puantaj\puantaj.db`
- Ayarlar: otel/departman, logo, imza alanları ve yazdırma tercihleri uygulama içinden yönetilir ve açılışta otomatik yüklenir.
- Ay kilidi: aylık Excel üretiminden sonra ay isteğe bağlı kilitlenir; kilitli ayda plan değişikliği ve yeniden belge üretimi engellenir. Kilit ayrı yönetim ekranından onayla kaldırılır.
- Yedekleme: SQLite verisi ve mevcut logo tek ZIP dosyasına alınır; lisans dosyası yedeğe dahil edilmez.
- Vardiya tanımları otel bazında SQLite'tan yönetilir; kod harfine bağlı iş mantığı yoktur. “İşten Ayrıldı” özel durumu aylık puantajda seçilen günden ay sonuna kadar siyah, içeriksiz hücre üretir.
- Ana kullanıcı yüzü personel kartı düzenindedir: personel arama, dinamik hafta sekmeleri, varsayılan vardiya + istisna matrisi, hafta kopyalama, çift tıkla gün düzenleme ve canlı aylık önizleme aynı ekrandadır.
- Lisans üreticisi yalnız cihaz bağlı 1 yıllık veya süresiz lisans üretir; tarih geri alma girişimleri yerel son çalışma kaydıyla sınırlı ölçüde denetlenir.
- Belgeler: [`docs/`](docs/)
