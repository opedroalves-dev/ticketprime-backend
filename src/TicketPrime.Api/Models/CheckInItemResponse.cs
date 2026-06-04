namespace TicketPrime.Api.Models;

public class CheckInItemResponse
{
    public int Id { get; set; }
    public int IngressoId { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public string NomeUsuario { get; set; } = string.Empty;
    public string UsuarioCpf { get; set; } = string.Empty;
    public string? TipoIngresso { get; set; }
    public DateTime DataCheckIn { get; set; }
}
