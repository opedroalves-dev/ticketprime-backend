using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Repositório mínimo para Reservas.
/// Criado na Etapa 8 para atender o domínio de Ingressos.
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
}
