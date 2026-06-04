using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Repositório de Ingressos.
/// Expandido na Etapa 8 com métodos CRUD, consultas detalhadas e geração de código único.
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

    public async Task<Ingresso?> ObterPorReservaIdAsync(int reservaId,
        IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT Id, ReservaId, TipoIngressoId, CodigoUnico, Status,
                   ValorBruto, ValorDesconto, TaxaServico, ValorFinal, DataCriacao
            FROM Ingressos
            WHERE ReservaId = @ReservaId";

        return await _db.QuerySingleOrDefaultAsync<Ingresso>(sql,
            new { ReservaId = reservaId },
            transaction: transaction);
    }

    /// <summary>
    /// Obtém um ingresso pelo código único.
    /// Usado pelo CheckInService na Etapa 9 e também disponível para fluxos futuros,
    /// como confirmação/cancelamento/admin nas próximas etapas.
    /// </summary>
    public async Task<Ingresso?> ObterPorCodigoAsync(string codigo,
        IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT Id, ReservaId, TipoIngressoId, CodigoUnico, Status,
                   ValorBruto, ValorDesconto, TaxaServico, ValorFinal, DataCriacao
            FROM Ingressos
            WHERE CodigoUnico = @Codigo";

        return await _db.QuerySingleOrDefaultAsync<Ingresso>(sql,
            new { Codigo = codigo },
            transaction: transaction);
    }

    public async Task<(int Id, DateTime DataCriacao)> InserirAsync(Ingresso ingresso,
        IDbTransaction? transaction = null)
    {
        var sql = @"
            INSERT INTO Ingressos (ReservaId, TipoIngressoId, CodigoUnico, Status,
                                   ValorBruto, ValorDesconto, TaxaServico, ValorFinal, DataCriacao)
            OUTPUT INSERTED.Id, INSERTED.DataCriacao
            VALUES (@ReservaId, @TipoIngressoId, @CodigoUnico, @Status,
                    @ValorBruto, @ValorDesconto, @TaxaServico, @ValorFinal, GETDATE())";

        var result = await _db.QuerySingleAsync(sql, new
        {
            ingresso.ReservaId,
            ingresso.TipoIngressoId,
            ingresso.CodigoUnico,
            ingresso.Status,
            ingresso.ValorBruto,
            ingresso.ValorDesconto,
            ingresso.TaxaServico,
            ingresso.ValorFinal
        }, transaction: transaction);

        return ((int)result.Id, (DateTime)result.DataCriacao);
    }

    public async Task<IngressoPorReservaResponse?> ObterDetalhadoPorReservaIdAsync(
        int reservaId, IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT i.Id, i.ReservaId, i.CodigoUnico AS CodigoIngresso, i.Status,
                   i.ValorFinal, i.DataCriacao,
                   e.Nome AS NomeEvento, e.DataEvento,
                   r.UsuarioCpf
            FROM Ingressos i
            INNER JOIN Reservas r ON r.Id = i.ReservaId
            INNER JOIN Eventos e ON e.Id = r.EventoId
            WHERE i.ReservaId = @ReservaId";

        return await _db.QuerySingleOrDefaultAsync<IngressoPorReservaResponse>(sql,
            new { ReservaId = reservaId },
            transaction: transaction);
    }

    public async Task<IEnumerable<IngressoDetalhadoResponse>> ObterDetalhadoPorCodigoAsync(
        string codigo, IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT i.Id, i.ReservaId, i.TipoIngressoId, i.CodigoUnico, i.Status,
                   i.ValorBruto, i.ValorDesconto, i.TaxaServico, i.ValorFinal, i.DataCriacao,
                   ti.Id, ti.Nome, ti.Preco,
                   e.Id, e.Nome, e.DataEvento,
                   u.Cpf, u.Nome
            FROM Ingressos i
            LEFT JOIN TiposIngresso ti ON ti.Id = i.TipoIngressoId
            INNER JOIN Reservas r ON r.Id = i.ReservaId
            INNER JOIN Eventos e ON e.Id = r.EventoId
            INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
            WHERE i.CodigoUnico = @Codigo";

        return await _db.QueryAsync<IngressoDetalhadoResponse, TipoIngressoResumo, EventoResumo, UsuarioResumo, IngressoDetalhadoResponse>(
            sql,
            (ingresso, tipoIngresso, evento, usuario) =>
            {
                ingresso.TipoIngresso = tipoIngresso?.Id > 0 ? tipoIngresso : null;
                ingresso.Evento = evento;
                ingresso.Usuario = usuario;
                return ingresso;
            },
            new { Codigo = codigo },
            splitOn: "Id,Id,Cpf",
            transaction: transaction);
    }

    public async Task<string> GerarCodigoUnicoAsync(IDbTransaction? transaction = null,
        int? commandTimeout = 30)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = Random.Shared;
        string codigo;

        do
        {
            codigo = new string(Enumerable.Range(0, 8).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }
        while (await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Ingressos WHERE CodigoUnico = @Codigo",
            new { Codigo = codigo },
            transaction: transaction, commandTimeout: commandTimeout) > 0);

        return codigo;
    }

    public async Task AtualizarStatusAsync(int id, string status,
        IDbTransaction? transaction = null)
    {
        await _db.ExecuteAsync(
            "UPDATE Ingressos SET Status = @Status WHERE Id = @Id",
            new { Id = id, Status = status },
            transaction: transaction);
    }
}
