using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Repositório para Reservas.
/// Expandido na Etapa 10b com métodos CRUD completos seguindo a convenção C6.
/// </summary>
public class ReservaRepository : IReservaRepository
{
    private readonly IDbConnection _db;

    public ReservaRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<Reserva?> ObterPorIdAsync(int id, IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT Id, UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago
            FROM Reservas
            WHERE Id = @Id";

        return await _db.QuerySingleOrDefaultAsync<Reserva>(sql,
            new { Id = id },
            transaction: transaction);
    }

    public async Task<int> InserirAsync(Reserva reserva, IDbTransaction? transaction = null)
    {
        var sql = @"INSERT INTO Reservas (UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago)
                    OUTPUT INSERTED.Id
                    VALUES (@UsuarioCpf, @EventoId, @CupomUtilizado, @ValorFinalPago)";

        return await _db.QuerySingleAsync<int>(sql, new
        {
            reserva.UsuarioCpf,
            reserva.EventoId,
            reserva.CupomUtilizado,
            reserva.ValorFinalPago
        }, transaction: transaction);
    }

    public async Task<int> ContarPorCpfEEventoAsync(string cpf, int eventoId, IDbTransaction? transaction = null)
    {
        var sql = "SELECT COUNT(1) FROM Reservas WHERE UsuarioCpf = @Cpf AND EventoId = @EventoId";

        return await _db.ExecuteScalarAsync<int>(sql,
            new { Cpf = cpf, EventoId = eventoId },
            transaction: transaction);
    }

    public async Task<int> ContarPorEventoAsync(int eventoId, IDbTransaction? transaction = null)
    {
        var sql = "SELECT COUNT(1) FROM Reservas WHERE EventoId = @EventoId";

        return await _db.ExecuteScalarAsync<int>(sql,
            new { EventoId = eventoId },
            transaction: transaction);
    }

    public async Task<IEnumerable<Reserva>> ObterPorCpfAsync(string cpf, IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT Id, UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago
            FROM Reservas
            WHERE UsuarioCpf = @Cpf";

        return await _db.QueryAsync<Reserva>(sql,
            new { Cpf = cpf },
            transaction: transaction);
    }

    public async Task<IEnumerable<Reserva>> ObterPorEventoIdAsync(int eventoId, IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT Id, UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago
            FROM Reservas
            WHERE EventoId = @EventoId";

        return await _db.QueryAsync<Reserva>(sql,
            new { EventoId = eventoId },
            transaction: transaction);
    }
}
