namespace FinanceCase.Web.Dtos;

public class ExchangeRateApiResponseDto
{
    public List<ExchangeRateApiDto> ExchangeRates { get; set; } = [];
    public ApiResponseHeaderDto? Header { get; set; }
}
