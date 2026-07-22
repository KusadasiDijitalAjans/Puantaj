# Lisans Mimarisi

## Amaç ve kapsam

Lisans sistemi, `PuantajApp` kopyası ve lisans dosyası başka bir bilgisayara taşındığında uygulamanın ana ekranını açmamasını sağlar. Sistem tamamen çevrimdışıdır; aktivasyon sunucusu, API, domain veya bulut bağımlılığı yoktur. Her kurulum tek otel departmanına ait lisans ve yerel veri taşır.

Haftalık plan için 26 sayfa ayrı özellik olarak ele alınmaz. Uygulamanın ileriki ekranlarında kullanılacak tek referans sayfa **`25.31.05.2026k`** olarak seçilmiştir. Sayfa görünürdür ve otel/departman, yedi tarih, çalışma+imza sütunları, personel/görev alanları, A–E vardiya saatleri, izin lejantı, onay/imza alanları ve alt açıklamayı birlikte içerir. Aylık çıktı referansı `HAZİRAN .2025 PUANTAJ` sayfasıdır.

## Tehdit modeli

Korunan senaryolar:

- EXE'nin USB, Drive veya dosya kopyasıyla başka bilgisayarda çalıştırılması.
- `license.dat` içindeki müşteri, otel, departman, cihaz veya süre alanının elle değiştirilmesi.
- Başka cihaza üretilmiş geçerli lisansın kullanılması.
- Süresi dolmuş veya yapısal olarak bozuk lisansın kullanılması.
- Müşteri uygulamasından lisans üretmeye çalışma; private key bu uygulamada yoktur.

Kapsam dışı/kalıntı riskler: yönetici yetkili saldırganın EXE'yi tersine mühendislik edip kontrolü yamaması, işletim sistemi saatini geri alması, tüm cihaz bileşenlerini taklit etmesi veya sahip bilgisayardaki private key'i ele geçirmesi. Tamamen kırılamaz masaüstü yazılım iddiası yoktur.

## Cihaz kodu üretimi

`WindowsDeviceComponentSource` aşağıdaki değerleri hata toleranslı okumaya çalışır:

1. `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid` (64-bit registry görünümü),
2. Windows sistem biriminin volume serial değeri (`GetVolumeInformation`),
3. erişilebiliyorsa BIOS/system product UUID (`wmic csproduct`),
4. erişilebiliyorsa işlemci kimliği (`wmic cpu`), aksi halde registry işlemci adı.

Tek başına MAC adresi kullanılmaz. Bulunan değerler anahtar adına göre sıralanır; baş/son boşluk, büyük/küçük harf ve tekrarlı boşluklar normalize edilir; `SHA-256` hash'i alınır. İlk 8 byte kullanıcıya `PUAN-XXXX-XXXX-XXXX-XXXX` biçiminde gösterilir. Ham donanım değerleri arayüzde veya lisans içinde gösterilmez.

Bir bileşenin okunamaması çökme nedeni değildir; kullanılabilen diğer bileşenlerle kod üretilir. Hiçbir bileşen okunamazsa uygulama anlaşılır hata vererek kapatılır. Bileşen kümesinin değişmesi cihaz kodunu değiştirir; ilk sürümde toleranslı çoğunluk eşleşmesi yoktur. Donanım/Windows kurulumu değişiminde yeni lisans gerekir.

## Dijital imza yaklaşımı

Lisans verisi deterministik JSON olarak UTF-8 serileştirilir ve **3072-bit RSA, SHA-256, RSA-PSS** ile imzalanır. Lisans kodu, lisans verisi ile Base64 imzayı içeren JSON zarfının Base64URL gösterimidir. Doğrulama sırası: kodu çözme, imzayı public key ile doğrulama, lisans sürümü, cihaz kodu, başlangıç tarihi ve bitiş tarihi.

Şifreleme yapılmaz; müşteri/otel/departman bilgileri Base64URL çözüldüğünde okunabilir. Bütünlük ve kaynak doğruluğu dijital imzayla korunur. Gizlilik hedef değildir.

## Public/private key ayrımı

- `PuantajApp`: yalnız geliştirme public key'ini `PublicKeyProvider` içinde taşır; lisans üretemez.
- `PuantajLicenseGenerator`: private key'i içine gömmez; sahip tarafından seçilen dış PEM dosyasını çalışma anında okur.
- Geliştirme private key'i yerel `secrets/development.private.pem` konumunda üretildi ve `secrets/`, `*.private.pem`, `*.pfx`, `license-private-key.*` kurallarıyla Git dışında tutulur.
- Depoya yalnız eş public key konmuştur. Generator private key bulunamazsa “Private key bulunamadı...” hatası gösterir.

Bu anahtar yalnız geliştirme içindir. Üretimden önce çevrimdışı/güvenli bilgisayarda yeni anahtar çifti üretilmeli, üretim private key'i şifreli ve erişimi sınırlı bir ortamda yedeklenmeli, yalnız public key dağıtılan uygulamaya alınmalıdır. Anahtar rotasyonu `LicenseVersion` artışı ve eski lisans geçiş planıyla yapılmalıdır.

## Lisans veri yapısı

İmzalanan `LicenseData` alanları:

| Alan | Açıklama |
|---|---|
| `LicenseId` | Her üretimde yeni GUID |
| `CustomerName` | Müşteri/yetkili adı |
| `HotelName` | Lisanslı otel |
| `DepartmentName` | Bu kurulumun tek departmanı |
| `DeviceId` | Normalize edilmiş kısa cihaz kodu |
| `IssuedAt` | UTC başlangıç/üretim zamanı |
| `ExpiresAt` | Süreli lisansın UTC bitiş zamanı; süresizde `null` |
| `IsLifetime` | Süresiz lisans işareti |
| `LicenseVersion` | Şema/doğrulama sürümü; ilk sürüm `1` |
| `Signature` | Zarf içinde Base64 RSA-PSS imzası |

## Aktivasyon akışı

1. `PuantajApp` cihaz kodunu üretir ve `%LocalAppData%\Puantaj\license.dat` dosyasını okur.
2. Dosya yok, bozuk, imzası geçersiz, süresi dolmuş veya başka cihaza aitse ana form oluşturulmaz; modal aktivasyon ekranı açılır.
3. Ekran yetkili `ERHAN DURGUN`, telefon `0 544 527 21 87`, cihaz kodu ve lisans kodu alanını gösterir.
4. Yazılım sahibi cihaz kodunu Generator'a girer; müşteri, otel, departman ve süreyi belirler; güvenli private key ile kod üretir.
5. Kullanıcı kodu girer. Uygulama embedded public key ile imzayı ve cihaz/süreyi doğrular.
6. Geçerliyse kod atomik olarak yerel lisans dosyasına yazılır ve geçici ana ekran otel/departman bilgisini gösterir. Geçersizse ana ekran açılmaz.

## Süreli ve süresiz lisans

Süreli lisansta `ExpiresAt` zorunludur; yerel UTC zamanı bitişi aştığında uygulama yenileme mesajıyla kilitlenir. Başlangıçtan önce de lisans geçerli değildir. Süresiz lisansta `IsLifetime=true`, `ExpiresAt=null` olmalıdır. Yerel saat çevrimdışı ortamda güvenilir bir otorite değildir; saat geri alma saldırısına tam koruma için çevrimiçi zaman/aktivasyon gerekir ve bu görev kapsam dışıdır.

## Lisans saklama

`LocalLicenseStore`, kodu `%LocalAppData%\Puantaj\license.dat` içine geçici dosya + atomik taşıma ile yazar. Dosya kullanıcı tarafından okunabilir; herhangi bir imzalı alan değişikliği imzayı bozar. Aynı dosya başka bilgisayara kopyalansa bile `DeviceId` uyuşmaz. Okuma hatası/eksik dosya lisans yokmuş gibi aktivasyon ekranına yönlendirir.

## Windows manuel testleri

macOS WinForms çalıştıramadığı için aşağıdakiler Windows 10/11 x64 üzerinde tamamlanmalıdır:

1. Lisans dosyasını silip App'i aç; yalnız aktivasyon ekranı görünmeli.
2. Gösterilen cihaz koduyla Generator'da süreli lisans üret; kodu kopyala/`.dat` kaydet.
3. App'te etkinleştir; geçici ana ekranda doğru otel/departman görünmeli.
4. `license.dat` içinde tek karakter değiştir; App ana ekranı açmamalı.
5. Başka cihaz koduyla lisans üret; reddedilmeli.
6. Süresi geçmiş lisans üret; belirtilen yenileme mesajı görünmeli.
7. Süresiz lisans üret; yeniden başlatmalarda açılmalı.
8. EXE ve lisansı ikinci bilgisayara kopyala; aktivasyon ekranı görünmeli.
9. Private key dosyasını kaldırıp Generator'da üretmeyi dene; açık hata görünmeli.

## Bilinen güvenlik sınırları

- Kod obfuscation, code signing ve anti-debug bu görevde yoktur.
- Offline saat manipülasyonu kesin engellenemez.
- MachineGuid/volume/UUID/CPU bilgileri ayrıcalıklı saldırgan tarafından taklit edilebilir.
- Donanım değişimi cihaz kodunu değiştirebilir; yeniden lisanslama operasyonu gerekir.
- Private key güvenliği yazılım sahibinin süreç ve yedek güvenliğine bağlıdır.
- Lisans müşteri bilgisi imzalıdır fakat şifreli değildir.
