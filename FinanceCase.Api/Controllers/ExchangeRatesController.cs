using FinanceCase.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceCase.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExchangeRatesController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var latestTimestamp = await dbContext.ExchangeRates
            .MaxAsync(x => (DateTime?)x.CurrentDate);

        if (!latestTimestamp.HasValue)
        {
            return NotFound(new { message = "Henüz kur verisi bulunmuyor." });
        }

        var rates = await dbContext.ExchangeRates
            .Where(x => x.CurrentDate == latestTimestamp.Value)
            .OrderBy(x => x.BaseCurrencyCode)
            .ThenBy(x => x.ForeignCurrencyCode)
            .Select(x => new
            {
                x.BaseCurrencyCode,
                x.ForeignCurrencyCode,
                x.ChangeRate,
                ExchangeRate = x.ExchangeRateValue,
                x.CashChangeRate,
                x.CashExchangeRate,
                x.CentralBankChangeRate,
                x.CentralBankExchangeRate,
                x.CrossRate,
                x.CurrentDate
            })
            .ToListAsync();

        return Ok(new
        {
            currentDate = latestTimestamp.Value,
            count = rates.Count,
            exchangeRates = rates
        });
    }
}
