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

public class DashboardLoteResponse
{
    public int TipoIngressoId { get; set; }
    public string NomeLote { get; set; } = string.Empty;
    public decimal PrecoAtual { get; set; }
    public int CapacidadeLote { get; set; }
    public decimal TaxaServico { get; set; }
    public int IngressosVendidos { get; set; }
    public int CapacidadeRestante { get; set; }
    public decimal ReceitaLote { get; set; }
    public int CheckInsRealizados { get; set; }
}
