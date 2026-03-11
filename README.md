# FinanceCase

Bu proje, verilen case için hazırladığım ASP.NET Core çözümüdür.

Solution içinde 2 proje bulunuyor:

- `FinanceCase.Web`
- `FinanceCase.Api`

Web tarafında veri yükleme, hesaplama ve sonuç ekranları yer alıyor. API tarafında ise güncel kur verilerini dışarı veren basit bir endpoint bulunuyor.

## Proje Özeti

Uygulamada şu akış var:

- Kur verileri verilen servisten çekilip veritabanına yazılıyor
- Varlık ve ÜFE verileri Excel dosyasından içeri alınıyor
- İçe aktarılan veri aralığına göre geçmiş kur verileri otomatik olarak senkronlanıyor
- İlk açılışta kullanıcı doğrudan veri yükleme ekranına yönlendiriliyor
- Veri yükleme tamamlandıktan sonra kullanıcı kısa bir sayaç sonrası otomatik olarak kurlar ekranına yönlendiriliyor
- Veri yüklenmeden kurlar ve sonuçlar ekranları menüde gösterilmiyor
- Dolarizasyon ve enflasyonizasyon hesapları yapılıyor
- Sonuçlar tablo ve grafik olarak gösteriliyor

Kur verilerinin belirli aralıklarla güncellenmesi için Hangfire kullanıldı.

## Ekran Görüntüleri

### Veri Yükleme

![Veri yükleme ekranı](FinanceCase.Web/screenshots/import-page.png)

### Başarılı Yükleme ve Yönlendirme

![Başarılı yükleme ekranı](FinanceCase.Web/screenshots/import-success.png)

### Kur Kayıtları

![Kur kayıtları ekranı](FinanceCase.Web/screenshots/exchange-rates.png)

### Sonuç Grafiği

![Sonuç grafik ekranı](FinanceCase.Web/screenshots/results-chart.png)

### Sonuç Tablosu

![Sonuç tablo ekranı](FinanceCase.Web/screenshots/results-table.png)

## Kullanılan Teknolojiler

- .NET 10
- ASP.NET Core MVC
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server / SQLEXPRESS
- Hangfire
- NPOI
- Chart.js

## Veri Kaynakları

- Kur servisi: `https://testapi.finmaks.com/ExchangeRates?key=Finmaks123`
- Varlık verisi: `.xls` / `.xlsx`
- ÜFE verisi: `.xls` / `.xlsx`

Not: Case metninde XML ifadesi geçiyor ama gönderilen örnek dosyalar Excel olduğu için import kısmını Excel üzerinden yaptım.

## Yapılanlar

- Kur verisini servisten çekme ve MSSQL’e kaydetme
- Hangfire ile saatlik güncelleme
- Varlık ve ÜFE Excel dosyalarını içeri alma
- İçe aktarılan tarih aralığına göre geçmiş kur verilerini otomatik çekme
- İlk açılışta kullanıcıyı veri yükleme ekranına yönlendirme
- Veri yüklendikten sonra kurlar ekranına otomatik yönlendirme
- Veri hazır değilken kurlar ve sonuçlar menüsünü gizleme
- Development ortamında tüm test verilerini temizleme butonu ekleme
- Hatalı dosya yüklenirse kullanıcıya düzgün hata mesajı gösterme
- Dolarizasyon ve enflasyonizasyon hesaplarını yapma
- Sonuçları tabloda ve grafikte gösterme
- Sonuç ekranını sayfa yenilenmeden belirli aralıklarla güncelleme
- Aynı solution içinde ayrı API projesi ile güncel kur verisini dışarı verme
- Kurlar ekranında pagination

## Kurulum

### 1. SQL Server

Bilgisayarda SQL Server / SQLEXPRESS çalışıyor olmalı.

### 2. Solution açma

Kök dizindeki `FinanceCase.slnx` dosyası açılabilir.

### 3. Paketleri yükleme

```bash
dotnet restore FinanceCase.slnx
```

### 4. Veritabanını oluşturma

```bash
dotnet ef database update --project FinanceCase.Web
```

### 5. Web projesini çalıştırma

```bash
dotnet run --project FinanceCase.Web
```

### 6. İlk veri yükleme

Uygulama açıldığında ilk olarak veri yükleme ekranı gelir. Varlık ve ÜFE dosyaları yüklendikten sonra ilgili tarih aralığındaki kur verileri otomatik olarak çekilir ve kullanıcı kurlar ekranına yönlendirilir.

### 7. API projesini çalıştırma

```bash
dotnet run --project FinanceCase.Api
```

## Örnek Dosyalar

`FinanceCase.Web/SampleFiles/` klasörü içinde örnek Excel dosyaları bulunuyor. İstenirse test amaçlı kullanılabilir.
