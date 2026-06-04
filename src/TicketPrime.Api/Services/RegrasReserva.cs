using TicketPrime.Api.Models;

namespace TicketPrime.Api.Services;

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

public static class RegrasReserva
{
    /// <summary>
    /// Valida e calcula uma reserva com base nas regras de negócio.
    /// Não utiliza Entity Framework, banco em memória ou TestContainers.
    /// </summary>
    public static ResultadoReserva ValidarReserva(
        string usuarioCpf,
        int eventoId,
        string? codigoCupom,
        List<Usuario> usuarios,
        List<Evento> eventos,
        List<Cupom> cupons,
        List<Reserva> reservasExistentes)
    {
        // 1. Verificar se o CPF do usuário existe
        var usuario = usuarios.FirstOrDefault(u => u.Cpf == usuarioCpf);
        if (usuario is null)
            return ResultadoReserva.Fail("CPF do usuário não encontrado. Realize o cadastro antes de reservar.");

        // 2. Verificar se o evento existe
        var evento = eventos.FirstOrDefault(e => e.Id == eventoId);
        if (evento is null)
            return ResultadoReserva.Fail("Evento não encontrado.");

        // 3. Verificar limite de reservas por CPF no mesmo evento (máximo 2)
        var reservasCpfEvento = reservasExistentes
            .Count(r => r.UsuarioCpf == usuarioCpf && r.EventoId == eventoId);

        if (reservasCpfEvento >= 2)
            return ResultadoReserva.Fail("Limite de reservas atingido para este CPF no evento (máximo 2).");

        // 4. Verificar capacidade do evento (lotado?)
        var reservasEvento = reservasExistentes.Count(r => r.EventoId == eventoId);
        if (reservasEvento >= evento.CapacidadeTotal)
            return ResultadoReserva.Fail("Evento lotado. Não há ingressos disponíveis.");

        // 5. Validar se o cupom informado existe na base
        if (!string.IsNullOrWhiteSpace(codigoCupom))
        {
            var cupom = cupons.FirstOrDefault(c =>
                c.Codigo.Equals(codigoCupom, StringComparison.OrdinalIgnoreCase));

            if (cupom is null)
                return ResultadoReserva.Fail("Cupom não encontrado.");
        }

        // 6. Calcular valor final
        decimal valorFinal = CalcularValorFinal(evento.PrecoPadrao, codigoCupom, cupons);

        return ResultadoReserva.Ok(valorFinal, codigoCupom);
    }

    /// <summary>
    /// Calcula o valor final aplicando cupom de desconto apenas quando
    /// o PrecoPadrao do evento for maior ou igual ao ValorMinimoRegra do cupom.
    /// </summary>
    public static decimal CalcularValorFinal(
        decimal precoPadrao,
        string? codigoCupom,
        List<Cupom> cupons)
    {
        if (string.IsNullOrWhiteSpace(codigoCupom))
            return precoPadrao;

        var cupom = cupons.FirstOrDefault(c =>
            c.Codigo.Equals(codigoCupom, StringComparison.OrdinalIgnoreCase));

        if (cupom is null)
            return precoPadrao;

        // Cupom só aplica desconto se PrecoPadrao >= ValorMinimoRegra
        if (precoPadrao < cupom.ValorMinimoRegra)
            return precoPadrao;

        // ValorFinalPago = PrecoPadrao × (1 - PorcentagemDesconto / 100)
        decimal desconto = precoPadrao * (cupom.PorcentagemDesconto / 100m);
        return precoPadrao - desconto;
    }

    /// <summary>
    /// Verifica se um cupom pode ser aplicado com base na regra de valor mínimo.
    /// </summary>
    public static bool CupomPodeSerAplicado(decimal precoPadrao, Cupom cupom)
    {
        return precoPadrao >= cupom.ValorMinimoRegra;
    }

    /// <summary>
    /// Constrói um ReservaResponse com o NomeEvento preenchido a partir da lista de eventos.
    /// Simula o que seria feito no endpoint GET /api/reservas/{cpf}.
    /// </summary>
    public static ReservaResponse? ConstruirReservaResponse(
        Reserva reserva,
        List<Evento> eventos)
    {
        var evento = eventos.FirstOrDefault(e => e.Id == reserva.EventoId);
        if (evento is null)
            return null;

        return new ReservaResponse
        {
            Id = reserva.Id,
            UsuarioCpf = reserva.UsuarioCpf,
            EventoId = reserva.EventoId,
            NomeEvento = evento.Nome,
            CupomUtilizado = reserva.CupomUtilizado,
            ValorFinalPago = reserva.ValorFinalPago
        };
    }
}
