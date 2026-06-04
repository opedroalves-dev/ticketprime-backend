namespace TicketPrime.Api.Models;

public class ResultadoCriacaoEvento
{
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
    public int Id { get; set; }
    public Evento? Evento { get; set; }
}
