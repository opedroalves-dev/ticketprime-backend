namespace TicketPrime.Api.Models;

/// <summary>
/// Resposta da simulação de preço com transparência total dos valores.
/// Endpoint: POST /api/reservas/simular-preco
/// </summary>
public class SimulacaoPrecoResponse
{
    /// <summary>
    /// Preço base do ingresso (PrecoPadrao do Evento).
    /// </summary>
    public decimal PrecoBase { get; set; }

    /// <summary>
    /// Taxa de serviço calculada como 10% do PrecoBase.
    /// Fórmula: PrecoBase × 0,10
    /// </summary>
    public decimal TaxaServico { get; set; }

    /// <summary>
    /// Valor do desconto aplicado pelo cupom.
    /// Zero se nenhum cupom for informado ou se PrecoBase < ValorMinimoRegra do cupom.
    /// Fórmula: PrecoBase × (PorcentagemDesconto / 100)
    /// </summary>
    public decimal ValorDesconto { get; set; }

    /// <summary>
    /// Valor final a ser pago, já incluindo taxa de serviço e descontos.
    /// Fórmula: PrecoBase + TaxaServico - ValorDesconto
    /// </summary>
    public decimal ValorFinal { get; set; }
}
