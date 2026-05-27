namespace TicketPrime.Api.Models;

public class HistoricoPrecoResponse
{
    public int Id { get; set; }
    public decimal? PrecoAnterior { get; set; }
    public decimal PrecoNovo { get; set; }
    public DateTime DataAlteracao { get; set; }
    public string? Motivo { get; set; }
    public int? TipoIngressoId { get; set; }
    public string? NomeLote { get; set; }
}
