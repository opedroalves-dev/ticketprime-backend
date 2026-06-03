using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

public class HistoricoPrecoRepository : IHistoricoPrecoRepository
{
    private readonly IDbConnection _db;

    public HistoricoPrecoRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task InserirPrecoInicialAsync(int eventoId, decimal precoNovo,
        IDbTransaction? transaction = null)
    {
        await _db.ExecuteAsync(@"
            INSERT INTO HistoricoPrecos (EventoId, TipoIngressoId, PrecoAnterior, PrecoNovo, Motivo)
            VALUES (@EventoId, NULL, NULL, @PrecoNovo, 'Preço inicial do evento')",
            new { EventoId = eventoId, PrecoNovo = precoNovo },
            transaction: transaction);
    }

    public async Task<IEnumerable<HistoricoPrecoResponse>> ObterPorEventoIdAsync(
        int eventoId, IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT hp.Id, hp.PrecoAnterior, hp.PrecoNovo, hp.DataAlteracao, hp.Motivo,
                   hp.TipoIngressoId, ti.Nome AS NomeLote
            FROM HistoricoPrecos hp
            LEFT JOIN TiposIngresso ti ON ti.Id = hp.TipoIngressoId
            WHERE hp.EventoId = @EventoId
            ORDER BY hp.DataAlteracao DESC";

        return await _db.QueryAsync<HistoricoPrecoResponse>(sql,
            new { EventoId = eventoId },
            transaction: transaction);
    }

    public async Task<IEnumerable<HistoricoPrecoResponse>> ObterPorLoteIdAsync(
        int loteId, IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT Id, PrecoAnterior, PrecoNovo, DataAlteracao, Motivo
            FROM HistoricoPrecos
            WHERE TipoIngressoId = @TipoIngressoId
            ORDER BY DataAlteracao DESC";

        return await _db.QueryAsync<HistoricoPrecoResponse>(sql,
            new { TipoIngressoId = loteId },
            transaction: transaction);
    }

    public async Task InserirHistoricoAsync(int eventoId, int tipoIngressoId,
        decimal? precoAnterior, decimal precoNovo, string motivo,
        IDbTransaction? transaction = null)
    {
        await _db.ExecuteAsync(@"
            INSERT INTO HistoricoPrecos (EventoId, TipoIngressoId, PrecoAnterior, PrecoNovo, Motivo)
            VALUES (@EventoId, @TipoIngressoId, @PrecoAnterior, @PrecoNovo, @Motivo)",
            new
            {
                EventoId = eventoId,
                TipoIngressoId = tipoIngressoId,
                PrecoAnterior = precoAnterior,
                PrecoNovo = precoNovo,
                Motivo = motivo
            },
            transaction: transaction);
    }

    public async Task ExcluirPorLoteIdAsync(int tipoIngressoId,
        IDbTransaction? transaction = null)
    {
        await _db.ExecuteAsync(
            "DELETE FROM HistoricoPrecos WHERE TipoIngressoId = @TipoIngressoId",
            new { TipoIngressoId = tipoIngressoId },
            transaction: transaction);
    }
}
