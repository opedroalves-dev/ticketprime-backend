namespace TicketPrime.Api.Models;

public class CriarTipoIngressoRequest
{
    public int EventoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int QuantidadeDisponivel { get; set; }
    public decimal Preco { get; set; }
    public string Lote { get; set; } = string.Empty;
}
