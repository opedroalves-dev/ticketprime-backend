namespace TicketPrime.Api.Models;

public class IngressoResponse
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public int? TipoIngressoId { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ValorBruto { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal TaxaServico { get; set; }
    public decimal ValorFinal { get; set; }
    public DateTime DataCriacao { get; set; }
}
