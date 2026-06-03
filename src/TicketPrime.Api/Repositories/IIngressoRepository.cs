using System.Data;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Interface mínima para consultas de Ingressos.
/// Criada na Etapa 7 apenas para operações de Lotes/TiposIngresso.
/// </summary>
public interface IIngressoRepository
{
    /// <summary>
    /// Retorna a quantidade de ingressos vendidos (status Confirmada ou Utilizada)
    /// para um determinado tipo-ingresso/lote.
    /// </summary>
    Task<int> ContarPorTipoAsync(int tipoIngressoId,
        IDbTransaction? transaction = null);
}
