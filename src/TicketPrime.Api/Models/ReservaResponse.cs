namespace TicketPrime.Api.Models;

public class ReservaResponse
{
    public int Id { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public string? CupomUtilizado { get; set; }
    public decimal ValorFinalPago { get; set; }
}
