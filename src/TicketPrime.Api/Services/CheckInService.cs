using System.Linq;
using TicketPrime.Api.Models;
using TicketPrime.Api.Repositories;

namespace TicketPrime.Api.Services;

/// <summary>
/// Service responsável por orquestrar validação, regras de negócio e
/// chamadas aos repositórios para o domínio CheckIn.
/// Criado na Etapa 9.
/// Depende apenas de ICheckInRepository, IIngressoRepository e IEventoRepository.
/// </summary>
public class CheckInService
{
    private readonly ICheckInRepository _checkInRepository;
    private readonly IIngressoRepository _ingressoRepository;
    private readonly IEventoRepository _eventoRepository;

    public CheckInService(
        ICheckInRepository checkInRepository,
        IIngressoRepository ingressoRepository,
        IEventoRepository eventoRepository)
    {
        _checkInRepository = checkInRepository;
        _ingressoRepository = ingressoRepository;
        _eventoRepository = eventoRepository;
    }

    /// <summary>
    /// POST /api/ingressos/{codigo}/checkin
    /// Realiza check-in de um ingresso pelo código único (route parameter).
    /// </summary>
    public async Task<(CheckInResponse? Response, string? Erro, int StatusCode)> RealizarCheckInPorCodigoAsync(string codigo)
    {
        if (codigo.Length != 8)
            return (null, "Código deve ter 8 caracteres.", 400);

        var ingresso = await _ingressoRepository.ObterPorCodigoAsync(codigo);

        if (ingresso is null)
            return (null, "Ingresso não encontrado.", 404);

        var result = await ExecutarCheckInAsync(ingresso);

        return result.Type switch
        {
            CheckInResultType.StatusInvalido => (null,
                $"Ingresso não está confirmado para check-in. Status atual: {ingresso.Status}", 409),
            CheckInResultType.CheckInDuplicado => (null,
                "Check-in já realizado para este ingresso.", 409),
            CheckInResultType.Sucesso => (
                new CheckInResponse
                {
                    Id = result.CheckInId,
                    IngressoId = ingresso.Id,
                    CodigoUnico = codigo,
                    DataCheckIn = result.DataCheckIn,
                    Mensagem = "Check-in realizado com sucesso. Bem-vindo ao evento!"
                },
                null, 201),
            _ => (null, "Erro inesperado ao realizar check-in.", 500)
        };
    }

    /// <summary>
    /// POST /api/checkin
    /// Realiza check-in de um ingresso pelo código informado no corpo da requisição.
    /// </summary>
    public async Task<(CheckInResponse? Response, string? Erro, int StatusCode)> RealizarCheckInPorRequestAsync(CheckInRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CodigoIngresso))
            return (null, "Código do ingresso é obrigatório.", 400);

        var ingresso = await _ingressoRepository.ObterPorCodigoAsync(request.CodigoIngresso);

        if (ingresso is null)
            return (null, "Ingresso não encontrado.", 404);

        var result = await ExecutarCheckInAsync(ingresso);

        return result.Type switch
        {
            CheckInResultType.StatusInvalido => (null,
                $"Ingresso já utilizado. Status atual: {ingresso.Status}", 400),
            CheckInResultType.CheckInDuplicado => (null,
                "Check-in já realizado para este ingresso.", 400),
            CheckInResultType.Sucesso => (
                new CheckInResponse
                {
                    Id = result.CheckInId,
                    IngressoId = ingresso.Id,
                    CodigoUnico = request.CodigoIngresso,
                    DataCheckIn = result.DataCheckIn,
                    Mensagem = "Check-in realizado com sucesso. Bem-vindo ao evento!"
                },
                null, 201),
            _ => (null, "Erro inesperado ao realizar check-in.", 500)
        };
    }

    /// <summary>
    /// GET /api/eventos/{eventoId}/checkins
    /// Lista todos os check-ins de um evento.
    /// </summary>
    public async Task<(CheckInListResponse? Response, string? Erro, int StatusCode)> ListarCheckInsAsync(int eventoId)
    {
        var evento = await _eventoRepository.ObterPorIdAsync(eventoId);

        if (evento is null)
            return (null, "Evento não encontrado.", 404);

        var checkins = await _checkInRepository.ListarPorEventoIdAsync(eventoId);
        var checkinsList = checkins.ToList();

        var response = new CheckInListResponse
        {
            EventoId = eventoId,
            NomeEvento = evento.Nome,
            TotalCheckIns = checkinsList.Count,
            CheckIns = checkinsList
        };

        return (response, null, 200);
    }

    /// <summary>
    /// GET /api/eventos/{eventoId}/checkins/stats
    /// Retorna estatísticas de check-in de um evento.
    /// </summary>
    public async Task<(CheckInStatsResponse? Response, string? Erro, int StatusCode)> ObterStatsAsync(int eventoId)
    {
        var evento = await _eventoRepository.ObterPorIdAsync(eventoId);

        if (evento is null)
            return (null, "Evento não encontrado.", 404);

        var stats = await _checkInRepository.ObterStatsPorEventoIdAsync(eventoId);

        var percentual = stats.TotalIngressosVendidos > 0
            ? Math.Round((decimal)stats.TotalCheckIns / stats.TotalIngressosVendidos * 100, 2)
            : 0;

        var response = new CheckInStatsResponse
        {
            EventoId = eventoId,
            NomeEvento = evento.Nome,
            TotalIngressosVendidos = stats.TotalIngressosVendidos,
            TotalCheckIns = stats.TotalCheckIns,
            Pendentes = stats.Pendentes,
            PercentualPresenca = percentual
        };

        return (response, null, 200);
    }

    /// <summary>
    /// Método privado compartilhado entre RealizarCheckInPorCodigoAsync e
    /// RealizarCheckInPorRequestAsync.
    /// Recebe um ingresso já validado (existente) e executa o fluxo comum:
    /// validar status → bloquear duplicidade → inserir check-in → atualizar status.
    /// </summary>
    private async Task<CheckInResult> ExecutarCheckInAsync(Ingresso ingresso)
    {
        // Validar se o ingresso está Confirmada
        if (ingresso.Status != "Confirmada")
            return new CheckInResult(CheckInResultType.StatusInvalido);

        // Bloquear check-in duplicado
        var checkinExistente = await _checkInRepository.ExistePorIngressoIdAsync(ingresso.Id);
        if (checkinExistente)
            return new CheckInResult(CheckInResultType.CheckInDuplicado);

        // Registrar check-in
        var (checkinId, dataCheckIn) = await _checkInRepository.InserirAsync(ingresso.Id);

        // Atualizar status do ingresso para Utilizada
        await _ingressoRepository.AtualizarStatusAsync(ingresso.Id, "Utilizada");

        return new CheckInResult(CheckInResultType.Sucesso, checkinId, dataCheckIn);
    }
}

internal enum CheckInResultType
{
    Sucesso,
    StatusInvalido,
    CheckInDuplicado
}

internal record CheckInResult(
    CheckInResultType Type,
    int CheckInId = 0,
    DateTime DataCheckIn = default);
