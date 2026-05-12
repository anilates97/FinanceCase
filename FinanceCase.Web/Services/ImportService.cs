using FinanceCase.Web.Data;
using FinanceCase.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Diagnostics;
using System.Globalization;

namespace FinanceCase.Web.Services;

public class ImportService(ApplicationDbContext dbContext, IExchangeRateService exchangeRateService) : IImportService
{
    public async Task<ImportSummary> ImportAsync(IFormFile assetFile, IFormFile inflationFile)
    {
        var stopwatch = Stopwatch.StartNew();
        var assetRecords = NormalizeAssetRecords(ReadAssetRecords(assetFile), out var skippedAssetRows);
        var inflationRecords = NormalizeInflationIndexRecords(ReadInflationIndexRecords(inflationFile), out var skippedInflationRows);

        return await ExecuteImportTransactionAsync(assetRecords, inflationRecords, skippedAssetRows, skippedInflationRows, stopwatch);
    }

    private async Task<ImportSummary> ExecuteImportTransactionAsync(
        List<AssetRecord> assetRecords,
        List<InflationIndexRecord> inflationRecords,
        int skippedAssetRows,
        int skippedInflationRows,
        Stopwatch stopwatch)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            var assetSummary = await UpsertAssetRecordsAsync(assetRecords, skippedAssetRows);
            var inflationSummary = await UpsertInflationIndexRecordsAsync(inflationRecords, skippedInflationRows);

            var startPeriod = assetRecords.Select(x => x.Period)
                .Concat(inflationRecords.Select(x => x.Period))
                .Min();
            var endPeriod = assetRecords.Select(x => x.Period)
                .Concat(inflationRecords.Select(x => x.Period))
                .Max();

            var exchangeRateSummary = await SyncExchangeRatesAsync(startPeriod, endPeriod);

            await transaction.CommitAsync();
            stopwatch.Stop();

            return BuildImportSummary(assetSummary, inflationSummary, exchangeRateSummary, stopwatch.Elapsed, startPeriod, endPeriod);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Import processing could not be completed. All changes were rolled back; no partial data was written to the database.", ex);
        }
    }

    private async Task<UpsertSummary> UpsertAssetRecordsAsync(List<AssetRecord> incomingRecords, int skippedCount)
    {
        var periods = incomingRecords.Select(x => x.Period).ToList();
        var minPeriod = periods.Min();
        var maxPeriod = periods.Max();

        var existingRecords = await dbContext.AssetRecords
            .Where(x => x.Period >= minPeriod && x.Period <= maxPeriod)
            .ToListAsync();

        var existingByPeriod = existingRecords.ToDictionary(x => x.Period);
        var insertedCount = 0;
        var updatedCount = 0;

        foreach (var incoming in incomingRecords)
        {
            if (existingByPeriod.TryGetValue(incoming.Period, out var existing))
            {
                existing.AssetAmount = incoming.AssetAmount;
                updatedCount++;
                continue;
            }

            await dbContext.AssetRecords.AddAsync(incoming);
            insertedCount++;
        }

        await dbContext.SaveChangesAsync();

        return new UpsertSummary(insertedCount, updatedCount, skippedCount);
    }

    private async Task<UpsertSummary> UpsertInflationIndexRecordsAsync(List<InflationIndexRecord> incomingRecords, int skippedCount)
    {
        var periods = incomingRecords.Select(x => x.Period).ToList();
        var minPeriod = periods.Min();
        var maxPeriod = periods.Max();

        var existingRecords = await dbContext.InflationIndexRecords
            .Where(x => x.Period >= minPeriod && x.Period <= maxPeriod)
            .ToListAsync();

        var existingByPeriod = existingRecords.ToDictionary(x => x.Period);
        var insertedCount = 0;
        var updatedCount = 0;

        foreach (var incoming in incomingRecords)
        {
            if (existingByPeriod.TryGetValue(incoming.Period, out var existing))
            {
                existing.Year = incoming.Year;
                existing.Month = incoming.Month;
                existing.IndexValue = incoming.IndexValue;
                updatedCount++;
                continue;
            }

            await dbContext.InflationIndexRecords.AddAsync(incoming);
            insertedCount++;
        }

        await dbContext.SaveChangesAsync();

        return new UpsertSummary(insertedCount, updatedCount, skippedCount);
    }

    private Task<ExchangeRateSyncSummary> SyncExchangeRatesAsync(DateTime startPeriod, DateTime endPeriod)
    {
        var startDate = new DateTime(startPeriod.Year, startPeriod.Month, 1);
        var endDate = new DateTime(endPeriod.Year, endPeriod.Month, DateTime.DaysInMonth(endPeriod.Year, endPeriod.Month));

        return exchangeRateService.FetchAndSaveRatesWithSummaryAsync(startDate, endDate);
    }

    private static ImportSummary BuildImportSummary(
        UpsertSummary assetSummary,
        UpsertSummary inflationSummary,
        ExchangeRateSyncSummary exchangeRateSummary,
        TimeSpan duration,
        DateTime startPeriod,
        DateTime endPeriod)
    {
        return new ImportSummary(
            assetSummary.InsertedCount,
            assetSummary.UpdatedCount,
            assetSummary.SkippedCount,
            inflationSummary.InsertedCount,
            inflationSummary.UpdatedCount,
            inflationSummary.SkippedCount,
            exchangeRateSummary.InsertedCount,
            exchangeRateSummary.UpdatedCount,
            exchangeRateSummary.SkippedCount,
            duration,
            startPeriod,
            endPeriod);
    }

    private static List<AssetRecord> NormalizeAssetRecords(List<AssetRecord> records, out int skippedDuplicateRows)
    {
        var normalizedRecords = records
            .Select(x => new AssetRecord
            {
                Period = NormalizeMonth(x.Period),
                AssetAmount = x.AssetAmount
            })
            .GroupBy(x => x.Period)
            .Select(x => x.Last())
            .OrderBy(x => x.Period)
            .ToList();

        skippedDuplicateRows = records.Count - normalizedRecords.Count;
        return normalizedRecords;
    }

    private static List<InflationIndexRecord> NormalizeInflationIndexRecords(List<InflationIndexRecord> records, out int skippedDuplicateRows)
    {
        var normalizedRecords = records
            .Select(x =>
            {
                var period = NormalizeMonth(x.Period);
                return new InflationIndexRecord
                {
                    Year = period.Year,
                    Month = period.Month,
                    Period = period,
                    IndexValue = x.IndexValue
                };
            })
            .GroupBy(x => x.Period)
            .Select(x => x.Last())
            .OrderBy(x => x.Period)
            .ToList();

        skippedDuplicateRows = records.Count - normalizedRecords.Count;
        return normalizedRecords;
    }

    private static List<AssetRecord> ReadAssetRecords(IFormFile assetFile)
    {
        ValidateExcelExtension(assetFile, "Asset file");

        using var workbook = OpenWorkbook(assetFile);
        var sheet = workbook.GetSheetAt(0) ?? throw new InvalidOperationException("The asset file could not be read.");
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
            throw new InvalidOperationException("No importable records were found in the asset file. Please upload a file that follows the sample template.");
        }

        return records;
    }

    private static List<InflationIndexRecord> ReadInflationIndexRecords(IFormFile inflationFile)
    {
        ValidateExcelExtension(inflationFile, "Inflation index file");

        using var workbook = OpenWorkbook(inflationFile);
        var sheet = workbook.GetSheetAt(0) ?? throw new InvalidOperationException("The inflation index file could not be read.");
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
            throw new InvalidOperationException("No importable records were found in the inflation index file. Please upload a file that follows the sample template.");
        }

        return records;
    }

    private static void ValidateExcelExtension(IFormFile file, string fileLabel)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".xls" and not ".xlsx")
        {
            throw new InvalidOperationException($"{fileLabel} must be an Excel file (.xls or .xlsx).");
        }
    }

    private static void ValidateAssetSheet(ISheet sheet)
    {
        var headerRow = sheet.GetRow(0);
        var firstHeader = NormalizeText(headerRow?.GetCell(0)?.ToString());
        var secondHeader = NormalizeText(headerRow?.GetCell(1)?.ToString());

        if (firstHeader != "tarih" || secondHeader != "varlik tutari")
        {
            throw new InvalidOperationException("The asset file does not match the expected template. The first two columns must be 'Date' and 'Asset Amount'.");
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
            throw new InvalidOperationException("The inflation index file does not match the expected template. Upload an index table with a 'Year' header.");
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
            _ => throw new InvalidOperationException("Unsupported file format.")
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
            return cell.DateCellValue ?? throw new InvalidOperationException("The date cell could not be read.");
        }

        var value = cell.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("The date field cannot be empty.");
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
            throw new InvalidOperationException("Numeric fields cannot be empty.");
        }

        var normalized = value.Replace("?", string.Empty).Trim();
        return decimal.Parse(normalized, NumberStyles.Number, new CultureInfo("tr-TR"));
    }

    private static DateTime NormalizeMonth(DateTime value)
    {
        return new DateTime(value.Year, value.Month, 1);
    }
}
