using FinanceCase.Web.Services;
using FinanceCase.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace FinanceCase.Web.Controllers;

public class CalculationController(ICalculationService calculationService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? startPeriod, DateTime? endPeriod)
    {
        var model = await BuildPageModelAsync(startPeriod, endPeriod);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> LiveResults(DateTime? startPeriod, DateTime? endPeriod)
    {
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
