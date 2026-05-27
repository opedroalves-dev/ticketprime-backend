namespace TicketPrime.Api.Models;

public class Carrinho
{
    public int Id { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataExpiracao { get; set; }
}
