using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

public interface ICupomRepository
{
    Task<Cupom?> ObterPorCodigoAsync(string codigo, IDbTransaction? transaction = null);
    Task<bool> ExisteAsync(string codigo, IDbTransaction? transaction = null);
    Task InserirAsync(Cupom cupom, IDbTransaction? transaction = null);
}
