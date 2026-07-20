using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using StokTakip.Data;
using StokTakip.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StokTakip
{
    public partial class MainWindow : Window
    {
        private int? _selectedId;

        private readonly List<string> _kullanicilar = new() { "Arda" };

        // --- SAYFALAMA ---
        private int _sayfaNo = 1;
        private const int SayfaBasinaKayit = 20;
        private int _toplamSayfa = 1;

        // --- ARAMA DEBOUNCE ---
        private CancellationTokenSource? _aramaCts;

        public MainWindow()
        {
            InitializeComponent();
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            using var context = new StokContext();
            await context.Database.EnsureCreatedAsync();

            if (!await context.AppUsers.AnyAsync())
            {
                context.AppUsers.Add(new AppUser { Username = "Arda" });
                await context.SaveChangesAsync();
            }

            await TazeleKullaniciListeleriAsync();
            await LoadProductsAsync();
            await LoadLogsAsync();
        }

        // UYGULAMA KAPATILIRKEN DÖNÜŞÜMLÜ YEDEKLEME

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            try
            {
                string uygulamaYolu = AppDomain.CurrentDomain.BaseDirectory;
                string anaDbYolu = Path.Combine(uygulamaYolu, "stok.db");

                if (File.Exists(anaDbYolu))
                {
                    // Yedeklerin toplanacağı klasör
                    string yedekKlasorYolu = Path.Combine(uygulamaYolu, "StokYedekleri");
                    if (!Directory.Exists(yedekKlasorYolu))
                    {
                        Directory.CreateDirectory(yedekKlasorYolu);
                    }

                    // Yeni yedek dosyasının adı (Örn: stok_yedek_20260719_1630.db)
                    string yeniYedekAdi = $"stok_yedek_{DateTime.Now:yyyyMMdd_HHmm}.db";
                    string yeniYedekYolu = Path.Combine(yedekKlasorYolu, yeniYedekAdi);

                    // Mevcut veritabanını kopyala
                    File.Copy(anaDbYolu, yeniYedekYolu, true);

                    // Depolama alanı koruması: Sadece son 5 yedeği tut
                    var yedekDosyalari = new DirectoryInfo(yedekKlasorYolu)
                        .GetFiles("stok_yedek_*.db")
                        .OrderBy(f => f.CreationTime)
                        .ToList();

                    if (yedekDosyalari.Count > 5)
                    {
                        int silinecekAdet = yedekDosyalari.Count - 5;
                        for (int i = 0; i < silinecekAdet; i++)
                        {
                            yedekDosyalari[i].Delete();
                        }
                    }
                }
            }
            catch
            {
                // Kapatırken programın takılmasını engellemek için hataları yutuyoruz
            }

            base.OnClosing(e);
        }
        // =================================================================

        private async Task TazeleKullaniciListeleriAsync()
        {
            using var context = new StokContext();

            string? eskiSecili = UserComboBox.SelectedItem as string;
            var dbKullanicilari = await context.AppUsers.Select(u => u.Username).ToListAsync();

            UserComboBox.ItemsSource = null;
            UserComboBox.ItemsSource = dbKullanicilari;

            if (!string.IsNullOrEmpty(eskiSecili) && dbKullanicilari.Contains(eskiSecili))
                UserComboBox.SelectedItem = eskiSecili;
            else if (dbKullanicilari.Count > 0)
                UserComboBox.SelectedIndex = 0;

            UsersListBox.ItemsSource = null;
            UsersListBox.ItemsSource = dbKullanicilari;
        }

        private void Menu_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button tiklananButon || tiklananButon.Tag is not string tagIndex)
                return;

            if (int.TryParse(tagIndex, out var index))
            {
                MainTabControl.SelectedIndex = index;
            }

            BtnUrunYonetimi.Background = Brushes.Transparent;
            BtnUrunYonetimi.Foreground = new SolidColorBrush(Color.Parse("#9CA3AF"));
            BtnYedekleme.Background = Brushes.Transparent;
            BtnYedekleme.Foreground = new SolidColorBrush(Color.Parse("#9CA3AF"));
            BtnYetkiler.Background = Brushes.Transparent;
            BtnYetkiler.Foreground = new SolidColorBrush(Color.Parse("#9CA3AF"));
            BtnLoglar.Background = Brushes.Transparent;
            BtnLoglar.Foreground = new SolidColorBrush(Color.Parse("#9CA3AF"));
            BtnLisans.Background = Brushes.Transparent;
            BtnLisans.Foreground = new SolidColorBrush(Color.Parse("#9CA3AF"));

            tiklananButon.Background = new SolidColorBrush(Color.Parse("#2D3142"));
            tiklananButon.Foreground = Brushes.White;
        }

        private async void AddUserButton_Click(object? sender, RoutedEventArgs e)
        {
            string yeniIsim = NewUserBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(yeniIsim)) return;

            using var context = new StokContext();

            bool zatenVar = await context.AppUsers.AnyAsync(u => u.Username.ToLower() == yeniIsim.ToLower());
            if (!zatenVar)
            {
                context.AppUsers.Add(new AppUser { Username = yeniIsim });
                await context.SaveChangesAsync();
                await TazeleKullaniciListeleriAsync();
                UserComboBox.SelectedItem = yeniIsim;
                NewUserBox.Text = string.Empty;
            }
        }

        private async void DeleteUserButton_Click(object? sender, RoutedEventArgs e)
        {
            if (UsersListBox.SelectedItem is not string silinecekKullanici)
                return;

            using var context = new StokContext();

            if (await context.AppUsers.CountAsync() <= 1) return;

            var targetUser = await context.AppUsers.FirstOrDefaultAsync(u => u.Username == silinecekKullanici);
            if (targetUser != null)
            {
                context.AppUsers.Remove(targetUser);
                await context.SaveChangesAsync();
                await TazeleKullaniciListeleriAsync();
            }
        }

        private async Task LoadProductsAsync()
        {
            using var context = new StokContext();

            var searchText = SearchBox.Text?.Trim() ?? string.Empty;
            var query = context.Products.AsQueryable();

            if (!string.IsNullOrEmpty(searchText))
            {
                var lowerSearch = searchText.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(lowerSearch));
            }

            query = query.OrderBy(p => p.Name);

            // --- SAYFALAMA HESABI ---
            int toplamKayit = await query.CountAsync();
            _toplamSayfa = Math.Max(1, (int)Math.Ceiling(toplamKayit / (double)SayfaBasinaKayit));

            if (_sayfaNo > _toplamSayfa) _sayfaNo = _toplamSayfa;
            if (_sayfaNo < 1) _sayfaNo = 1;

            ProductGrid.ItemsSource = await query
                .Skip((_sayfaNo - 1) * SayfaBasinaKayit)
                .Take(SayfaBasinaKayit)
                .ToListAsync();

            PageInfoText.Text = $"Sayfa {_sayfaNo} / {_toplamSayfa}  ({toplamKayit} kayıt)";
            PrevPageButton.IsEnabled = _sayfaNo > 1;
            NextPageButton.IsEnabled = _sayfaNo < _toplamSayfa;
            // --- SAYFALAMA HESABI SONU ---

            NameBox.ItemsSource = await context.Products
                .Select(p => p.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();
        }

        private async void PrevPageButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_sayfaNo > 1)
            {
                _sayfaNo--;
                await LoadProductsAsync();
            }
        }

        private async void NextPageButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_sayfaNo < _toplamSayfa)
            {
                _sayfaNo++;
                await LoadProductsAsync();
            }
        }

        private async Task LoadLogsAsync()
        {
            using var context = new StokContext();

            LogGrid.ItemsSource = await context.StockLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .ToListAsync();
        }

        private async Task LogEkleAsync(string productName, string actionType, string details)
        {
            string aktifKullanici = UserComboBox.SelectedItem as string ?? "Bilinmeyen";

            using var context = new StokContext();

            var log = new StockLog
            {
                ProductName = productName,
                ActionType = actionType,
                Details = details,
                Operator = aktifKullanici,
                Timestamp = DateTime.Now
            };

            context.StockLogs.Add(log);
            await context.SaveChangesAsync();
            await LoadLogsAsync();
        }

        private async void FastDecreaseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedId is null)
            {
                StatusText.Text = "Hızlı azaltmak için listeden bir ilaç seçmelisiniz.";
                return;
            }

            using var context = new StokContext();
            var product = await context.Products.FindAsync(_selectedId);
            if (product != null)
            {
                if (product.Quantity > 0)
                {
                    product.Quantity -= 1;
                    product.LastUpdated = DateTime.Now;
                    await context.SaveChangesAsync();

                    await LogEkleAsync(product.Name, "Hızlı Satış", "Stok 1 adet düşürüldü.");
                    StatusText.Text = string.Empty;
                    await LoadProductsAsync();
                    QuantityBox.Text = product.Quantity.ToString();
                }
                else
                {
                    StatusText.Text = "Stok zaten sıfır, daha fazla azaltılamaz!";
                }
            }
        }

        private async void FastIncreaseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedId is null)
            {
                StatusText.Text = "Hızlı artırmak için listeden bir ilaç seçmelisiniz.";
                return;
            }

            using var context = new StokContext();
            var product = await context.Products.FindAsync(_selectedId);
            if (product != null)
            {
                product.Quantity += 1;
                product.LastUpdated = DateTime.Now;
                await context.SaveChangesAsync();

                await LogEkleAsync(product.Name, "Hızlı Giriş", "Stok 1 adet artırıldı.");
                StatusText.Text = string.Empty;
                await LoadProductsAsync();
                QuantityBox.Text = product.Quantity.ToString();
            }
        }

        private void DownloadTemplateButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                string masaustuYolu = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string tamYol = Path.Combine(masaustuYolu, "Stok_Yukleme_Sablonu.xlsx");

                using var workbook = new XLWorkbook();
                var sheet = workbook.Worksheets.Add("Şablon");

                sheet.Cell(1, 1).Value = "İlaç Adı";
                sheet.Cell(1, 2).Value = "Stok Miktarı";

                var baslikSatiri = sheet.Range(1, 1, 1, 2);
                baslikSatiri.Style.Font.Bold = true;
                baslikSatiri.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A1C23");
                baslikSatiri.Style.Font.FontColor = XLColor.White;

                sheet.Cell(2, 1).Value = "Delmetrin 2.5 EC";
                sheet.Cell(2, 2).Value = 50;
                sheet.Cell(3, 1).Value = "Glyphosate 480 g/l";
                sheet.Cell(3, 2).Value = 120;

                sheet.Columns().AdjustToContents();

                workbook.SaveAs(tamYol);

                StatusText.Text = "Örnek Excel yükleme şablonu (.xlsx) Masaüstünüze başarıyla indirildi!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Şablon oluşturulamadı: {ex.Message}";
            }
        }

        private async void PrintReportButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                string masaustuYolu = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string tamYol = Path.Combine(masaustuYolu, "Stok_Raporu.txt");

                using var context = new StokContext();
                var urunler = await context.Products.OrderBy(p => p.Name).ToListAsync();

                using (StreamWriter sw = new StreamWriter(tamYol, false, System.Text.Encoding.UTF8))
                {
                    sw.WriteLine("==================================================");
                    sw.WriteLine("                  STOK.IO REPORT                  ");
                    sw.WriteLine("            ZİRAİ İLAÇ MEVCUT STOK RAPORU         ");
                    sw.WriteLine($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}");
                    sw.WriteLine("==================================================");
                    sw.WriteLine(string.Format("{0,-6} | {1,-30} | {2,-10}", "ID", "İlaç Adı", "Stok Miktarı"));
                    sw.WriteLine("--------------------------------------------------");

                    foreach (var u in urunler)
                    {
                        string limitDurum = u.Quantity <= 5 ? " (KRİTİK)" : "";
                        sw.WriteLine(string.Format("{0,-6} | {1,-30} | {2,-10}{3}", u.Id, u.Name.Length > 28 ? u.Name.Substring(0, 28) : u.Name, u.Quantity, limitDurum));
                    }
                    sw.WriteLine("==================================================");
                }

                StatusText.Text = "Güncel Stok Raporu (Stok_Raporu.txt) Masaüstünüze çıkartıldı!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Rapor oluşturulamadı: {ex.Message}";
            }
        }

        private async void ClearLogsButton_Click(object? sender, RoutedEventArgs e)
        {
            var evetButon = new Button { Content = "Evet, Sil", Background = new SolidColorBrush(Color.Parse("#EF4444")), Foreground = Brushes.White, Width = 80, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            var iptalButon = new Button { Content = "İptal", Background = new SolidColorBrush(Color.Parse("#6C757D")), Foreground = Brushes.White, Width = 80, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center };

            var dialog = new Window
            {
                Title = "Log Silme Onayı",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = new SolidColorBrush(Color.Parse("#F8F9FA")),
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 15,
                    Children =
                    {
                        new TextBlock { Text = "Tüm işlem geçmişi logları kalıcı olarak silinecek.\nEmin misiniz?", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, TextAlignment = TextAlignment.Center },
                        new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Spacing = 10, Children = { evetButon, iptalButon } }
                    }
                }
            };

            evetButon.Click += (s, ae) => dialog.Close(true);
            iptalButon.Click += (s, ae) => dialog.Close(false);

            var sonuc = await dialog.ShowDialog<bool>(this);

            if (sonuc)
            {
                using var context = new StokContext();
                var tumLoglar = await context.StockLogs.ToListAsync();
                context.StockLogs.RemoveRange(tumLoglar);
                await context.SaveChangesAsync();
                await LoadLogsAsync();
            }
        }

        private async void ExportExcelButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new StokContext();
                var urunler = await context.Products.OrderBy(p => p.Name).ToListAsync();

                string masaustuYolu = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string dosyaAdi = $"Stok_Disa_Aktarim_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                string tamYol = Path.Combine(masaustuYolu, dosyaAdi);

                using var workbook = new XLWorkbook();
                var sheet = workbook.Worksheets.Add("Stok Listesi");

                // Başlık satırı
                sheet.Cell(1, 1).Value = "ID";
                sheet.Cell(1, 2).Value = "İlaç Adı";
                sheet.Cell(1, 3).Value = "Miktar";
                sheet.Cell(1, 4).Value = "Son Güncelleme";

                var baslikSatiri = sheet.Range(1, 1, 1, 4);
                baslikSatiri.Style.Font.Bold = true;
                baslikSatiri.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A1C23");
                baslikSatiri.Style.Font.FontColor = XLColor.White;

                // Veri satırları
                int satirNo = 2;
                foreach (var u in urunler)
                {
                    sheet.Cell(satirNo, 1).Value = u.Id;
                    sheet.Cell(satirNo, 2).Value = u.Name;
                    sheet.Cell(satirNo, 3).Value = u.Quantity;
                    sheet.Cell(satirNo, 4).Value = u.LastUpdated;
                    sheet.Cell(satirNo, 4).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";

                    satirNo++;
                }

                sheet.Columns().AdjustToContents();
                sheet.SheetView.FreezeRows(1); // Başlık satırı kayarken sabit kalsın

                workbook.SaveAs(tamYol);

                StatusText.Text = $"{urunler.Count} ürün '{dosyaAdi}' olarak Masaüstüne aktarıldı!";
                await LogEkleAsync("Sistem", "Dışa Aktar", $"{urunler.Count} adet ürün Excel (.xlsx) olarak dışa aktarıldı.");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Dışa aktarım başarısız: {ex.Message}";
            }
        }

        private bool TryReadForm(out string name, out int quantity, out string error)
        {
            name = string.Empty;
            quantity = 0;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                error = "İlaç adı boş olamaz.";
                return false;
            }

            if (!int.TryParse(QuantityBox.Text, out var qty) || qty < 0)
            {
                error = "Miktar geçerli, negatif olmayan bir sayı olmalı.";
                return false;
            }

            name = NameBox.Text.Trim();
            quantity = qty;
            return true;
        }

        private async void AddButton_Click(object? sender, RoutedEventArgs e)
        {
            if (!TryReadForm(out var inputName, out var inputQuantity, out var error))
            {
                StatusText.Text = error;
                return;
            }

            using var context = new StokContext();

            bool isimZatenVar = await context.Products.AnyAsync(p => p.Name.ToLower() == inputName.ToLower());
            if (isimZatenVar)
            {
                StatusText.Text = $"'{inputName}' isimli bir ilaç zaten sistemde kayıtlı!";
                return;
            }

            int yeniId = 1;
            if (await context.Products.AnyAsync())
            {
                yeniId = await context.Products.MaxAsync(p => p.Id) + 1;
            }

            var product = new Product
            {
                Id = yeniId,
                Name = inputName,
                Quantity = inputQuantity,
                LastUpdated = DateTime.Now
            };

            context.Products.Add(product);
            await context.SaveChangesAsync();

            await LogEkleAsync(product.Name, "Ekleme", $"{product.Quantity} adet yeni stok girildi.");
            StatusText.Text = string.Empty;
            await LoadProductsAsync();
            ClearForm();
        }

        private async void UpdateButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedId is null)
            {
                StatusText.Text = "Güncellemek için önce listeden bir ilaç seç.";
                return;
            }

            if (!TryReadForm(out var inputName, out var inputQuantity, out var error))
            {
                StatusText.Text = error;
                return;
            }

            using var context = new StokContext();

            bool isimBaskaUrundeVar = await context.Products.AnyAsync(p => p.Name.ToLower() == inputName.ToLower() && p.Id != _selectedId);
            if (isimBaskaUrundeVar)
            {
                StatusText.Text = $"'{inputName}' ismi başka bir ilaçta zaten kullanılıyor!";
                return;
            }

            var existing = await context.Products.FindAsync(_selectedId);
            if (existing is null)
            {
                StatusText.Text = "İlaç bulunamadı, liste yenileniyor.";
                await LoadProductsAsync();
                return;
            }

            string logDetay = $"Eski Stok: {existing.Quantity} -> Yeni Stok: {inputQuantity}";
            existing.Name = inputName;
            existing.Quantity = inputQuantity;
            existing.LastUpdated = DateTime.Now;

            await context.SaveChangesAsync();
            await LogEkleAsync(existing.Name, "Güncelleme", logDetay);
            StatusText.Text = string.Empty;
            await LoadProductsAsync();
            ClearForm();
        }

        private async void DeleteButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedId is null)
            {
                StatusText.Text = "Silmek için önce listeden bir ilaç seç.";
                return;
            }

            using var context = new StokContext();
            var existing = await context.Products.FindAsync(_selectedId);
            if (existing is not null)
            {
                string silinenAd = existing.Name;
                int silinenMiktar = existing.Quantity;

                context.Products.Remove(existing);
                await context.SaveChangesAsync();

                await LogEkleAsync(silinenAd, "Silme", $"{silinenMiktar} adet stok barındıran ilaç kaydı tamamen silindi.");
            }

            StatusText.Text = string.Empty;
            await LoadProductsAsync();
            ClearForm();
        }

        private void ClearButton_Click(object? sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            _selectedId = null;
            NameBox.Text = string.Empty;
            QuantityBox.Text = string.Empty;
            ProductGrid.SelectedItem = null;
            StatusText.Text = string.Empty;
        }

        private void ProductGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ProductGrid.SelectedItem is not Product selected)
                return;

            _selectedId = selected.Id;
            NameBox.Text = selected.Name;
            QuantityBox.Text = selected.Quantity.ToString();
        }

        // --- ARAMA: 300ms DEBOUNCE ---
        private async void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            _sayfaNo = 1;

            _aramaCts?.Cancel();
            _aramaCts = new CancellationTokenSource();
            var token = _aramaCts.Token;

            try
            {
                await Task.Delay(300, token);
            }
            catch (TaskCanceledException)
            {
                return; // kullanıcı yazmaya devam ediyor, bu isteği iptal et
            }

            if (!token.IsCancellationRequested)
            {
                await LoadProductsAsync();
            }
        }

        private void ImportExcelButton_Click(object? sender, RoutedEventArgs e)
        {
            var window = new ExcelImportWindow();
            window.DataImported += (_, _) => { _ = LoadProductsAsync(); _ = LoadLogsAsync(); };
            window.Closed += (_, _) => { _ = LoadProductsAsync(); _ = LoadLogsAsync(); };
            window.Show();
        }
    }
}