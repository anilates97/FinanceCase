using FinanceCase.Web.ViewModels;

namespace FinanceCase.Web.Services;

public interface ICalculationService
{
    Task<List<CalculationResultRowViewModel>> CalculateAsync(DateTime? startPeriod = null, DateTime? endPeriod = null);
}
