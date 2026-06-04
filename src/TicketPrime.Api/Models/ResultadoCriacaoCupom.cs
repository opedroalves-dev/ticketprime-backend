namespace TicketPrime.Api.Models;

public class ResultadoCriacaoCupom
{
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
    public string? Codigo { get; set; }
    public Cupom? Cupom { get; set; }
}
