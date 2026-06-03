using Dapper;
using System.Data;
using TicketPrime.Api.Repositories;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Repositório mínimo para Ingressos.
/// Criado na Etapa 7 apenas para operações de Lotes/TiposIngresso.
/// </summary>
public class IngressoRepository : IIngressoRepository
{
    private readonly IDbConnection _db;

    public IngressoRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<int> ContarPorTipoAsync(int tipoIngressoId,
        IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT COUNT(1)
            FROM Ingressos
            WHERE TipoIngressoId = @TipoIngressoId
              AND Status IN ('Confirmada', 'Utilizada')";

        return await _db.ExecuteScalarAsync<int>(sql,
            new { TipoIngressoId = tipoIngressoId },
            transaction: transaction);
    }
}
