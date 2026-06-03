using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

public interface IEventoRepository
{
    Task<Evento?> ObterPorIdAsync(int id, IDbTransaction? transaction = null);
    Task<IEnumerable<Evento>> ObterTodosAsync(IDbTransaction? transaction = null);
    Task<int> InserirAsync(Evento evento, IDbTransaction? transaction = null);
}
