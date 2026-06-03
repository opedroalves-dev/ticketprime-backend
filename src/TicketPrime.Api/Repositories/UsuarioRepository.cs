using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly IDbConnection _db;

    public UsuarioRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<Usuario?> ObterPorCpfAsync(string cpf, IDbTransaction? transaction = null)
    {
        return await _db.QuerySingleOrDefaultAsync<Usuario>(
            "SELECT Cpf, Nome, Email FROM Usuarios WHERE Cpf = @Cpf",
            new { Cpf = cpf },
            transaction: transaction);
    }

    public async Task<bool> ExisteAsync(string cpf, IDbTransaction? transaction = null)
    {
        var count = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Usuarios WHERE Cpf = @Cpf",
            new { Cpf = cpf },
            transaction: transaction);
        return count > 0;
    }

    public async Task InserirAsync(Usuario usuario, IDbTransaction? transaction = null)
    {
        await _db.ExecuteAsync(
            "INSERT INTO Usuarios (Cpf, Nome, Email) VALUES (@Cpf, @Nome, @Email)",
            new { usuario.Cpf, usuario.Nome, usuario.Email },
            transaction: transaction);
    }
}
