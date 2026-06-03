using Dapper;
using System.Data;

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
}
