# Dönüşüm Kuralları

## Veri akışı

Haftalık plandaki her personel-gün kaydı, sütun konumundan önce hücredeki gerçek takvim tarihiyle anlamlandırılır. Seçilen ayla kesişen kayıtlar personel kimliği ve tarih üzerinden tek aylık matriste birleştirilir. Ardından onaylı kod dönüşümü uygulanır, kişi/gün toplamları hesaplanır ve aylık puantaj düzenine yazılır.

## Kaynak alan → hedef alan eşleştirmesi

| Haftalık kaynak | Aylık hedef | Kanıt / durum |
|---|---|---|
| Otel adı (`C3/C5/C11` varyantları) | `AP3:AS3` | Her iki dosyada otel alanı var; metin birebirliği kullanıcı verisine bağlıdır. |
| Departman (`C4/C6/C12`) | `AP2:AS2` | Her iki dosyada departman alanı var. |
| Personel sıra no (`A` personel satırı) | `B7:B38` | Görsel sıra alanıdır; kalıcı eşleştirme anahtarı olduğu kanıtlanmamıştır. |
| Ad soyad (`B` personel satırı) | `C7:C38` | Açık alan eşleşmesi. |
| Görev/pozisyon (`C` personel satırı) | `D7:D38` | Açık alan eşleşmesi. |
| Gün tarihleri (haftalıkta D/F/H/J/L/N/P başlıkları) | Ay gününe göre `E:AI` (1–31) | Takvim tarihine göre eşleşme kesin; haftalık sayfalardaki bazı tarih hücreleri hatalı olduğundan doğrulama gerekir. |
| Günlük vardiya/izin kodu | İlgili personel ve gün hücresi | Taşıma/dönüşüm tablosu aşağıdaki belirsizlikler çözülmeden kesin değildir. |
| Haftalık imza sütunları E/G/I/K/M/O/Q | Aylık `AT` imza sütunu | Birebir dönüşüm kanıtlanmadı; aylık dosyada tek imza alanı vardır. |
| Haftalık Department Head / HR onayı | Aylık alt imzalar | Aylıkta HR Manager, Department Head, General Manager vardır; birebir ilişki eksiktir. |

## Kesin olarak tespit edilen kurallar

- Aylık dosyada gün numarası 1–31 sırasıyla `E:AI` sütunlarındadır.
- Aylık kişi toplamları `AJ:AS` sütunlarındadır: X, HT, RT, Mİ, Üİ, RP, Yİ, ÜZ, DZ, GR.
- Aylık `AJ` çalışma toplamı, dosyadaki formüle göre `X` ve `G` adetlerinin toplamıdır.
- Diğer kişi toplamları ilgili kodun `COUNTIF` sayımıdır; ancak Üİ başlığı/formülü tutarsızlığı aşağıda belirtilmiştir.
- Günlük özet satırları 41–50, ilgili gün sütununda kodları sayar; 40. satır bu özetlerin toplamıdır.
- Seçilen ay dışında kalan haftalık günler aylık matrise alınmamalıdır. Bu, hedefin seçilen aya ait 1–31 sütunlarından oluşmasının doğrudan sonucudur.
- Aylık dosyada 29 dolu personel satırı vardır; 7–38 aralığı 32 satır kapasitelidir. Bu kapasitenin aşılması için davranış kaynak dosyada tanımlı değildir.

## Kod dönüşümü

| Haftalık gözlem | Haftalık açıklama | Aylık karşılık | Durum |
|---|---|---|---|
| A, B, C, D, E | Vardiya harfleri; A=09–17, B=08–16, C=16–24, D=24–08, E=13–21 | `X` | **Kullanıcı tarafından kesinleştirildi.** Saatler daha sonra ayarlanabilir. |
| G | Görevli | `G` | **Kullanıcı tarafından kesinleştirildi.** |
| HT | Hafta tatili | HT | Kod ve açıklama iki dosyada uyumlu. |
| RT | Resmî tatil | RT | Kod ve açıklama iki dosyada uyumlu. |
| RP | Raporlu | RP | Kod ve açıklama iki dosyada uyumlu. |
| Üİ | Ücretli İzin | Üİ | **Kesin.** Excel'deki Sİ ifadeleri hatadır ve uygulanmaz. |
| Yİ | Yıllık izin | Yİ | Kod sütunu uyumlu; aylık açıklama satırı D47'de hatalı biçimde `202`. |
| ÜZ | Haftalık lejantta ücretsiz izin | Aylık ÜZ | Başlık uyumlu; günlük özet formülü satır 48'de Üİ saydığı için tutarsız. |
| Aİ | Alacak İzin | Aİ | **Kullanıcı tarafından kesinleştirildi.** Aylık şablonda ayrı toplam sütunu olmadığından toplam yerleşimi ayrıca kararlaştırılmalıdır. |
| R | Haftalıkta kullanılmış | Aylık örnekte R var, fakat toplam sütunu yok | **Belirsiz.** Aylık R değerleri hiçbir kişi toplamında sayılmıyor. |
| Mİ, DZ, GR | Haftalık örnekte gözlenmedi | Aylıkta toplam/özet kodu | Haftalık girişte desteklenip desteklenmeyeceği kullanıcı kararıdır. |

## Formül ve toplam kuralları

- Personel satırı `r` için: `AJr = COUNTIF(Er:AIr,"X") + COUNTIF(Er:AIr,"G")`.
- `AK:AS`, sırasıyla HT, RT, Mİ, başlıkta Üİ, RP, Yİ, ÜZ, DZ, GR adetlerini gösterir.
- Dosyada AN kişi formüllerinin `Sİ` sayması şablon hatasıdır; program `Sİ` kodunu kabul etmeyecek ve Üİ için `Üİ` sayacaktır.
- Günlük özetlerdeki `Sİ` ifadeleri ve ÜZ satırının `Üİ` sayması şablon hatasıdır; program satır etiketlerine uygun olarak Üİ ve ÜZ kodlarını ayrı sayacaktır.
- 40. satır günlük kategori sayılarının toplamı, AJ40 ve AK40 kişi toplamlarının kolon toplamıdır. Referans dosyada birçok formül paylaşımlı formül kaydı olarak tutulmuştur.
- R kodu formüllere dahil değildir. Formüllerin bu kodu nasıl ele alacağı kullanıcı kararı gerektirir.

## Tarih sınırı kuralları

- Kayıt, gerçek tarihi hedef ayın ilk ve son günü arasındaysa alınır; değilse alınmaz.
- Ay ortasında başlayan/biten hafta parçalanmaz; yalnız kesişen gün kayıtları seçilir.
- Şubat ve 30 günlük aylarda olmayan gün sütunları boş bırakılır; gizleme/biçim davranışı Excel'den kesin anlaşılmamaktadır.
- Haftalık dosyada seri tarih, düz metin ve hatalı metin birlikte bulunduğundan tarih; seri değer + sayı biçimi, gün başlığı ve hafta aralığı çapraz kontrolüyle doğrulanmalıdır. Çelişki kullanıcıya gösterilmelidir.

## Personel eşleştirme riskleri

- Sıra numaraları haftadan haftaya değişebilir veya boş olabilir; anahtar olamaz.
- Yazım örnekleri değişmektedir: `ÖMER MERTOĞULLARI` / `ÖMER MERTOĞULARI`, `İZZETTİN` / `İZETİN`, `SONGÜL ÇAKMAKCI` / `ÇAKMAKÇI`, çift boşluklar ve görev yazımları.
- Yalnız ad normalizasyonuyla otomatik birleştirme iki farklı kişiyi yanlış eşleştirebilir. Kalıcı personel kimliği kullanılmalı; olası benzerlikler kullanıcı onayına sunulmalıdır.
- Büyük/küçük harf, Türkçe karakter ve fazla boşluklar arama için normalize edilebilir fakat saklanan/gösterilen özgün ad değiştirilmemelidir.

## Belirsiz / Kullanıcıya Sorulacak

1. Aİ aylık gün hücresinde korunacaktır; ayrı toplam sütunu olmadığı için toplam/özet yerleşimi nasıl olmalıdır?
2. Aylık örnekteki tanımsız `R` değerleri veri hatası mı, eski bir kod mu?
3. Mİ ve DZ haftalık ekranda geçerli kod listesinde olmadığı için tamamen dışlanacak mı? (`G`, Görevli olarak kesinleşmiştir.)
4. Aynı personel/tarih iki haftada farklı kodla gelirse hangisi kazanır, yoksa kullanıcı onayı zorunlu mu?
5. Personel için sicil/personel numarası var mı? Yoksa ilk kayıt sırasında uygulama içi kimlik nasıl doğrulanacak?
6. Aylık 32 satır kapasitesi aşılırsa yeni sayfa mı, genişletilmiş tek sayfa mı kullanılacak?
7. 28/29/30 günlük aylarda kullanılmayan gün sütunları görünür boş mu kalacak, gizlenecek mi?
8. Haftalık günlük imzalar aylık tek imza hücresine aktarılacak mı, aylık imza boş mu bırakılacak?
9. Genel Müdür imzası haftalık dosyada yoktur; aylıkta her zaman yalnız etiket olarak mı kalacaktır?
