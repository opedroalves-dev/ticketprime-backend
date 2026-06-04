namespace TicketPrime.Api.Models;

public class DashboardEventoDetalhadoResponse
{
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
    public int CapacidadeTotal { get; set; }
    public decimal PrecoPadrao { get; set; }
    public int TotalIngressosVendidos { get; set; }
    public decimal ReceitaTotal { get; set; }
    public decimal PercentualOcupacao { get; set; }
    public int TotalCheckIns { get; set; }
    public int PendentesCheckIn { get; set; }
    public int TotalCancelados { get; set; }
    public List<DashboardLoteResponse> Lotes { get; set; } = new();
}
