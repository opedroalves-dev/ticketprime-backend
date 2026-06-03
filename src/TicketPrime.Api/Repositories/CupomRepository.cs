using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

public class CupomRepository : ICupomRepository
{
    private readonly IDbConnection _db;

    public CupomRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<Cupom?> ObterPorCodigoAsync(string codigo, IDbTransaction? transaction = null)
    {
        return await _db.QuerySingleOrDefaultAsync<Cupom>(
            "SELECT Codigo, PorcentagemDesconto, ValorMinimoRegra FROM Cupons WHERE Codigo = @Codigo",
            new { Codigo = codigo },
            transaction: transaction);
    }

    public async Task<bool> ExisteAsync(string codigo, IDbTransaction? transaction = null)
    {
        return await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Cupons WHERE Codigo = @Codigo",
            new { Codigo = codigo },
            transaction: transaction) > 0;
    }

    public async Task InserirAsync(Cupom cupom, IDbTransaction? transaction = null)
    {
        await _db.ExecuteAsync(
            "INSERT INTO Cupons (Codigo, PorcentagemDesconto, ValorMinimoRegra) VALUES (@Codigo, @PorcentagemDesconto, @ValorMinimoRegra)",
            cupom,
            transaction: transaction);
    }
}
