namespace TicketPrime.Api.Models;

public class LoteHistoricoPrecosResponse
{
    public int LoteId { get; set; }
    public string NomeLote { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public List<HistoricoPrecoResponse> Historico { get; set; } = new();
}
