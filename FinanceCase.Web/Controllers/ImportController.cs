using FinanceCase.Web.Services;
using FinanceCase.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace FinanceCase.Web.Controllers;

public class ImportController(IImportService importService) : Controller
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
            model.ErrorMessage = "Lütfen hem varlık hem de ÜFE dosyasını seçin.";
            return View(model);
        }

        try
        {
            var summary = await importService.ImportAsync(model.AssetFile, model.InflationFile);
            model.SuccessMessage = $"İçe aktarım tamamlandı. Varlık satırı: {summary.AssetCount}, ÜFE satırı: {summary.InflationCount}, senkronlanan kur kaydı: {summary.SyncedExchangeRateCount}. Kur aralığı: {summary.StartPeriod:MM.yyyy} - {summary.EndPeriod:MM.yyyy}";
            model.ShouldRedirectToExchangeRates = true;
            model.RedirectDelaySeconds = 3;
        }
        catch (InvalidOperationException ex)
        {
            model.ErrorMessage = ex.Message;
        }
        catch (FormatException)
        {
            model.ErrorMessage = "Yüklenen dosya şablonu beklenen formatla uyuşmuyor. Lütfen örnek dosya yapısına uygun Excel dosyaları yükleyin.";
        }
        catch
        {
            model.ErrorMessage = "Dosyalar işlenirken beklenmeyen bir hata oluştu. Lütfen dosya formatını kontrol edip tekrar deneyin.";
        }

        return View(model);
    }
}
