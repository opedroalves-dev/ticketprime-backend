using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorCpfAsync(string cpf, IDbTransaction? transaction = null);
    Task<bool> ExisteAsync(string cpf, IDbTransaction? transaction = null);
    Task InserirAsync(Usuario usuario, IDbTransaction? transaction = null);
}
