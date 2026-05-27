namespace TicketPrime.Api.Models;

public class CarrinhoResponse
{
    public int CarrinhoId { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataExpiracao { get; set; }
    public int MinutosRestantes { get; set; }
    public List<CarrinhoItemResponse> Itens { get; set; } = new();
    public decimal Total { get; set; }
    public string? Mensagem { get; set; }
}

public class CarrinhoItemResponse
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public int? TipoIngressoId { get; set; }
    public string? NomeLote { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Subtotal { get; set; }
}
