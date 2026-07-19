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

                var statusOptions = new List<object> { NoneOption };
                statusOptions.AddRange(columnOptions);
                StatusColumnCombo.ItemsSource = statusOptions;
                StatusColumnCombo.SelectedItem = NoneOption;

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

        private void StatusFilterCheck_Toggled(object? sender, RoutedEventArgs e)
        {
            UpdatePreviewAndButtonState();
        }

        private void StatusValueBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdatePreviewAndButtonState();
        }

        private void UpdatePreviewAndButtonState()
        {
            var productCol = ProductColumnCombo.SelectedItem as ColumnOption;

            var statusColSelected = StatusFilterCheck.IsChecked == true;
            var statusCol = StatusColumnCombo.SelectedItem as ColumnOption;
            var statusColValid = !statusColSelected || (statusCol is not null && statusCol.Index >= 0);

            ImportButton.IsEnabled = _sheetData is not null && productCol is not null && statusColValid;

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

                if (statusColSelected && statusCol is not null && statusCol.Index >= 0)
                {
                    var statusValue = _sheetData.GetCell(row, statusCol.Index);
                    sb.Append("  |  Durum: ").Append(statusValue);
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

            int? statusColIndex = null;
            string? statusRequiredValue = null;

            if (StatusFilterCheck.IsChecked == true)
            {
                var statusCol = StatusColumnCombo.SelectedItem as ColumnOption;
                if (statusCol is null || statusCol.Index < 0)
                {
                    StatusText.Text = "Durum filtresini işaretlediysen bir durum sütunu seçmelisin.";
                    StatusText.Foreground = Avalonia.Media.Brushes.DarkRed;
                    return;
                }

                statusColIndex = statusCol.Index;
                statusRequiredValue = StatusValueBox.Text?.Trim();
            }

            try
            {
                ImportButton.IsEnabled = false;
                var result = ExcelImportService.Import(
                    _context,
                    _sheetData,
                    productCol.Index,
                    statusColIndex,
                    statusRequiredValue);

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
