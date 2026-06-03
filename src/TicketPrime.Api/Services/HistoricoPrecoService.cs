using Dapper;
using TicketPrime.Api.Models;
using TicketPrime.Api.Repositories;

namespace TicketPrime.Api.Services;

public class HistoricoPrecoService
{
    private readonly IHistoricoPrecoRepository _historicoPrecoRepository;
    private readonly IEventoRepository _eventoRepository;
    private readonly ITipoIngressoRepository _tipoIngressoRepository;

    public HistoricoPrecoService(
        IHistoricoPrecoRepository historicoPrecoRepository,
        IEventoRepository eventoRepository,
        ITipoIngressoRepository tipoIngressoRepository)
    {
        _historicoPrecoRepository = historicoPrecoRepository;
        _eventoRepository = eventoRepository;
        _tipoIngressoRepository = tipoIngressoRepository;
    }

    /// <summary>
    /// Retorna o histórico de preços de um evento.
    /// Valida existência do evento via IEventoRepository.
    /// Retorna null se o evento não existir.
    /// </summary>
    public async Task<EventoHistoricoPrecosResponse?> ObterPorEventoIdAsync(int eventoId)
    {
        // 1. Validar existência do evento via EventoRepository (SRP)
        var evento = await _eventoRepository.ObterPorIdAsync(eventoId);
        if (evento is null)
            return null;

        // 2. Buscar histórico via HistoricoPrecoRepository (SRP)
        var historico = await _historicoPrecoRepository.ObterPorEventoIdAsync(eventoId);

        // 3. Montar response
        return new EventoHistoricoPrecosResponse
        {
            EventoId = eventoId,
            NomeEvento = evento.Nome,
            Historico = historico.AsList()
        };
    }

    /// <summary>
    /// Retorna o histórico de preços de um lote/tipo-ingresso.
    /// Valida existência do lote via ITipoIngressoRepository.
    /// Busca nome do evento via IEventoRepository.
    /// Retorna null se o lote não existir.
    /// </summary>
    public async Task<LoteHistoricoPrecosResponse?> ObterPorLoteIdAsync(int loteId)
    {
        // 1. Validar existência do lote via TipoIngressoRepository (SRP)
        var lote = await _tipoIngressoRepository.ObterPorIdAsync(loteId);
        if (lote is null)
            return null;

        // 2. Buscar nome do evento via EventoRepository (SRP)
        var evento = await _eventoRepository.ObterPorIdAsync(lote.EventoId);

        // 3. Buscar histórico via HistoricoPrecoRepository (SRP)
        var historico = await _historicoPrecoRepository.ObterPorLoteIdAsync(loteId);

        // 4. Montar response
        return new LoteHistoricoPrecosResponse
        {
            LoteId = loteId,
            NomeLote = lote.Nome,
            EventoId = lote.EventoId,
            NomeEvento = evento?.Nome ?? "",
            Historico = historico.AsList()
        };
    }
}
