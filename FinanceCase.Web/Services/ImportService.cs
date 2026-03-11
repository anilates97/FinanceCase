using FinanceCase.Web.Data;
using FinanceCase.Web.Models;
using Microsoft.AspNetCore.Http;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Globalization;

namespace FinanceCase.Web.Services;

public class ImportService(ApplicationDbContext dbContext) : IImportService
{
    public async Task<int> ImportAssetRecordsAsync(IFormFile assetFile)
    {
        ValidateExcelExtension(assetFile, "Varlık dosyası");

        using var workbook = OpenWorkbook(assetFile);
        var sheet = workbook.GetSheetAt(0) ?? throw new InvalidOperationException("Varlık dosyası okunamadı.");
        ValidateAssetSheet(sheet);

        var records = new List<AssetRecord>();

        for (var rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            var row = sheet.GetRow(rowIndex);
            if (row is null)
            {
                continue;
            }

            var dateCell = row.GetCell(0);
            var amountCell = row.GetCell(1);

            if (dateCell is null || amountCell is null || IsCellEmpty(dateCell) || IsCellEmpty(amountCell))
            {
                continue;
            }

            var period = ParsePeriod(dateCell);
            var assetAmount = ParseDecimalCell(amountCell);

            records.Add(new AssetRecord
            {
                Period = period,
                AssetAmount = assetAmount
            });
        }

        if (records.Count == 0)
        {
            throw new InvalidOperationException("Varlık dosyasında içe aktarılabilir kayıt bulunamadı. Lütfen örnek şablona uygun bir dosya yükleyin.");
        }

        dbContext.AssetRecords.RemoveRange(dbContext.AssetRecords);
        await dbContext.AssetRecords.AddRangeAsync(records);
        await dbContext.SaveChangesAsync();

        return records.Count;
    }

    public async Task<int> ImportInflationIndexRecordsAsync(IFormFile inflationFile)
    {
        ValidateExcelExtension(inflationFile, "ÜFE dosyası");

        using var workbook = OpenWorkbook(inflationFile);
        var sheet = workbook.GetSheetAt(0) ?? throw new InvalidOperationException("ÜFE dosyası okunamadı.");
        ValidateInflationSheet(sheet);

        var records = new List<InflationIndexRecord>();

        for (var rowIndex = 0; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            var row = sheet.GetRow(rowIndex);
            if (row is null)
            {
                continue;
            }

            if (!TryParseYear(row.GetCell(0), out var year))
            {
                continue;
            }

            for (var month = 1; month <= 12; month++)
            {
                var cell = row.GetCell(month);
                if (cell is null || IsCellEmpty(cell))
                {
                    continue;
                }

                var indexValue = ParseDecimalCell(cell);
                records.Add(new InflationIndexRecord
                {
                    Year = year,
                    Month = month,
                    Period = new DateTime(year, month, 1),
                    IndexValue = indexValue
                });
            }
        }

        if (records.Count == 0)
        {
            throw new InvalidOperationException("ÜFE dosyasında içe aktarılabilir kayıt bulunamadı. Lütfen örnek şablona uygun bir dosya yükleyin.");
        }

        dbContext.InflationIndexRecords.RemoveRange(dbContext.InflationIndexRecords);
        await dbContext.InflationIndexRecords.AddRangeAsync(records);
        await dbContext.SaveChangesAsync();

        return records.Count;
    }

    private static void ValidateExcelExtension(IFormFile file, string fileLabel)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".xls" and not ".xlsx")
        {
            throw new InvalidOperationException($"{fileLabel} Excel formatında olmalıdır (.xls veya .xlsx).");
        }
    }

    private static void ValidateAssetSheet(ISheet sheet)
    {
        var headerRow = sheet.GetRow(0);
        var firstHeader = headerRow?.GetCell(0)?.ToString()?.Trim();
        var secondHeader = headerRow?.GetCell(1)?.ToString()?.Trim();

        // varlık dosyası daha basit bir iki kolon yapısı ile okunur
        if (!string.Equals(firstHeader, "Tarih", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(secondHeader, "Varlık Tutarı", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Varlık dosyası beklenen şablonda değil. İlk iki sütun 'Tarih' ve 'Varlık Tutarı' olmalıdır.");
        }
    }

    private static void ValidateInflationSheet(ISheet sheet)
    {
        var hasYearHeader = false;

        for (var rowIndex = 0; rowIndex <= Math.Min(sheet.LastRowNum, 10); rowIndex++)
        {
            var row = sheet.GetRow(rowIndex);
            var firstCell = row?.GetCell(0)?.ToString()?.Trim();
            if (string.Equals(firstCell, "Yıl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(firstCell, "Year", StringComparison.OrdinalIgnoreCase))
            {
                hasYearHeader = true;
                break;
            }
        }

        // üfe dosyasında tablo üstünde açıklama satırları olduğu için önce başlık aranır
        if (!hasYearHeader)
        {
            throw new InvalidOperationException("ÜFE dosyası beklenen şablonda değil. 'Yıl/Year' başlığını içeren endeks tablosu yükleyin.");
        }
    }

    private static IWorkbook OpenWorkbook(IFormFile file)
    {
        var stream = file.OpenReadStream();
        return Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".xls" => new HSSFWorkbook(stream),
            ".xlsx" => new XSSFWorkbook(stream),
            _ => throw new InvalidOperationException("Desteklenmeyen dosya formatı.")
        };
    }

    private static bool TryParseYear(ICell? cell, out int year)
    {
        year = 0;
        if (cell is null || IsCellEmpty(cell))
        {
            return false;
        }

        if (cell.CellType == CellType.Numeric)
        {
            year = Convert.ToInt32(cell.NumericCellValue);
            return year >= 1900 && year <= 2100;
        }

        return int.TryParse(cell.ToString()?.Trim(), out year) && year >= 1900 && year <= 2100;
    }

    private static bool IsCellEmpty(ICell cell)
    {
        return cell.CellType == CellType.Blank || string.IsNullOrWhiteSpace(cell.ToString());
    }

    private static DateTime ParsePeriod(ICell cell)
    {
        if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
        {
            return cell.DateCellValue ?? throw new InvalidOperationException("Tarih hücresi okunamadı.");
        }

        var value = cell.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Tarih alanı boş olamaz.");
        }

        var culture = new CultureInfo("tr-TR");
        var formats = new[] { "dd-MMM-yyyy", "d-MMM-yyyy", "MMM-yy", "dd.MM.yyyy", "d.MM.yyyy" };

        return DateTime.ParseExact(value, formats, culture, DateTimeStyles.None);
    }

    private static decimal ParseDecimalCell(ICell cell)
    {
        if (cell.CellType == CellType.Numeric)
        {
            return Convert.ToDecimal(cell.NumericCellValue);
        }

        var value = cell.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Sayısal alan boş olamaz.");
        }

        var normalized = value.Replace("?", string.Empty).Trim();
        return decimal.Parse(normalized, NumberStyles.Number, new CultureInfo("tr-TR"));
    }
}
