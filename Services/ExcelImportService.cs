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
        public int AlreadyExisted { get; set; }
        public int SkippedRows { get; set; }
        public int TotalRows { get; set; }

        public string Summary =>
            $"Toplam {TotalRows} satır işlendi: {NewProducts} yeni ilaç eklendi, " +
            $"{AlreadyExisted} zaten kayıtlıydı (atlandı), {SkippedRows} satır boş/geçersiz olduğu için atlandı.";
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

        // Belirtilen sütun eşleştirmesine göre yeni ilaçları Products tablosuna ekler.
        // Miktar alanına dokunulmaz: yeni ürünler 0 stokla eklenir, zaten var olanlar atlanır.
        public static ImportResult Import(
            StokContext context,
            ExcelSheetData data,
            int productNameColumnIndex,
            int? statusColumnIndex,
            string? requiredStatusContains)
        {
            var result = new ImportResult { TotalRows = data.Rows.Count };

            var existingNames = context.Products
                .Select(p => p.Name)
                .ToList()
                .Select(Normalize)
                .ToHashSet();

            foreach (var row in data.Rows)
            {
                var productName = data.GetCell(row, productNameColumnIndex);

                if (string.IsNullOrWhiteSpace(productName))
                {
                    result.SkippedRows++;
                    continue;
                }

                if (statusColumnIndex is not null && !string.IsNullOrWhiteSpace(requiredStatusContains))
                {
                    var statusValue = data.GetCell(row, statusColumnIndex.Value);
                    if (!statusValue.Contains(requiredStatusContains, StringComparison.OrdinalIgnoreCase))
                    {
                        result.SkippedRows++;
                        continue;
                    }
                }

                var key = Normalize(productName);
                if (existingNames.Contains(key))
                {
                    result.AlreadyExisted++;
                    continue;
                }

                context.Products.Add(new Product
                {
                    Name = productName,
                    Quantity = 0,
                    LastUpdated = DateTime.Now
                });
                existingNames.Add(key);
                result.NewProducts++;
            }

            context.SaveChanges();
            return result;
        }

        private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    }
}
