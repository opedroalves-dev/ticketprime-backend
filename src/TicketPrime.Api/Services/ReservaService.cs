using TicketPrime.Api.Models;

namespace TicketPrime.Api.Services;

public class ReservaService
{
    /// <summary>
    /// Valida e calcula uma reserva com base nas regras de negócio.
    /// Não utiliza Entity Framework, banco em memória ou TestContainers.
    /// </summary>
    public ResultadoReserva ValidarReserva(
        string usuarioCpf,
        int eventoId,
        string? codigoCupom,
        List<Usuario> usuarios,
        List<Evento> eventos,
        List<Cupom> cupons,
        List<Reserva> reservasExistentes)
    {
        return RegrasReserva.ValidarReserva(usuarioCpf, eventoId, codigoCupom, usuarios, eventos, cupons, reservasExistentes);
    }

    /// <summary>
    /// Calcula o valor final aplicando cupom de desconto apenas quando
    /// o PrecoPadrao do evento for maior ou igual ao ValorMinimoRegra do cupom.
    /// </summary>
    public decimal CalcularValorFinal(
        decimal precoPadrao,
        string? codigoCupom,
        List<Cupom> cupons)
    {
        return RegrasReserva.CalcularValorFinal(precoPadrao, codigoCupom, cupons);
    }

    /// <summary>
    /// Verifica se um cupom pode ser aplicado com base na regra de valor mínimo.
    /// </summary>
    public bool CupomPodeSerAplicado(decimal precoPadrao, Cupom cupom)
    {
        return RegrasReserva.CupomPodeSerAplicado(precoPadrao, cupom);
    }

    /// <summary>
    /// Constrói um ReservaResponse com o NomeEvento preenchido a partir da lista de eventos.
    /// Simula o que seria feito no endpoint GET /api/reservas/{cpf}.
    /// </summary>
    public ReservaResponse? ConstruirReservaResponse(
        Reserva reserva,
        List<Evento> eventos)
    {
        return RegrasReserva.ConstruirReservaResponse(reserva, eventos);
    }
}
