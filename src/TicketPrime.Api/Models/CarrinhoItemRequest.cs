namespace TicketPrime.Api.Models;

public class CarrinhoItemRequest
{
    public int EventoId { get; set; }
    public int Quantidade { get; set; }
    public int? TipoIngressoId { get; set; }
    public decimal PrecoUnitario { get; set; }
}
