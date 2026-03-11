using FinanceCase.Web.Data;
using FinanceCase.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceCase.Web.Controllers;

public class ExchangeRatesController(ApplicationDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index(int page = 1)
    {
        var model = await BuildModelAsync(page);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Table(int page = 1)
    {
        var model = await BuildModelAsync(page);
        return PartialView("_ExchangeRatesTable", model);
    }

    private async Task<ExchangeRatesPageViewModel> BuildModelAsync(int page)
    {
        const int pageSize = 25;
        var safePage = page < 1 ? 1 : page;

        var query = dbContext.ExchangeRates
            .OrderBy(x => x.BaseCurrencyCode)
            .ThenByDescending(x => x.CurrentDate);

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (safePage > totalPages)
        {
            safePage = totalPages;
        }

        var rates = await query
            .Skip((safePage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var latestRateTimestamp = await dbContext.ExchangeRates.MaxAsync(x => (DateTime?)x.CurrentDate);

        return new ExchangeRatesPageViewModel
        {
            Rates = rates,
            LastRateDataAt = latestRateTimestamp,
            CurrentPage = safePage,
            TotalPages = totalPages,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
