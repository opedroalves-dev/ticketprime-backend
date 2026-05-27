namespace TicketPrime.Api.Models;

public class CarrinhoConfirmacaoResponse
{
    public string Mensagem { get; set; } = string.Empty;
    public int CarrinhoId { get; set; }
    public List<ReservaConfirmadaResponse> ReservasCriadas { get; set; } = new();
    public decimal TotalPago { get; set; }
}

public class ReservaConfirmadaResponse
{
    public int ReservaId { get; set; }
    public int IngressoId { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public string TipoIngresso { get; set; } = string.Empty;
    public decimal ValorFinal { get; set; }
    public string Status { get; set; } = string.Empty;
}
