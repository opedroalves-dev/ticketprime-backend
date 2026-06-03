namespace TicketPrime.Api.Models;

public class ReservaRequest
{
    public string UsuarioCpf { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public string? CupomUtilizado { get; set; }
}
