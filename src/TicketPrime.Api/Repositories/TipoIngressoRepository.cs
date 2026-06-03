using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Repositório mínimo para TiposIngresso/Lotes.
/// Criado na Etapa 6 apenas com o método necessário para validação.
/// Será complementado na Etapa 7 com os demais métodos CRUD.
/// </summary>
public class TipoIngressoRepository : ITipoIngressoRepository
{
    private readonly IDbConnection _db;

    public TipoIngressoRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<TipoIngresso?> ObterPorIdAsync(int id,
        IDbTransaction? transaction = null)
    {
        return await _db.QuerySingleOrDefaultAsync<TipoIngresso>(
            "SELECT Id, Nome, EventoId FROM TiposIngresso WHERE Id = @Id",
            new { Id = id },
            transaction: transaction);
    }
}
