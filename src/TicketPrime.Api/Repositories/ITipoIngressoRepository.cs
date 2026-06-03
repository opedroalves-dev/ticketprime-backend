using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Interface para operações de TiposIngresso/Lotes.
/// Complementada na Etapa 7 com métodos CRUD completos.
/// </summary>
public interface ITipoIngressoRepository
{
    /// <summary>
    /// Retorna dados do lote (Id, Nome, EventoId, Preco, Capacidade, ...).
    /// Retorna null se não encontrado.
    /// </summary>
    Task<TipoIngresso?> ObterPorIdAsync(int id,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna todos os tipos-ingresso/lotes de um evento.
    /// </summary>
    Task<IEnumerable<TipoIngresso>> ObterPorEventoIdAsync(int eventoId,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Insere um novo tipo-ingresso/lote. Retorna o Id gerado.
    /// </summary>
    Task<int> InserirAsync(TipoIngresso tipoIngresso,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Atualiza os dados de um tipo-ingresso/lote.
    /// Retorna true se alguma linha foi afetada.
    /// </summary>
    Task<bool> AtualizarAsync(TipoIngresso tipoIngresso,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Remove um tipo-ingresso/lote pelo Id.
    /// Retorna true se alguma linha foi afetada.
    /// </summary>
    Task<bool> RemoverAsync(int id,
        IDbTransaction? transaction = null);
}
