using TicketPrime.Api.Models;
using TicketPrime.Api.Repositories;

namespace TicketPrime.Api.Services;

public class ReservaService
{
    private readonly IReservaRepository _reservaRepository;
    private readonly IEventoRepository _eventoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ICupomRepository _cupomRepository;

    public ReservaService(
        IReservaRepository reservaRepository,
        IEventoRepository eventoRepository,
        IUsuarioRepository usuarioRepository,
        ICupomRepository cupomRepository)
    {
        _reservaRepository = reservaRepository;
        _eventoRepository = eventoRepository;
        _usuarioRepository = usuarioRepository;
        _cupomRepository = cupomRepository;
    }

    /// <summary>
    /// Cria uma reserva com validações manuais e mensagens originais.
    /// Usa RegrasReserva apenas para CalcularValorFinal e ConstruirReservaResponse.
    /// </summary>
    public async Task<(ReservaResponse? Reserva, string? Erro)> CriarReservaAsync(ReservaRequest request)
    {
        // 1. Validar formato do CPF
        if (string.IsNullOrWhiteSpace(request.UsuarioCpf))
            return (null, "CPF do usuário é obrigatório.");

        if (request.UsuarioCpf.Length != 11 || !request.UsuarioCpf.All(char.IsDigit))
            return (null, "CPF deve conter 11 dígitos numéricos.");

        // 2. Validar EventoId
        if (request.EventoId <= 0)
            return (null, "EventoId deve ser maior que zero.");

        // 3. Validar cupom inexistente
        if (!string.IsNullOrWhiteSpace(request.CupomUtilizado))
        {
            var cupomExistente = await _cupomRepository.ObterPorCodigoAsync(request.CupomUtilizado);
            if (cupomExistente is null)
                return (null, "Cupom não encontrado.");
        }

        // 4. Buscar usuário
        var usuario = await _usuarioRepository.ObterPorCpfAsync(request.UsuarioCpf);
        if (usuario is null)
            return (null, "Usuário não encontrado para o CPF informado.");

        // 5. Buscar evento
        var evento = await _eventoRepository.ObterPorIdAsync(request.EventoId);
        if (evento is null)
            return (null, "Evento não encontrado para o Id informado.");

        // 6. Verificar limite de 2 reservas por CPF no mesmo evento
        var reservasExistentes = (await _reservaRepository.ObterPorEventoIdAsync(request.EventoId)).ToList();
        var reservasCpfEvento = reservasExistentes.Count(r => r.UsuarioCpf == request.UsuarioCpf);
        if (reservasCpfEvento >= 2)
            return (null, "CPF já possui o limite máximo de 2 reservas para este evento.");

        // 7. Verificar capacidade do evento
        if (reservasExistentes.Count >= evento.CapacidadeTotal)
            return (null, "Evento lotado. Não há vagas disponíveis.");

        // 8. Processar cupom (se informado)
        var cupons = new List<Cupom>();
        if (!string.IsNullOrWhiteSpace(request.CupomUtilizado))
        {
            var cupom = await _cupomRepository.ObterPorCodigoAsync(request.CupomUtilizado);
            if (cupom is not null)
                cupons.Add(cupom);
        }

        // 6. Calcular valor final usando RegrasReserva (apenas cálculo, sem validação)
        decimal valorFinal = RegrasReserva.CalcularValorFinal(evento.PrecoPadrao, request.CupomUtilizado, cupons);

        // 7. Persistir reserva
        var reserva = new Reserva
        {
            UsuarioCpf = request.UsuarioCpf,
            EventoId = request.EventoId,
            CupomUtilizado = string.IsNullOrWhiteSpace(request.CupomUtilizado) ? null : request.CupomUtilizado,
            ValorFinalPago = valorFinal
        };
        var reservaId = await _reservaRepository.InserirAsync(reserva);

        // 8. Construir response usando RegrasReserva
        reserva.Id = reservaId;
        var eventos = new List<Evento> { evento };
        var response = RegrasReserva.ConstruirReservaResponse(reserva, eventos);

        return (response, null);
    }

    /// <summary>
    /// Simula o cálculo de preço sem criar reserva.
    /// Não usa IncrementoService para evitar dependência morta.
    /// Replica a lógica inline original do endpoint POST /api/reservas/simular-preco.
    /// Retorna (Simulacao, Erro, StatusCode) para o endpoint mapear 404 vs 400.
    /// </summary>
    public async Task<(SimulacaoPrecoResponse? Simulacao, string? Erro, int StatusCode)> SimularPrecoAsync(SimulacaoPrecoRequest request)
    {
        // 1. Validar formato do CPF
        if (string.IsNullOrWhiteSpace(request.UsuarioCpf))
            return (null, "CPF do usuário é obrigatório.", 400);

        if (request.UsuarioCpf.Length != 11 || !request.UsuarioCpf.All(char.IsDigit))
            return (null, "CPF deve conter 11 dígitos numéricos.", 400);

        // 2. Validar EventoId
        if (request.EventoId <= 0)
            return (null, "EventoId deve ser maior que zero.", 400);

        // 3. Buscar evento
        var evento = await _eventoRepository.ObterPorIdAsync(request.EventoId);
        if (evento is null)
            return (null, "Evento não encontrado para o Id informado.", 404);

        // 4. Calcular PrecoBase
        decimal precoBase = evento.PrecoPadrao;

        // 5. Calcular TaxaServico (10%)
        decimal taxaServico = Math.Round(precoBase * 0.10m, 2);

        // 6. Calcular ValorDesconto
        decimal valorDesconto = 0m;
        if (!string.IsNullOrWhiteSpace(request.CupomUtilizado))
        {
            var cupom = await _cupomRepository.ObterPorCodigoAsync(request.CupomUtilizado);
            if (cupom is not null && precoBase >= cupom.ValorMinimoRegra)
            {
                valorDesconto = Math.Round(precoBase * cupom.PorcentagemDesconto / 100m, 2);
            }
        }

        // 7. Calcular ValorFinal
        decimal valorFinal = precoBase + taxaServico - valorDesconto;

        var response = new SimulacaoPrecoResponse
        {
            PrecoBase = precoBase,
            TaxaServico = taxaServico,
            ValorDesconto = valorDesconto,
            ValorFinal = valorFinal
        };

        return (response, null, 200);
    }

    /// <summary>
    /// Obtém todas as reservas de um CPF, com NomeEvento preenchido via RegrasReserva.
    /// </summary>
    public async Task<(List<ReservaResponse>? Reservas, string? Erro)> ObterReservasPorCpfAsync(string cpf)
    {
        // 1. Verificar se o CPF existe
        var existe = await _usuarioRepository.ExisteAsync(cpf);
        if (!existe)
            return (null, "CPF não encontrado.");

        // 2. Buscar reservas do CPF
        var reservas = (await _reservaRepository.ObterPorCpfAsync(cpf)).ToList();
        if (reservas.Count == 0)
            return (new List<ReservaResponse>(), null);

        // 3. Coletar EventoIds distintos
        var eventoIds = reservas.Select(r => r.EventoId).Distinct().ToList();

        // 4. Buscar eventos para preencher NomeEvento
        var eventos = (await _eventoRepository.ObterTodosAsync()).ToList();

        // 5. Construir responses usando RegrasReserva
        var responses = new List<ReservaResponse>();
        foreach (var reserva in reservas)
        {
            var response = RegrasReserva.ConstruirReservaResponse(reserva, eventos);
            if (response is not null)
                responses.Add(response);
        }

        return (responses, null);
    }
}
