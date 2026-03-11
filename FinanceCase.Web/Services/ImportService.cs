using FinanceCase.Web.Data;
using FinanceCase.Web.Models;
using Microsoft.AspNetCore.Http;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Globalization;

namespace FinanceCase.Web.Services;

public class ImportService(ApplicationDbContext dbContext, IExchangeRateService exchangeRateService) : IImportService
{
    public async Task<ImportSummary> ImportAsync(IFormFile assetFile, IFormFile inflationFile)
    {
        var assetRecords = ReadAssetRecords(assetFile);
        var inflationRecords = ReadInflationIndexRecords(inflationFile);

        dbContext.AssetRecords.RemoveRange(dbContext.AssetRecords);
        dbContext.InflationIndexRecords.RemoveRange(dbContext.InflationIndexRecords);

        await dbContext.AssetRecords.AddRangeAsync(assetRecords);
        await dbContext.InflationIndexRecords.AddRangeAsync(inflationRecords);
        await dbContext.SaveChangesAsync();

        var startPeriod = assetRecords.Select(x => x.Period)
            .Concat(inflationRecords.Select(x => x.Period))
            .Min();
        var endPeriod = assetRecords.Select(x => x.Period)
            .Concat(inflationRecords.Select(x => x.Period))
            .Max();

        // dosya yüklendikten sonra hesaplama için gerekli tarih aralığındaki kur verileri de senkronlanır
        var syncedExchangeRateCount = await exchangeRateService.FetchAndSaveRatesAsync(
            new DateTime(startPeriod.Year, startPeriod.Month, 1),
            new DateTime(endPeriod.Year, endPeriod.Month, DateTime.DaysInMonth(endPeriod.Year, endPeriod.Month)));

        return new ImportSummary(
            assetRecords.Count,
            inflationRecords.Count,
            syncedExchangeRateCount,
            startPeriod,
            endPeriod);
    }

    private static List<AssetRecord> ReadAssetRecords(IFormFile assetFile)
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

        return records;
    }

    private static List<InflationIndexRecord> ReadInflationIndexRecords(IFormFile inflationFile)
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

        return records;
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
        var firstHeader = NormalizeText(headerRow?.GetCell(0)?.ToString());
        var secondHeader = NormalizeText(headerRow?.GetCell(1)?.ToString());

        if (firstHeader != "tarih" || secondHeader != "varlik tutari")
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
            var firstCell = NormalizeText(row?.GetCell(0)?.ToString());
            if (firstCell is "yil" or "year")
            {
                hasYearHeader = true;
                break;
            }
        }

        if (!hasYearHeader)
        {
            throw new InvalidOperationException("ÜFE dosyası beklenen şablonda değil. 'Yıl/Year' başlığını içeren endeks tablosu yükleyin.");
        }
    }

    private static string NormalizeText(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Replace('\u0131', 'i')
            .Replace('\u0130', 'I')
            .Replace('\u00FC', 'u')
            .Replace('\u00DC', 'U')
            .Replace('\u011F', 'g')
            .Replace('\u011E', 'G')
            .Replace('\u015F', 's')
            .Replace('\u015E', 'S')
            .Replace('\u00F6', 'o')
            .Replace('\u00D6', 'O')
            .Replace('\u00E7', 'c')
            .Replace('\u00C7', 'C')
            .Replace('\u00FD', 'i')
            .Replace('\u00DD', 'I')
            .Replace('\u00FE', 's')
            .Replace('\u00DE', 'S')
            .Replace('\u00F0', 'g')
            .Replace('\u00D0', 'G')
            .ToLowerInvariant();
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
