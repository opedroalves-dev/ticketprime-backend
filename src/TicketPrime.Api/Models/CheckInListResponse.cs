namespace TicketPrime.Api.Models;

public class CheckInListResponse
{
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public int TotalCheckIns { get; set; }
    public List<CheckInItemResponse> CheckIns { get; set; } = new();
}
