namespace TicketPrime.Api.Models;

public class AdicionarItensRequest
{
    public List<CarrinhoItemRequest> Itens { get; set; } = new();
}
