namespace TicketPrime.Api.Models;

public class EventoHistoricoPrecosResponse
{
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public List<HistoricoPrecoResponse> Historico { get; set; } = new();
}
