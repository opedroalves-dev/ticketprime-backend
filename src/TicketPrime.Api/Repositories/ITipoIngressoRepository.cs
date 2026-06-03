using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Interface mínima para consulta de TiposIngresso/Lotes.
/// Criada na Etapa 6 apenas para validação de existência.
/// Será complementada na Etapa 7 (Migrar Domínio Lotes/TiposIngresso).
/// </summary>
public interface ITipoIngressoRepository
{
    /// <summary>
    /// Retorna dados básicos do lote para validação de existência.
    /// Retorna null se não encontrado.
    /// </summary>
    Task<TipoIngresso?> ObterPorIdAsync(int id,
        IDbTransaction? transaction = null);
}
