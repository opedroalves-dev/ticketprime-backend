namespace TicketPrime.Api.Models;

public class CheckIn
{
    public int Id { get; set; }
    public int IngressoId { get; set; }
    public DateTime DataCheckIn { get; set; }
}

public class CheckInRequest
{
    public string CodigoIngresso { get; set; } = string.Empty;
}
