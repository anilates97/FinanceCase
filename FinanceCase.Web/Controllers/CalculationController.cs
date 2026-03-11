using FinanceCase.Web.Services;
using FinanceCase.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace FinanceCase.Web.Controllers;

public class CalculationController(ICalculationService calculationService, IAppStateService appStateService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? startPeriod, DateTime? endPeriod)
    {
        if (!await appStateService.IsDataReadyAsync())
        {
            return RedirectToAction("Index", "Import");
        }

        var model = await BuildPageModelAsync(startPeriod, endPeriod);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> LiveResults(DateTime? startPeriod, DateTime? endPeriod)
    {
        if (!await appStateService.IsDataReadyAsync())
        {
            return RedirectToAction("Index", "Import");
        }

        var model = await BuildPageModelAsync(startPeriod, endPeriod);
        return PartialView("_ResultsContent", model);
    }

    private async Task<CalculationPageViewModel> BuildPageModelAsync(DateTime? startPeriod, DateTime? endPeriod)
    {
        var rows = await calculationService.CalculateAsync(startPeriod, endPeriod);

        return new CalculationPageViewModel
        {
            StartPeriod = startPeriod,
            EndPeriod = endPeriod,
            Rows = rows
        };
    }
}
