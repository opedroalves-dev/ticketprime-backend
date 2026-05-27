namespace TicketPrime.Api.Models;

public class CheckInResponse
{
    public int Id { get; set; }
    public int IngressoId { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public DateTime DataCheckIn { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}
