namespace TicketPrime.Api.Models;

public class CarrinhoRequest
{
    public string UsuarioCpf { get; set; } = string.Empty;
    public List<CarrinhoItemRequest> Itens { get; set; } = new();
}

public class CarrinhoItemRequest
{
    public int EventoId { get; set; }
    public int? TipoIngressoId { get; set; }
    public int Quantidade { get; set; } = 1;
}
