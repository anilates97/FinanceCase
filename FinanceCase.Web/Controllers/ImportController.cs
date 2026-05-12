using FinanceCase.Web.Data;
using FinanceCase.Web.Services;
using FinanceCase.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceCase.Web.Controllers;

public class ImportController(
    IImportService importService,
    IDemoDatasetService demoDatasetService,
    ApplicationDbContext dbContext,
    IWebHostEnvironment environment) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View(new ImportFilesViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ImportFilesViewModel model)
    {
        if (model.AssetFile is null || model.InflationFile is null)
        {
            model.ErrorMessage = "Please select both the asset file and the inflation index file.";
            return View(model);
        }

        try
        {
            var summary = await importService.ImportAsync(model.AssetFile, model.InflationFile);
            TempData["PipelineStatus"] = BuildPipelineStatusMessage(summary, "Import completed safely");
            return RedirectToAction("Index", "Calculation");
        }
        catch (InvalidOperationException ex)
        {
            model.ErrorMessage = ex.Message;
        }
        catch (FormatException)
        {
            model.ErrorMessage = "The uploaded file template does not match the expected format. Please upload Excel files that follow the sample structure.";
        }
        catch
        {
            model.ErrorMessage = "An unexpected error occurred while processing the files. Please verify the file format and try again.";
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadDemoDataset()
    {
        var model = new ImportFilesViewModel();

        try
        {
            var summary = await demoDatasetService.LoadDemoDatasetAsync();
            TempData["PipelineStatus"] = BuildPipelineStatusMessage(summary, "Demo dataset loaded successfully");
            return RedirectToAction("Index", "Calculation");
        }
        catch (InvalidOperationException ex)
        {
            model.ErrorMessage = ex.Message;
        }
        catch
        {
            model.ErrorMessage = "The demo dataset could not be loaded. Please try again or use the manual Excel import workflow.";
        }

        return View("Index", model);
    }

    private static string BuildPipelineStatusMessage(ImportSummary summary, string title)
    {
        return $"{title}. Assets: {summary.AssetInsertedCount} inserted, {summary.AssetUpdatedCount} updated. " +
            $"Inflation index: {summary.InflationInsertedCount} inserted, {summary.InflationUpdatedCount} updated. " +
            $"Exchange rates: {summary.ExchangeRateInsertedCount} inserted, {summary.ExchangeRateUpdatedCount} updated. " +
            $"Range: {summary.StartPeriod:MMM yyyy} - {summary.EndPeriod:MMM yyyy}.";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearData()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        dbContext.AssetRecords.RemoveRange(dbContext.AssetRecords);
        dbContext.InflationIndexRecords.RemoveRange(dbContext.InflationIndexRecords);
        dbContext.ExchangeRates.RemoveRange(dbContext.ExchangeRates);
        await dbContext.SaveChangesAsync();

        return View("Index", new ImportFilesViewModel
        {
            InfoMessage = "All data has been cleared. You can upload files again for a new test run."
        });
    }
}
