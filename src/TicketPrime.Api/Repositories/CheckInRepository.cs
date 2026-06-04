using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Repositório de CheckIn.
/// Criado na Etapa 9 com 4 métodos para operações CRUD e consultas na tabela CheckIns.
/// </summary>
public class CheckInRepository : ICheckInRepository
{
    private readonly IDbConnection _db;

    public CheckInRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<(int Id, DateTime DataCheckIn)> InserirAsync(int ingressoId,
        IDbTransaction? transaction = null)
    {
        var sql = @"INSERT INTO CheckIns (IngressoId, DataCheckIn)
                     OUTPUT INSERTED.Id, INSERTED.DataCheckIn
                     VALUES (@IngressoId, GETDATE())";

        var result = await _db.QuerySingleAsync(sql,
            new { IngressoId = ingressoId },
            transaction: transaction);

        return ((int)result.Id, (DateTime)result.DataCheckIn);
    }

    public async Task<bool> ExistePorIngressoIdAsync(int ingressoId,
        IDbTransaction? transaction = null)
    {
        var count = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM CheckIns WHERE IngressoId = @IngressoId",
            new { IngressoId = ingressoId },
            transaction: transaction);

        return count > 0;
    }

    public async Task<IEnumerable<CheckInItemResponse>> ListarPorEventoIdAsync(int eventoId,
        IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT ci.Id, ci.IngressoId, i.CodigoUnico, u.Nome AS NomeUsuario,
                   u.Cpf AS UsuarioCpf, ti.Nome AS TipoIngresso, ci.DataCheckIn
            FROM CheckIns ci
            INNER JOIN Ingressos i ON i.Id = ci.IngressoId
            INNER JOIN Reservas r ON r.Id = i.ReservaId
            INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
            LEFT JOIN TiposIngresso ti ON ti.Id = i.TipoIngressoId
            WHERE r.EventoId = @EventoId
            ORDER BY ci.DataCheckIn DESC";

        return await _db.QueryAsync<CheckInItemResponse>(sql,
            new { EventoId = eventoId },
            transaction: transaction);
    }

    public async Task<CheckInStatsRaw> ObterStatsPorEventoIdAsync(int eventoId,
        IDbTransaction? transaction = null)
    {
        var sql = @"
            SELECT
                ISNULL(SUM(CASE WHEN ig.Status IN ('Confirmada', 'Utilizada') THEN 1 ELSE 0 END), 0) AS TotalIngressosVendidos,
                ISNULL(COUNT(DISTINCT ci.Id), 0) AS TotalCheckIns,
                ISNULL(SUM(CASE WHEN ig.Status = 'Confirmada' THEN 1 ELSE 0 END), 0) AS Pendentes
            FROM Eventos e
            LEFT JOIN TiposIngresso ti ON ti.EventoId = e.Id
            LEFT JOIN Ingressos ig ON ig.TipoIngressoId = ti.Id
            LEFT JOIN CheckIns ci ON ci.IngressoId = ig.Id
            WHERE e.Id = @EventoId";

        return await _db.QuerySingleAsync<CheckInStatsRaw>(sql,
            new { EventoId = eventoId },
            transaction: transaction);
    }
}

/// <summary>
/// Classe auxiliar para mapeamento Dapper das consultas de stats.
/// </summary>
public class CheckInStatsRaw
{
    public int TotalIngressosVendidos { get; set; }
    public int TotalCheckIns { get; set; }
    public int Pendentes { get; set; }
}
