# 📦 STOK.IO — Zirai İlaç Bayii Stok Takip Sistemi

Zirai ilaç bayileri ve depoları için geliştirilmiş, hafif ve modern bir masaüstü stok yönetim uygulaması. Kullanıcıyı yormayan minimalist bir arayüzle depo hareketlerini anlık olarak kontrol etmeyi amaçlar.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Avalonia](https://img.shields.io/badge/Avalonia-UI-6E40C9)
![SQLite](https://img.shields.io/badge/Veritabanı-SQLite-003B57?logo=sqlite)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)

---

## 📌 Ne İşe Yarar?

STOK.IO, bir zirai ilaç bayisinin günlük stok hareketlerini hızlı ve hatasız şekilde takip edebilmesi için tasarlandı. Excel'e bağımlı, dağınık takip yöntemlerinin yerine tek ekrandan yönetilebilen, çoklu kullanıcı destekli bir sistem sunar.

## ✨ Özellikler

- **Ürün Yönetimi** — İlaç ekleme, güncelleme, silme ve hızlı arama (300ms debounce ile optimize edilmiş).
- **Hızlı Stok Hareketi** — Seçili ürün için tek tıkla `+1` / `-1` stok değişimi.
- **Sayfalama** — Büyük ürün listelerinde performanslı gezinme.
- **Sistem Logları** — Kim, ne zaman, hangi işlemi yaptı; kronolojik ve filtrelenebilir kayıt.
- **Çoklu Kullanıcı Desteği** — Personel ekleme/çıkarma, işlemler aktif kullanıcı adına loglanır.
- **Veri Kontrolü**
  - 📥 Excel/CSV şablonu ile toplu ürün yükleme
  - 📤 Güncel stok listesini CSV olarak dışa aktarma
  - 🖨️ Anlık stok durum raporu (TXT)
- **Otomatik Yedekleme** — Uygulama her kapatıldığında veritabanının otomatik yedeği alınır, son 5 yedek saklanır.
- **Cross-Platform** — Avalonia UI sayesinde Windows, macOS ve Linux'ta aynı deneyim.

## 🛠️ Teknoloji Altyapısı

| Katman | Teknoloji |
|---|---|
| Arayüz (UI) | Avalonia UI (.NET 8.0) |
| Veritabanı | SQLite |
| ORM | Entity Framework Core |
| Mimari | Tek pencere, sekme (tab) tabanlı dashboard |

### Gereksinimler
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)

### Adımlar

```bash
# Depoyu klonla
git clone https://github.com/kullaniciadin/StokTakip.git
cd StokTakip

# Bağımlılıkları yükle
dotnet restore

# Uygulamayı çalıştır
dotnet run
```

Veritabanı (`stok.db`) ilk çalıştırmada otomatik oluşturulur, ekstra bir kurulum gerekmez.

## 📂 Proje Yapısı

```
StokTakip/
├── Data/
│   └── StokContext.cs        # EF Core DbContext
├── Models/
│   └── Product.cs             # Ürün, kullanıcı, log modelleri
├── Services/                  # Yardımcı servisler
├── MainWindow.axaml           # Ana arayüz
├── MainWindow.axaml.cs        # Ana arayüz mantığı
├── ExcelImportWindow.axaml    # Toplu içe aktarma penceresi
└── Program.cs                 # Uygulama giriş noktası
```

## 📄 Lisans

Bu proje şu an özel kullanım içindir.
