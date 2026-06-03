using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Repositório para TiposIngresso/Lotes.
/// Complementado na Etapa 7 com métodos CRUD completos.
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
            "SELECT Id, Nome, EventoId, Preco, Capacidade, TaxaServico, DataInicioVenda, DataFimVenda, Lote FROM TiposIngresso WHERE Id = @Id",
            new { Id = id },
            transaction: transaction);
    }

    public async Task<IEnumerable<TipoIngresso>> ObterPorEventoIdAsync(
        int eventoId, IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT Id, EventoId, Nome, Preco, Capacidade, TaxaServico,
                   DataInicioVenda, DataFimVenda, Lote
            FROM TiposIngresso
            WHERE EventoId = @EventoId
            ORDER BY Id";

        return await _db.QueryAsync<TipoIngresso>(sql,
            new { EventoId = eventoId },
            transaction: transaction);
    }

    public async Task<int> InserirAsync(TipoIngresso tipoIngresso,
        IDbTransaction? transaction = null)
    {
        var sql = @"
            INSERT INTO TiposIngresso (EventoId, Nome, Preco, Capacidade, TaxaServico,
                                       DataInicioVenda, DataFimVenda, Lote)
            OUTPUT INSERTED.Id
            VALUES (@EventoId, @Nome, @Preco, @Capacidade, @TaxaServico,
                    @DataInicioVenda, @DataFimVenda, @Lote)";

        return await _db.QuerySingleAsync<int>(sql, new
        {
            tipoIngresso.EventoId,
            tipoIngresso.Nome,
            tipoIngresso.Preco,
            Capacidade = tipoIngresso.Capacidade,
            tipoIngresso.TaxaServico,
            tipoIngresso.DataInicioVenda,
            tipoIngresso.DataFimVenda,
            Lote = tipoIngresso.Lote ?? (object)DBNull.Value
        }, transaction: transaction);
    }

    public async Task<bool> AtualizarAsync(TipoIngresso tipoIngresso,
        IDbTransaction? transaction = null)
    {
        var sql = @"
            UPDATE TiposIngresso
            SET Nome = @Nome, Preco = @Preco, Capacidade = @Capacidade,
                TaxaServico = @TaxaServico, DataInicioVenda = @DataInicioVenda,
                DataFimVenda = @DataFimVenda
            WHERE Id = @Id";

        var linhas = await _db.ExecuteAsync(sql, new
        {
            tipoIngresso.Id,
            tipoIngresso.Nome,
            tipoIngresso.Preco,
            tipoIngresso.Capacidade,
            tipoIngresso.TaxaServico,
            tipoIngresso.DataInicioVenda,
            tipoIngresso.DataFimVenda
        }, transaction: transaction);

        return linhas > 0;
    }

    public async Task<bool> RemoverAsync(int id,
        IDbTransaction? transaction = null)
    {
        var linhas = await _db.ExecuteAsync(
            "DELETE FROM TiposIngresso WHERE Id = @Id",
            new { Id = id },
            transaction: transaction);

        return linhas > 0;
    }
}
