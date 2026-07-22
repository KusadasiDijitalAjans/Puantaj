# PUANTAJ V1.0 — BAĞIMSIZ QA DENETİM RAPORU

**Denetim tarihi:** 2026-07-22
**Denetim türü:** Statik kod incelemesi + mimari/veri akışı analizi (kaynak kod, testler, dokümantasyon, derleme yapılandırması). macOS ortamında WinForms EXE çalıştırılamadığı için **UI'de fiilen tıklanarak yapılan bir kabul testi değil**; bulgular kod okunarak ve iş mantığı izlenerek çıkarılmıştır. Bu, raporun güvenilirliğini düşürmez ancak "ekranda böyle görünüyor" değil "kod böyle davranacak" ifadesiyle okunmalıdır. Windows'ta doğrulanması gereken maddeler ayrıca işaretlenmiştir.
**Kapsam:** `Puantaj.Core`, `PuantajApp` (WinForms), `PuantajLicenseGenerator`, SQLite şeması, Excel/PDF/Yazdırma, Lisanslama, Cihaz Kimliği, Backup/Restore, Ay Kilidi, testler (64 dosya, ~3.789 satır).

---

## Yönetici Özeti

Kod tabanı küçük, okunaklı ve büyük ölçüde disiplinli (parametreli SQL, transaction kullanımı, kapsamlı lisans imza doğrulaması, COM nesnesi temizliği). Ancak **denetim, ürünün ana amacını fiilen yerine getirmediğini ortaya çıkardı**: uygulamanın çalışan ekranında (MainForm → PersonnelCardControl) **aylık puantaj Excel/PDF/yazdırma çıktısı üretecek hiçbir buton yok** ve **ay kilitleme hiçbir çalışan ekrandan tetiklenemiyor**. Bu işlevler yalnızca `MonthlyExportControl` adlı, hiçbir yerden çağrılmayan "yetim" bir sınıfta duruyor. Ayrıca haftalık planlama ekranında personel/hafta değişiminde temizlenmeyen bir seçim matrisi, personel düzenlemede sessizce silinen bir "işe giriş tarihi" alanı ve geliştirme lisans anahtarının üretime taşınıp taşınmadığının doğrulanamaması gibi başka kritik bulgular da mevcut.

**Sonuç: V1.0, mevcut haliyle yayına hazır değildir.** Aşağıdaki "Kritik Hatalar" bölümündeki 5 madde çözülmeden satın alma/canlı kullanım önerilmez.

---

## 1. Kritik Hatalar

### 1.1 Aylık puantaj Excel/PDF/yazdırma çıktısı hiçbir çalışan ekrandan üretilemiyor
`MainForm.cs` yalnızca `PersonnelCardControl`'ü barındırıyor; başlıktaki tek belge eylemleri "▣ Kaydet" (haftalık) ve "▤ Yazdır" (seçili personelin haftalık planı). `MonthlyExcelExporter` sınıfı yalnızca `MonthlyExportControl.cs` içinden ve testlerden çağrılıyor; `MonthlyExportControl` ise `MainForm`, `PersonnelCardControl` veya başka hiçbir aktif dosyada `new MonthlyExportControl(...)` olarak örneklenmiyor (doğrulandı: kaynak ağacında bu sınıfı örnekleyen tek bir çağrı yok).
Sonuç: Kullanıcı otelde aylık puantajı **Excel olarak kaydedemiyor, PDF alamıyor, yazdıramıyor**. Bu, projenin `docs/PROJE_KAPSAMI.md` içinde tanımlı birinci sınıf kabul kriteridir ("Aylık kişi toplamları... hesaplanır", "Üretilen `.xlsx`... kabul testini geçer").
**Aynı zamanda ay kilitleme de bu yoldan tetikleniyor** (`MonthlyExportControl.AskToLockMonth()` → `_database.LockMonth(...)`, kod tabanında `LockMonth(` çağrısının **tek** yeri budur). `LockedMonthsControl` yalnızca kilit **kaldırmayı** destekliyor; kilit **koymanın** başka hiçbir yolu yok. Sonuç: bu haliyle bir ay **hiçbir zaman kilitlenemez**.
**Etki:** Ürünün reklam edilen ana işlevi çalışmıyor. **Yayın engelleyici.**
**Konum:** `src/PuantajApp/MainForm.cs`, `src/PuantajApp/MonthlyExportControl.cs`, `src/Puantaj.Core/Excel/MonthlyExcelExporter.cs`.

### 1.2 İstisna matrisi personel/hafta değişince temizlenmiyor → yanlış kişiye/haftaya veri yazılabilir
`PersonnelCardControl` içinde işaretlenen "istisna matrisi" (`_matrix`) yalnızca `GenerateWeek` başarıyla tamamlandığında (`ClearMatrix()`) veya kullanıcı elle "Seçimleri Temizle"ye bastığında sıfırlanıyor. `_employees.SelectedIndexChanged` (personel değişimi) ve `_weeks.SelectedIndexChanged` (hafta değişimi) olayları yalnızca `RefreshPerson()`/`RefreshWeek()` çağırıyor; ikisi de matrisin checkbox **değerlerini** temizlemiyor (`RefreshWeek` sadece ay-dışı günleri devre dışı bırakıp false yapıyor, seçili günleri değil).
**Gerçek senaryo:** Kullanıcı personel A için Salı gününe "Yıllık İzin" işaretler, "Haftayı Oluştur"a basmadan personel B'ye geçer, B için varsayılan vardiyayı seçip "Haftayı Oluştur"a basar → A'nın Salı istisnası **B'ye uygulanır**. Otelde çok personelli hızlı veri girişinde bu senaryo kaçınılmaz şekilde tekrar edecektir.
**Etki:** Sessiz veri bozulması; puantaj güvenilirliğini doğrudan tehdit eder. **Yayın engelleyici.**
**Konum:** `src/PuantajApp/PersonnelCardControl.cs:141-147` (`RefreshPerson`), `176-190` (`RefreshWeek`), `257-258` (matris temizleme yalnızca `GenerateWeek` sonrası).

### 1.3 "İşe giriş tarihi" (HireDate) hiçbir ekrandan girilemiyor ve her düzenlemede sessizce siliniyor
`Employee.HireDate` alanı veritabanında, dışa aktarımlarda ve `PersonnelCardControl` bilgi panelinde ("Giriş: dd.MM.yyyy") gösteriliyor, ancak `EmployeesControl.EditEmployee()` her düzenlemede `_database.UpdateEmployeeDetails(id, position, pattern, null)` çağırıyor — üçüncü parametre **her zaman literal `null`**. `PuantajDatabase.UpdateEmployeeDetails` de bu değeri koşulsuz `UPDATE ... SET HireDate=$hire` ile yazıyor. Sonuç: (a) hiçbir ekranda bu alanı **ayarlayacak** bir tarih seçici yok (proje genelinde `HireDate` için tek bir `DateTimePicker`/giriş kontrolü bulunmuyor), (b) var olan bir HireDate değeri (ör. ileride eklenecek bir göç betiğiyle) personel her "Düzenle"ye tıklandığında **kalıcı olarak sıfırlanır**.
**Etki:** Dokümante edilmiş bir MVP özelliği ("varsa işe giriş tarihi") fiilen hiç çalışmıyor; ayrıca personel düzenleme akışı veri kaybına neden oluyor.
**Konum:** `src/PuantajApp/EmployeesControl.cs:63-74`, `src/Puantaj.Core/Data/PuantajDatabase.cs:152-157`.

### 1.4 Personel düzenlerken bir alt-diyalog iptal edilirse mevcut değer siliniyor
Aynı `EditEmployee()` akışında ad, görev ve çalışma şekli sırayla üç ayrı `PromptDialog.Show(...)` ile soruluyor. Kullanıcı "Görevi" veya "Çalışma şekli" diyaloğunda **İptal**'e basarsa `PromptDialog.Show` `null` döner ve kod `position ?? ""` / `pattern ?? ""` ile bunu **boş string**e çeviriyor; önceki değeri korumuyor. Yani bir personelin adını değiştirmek isteyen kullanıcı, ikinci veya üçüncü diyalogda yanlışlıkla İptal'e basarsa (ya da bilinçli olarak "bu alanı değiştirmek istemiyorum" niyetiyle kapatırsa) mevcut görev/çalışma şekli bilgisini kaybeder.
**Etki:** Sık kullanılan bir akışta beklenmedik veri kaybı.
**Konum:** `src/PuantajApp/EmployeesControl.cs:63-74`.

### 1.5 Üretim lisans anahtarı geçişi doğrulanamıyor — geliştirme anahtarıyla yayına çıkma riski
`PublicKeyProvider.cs` içindeki yorum açıkça uyarıyor: *"Development public key only. Replace it together with the securely held production private key before production distribution."* Depoda yalnızca bir "development" özel anahtarı var (`secrets/development.private.pem`, izinleri kısıtlı ve `.gitignore` ile hariç tutulmuş — bu doğru). Ancak bu denetimde **üretime özel yeni bir anahtar çiftinin üretilip `PublicKeyProvider.Pem` içine gömüldüğüne dair hiçbir kanıt yok**; repo hâlâ geliştirme anahtarını taşıyor. Bu haliyle EXE dağıtılırsa: (a) geliştirme private key'i her nasılsa ele geçirilirse **tüm müşteriler için** sahte lisans üretilebilir, (b) anahtar döngüsü (`LicenseVersion` artışı, eski lisans geçişi) hiç uygulanmamış olur.
**Etki:** Lisanslama modelinin bütün güvenilirliği bu tek maddeye bağlı; atlanırsa lisans sistemi göstermelik kalır. **Yayın öncesi mutlaka doğrulanmalı.**
**Konum:** `src/PuantajApp/PublicKeyProvider.cs`, `docs/LISANS_MIMARISI.md` (satır 47).

---

## 2. Orta Seviye Hatalar

1. **UI iş parçacığı Excel/COM işlemlerinde donuyor.** `WeeklyExcelExporter`/`MonthlyExcelExporter` (ClosedXML) ve `ExcelInteropService` (COM Interop) tüm çağrı zincirinde senkron çalışıyor; `Task.Run`, `BackgroundWorker` veya `async/await` hiçbir yerde kullanılmıyor. 32-100 personelli bir aylık şablon üretimi + gizli bir `Excel.Application` başlatıp `ExportAsFixedFormat`/`Dialogs[8].Show()` çağırmak saniyeler sürebilir; bu süre boyunca pencere "Yanıt Vermiyor" durumuna düşer. Daha kötüsü: Excel görünmez (`Visible=false`) çalıştığı için, dosya başka bir Excel oturumunda açıksa veya makro/uyumluluk uyarısı çıkarsa, kullanıcının **görmediği ve kapatamadığı** bir modal pencere COM çağrısını sonsuza kadar bekletebilir — uygulamayı görünürde tamamen kilitler, çözüm yalnızca Görev Yöneticisi'nden sonlandırmaktır.
   *Konum:* `src/PuantajApp/ExcelInteropService.cs`, tüm `*Control.cs` dosyalarındaki `SaveExcel/SavePdf/Print` metodları.
2. **Tek örnek (single-instance) koruması yok.** Uygulama simgesine iki kez tıklanması (ya da kısayolun yanlışlıkla iki kez açılması, yaygın bir kullanıcı hatası) aynı SQLite dosyasına karşı iki bağımsız `MainForm` başlatır; eşzamanlı yazımlar "database is locked" hatalarına veya `license-time.dat` saat-koruma dosyasının çakışan biçimde güncellenmesine yol açabilir.
   *Konum:* `src/PuantajApp/Program.cs`.
3. **Şablon bulma, dosya adı alt dizesiyle eşleştirme yapıyor — kırılgan.** `FindWeeklyTemplate`/`FindMonthlyTemplate` `templates` klasöründe adında "HAFTALIK"/"PUANTAJ" geçen **tam olarak bir** `.xlsx` bekliyor. Kullanıcı şablonu Excel'de önizlemek için açarsa Office otomatik olarak `~$HAFTALIK ÇALIŞMA PLANI 2026.xlsx` gizli kilit dosyası oluşturur — bu da adında "HAFTALIK" geçen **ikinci bir `.xlsx`** olarak sayılır ve dışa aktarma tamamen kırılır ("tek bir HAFTALIK şablon bulunmalıdır" hatası). Aynı şey antivirüs karantina/yedek kopyalarında da olur.
   *Konum:* `src/Puantaj.Core/Excel/WeeklyExcelExporter.cs:90-99`, `MonthlyExcelExporter.cs:103-113`.
4. **Türkçe İ/ı büyük-küçük harf normalizasyonu tutarsız olabilir.** `AttendanceCodes.Normalize`, `PuantajDatabase.RequireCode` ve `DeviceIdentityService.Normalize` `ToUpperInvariant()` kullanıyor. .NET'te değişmez kültürde küçük `"i".ToUpperInvariant()` → `"I"` (noktasız), **`"İ"` değil**. Mevcut sabit kodlar ("Üİ", "Yİ", "Aİ", "İA") zaten doğru harfle tanımlı olduğundan bugün sorun yaratmıyor, ancak bir kullanıcı Ayarlar → Vardiya ve Kod Tanımları ekranından **yeni bir özel kod** girerken Türkçe klavye dışı bir düzenle "üi" yazarsa, görünüşte aynı ama farklı bir anahtarla ("Üi" ≠ "Üİ") ayrı bir kayıt oluşur; kafa karıştırıcı, sessiz kopyalara yol açar.
   *Konum:* `src/Puantaj.Core/Data/AttendanceCodes.cs`, `PuantajDatabase.cs:463-464`.
5. **Restore sonrası hiçbir ekran otomatik yenilenmiyor.** `SettingsControl.Restore()` başarılı olduğunda kullanıcıya "Tüm ekranların yenilenmesi için programı kapatıp yeniden açın" deniyor — teknik olarak doğru (SQLite bağlantı havuzu temizleniyor) ama şu an açık olan `PersonnelCardControl`, `EmployeesControl` vb. ekranlar bellekte eski veriyle kalmaya devam eder; kullanıcı restore sonrası kapatmayı unutursa **eski (geri yüklenmeden önceki) veri üzerinde çalışmaya devam edip üstüne yeni kayıtlar ekleyebilir**, bu da geri yüklemenin faydasını sessizce iptal eder.
   *Konum:* `src/PuantajApp/SettingsControl.cs:76-90`.
6. **Restore güvenlik yedekleri hiç temizlenmiyor.** `restore-oncesi-*.zip` dosyaları veritabanı klasörüne (`%LocalAppData%\Puantaj`) sınırsız sayıda birikir; disk temizliği veya rotasyon yok. Yıllar içinde çok sayıda restore işlemi yapan bir otelde AppData klasörü şişebilir.
   *Konum:* `src/PuantajApp/SettingsControl.cs:84-85`.
7. **`MonthlyExportControl.AskToLockMonth()` ay zaten kilitliyken de soruyor** (mevcut kod ay durumunu kontrol etmiyor); pratikte bu ekran hiç açılmadığı için (bkz. 1.1) şu an gözlemlenemez, ama 1.1 düzeltilirse bu ikinci bir küçük kusur olarak ortaya çıkacaktır.
8. **`wmic` komutu modern Windows'ta kaldırılmış/isteğe bağlı olabilir.** `WindowsDeviceComponentSource`, BIOS UUID ve İşlemci Kimliği için `wmic.exe`'ye bağımlı; Microsoft bu aracı Windows 11 24H2+ sürümlerinde varsayılan kurulumdan çıkarmaya başladı. Kod bu bileşenlerin `null` dönmesini tolere ediyor (çökmüyor) ama cihaz kimliği yalnızca `MachineGuid` + `SystemVolumeSerial`'e daralabilir — bu iki bileşen sistem geri yükleme/temiz kurulumda değişebileceğinden lisans reaktivasyon ihtiyacı artabilir.
   *Konum:* `src/Puantaj.Core/Device/WindowsDeviceComponentSource.cs`.
9. **`ExcelBranding.AddLogo`, logo dosyası bozuksa veya desteklenmeyen bir biçimdeyse (ör. yarım inen bir PNG) istisna fırlatabilir**, bu da tüm dışa aktarma işlemini (haftalık/aylık/print/PDF) engeller — logo hiçbir yerde try/catch ile izole edilmemiş.
   *Konum:* `src/Puantaj.Core/Excel/ExcelBranding.cs:27-34`.
10. **Aynı ada sahip iki personel ayırt edilemiyor.** `EmployeesControl`/`PersonnelCardControl` arama ve listelemede yalnızca `FullName` gösteriliyor; sıra numarası dışında ayırt edici bir kimlik (ör. sicil no) yok. Küçük ama gerçek otel senaryosu: iki "Mehmet Yılmaz".

---

## 3. Düşük Öncelikli Hatalar

1. `WeeklyPlanControl.cs` ve `MonthlyExportControl.cs` tamamen ölü koddur (hiçbir aktif ekrandan çağrılmıyor, yalnızca birbirlerini ve `Puantaj.Core`'u referans alıyorlar). Bakımı ilerledikçe kafa karışıklığına yol açar ve derleme süresini gereksiz uzatır. (Not: `MonthlyExportControl` aslında eksik olan aylık dışa aktarım işlevselliğinin **kaynağı** olabilir — bkz. 1.1 önerisi.)
2. `PuantajApp` ve `PuantajLicenseGenerator` projelerinde `TreatWarningsAsErrors` **kapalı** (yalnızca `Puantaj.Core.csproj`'da açık); WinForms katmanındaki nullable/analiz uyarıları sessizce göz ardı edilebilir.
3. Uygulamalarda özel bir `.ico` simgesi veya `app.manifest` yok; varsayılan WinForms simgesi görev çubuğunda görünecektir — ticari bir üründe markalama eksikliği.
4. `PromptDialog`/`CodeChoiceDialog` gibi ad hoc diyalog sınıfları kod tekrarına yol açıyor (benzer 6-8 satırlık form kurulum kodu birden fazla dosyada tekrarlanıyor); işlevsel bir hata değil ama bakım maliyeti.
5. `AssignmentCodeResolver` her çağrıda yeniden `ToDictionary` kuruyor (ör. `WeeklyExcelExporter.Export`, `MonthlyExcelExporter.Export`, `RefreshMonthly` her seferinde yeni resolver); performans sorunu yaratmayacak kadar küçük ama gereksiz tahsis.
6. `LockedMonthsControl` listesinde kilitleme tarihi (`LockedAt`) kullanıcıya gösterilmiyor; sadece ay/yıl görünüyor — denetim amaçlı yardımcı olurdu.
7. `ShiftSettingsControl.Save()` tüm satırları döngüyle tek tek `SaveAssignmentCode` çağırarak kaydediyor (transaction yok); çok satırlı bir kaydetmede yarıda bir hata olursa kısmi kayıt oluşabilir (düşük olasılık, küçük veri seti).

---

## 4. UI Problemleri

1. **(Kritik ile çakışan ama UI açısından da not edilmeli)** Ana ekranda aylık çıktılar için buton yok — kullanıcı "aylık puantajımı nasıl alırım?" sorusuna ekranda cevap bulamaz.
2. Excel görünmez modda çalışırken yazdırma/PDF diyalogları görev çubuğunda temsilci pencere olmadan açılabilir; kullanıcı "hiçbir şey olmadı" sanıp tekrar tekrar tıklayabilir, bu da arka planda birden fazla `EXCEL.EXE` örneği başlatabilir (her tıklamada yeni `CreateExcel()` çağrısı).
3. `PersonnelCardControl` içinde "Kapat" (MainForm) veya diyalog kapatma öncesi kaydedilmemiş matris seçimleri için hiçbir uyarı yok; kullanıcı işaretleme yapıp "Haftayı Oluştur"a basmadan pencereyi kapatırsa veri sessizce kaybolur (bu, 1.2'deki kirlenme riskiyle simetrik ama farklı bir sorun — burada en azından veri başka kişiye yazılmıyor, sadece kayboluyor).
4. `MessageBox` metinleri tutarlı Türkçe ve nazik ama bazı hata mesajları ham istisna mesajını (`exception.Message`) doğrudan kullanıcıya gösteriyor (`ShowOutputError`, `SettingsControl.ShowError`, `PersonnelCardControl.GenerateWeek` vb.) — teknik/İngilizce .NET/SQLite hata metinleri son kullanıcıya sızabilir (ör. "SQLite Error 5: 'database is locked'").
5. `_matrix` (istisna seçim tablosu) satır başlıkları için sabit 170px genişlik kullanılıyor; uzun açıklamalı özel kodlar (ör. "Ücretsiz İzin (Sağlık)") kesilebilir — DPI ölçeklemesi Windows'ta doğrulanmadı (GOREV 6 raporunda da açık madde olarak listeleniyor).
6. Kilitli ay göstergesi yalnızca ayrı bir "Kilitli Aylar" diyaloğunda var; `PersonnelCardControl` ana ekranında seçili ay kilitliyse kullanıcıya proaktif bir görsel ipucu (ör. kilit simgesi/banner) yok — kullanıcı ancak bir değişiklik yapmayı **denediğinde** hatayla öğreniyor.

---

## 5. Performans Riskleri

1. Excel/PDF/Yazdırma işlemlerinin UI iş parçacığında senkron çalışması (bkz. Orta #1) — en büyük performans/algılanan-donma riski.
2. `PersonnelCardControl.RefreshMonthly()` her personel değişiminde `_monthly` `DataGridView`'in tüm sütunlarını (gün sayısı kadar, 28-31) sıfırdan yeniden oluşturuyor; tek satırlı görünüm için sorun değil ama personel listesi büyüdükçe (100+) hızlı ileri-geri tıklamalarda gözle görülür gecikmeye neden olabilir.
3. Her veritabanı çağrısı (`Open()`) yeni bir `SqliteConnection` açıp kapatıyor; `Microsoft.Data.Sqlite` bağlantı havuzlaması sayesinde bu genelde ucuz olsa da, WAL modu **açık değil** — varsayılan rollback-journal modunda çoklu ekran/arka plan işlemi eşzamanlı yazma denerse kilitlenme riski, tek-örnek olmayan kullanım (Orta #2) ile birleşince artar.
4. `ExcelInteropService.CloseExcel` her çağrıda `GC.Collect()` + `GC.WaitForPendingFinalizers()` işlemini **iki kez** art arda yapıyor — COM sızıntısını önlemek için yaygın ama pahalı bir desendir; sık yazdırma/PDF alan bir kullanıcıda gözle görülür ek gecikmeye neden olabilir. Kabul edilebilir bir tercih ama bilinçli bir maliyet olarak not edilmeli.
5. 100 personelli senaryo yalnızca listeleme/sıralama için test edilmiş (`DatabaseListsOneHundredEmployeesInDisplayOrder`); 100 personel × tam ay için `GetAssignmentsForPeriod` + Excel/COM dışa aktarımının uçtan uca gerçek performansı (özellikle COM Interop ile PDF/print) hiçbir testte ya da GOREV raporunda ölçülmemiş.

---

## 6. Güvenlik Riskleri

1. **Geliştirme lisans anahtarının üretime taşınıp taşınmadığı doğrulanamıyor** (bkz. Kritik 1.5) — en önemli güvenlik bulgusu.
2. Lisans verisi **imzalı ama şifreli değil** (bilinçli tasarım kararı, dokümante edilmiş). Müşteri/otel/departman bilgisi lisans kodunu Base64URL çözen herkes tarafından okunabilir. Gizlilik hedeflenmediği için bu bir "kusur" değil ama üçüncü bir tarafın eline geçen bir lisans kodundan otel/müşteri bilgisi çıkarılabileceği unutulmamalı.
3. Cihaz kimliği bileşenleri (`MachineGuid`, `VolumeSerial`, `BiosUuid`, `ProcessorId`) yönetici yetkili bir saldırgan tarafından taklit edilebilir/manipüle edilebilir — dokümanda zaten kapsam dışı olarak kabul edilmiş, tekrar teyit edildi.
4. `LicenseClockGuard`, yalnızca **geriye** 12 saatten fazla saat oynamasını engelliyor; ileri alınan saat hiç kontrol edilmiyor (bilinçli/dokümante edilmiş sınırlama — süresi dolmadan önce ileri atıp normale dönme senaryosu düşük risk çünkü `ExpiresAt` kontrolü ayrıca her açılışta çalışan `now`'a bakıyor, ancak "12 saatten az" geri alma pencereleri kısa vadeli suistimale hâlâ açık).
5. `secrets/development.private.pem` deposu doğru şekilde `.gitignore` ile hariç tutulmuş ve dosya izinleri (`600`) kısıtlı — bu doğru yapılmış, olumlu bir bulgu olarak not edilmeli.
6. SQL enjeksiyonu: Tüm sorgular parametreli; kullanıcıdan gelen hiçbir veri doğrudan `CommandText`'e enterpolasyon yapılmıyor. Bu katmanda risk tespit edilmedi.
7. `wmic cpu get ProcessorId`/`wmic csproduct get uuid` komutları `Process.Start` ile çalıştırılıyor; argümanlar sabit literal olduğundan komut enjeksiyonu riski yok.

---

## 7. Veri Kaybı Riskleri

1. **HireDate her personel düzenlemesinde sıfırlanıyor** (Kritik 1.3) — en somut veri kaybı.
2. **İptal edilen alt-diyalog mevcut değeri boşaltıyor** (Kritik 1.4).
3. **İstisna matrisinin başka personele/haftaya sızması** (Kritik 1.2) — teknik olarak "kayıp" değil ama yanlış kişiye yazılan veri, geri dönüşü daha da zor bir veri bütünlüğü sorunu.
4. Restore işlemi öncesi otomatik güvenlik yedeği alınıyor (`SettingsControl.Restore`) — bu doğru ve olumlu bir tasarım; ancak restore sırasında `ArchiveEntry.ExtractToFile` veya logo geri yükleme adımı yarıda hata verirse (bkz. `PuantajBackupService.Restore`, satır 56-72), ana veritabanı zaten `File.Copy` ile üzerine yazılmış olur ve yalnızca logo/ayarlar adımı yarım kalabilir — kısmi ama kurtarılabilir tutarsızlık (güvenlik yedeği sayesinde kurtarılabilir, ama otomatik değil, kullanıcı manuel olarak `restore-oncesi-*.zip`'i geri yüklemeyi bilmeli).
5. Kilitli ay kaldırıldıktan sonra (`UnlockMonth`) o aya ait geçmiş kayıtlar hiçbir "değişiklik geçmişi/audit" olmadan doğrudan değiştirilebiliyor — kilit kaldırıldıktan sonra kim, ne zaman, neyi değiştirdiği hiçbir yerde loglanmıyor (bkz. Bölüm 9).
6. Yedekleme dosyaları (backup zip) şifresiz; fiziksel olarak USB'ye alınıp kaybolursa otel/personel verisi açık biçimde ifşa olabilir (lisans dosyası hariç tutuluyor ama personel adları, izin/rapor kodları dahil).

---

## 8. Kullanılabilirlik Problemleri

1. Aylık çıktı işlevinin ekranda bulunmaması (bkz. 1.1) tek başına en büyük kullanılabilirlik sorunudur.
2. Personel düzenleme akışının üç ayrı sıralı diyalog kutusuyla yapılması (ad → görev → çalışma şekli) hem yavaş hem hataya açık (bkz. 1.4); tek bir form olması gerekirdi.
3. "İşe giriş tarihi" alanı hiçbir yerde düzenlenemiyor ama bilgi panelinde placeholder olarak yer kaplıyor — kullanıcı bunun nasıl doldurulacağını arayıp bulamayacak.
4. Ay kilidi kaldırıldığında kullanıcıya "bu aya ait kayıtlar yeniden değiştirilebilir olacak" deniyor ama kilitleme hiçbir zaman mümkün olmadığından bu ekranın pratik faydası şu an sıfır.
5. Hata mesajlarının çoğu iyi Türkçeleştirilmiş olsa da bazı ham `.NET`/`SQLite` istisna metinleri kullanıcıya sızıyor (bkz. UI #4) — otel personeli için anlaşılmaz olabilir.

---

## 9. Kod Kalitesi Riskleri

1. **Sıfır loglama altyapısı.** Projede `ILogger`, dosya logu, Windows Event Log veya benzeri hiçbir mekanizma yok. Tüm hatalar yalnızca bir `MessageBox` ile kullanıcıya gösterilip kayboluyor. Uzak destekte (yazılım sahibi telefonla destek veriyor, `docs/LISANS_MIMARISI.md`'de belirtildiği gibi) "ekranda ne yazıyordu?" sorusuna güvenmek zorunda kalınacak; bir dosya logu (ör. `%LocalAppData%\Puantaj\logs\`) olmadan uzaktan teşhis pratik olarak imkânsız.
2. Ölü kod (`WeeklyPlanControl`, `MonthlyExportControl`) — bkz. Düşük #1.
3. `TreatWarningsAsErrors` yalnızca Core projesinde aktif; WinForms katmanı daha gevşek denetleniyor.
4. Çok sayıda dosyada (ör. `PersonnelCardControl.cs`, `PuantajDatabase.cs`) satırlar bilinçli olarak çok yoğun/tek satıra sıkıştırılmış (ör. `MoveEmployee`, `BuildLayout`, `RefreshWeek`) — okunabilirlik açısından yoğun ama incelemede hataya neden olmadı; ekip tercihi olarak kabul edilebilir, yine de yeni katılacak bir geliştirici için öğrenme eğrisini artırır.
5. Test kapsamı yalnızca `Puantaj.Core`'da; `PuantajApp` (tüm WinForms ekranları — asıl kullanıcı akışının bulunduğu yer) için **hiçbir otomatik test yok**. Bu, tam olarak 1.1-1.4 gibi UI-katmanı hatalarının hiçbirinin bir CI/test koşusunda yakalanamamış olmasının doğrudan nedenidir.
6. `AssignmentCodeResolver`'ın her çağrı noktasında yeniden inşa edilmesi (bkz. Düşük #5) hafif bir tekrar/performans kokusu.
7. `ExcelPageSetup.EnsureSavedA4`, ClosedXML'in kaydettiği sayfa ayarını Open XML SDK ile **tekrar** açıp düzeltiyor (ClosedXML API'sinin bazı sayfa ayarı alanlarını beklendiği gibi yazmadığı bilinen bir sınırlama nedeniyle olduğu anlaşılıyor) — çalışıyor ama iki farklı Excel kütüphanesine (ClosedXML + DocumentFormat.OpenXml) bağımlılık ve aynı dosyanın iki kez açılıp kaydedilmesi kırılgan bir desen; ClosedXML sürüm güncellemesinde sessizce bozulabilir.

---

## 10. Yayına Hazır mı? — QA Skoru ve Karar

### Genel Puan: **41 / 100**

| Kategori | Ağırlık | Not |
|---|---|---|
| Temel işlevsellik (ana amaç) | Kritik | Aylık çıktı üretilemiyor → ürün kendi amacını yerine getirmiyor |
| Veri bütünlüğü | Kritik | Matris sızıntısı + HireDate kaybı |
| Lisanslama mimarisi (tasarım) | İyi | Kriptografik tasarım sağlam; üretim anahtarı teyidi eksik |
| Kod kalitesi / test disiplini (Core) | İyi | Parametreli SQL, kapsamlı lisans testleri, transaction kullanımı |
| Test kapsamı (UI) | Zayıf | PuantajApp'te sıfır otomatik test |
| Loglama/teşhis | Yok | Hiç loglama altyapısı yok |
| Performans (algılanan) | Riskli | Senkron COM/Excel işlemleri UI'yi dondurabilir |
| Windows'a özgü doğrulama | Beklemede | Hiçbir madde Windows'ta fiilen çalıştırılarak doğrulanmadı |

### V1.0 Yayına Hazır mı? **HAYIR.**

Yayın için asgari şart: Bölüm 1'deki 5 kritik maddenin tamamı kapatılmalı ve en az bir Windows makinesinde uçtan uca kabul testi (lisans aktivasyonu → personel ekleme → haftalık plan → **aylık Excel/PDF/yazdırma** → ay kilitleme → kilit kaldırma → yedekleme/geri yükleme) elle yürütülmelidir. Bu adım atlanırsa, otel personeli ilk ayın sonunda "aylık puantajımı nasıl alacağım?" sorusuyla karşı karşıya kalacaktır.

---

## 11. Yayın Öncesi Düzeltilmesi Gerekenler (Release Blocker)

1. Aylık Excel/PDF/yazdırma çıktısını ve ay kilitleme tetiklemesini ana ekrana (MainForm/PersonnelCardControl) bağlamak — muhtemelen `MonthlyExportControl`'ün mantığını yeniden kullanarak.
2. `PersonnelCardControl` istisna matrisini personel **ve** hafta değişiminde (`RefreshPerson`/`RefreshWeek` başında) koşulsuz temizlemek.
3. `EmployeesControl.EditEmployee()` akışını tek bir formda toplamak (ad/görev/çalışma şekli/**işe giriş tarihi**) ve iptal edilen alanların mevcut değeri koruduğundan emin olmak; HireDate için gerçek bir giriş alanı eklemek.
4. Üretim RSA anahtar çiftinin üretilip üretilmediğini, `PuantajApp`'e gömülen `PublicKeyProvider.Pem`'in gerçekten üretim public key'i olduğunu ve geliştirme private key'inin hiçbir dağıtım paketine dahil edilmediğini teyit etmek.
5. Excel/COM/ClosedXML işlemlerini UI iş parçacığından ayırmak (en azından bir "İşleniyor..." meşgul göstergesiyle) ve makul bir zaman aşımı/iptal mekanizması eklemek.
6. Şablon bulma mantığını sağlamlaştırmak: `~$*.xlsx` gizli kilit dosyalarını filtrelemek ve birden fazla eşleşme olduğunda kullanıcıyı açık bir seçim diyaloğuna yönlendirmek.
7. Tek-örnek (Mutex) koruması eklemek.
8. En azından dosya tabanlı asgari bir hata/işlem günlüğü eklemek (`%LocalAppData%\Puantaj\logs\`).

## 12. Yayın Sonrasına Bırakılabilecekler

1. Ölü kodun (`WeeklyPlanControl`, kullanılmayan kısımlar) temizlenmesi.
2. Türkçe İ/ı normalizasyon tutarlılığı (mevcut sabit kodlar etkilenmiyor; yalnızca gelecekte girilecek özel kodlar için risk).
3. Restore güvenlik yedeklerinin otomatik rotasyonu/temizliği.
4. Uygulama simgesi/marka kimliği, DPI görsel ince ayarları.
5. `ExcelPageSetup.EnsureSavedA4`'ün ClosedXML/OpenXML çifte bağımlılığının sadeleştirilmesi.
6. Aynı isimli personelleri ayırt etmek için görünür bir sicil no alanı.
7. `TreatWarningsAsErrors`'ın WinForms projelerine de yayılması.

---

## Ek A — Gözden Geçirilen Gerçek Otel Senaryoları (Kod Analizine Dayalı, 100+ Madde)

Aşağıdaki tablo, kodun bu senaryoyu nasıl ele aldığını (veya alamadığını) satır satır izleyerek hazırlanmıştır. **"Test"** sütunu, senaryonun mevcut otomatik test paketinde (xUnit) doğrulanıp doğrulanmadığını gösterir; işaretli olmayanlar yalnızca kod okumasıyla değerlendirilmiştir ve Windows'ta elle doğrulanmalıdır.

### Personel yönetimi (1–14)
| # | Senaryo | Durum | Test |
|---|---|---|---|
| 1 | Tek personel ekleme | OK | ✔ |
| 2 | 100 personel ekleme/sıralama | OK | ✔ |
| 3 | Personel adını düzenleme | OK | — |
| 4 | Personel düzenlerken görev alanını değiştirme | OK ama iptal veri kaybı riski | — |
| 5 | Personel düzenlerken "İşe giriş tarihi" girme | **Çalışmıyor (UI yok)** | — |
| 6 | Personeli düzenleyip her seferinde HireDate'in silinmesi | **Bug (1.3)** | — |
| 7 | Personeli pasif yapma | OK | ✔ |
| 8 | Pasif personeli tekrar aktif etme | OK | ✔ |
| 9 | Pasif personelin geçmiş ay dışa aktarımında görünmesi | OK | ✔ |
| 10 | Pasif personelin aktif ay aramasında görünmemesi | OK | ✔ |
| 11 | Aynı ada sahip iki personel ekleme | Ayırt edilemiyor (Orta #10) | — |
| 12 | Personel sırasını yukarı/aşağı taşıma | OK | — |
| 13 | Personel arama (kısmi ad) | OK | — |
| 14 | Boş ad ile personel ekleme denemesi | Reddediliyor (`RequireName`) | — |

### Haftalık plan / vardiya ataması (15–34)
| # | Senaryo | Durum | Test |
|---|---|---|---|
| 15 | Varsayılan vardiya + istisna ile hafta oluşturma | OK | ✔ |
| 16 | Aynı güne iki kez atama (ikincisi geçerli) | OK | ✔ |
| 17 | Haftada 0 HT | OK | ✔ |
| 18 | Haftada 2 HT | OK | ✔ |
| 19 | Aynı haftada RT + RP | OK | ✔ |
| 20 | Ayın ilk haftası, ay öncesi günlerin dışlanması | OK | ✔ |
| 21 | Ayın son haftası, ay sonrası günlerin dışlanması | OK | ✔ |
| 22 | Boş/bekleyen hafta durumu | OK | ✔ |
| 23 | Eksik (kısmi doldurulmuş) hafta durumu | OK | ✔ |
| 24 | Tamamlanmış hafta durumu | OK | ✔ |
| 25 | Hafta kopyalama (aynı personel, sonraki hafta) | OK | ✔ |
| 26 | Hafta kopyalama, ay dışı günlerin hariç tutulması | OK | ✔ |
| 27 | Hafta kopyalama, birden fazla hedef personel | OK (UI destekliyor) | — |
| 28 | Hedefte mevcut kayıt varken üzerine yazma onayı | OK | — |
| 29 | **Personel A'da işaretlenip kaydedilmeyen istisnaların Personel B'ye geçmesi** | **Bug (1.2)** | — |
| 30 | Gün üzerine çift tıklayarak manuel kod değiştirme | OK | — |
| 31 | Manuel gün değişikliğinde kilitli ay kontrolü | OK | — |
| 32 | Özel/dinamik kod tanımlama (ör. "Z9 Gece Ekibi") | OK | ✔ |
| 33 | Vardiya saatlerini SS:DD dışında bir biçimde girme | Reddediliyor (`FormatException`) | — |
| 34 | Tanımsız bir kodla atama denemesi | Reddediliyor | ✔ |

### İşten ayrılma (35–42)
| # | Senaryo | Durum | Test |
|---|---|---|---|
| 35 | Ayın ortasında işten ayrılma → sonraki günler siyah/boş | OK | ✔ |
| 36 | Ayın ilk günü işten ayrılma | OK | ✔ |
| 37 | Ayın son günü işten ayrılma | OK | ✔ |
| 38 | İşten ayrılma yokken normal ay | OK | ✔ |
| 39 | İşten ayrılan personelin aylık toplamlarda hariç tutulması | OK | ✔ |
| 40 | İşten ayrılan personelin haftalık şablonda siyah hücre alması | OK | ✔ |
| 41 | İşten ayrılma sonrası iç kodun ("İA"/"F") dışa yansımaması | OK | ✔ |
| 42 | İşten ayrılmış personelin tekrar "aktif" yapılması senaryosu | Kısmen — `IsActive` ile `İşten Ayrıldı` kodu **bağımsız** iki kavram; biri diğerini otomatik güncellemiyor (kullanıcı ikisini de elle yönetmeli) | — |

### Ay kilidi (43–54)
| # | Senaryo | Durum | Test |
|---|---|---|---|
| 43 | Ayı kilitleme | **UI'den imkânsız (1.1)** | ✔ (yalnızca Core testinde) |
| 44 | Kilitli ayda tekli atama denemesi | Reddediliyor | ✔ |
| 45 | Kilitli ayda toplu (bulk) atama denemesi | Reddediliyor | — |
| 46 | Kilitli ayda hafta kopyalama denemesi | Reddediliyor | — |
| 47 | Kilitli ayda personel düzenleme (ad/görev) | İzin veriliyor (doğru — kişi kaydı ay'a bağlı değil) | — |
| 48 | Kilitli ayın Excel/PDF/yazdırma çıktısını tekrar alma | Kod destekliyor ama **UI'den erişilemiyor (1.1)** | — |
| 49 | Kilidi kaldırma | OK | ✔ |
| 50 | Kilit kaldırıldıktan sonra değişiklik | OK (izin veriliyor) | — |
| 51 | Kilit kaldırma sonrası kim/ne zaman değiştirdiğinin izlenmesi | **Yok (audit log yok)** | — |
| 52 | Yıl sınırını aşan ay/yıl değerleriyle kilitleme denemesi | Reddediliyor (`ValidateMonth`) | — |
| 53 | Aynı ayı iki kez kilitleme (`INSERT OR IGNORE`) | Sorunsuz, sessiz | — |
| 54 | Kilitli olmayan bir ayın kilidini kaldırma denemesi | Sorunsuz, sessiz (no-op) | — |

### Excel / PDF / Yazdırma (55–72)
| # | Senaryo | Durum | Test |
|---|---|---|---|
| 55 | Haftalık Excel kaydetme | OK | ✔ |
| 56 | Aylık Excel kaydetme | Kod OK ama **UI'den erişilemiyor** | ✔ (Core) |
| 57 | Haftalık PDF (Excel kuruluyken) | Kod OK, Windows'ta doğrulanmadı | — |
| 58 | Aylık PDF | Kod OK, **UI'den erişilemiyor** | — |
| 59 | Excel kurulu değilken PDF isteme | `.xlsx`'e düşüyor + uyarı gösteriyor | — |
| 60 | Yazdırma diyaloğu (Excel kuruluyken) | Kod OK, Windows'ta doğrulanmadı; görünmez Excel + gizli dialog riski (Orta #1) | — |
| 61 | Yazdırma sırasında yazıcı seçilmeden iptal | Beklenen davranış: dialog kapanır, temp dosya silinir | — |
| 62 | 17 personeli aşan haftalık dışa aktarım | Reddediliyor ("en fazla 17 personel") | — |
| 63 | 32 personeli aşan aylık dışa aktarım | Reddediliyor ("en fazla 32 personel") | — |
| 64 | UI'de 100 personel olup şablon kapasitesini aşma | **Çelişki**: UI sınırsız personel gösterebiliyor ama export sert limitle reddediyor; kullanıcıya önceden uyarı yok | — |
| 65 | Şubat (28/29 gün) için gün sütunlarının doğru boşaltılması | OK | ✔ |
| 66 | Şablon klasöründe birden fazla eşleşen dosya (bkz. Orta #3) | **Kırılıyor** | — |
| 67 | Şablon dosyası eksik/silinmiş | Anlamlı hata (`FileNotFoundException`) | — |
| 68 | Logo olmadan dışa aktarım | OK (logo atlanıyor) | — |
| 69 | Bozuk/okunamayan logo dosyasıyla dışa aktarım | **Muhtemelen istisna fırlatır, izole edilmemiş** | — |
| 70 | Uzun imza metinleriyle dışa aktarım | `WrapText` uygulanıyor, görsel taşma riski Windows'ta doğrulanmadı | — |
| 71 | Kaynak şablon dosyalarının değişmediğinin doğrulanması (hash) | OK | ✔ |
| 72 | Aynı anda birden fazla dışa aktarım isteği (çift tık) | Buton devre dışı bırakılmıyor — hızlı çift tık iki paralel Excel COM örneği başlatabilir | — |

### Lisanslama / Cihaz kimliği (73–90)
| # | Senaryo | Durum | Test |
|---|---|---|---|
| 73 | Geçerli lisansla açılış | OK | ✔ |
| 74 | Lisans dosyası yok → aktivasyon ekranı | OK | — |
| 75 | Bozuk/geçersiz lisans kodu girme | Reddediliyor, anlamlı hata | ✔ |
| 76 | Başka cihaz için üretilmiş lisans | Reddediliyor (`DeviceMismatch`) | ✔ |
| 77 | Süresi dolmuş lisans | Reddediliyor, yenileme mesajı | ✔ |
| 78 | Süresiz lisans | Kabul ediliyor | ✔ |
| 79 | Lisans dosyasında tek karakter değişikliği | İmza bozulur, reddedilir | ✔ |
| 80 | Yanlış public key ile doğrulama | Reddediliyor | ✔ |
| 81 | EXE + lisansın başka bilgisayara kopyalanması | `DeviceId` uyuşmazlığıyla reddedilir | — (Windows'ta doğrulanmalı) |
| 82 | Sistem saatinin 12 saatten fazla geri alınması | Reddediliyor (ClockGuard) | ✔ |
| 83 | Sistem saatinin ileri alınıp normale döndürülmesi | **Kontrol edilmiyor** (dokümante edilmiş sınırlama) | — |
| 84 | MachineGuid okunamıyor ama diğer bileşenler var | Cihaz kimliği yine üretilir | ✔ |
| 85 | Hiçbir bileşen okunamıyor | Anlamlı hata ile kapanır | — |
| 86 | `wmic` komutu bulunamıyor (modern Windows) | Diğer bileşenlerle devam eder | — (Windows 11 24H2+'da doğrulanmalı) |
| 87 | Bir yıllık lisansın tam yıl dönümünde geçerliliğini yitirmesi | OK | ✔ |
| 88 | Private key olmadan Generator'da lisans üretme denemesi | Anlamlı hata | — |
| 89 | Lisans kodunun panoya kopyalanması | OK | — |
| 90 | Lisans kodunun `.dat` dosyası olarak kaydedilmesi | OK | — |

### Ayarlar / Yedekleme / Geri Yükleme (91–104)
| # | Senaryo | Durum | Test |
|---|---|---|---|
| 91 | Otel/departman adı güncelleme | OK | ✔ |
| 92 | Logo yükleme ve önizleme | OK | — |
| 93 | Logo kaldırma | OK | — |
| 94 | Geçersiz kenar boşluğu (negatif) girme | Reddediliyor | — |
| 95 | Yedek alma | OK | ✔ |
| 96 | Yedeğin lisans dosyasını içermediğinin doğrulanması | OK | ✔ |
| 97 | Geri yükleme öncesi otomatik güvenlik yedeği | OK (UI katmanında) | — |
| 98 | Geçersiz/bozuk yedek dosyasıyla geri yükleme | Şema doğrulamasıyla reddediliyor | — |
| 99 | Geri yükleme sonrası ekranların bayat veri göstermesi | **Bug (Orta #5)** | — |
| 100 | Geri yükleme sonrası ay kilitlerinin, vardiya tanımlarının, personel/atamaların geri gelmesi | OK | ✔ |
| 101 | Ardışık çok sayıda geri yükleme (güvenlik yedeklerinin birikmesi) | **Temizlenmiyor (Orta #6)** | — |
| 102 | Restore sırasında disk dolu / yazma hatası | Bir miktar korumalı (güvenlik yedeği önce alınıyor) ama otomatik geri alma yok | — |
| 103 | Vardiya saatlerini toplu düzenleyip kaydetme | OK | — |
| 104 | Vardiya saatine `24:00` (gece yarısı) girilmesi | Özel olarak destekleniyor | — |

### Eşzamanlılık / Windows davranışı (105–112)
| # | Senaryo | Durum | Test |
|---|---|---|---|
| 105 | Uygulamanın iki kez açılması | **Korumasız (Orta #2)** | — |
| 106 | Şablon dosyası kullanıcı tarafından Excel'de açıkken dışa aktarım | **Kırılma riski (Orta #3)** | — |
| 107 | Üretilen çıktı dosyası zaten Excel'de açıkken tekrar üretilmesi | Muhtemel dosya kilidi hatası, izole edilmemiş | — |
| 108 | Yazdırma/PDF sırasında Excel'in daha önce açık bir kullanıcı oturumunu etkilememesi | Kod yalnızca kendi `Excel.Application` örneğini kapatıyor (doğru tasarım) | — (Windows'ta doğrulanmalı) |
| 109 | Uygulamanın Windows kullanıcı hesabı değişikliğinde (yeni profil) çalışması | `%LocalAppData%` kullanıcıya özel — beklenen davranış her kullanıcı için ayrı DB/lisans | — |
| 110 | Yüksek DPI (%125/%150) ölçekte arayüz | Doğrulanmadı (GOREV 6'da açık madde) | — |
| 111 | Düşük çözünürlük (1366×768) ekranda arayüz | Doğrulanmadı (GOREV 6'da açık madde) | — |
| 112 | Antivirüs/EDR'nin `wmic`/registry erişimini engellemesi | Bileşen `null` döner, kod tolere eder ama cihaz kimliği daha az bileşenle üretilir | — |

---

## Ek B — Olumlu Bulgular (Adil Denetim İçin)

Acımasız olmak, iyi yapılan şeyleri görmezden gelmek anlamına gelmemeli:

- Tüm SQL parametreli; enjeksiyon riski yok.
- Lisans imzalama/doğrulama akışı (RSA-PSS, SHA-256, deterministik JSON) kriptografik olarak sağlam ve iyi test edilmiş.
- COM nesnelerinin serbest bırakılması (`Marshal.FinalReleaseComObject` + çift GC) EXCEL.EXE artığı bırakmamak için bilinçli ve doğru bir desen.
- Geliştirme private key'i `.gitignore` ile doğru şekilde dışlanmış, dosya izinleri kısıtlı.
- Ay kilidi iş kuralı (`EnsureMonthUnlocked`) veritabanı katmanında merkezi olarak uygulanmış; UI'nin her yerinde tekrar edilmemiş.
- `Puantaj.Core` testleri (40 test) gerçek senaryoları (İşten Ayrıldı, ay sınırları, işlenmiş yıl dönümü) isabetle hedefliyor.
- Restore öncesi otomatik güvenlik yedeği — veri kaybına karşı iyi bir refleks.
- Backup ZIP'in lisans dosyasını bilinçli olarak hariç tutması — doğru güvenlik sınırı.

---

*Bu rapor sırasında hiçbir kaynak dosya değiştirilmedi, hiçbir commit/push/PR oluşturulmadı.*
