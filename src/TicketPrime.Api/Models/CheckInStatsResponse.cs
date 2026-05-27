namespace TicketPrime.Api.Models;

public class CheckInStatsResponse
{
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public int TotalIngressosVendidos { get; set; }
    public int TotalCheckIns { get; set; }
    public int Pendentes { get; set; }
    public decimal PercentualPresenca { get; set; }
}
