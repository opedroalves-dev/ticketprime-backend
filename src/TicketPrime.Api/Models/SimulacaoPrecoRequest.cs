namespace TicketPrime.Api.Models;

/// <summary>
/// Request para simulação de preço de reserva/ingresso.
/// Endpoint: POST /api/reservas/simular-preco
/// </summary>
public class SimulacaoPrecoRequest
{
    /// <summary>CPF do usuário (11 dígitos numéricos, sem máscara).</summary>
    public string UsuarioCpf { get; set; } = string.Empty;

    /// <summary>Id do evento para o qual deseja simular o preço.</summary>
    public int EventoId { get; set; }

    /// <summary>Código do cupom de desconto (opcional).</summary>
    public string? CupomUtilizado { get; set; }
}
