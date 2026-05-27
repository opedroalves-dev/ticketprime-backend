namespace TicketPrime.Api.Models;

public class HistoricoPreco
{
    public int Id { get; set; }
    public int? EventoId { get; set; }
    public int? TipoIngressoId { get; set; }
    public decimal? PrecoAnterior { get; set; }
    public decimal PrecoNovo { get; set; }
    public DateTime DataAlteracao { get; set; }
    public string? Motivo { get; set; }
}
