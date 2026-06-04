using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Interface mínima para consultas de Reservas.
/// Criada na Etapa 8 para atender o domínio de Ingressos.
/// Será expandida na Etapa 10b com os demais métodos CRUD.
/// </summary>
public interface IReservaRepository
{
    /// <summary>
    /// Retorna uma reserva pelo ID.
    /// </summary>
    Task<Reserva?> ObterPorIdAsync(int id, IDbTransaction? transaction = null);
}
