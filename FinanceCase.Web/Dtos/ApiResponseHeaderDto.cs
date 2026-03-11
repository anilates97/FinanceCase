namespace FinanceCase.Web.Dtos;

public class ApiResponseHeaderDto
{
    public int Status { get; set; }
    public string ResponseCode { get; set; } = string.Empty;
    public string ResponseMessage { get; set; } = string.Empty;
}
