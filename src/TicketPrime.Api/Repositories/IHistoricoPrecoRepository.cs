using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

public interface IHistoricoPrecoRepository
{
    /// <summary>
    /// Registra o preço inicial de um evento no histórico (RF05).
    /// </summary>
    Task InserirPrecoInicialAsync(int eventoId, decimal precoNovo,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna o histórico completo de preços de um evento,
    /// incluindo nome do lote via LEFT JOIN (apenas para exibição).
    /// A consulta principal é na tabela HistoricoPrecos.
    /// </summary>
    Task<IEnumerable<HistoricoPrecoResponse>> ObterPorEventoIdAsync(int eventoId,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna o histórico de preços de um lote/tipo-ingresso específico.
    /// Consulta APENAS a tabela HistoricoPrecos.
    /// </summary>
    Task<IEnumerable<HistoricoPrecoResponse>> ObterPorLoteIdAsync(int loteId,
        IDbTransaction? transaction = null);
}
