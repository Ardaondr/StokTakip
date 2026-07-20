using ClosedXML.Excel;
using StokTakip.Data;
using StokTakip.Models;

namespace StokTakip.Services
{
    // Başlık + sütun indeksi
    public record ColumnOption(int Index, string Header)
    {
        public override string ToString() => Header;
    }

    //Başlıklar + veri satırları
    public class ExcelSheetData
    {
        public List<ColumnOption> Columns { get; init; } = new();
        public List<string[]> Rows { get; init; } = new();

        public string GetCell(string[] row, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= row.Length)
                return string.Empty;
            return row[columnIndex]?.Trim() ?? string.Empty;
        }
    }

    public class ImportResult
    {
        public int NewProducts { get; set; }
        public int UpdatedProducts { get; set; }
        public int AlreadyExisted { get; set; }
        public int SkippedRows { get; set; }
        public int QuantityParseFailures { get; set; }
        public int TotalRows { get; set; }

        public string Summary
        {
            get
            {
                var mesaj =
                    $"Toplam {TotalRows} satır işlendi: {NewProducts} yeni ilaç eklendi, " +
                    $"{UpdatedProducts} mevcut ürünün miktarı güncellendi, " +
                    $"{AlreadyExisted} zaten kayıtlıydı ve değişmedi, " +
                    $"{SkippedRows} satır boş/geçersiz olduğu için atlandı.";

                if (QuantityParseFailures > 0)
                {
                    mesaj += $" {QuantityParseFailures} satırda miktar okunamadı.";
                }

                return mesaj;
            }
        }
    }

    public static class ExcelImportService
    {
        // İlk çalışma sayfasını okur. 1. satır başlık kabul edilir. Boş satırlar atlanır. Sütun sayısı değişken olabilir.
        public static ExcelSheetData ReadWorkbook(string filePath)
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheets.First();
            var usedRange = worksheet.RangeUsed()
                ?? throw new InvalidOperationException("Excel dosyasında veri bulunamadı.");

            var firstRow = usedRange.FirstRow();
            var lastColumnUsed = usedRange.LastColumn().ColumnNumber();
            var firstColumnUsed = usedRange.FirstColumn().ColumnNumber();

            var columns = new List<ColumnOption>();
            for (var col = firstColumnUsed; col <= lastColumnUsed; col++)
            {
                var headerText = firstRow.Cell(col).GetString().Trim();
                if (string.IsNullOrEmpty(headerText))
                    headerText = $"(Sütun {col})";
                columns.Add(new ColumnOption(col - firstColumnUsed, headerText));
            }

            var rows = new List<string[]>();
            var lastRowUsed = usedRange.LastRow().RowNumber();
            var firstDataRow = firstRow.RowNumber() + 1;

            for (var r = firstDataRow; r <= lastRowUsed; r++)
            {
                var rowValues = new string[columns.Count];
                for (var col = firstColumnUsed; col <= lastColumnUsed; col++)
                {
                    rowValues[col - firstColumnUsed] = worksheet.Cell(r, col).GetString();
                }

                if (rowValues.All(string.IsNullOrWhiteSpace))
                    continue;

                rows.Add(rowValues);
            }

            return new ExcelSheetData { Columns = columns, Rows = rows };
        }

        // Belirtilen sütun eşleştirmesine göre ürünleri Products tablosuna işler:
        // - İlaç sistemde yoksa yeni kayıt olarak eklenir (miktar sütunu okunabiliyorsa o değerle, yoksa 0 ile).
        // - İlaç sistemde zaten varsa VE miktar sütunu o satırda okunabiliyorsa, mevcut ürünün miktarı güncellenir.
        // - İlaç sistemde zaten varsa AMA miktar sütunu seçilmemişse/okunamıyorsa, ürüne dokunulmaz.
        public static ImportResult Import(
            StokContext context,
            ExcelSheetData data,
            int productNameColumnIndex,
            int? quantityColumnIndex)
        {
            var result = new ImportResult { TotalRows = data.Rows.Count };

            var existingProducts = context.Products
                .ToList()
                .ToDictionary(p => Normalize(p.Name), p => p);

            foreach (var row in data.Rows)
            {
                var productName = data.GetCell(row, productNameColumnIndex);

                if (string.IsNullOrWhiteSpace(productName))
                {
                    result.SkippedRows++;
                    continue;
                }

                int? parsedQuantity = null;
                if (quantityColumnIndex is not null)
                {
                    var quantityText = data.GetCell(row, quantityColumnIndex.Value);
                    if (!string.IsNullOrWhiteSpace(quantityText))
                    {
                        if (TryParseQuantity(quantityText, out var q))
                        {
                            parsedQuantity = q;
                        }
                        else
                        {
                            // Hücrede bir şey yazıyordu ama sayıya çevrilemedi (örn. "elli adet" gibi serbest metin)
                            result.QuantityParseFailures++;
                        }
                    }
                }

                var key = Normalize(productName);

                if (existingProducts.TryGetValue(key, out var mevcutUrun))
                {
                    if (parsedQuantity is not null)
                    {
                        mevcutUrun.Quantity = parsedQuantity.Value;
                        mevcutUrun.LastUpdated = DateTime.Now;
                        result.UpdatedProducts++;
                    }
                    else
                    {
                        result.AlreadyExisted++;
                    }
                    continue;
                }

                var yeniUrun = new Product
                {
                    Name = productName,
                    Quantity = parsedQuantity ?? 0,
                    LastUpdated = DateTime.Now
                };

                context.Products.Add(yeniUrun);
                existingProducts[key] = yeniUrun;
                result.NewProducts++;
            }

            context.SaveChanges();
            return result;
        }

        // "50", "50,0", "50.0" gibi hem virgüllü hem noktalı ondalık ayraçları tolere eder,
        // negatif miktar girilirse 0'a düşürür.
        private static bool TryParseQuantity(string text, out int quantity)
        {
            var temizlenmis = text.Trim().Replace(",", ".");

            if (double.TryParse(
                    temizlenmis,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var sayisalDeger))
            {
                quantity = Math.Max(0, (int)Math.Round(sayisalDeger));
                return true;
            }

            quantity = 0;
            return false;
        }

        private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    }
}