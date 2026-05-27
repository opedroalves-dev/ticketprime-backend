namespace TicketPrime.Api.Models;

public class CarrinhoItem
{
    public int Id { get; set; }
    public int CarrinhoId { get; set; }
    public int EventoId { get; set; }
    public int? TipoIngressoId { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}
