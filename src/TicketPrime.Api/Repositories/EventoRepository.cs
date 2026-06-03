using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

public class EventoRepository : IEventoRepository
{
    private readonly IDbConnection _db;

    public EventoRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<Evento?> ObterPorIdAsync(int id, IDbTransaction? transaction = null)
    {
        return await _db.QuerySingleOrDefaultAsync<Evento>(
            "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos WHERE Id = @Id",
            new { Id = id },
            transaction: transaction);
    }

    public async Task<IEnumerable<Evento>> ObterTodosAsync(IDbTransaction? transaction = null)
    {
        return await _db.QueryAsync<Evento>(
            "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos",
            transaction: transaction);
    }

    public async Task<int> InserirAsync(Evento evento, IDbTransaction? transaction = null)
    {
        var sql = @"INSERT INTO Eventos (Nome, CapacidadeTotal, DataEvento, PrecoPadrao)
                    OUTPUT INSERTED.Id
                    VALUES (@Nome, @CapacidadeTotal, @DataEvento, @PrecoPadrao)";

        return await _db.QuerySingleAsync<int>(sql, new
        {
            evento.Nome,
            evento.CapacidadeTotal,
            evento.DataEvento,
            evento.PrecoPadrao
        }, transaction: transaction);
    }
}
