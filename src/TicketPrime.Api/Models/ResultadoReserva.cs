namespace TicketPrime.Api.Models;

public class ResultadoReserva
{
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
    public decimal ValorFinalPago { get; set; }
    public string? CupomAplicado { get; set; }

    public static ResultadoReserva Ok(decimal valorFinal, string? cupomAplicado = null)
        => new() { Sucesso = true, ValorFinalPago = valorFinal, CupomAplicado = cupomAplicado };

    public static ResultadoReserva Fail(string erro)
        => new() { Sucesso = false, Erro = erro };
}
