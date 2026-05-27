namespace TicketPrime.Api.Models;

public class EventoResumoResponse
{
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
    public int CapacidadeTotal { get; set; }
    public int TotalReservas { get; set; }
    public int IngressosDisponiveis { get; set; }
    public decimal ReceitaTotal { get; set; }
    public int TotalCheckIns { get; set; }
}
