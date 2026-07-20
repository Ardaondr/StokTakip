using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StokTakip.Data;
using StokTakip.Services;

namespace StokTakip
{
    public partial class ExcelImportWindow : Window
    {
        private readonly StokContext _context = new();
        private ExcelSheetData? _sheetData;

        // MainWindow bu event'i dinleyip listeyi yeniler
        public event EventHandler? DataImported;

        private static readonly ColumnOption NoneOption = new(-1, "(Kullanma)");

        public ExcelImportWindow()
        {
            InitializeComponent();
        }

        private async void SelectFileButton_Click(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Excel Dosyası Seç",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Excel Dosyaları") { Patterns = new[] { "*.xlsx" } }
                }
            });

            var file = files.Count > 0 ? files[0] : null;
            var localPath = file?.TryGetLocalPath();

            if (string.IsNullOrEmpty(localPath))
                return;

            try
            {
                _sheetData = ExcelImportService.ReadWorkbook(localPath);
                FileNameText.Text = file!.Name;
                FileNameText.FontStyle = Avalonia.Media.FontStyle.Normal;

                var columnOptions = _sheetData.Columns.Cast<object>().ToList();
                ProductColumnCombo.ItemsSource = columnOptions;

                var quantityOptions = new List<object> { NoneOption };
                quantityOptions.AddRange(columnOptions);
                QuantityColumnCombo.ItemsSource = quantityOptions;
                QuantityColumnCombo.SelectedItem = NoneOption;

                StatusText.Text = $"{_sheetData.Rows.Count} veri satırı bulundu.";
                StatusText.Foreground = Avalonia.Media.Brushes.Gray;
            }
            catch (Exception ex)
            {
                _sheetData = null;
                StatusText.Text = $"Dosya okunamadı: {ex.Message}";
                StatusText.Foreground = Avalonia.Media.Brushes.DarkRed;
            }

            UpdatePreviewAndButtonState();
        }

        private void ColumnCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdatePreviewAndButtonState();
        }

        private void UpdatePreviewAndButtonState()
        {
            var productCol = ProductColumnCombo.SelectedItem as ColumnOption;
            var quantityCol = QuantityColumnCombo.SelectedItem as ColumnOption;

            ImportButton.IsEnabled = _sheetData is not null && productCol is not null;

            if (_sheetData is null || productCol is null)
            {
                PreviewText.Text = string.Empty;
                return;
            }

            var sb = new StringBuilder();
            var sample = _sheetData.Rows.Take(5).ToList();

            if (sample.Count == 0)
            {
                PreviewText.Text = "(Önizlenecek veri satırı yok)";
                return;
            }

            var n = 1;
            foreach (var row in sample)
            {
                var productName = _sheetData.GetCell(row, productCol.Index);
                sb.Append(n).Append(". İlaç: ").Append(productName);

                if (quantityCol is not null && quantityCol.Index >= 0)
                {
                    var quantityValue = _sheetData.GetCell(row, quantityCol.Index);
                    sb.Append("  |  Miktar: ").Append(string.IsNullOrWhiteSpace(quantityValue) ? "(boş → 0)" : quantityValue);
                }

                sb.AppendLine();
                n++;
            }

            PreviewText.Text = sb.ToString();
        }

        private void ImportButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_sheetData is null)
                return;

            var productCol = ProductColumnCombo.SelectedItem as ColumnOption;
            if (productCol is null)
            {
                StatusText.Text = "Lütfen ilaç adı sütununu seç.";
                StatusText.Foreground = Avalonia.Media.Brushes.DarkRed;
                return;
            }

            int? quantityColIndex = null;
            var quantityCol = QuantityColumnCombo.SelectedItem as ColumnOption;
            if (quantityCol is not null && quantityCol.Index >= 0)
            {
                quantityColIndex = quantityCol.Index;
            }

            try
            {
                ImportButton.IsEnabled = false;
                var result = ExcelImportService.Import(
                    _context,
                    _sheetData,
                    productCol.Index,
                    quantityColIndex);

                StatusText.Text = result.Summary;
                StatusText.Foreground = Avalonia.Media.Brushes.DarkGreen;
                DataImported?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"İçe aktarma sırasında hata oluştu: {ex.Message}";
                StatusText.Foreground = Avalonia.Media.Brushes.DarkRed;
            }
            finally
            {
                ImportButton.IsEnabled = true;
            }
        }
    }
}