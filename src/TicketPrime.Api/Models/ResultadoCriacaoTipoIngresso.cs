namespace TicketPrime.Api.Models;

public class ResultadoCriacaoTipoIngresso
{
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
    public int? StatusCode { get; set; }
    public int Id { get; set; }
    public TipoIngressoResponse? TipoIngresso { get; set; }
}
