using System.Text.Json.Serialization;

namespace FinanceCase.Web.Dtos;

public class ExchangeRateApiDto
{
    public int BaseCurrencyCode { get; set; }
    public int ForeignCurrencyCode { get; set; }
    public decimal ChangeRate { get; set; }

    [JsonPropertyName("ExchangeRate")]
    public decimal ExchangeRateValue { get; set; }

    public decimal CashChangeRate { get; set; }
    public decimal CashExchangeRate { get; set; }
    public decimal CentralBankChangeRate { get; set; }
    public decimal CentralBankExchangeRate { get; set; }
    public decimal CrossRate { get; set; }
    public DateTime CurrentDate { get; set; }
}
