namespace TicketPrime.Api.Models;

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
