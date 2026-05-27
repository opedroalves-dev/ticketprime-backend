namespace TicketPrime.Api.Models;

public class IngressoPorReservaResponse
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public string CodigoIngresso { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ValorFinal { get; set; }
    public DateTime DataCriacao { get; set; }

    // Dados do evento
    public string NomeEvento { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }

    // Dados do usuário
    public string UsuarioCpf { get; set; } = string.Empty;
}
