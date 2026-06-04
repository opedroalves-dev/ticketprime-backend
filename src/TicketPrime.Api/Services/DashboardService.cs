using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Services;

public class DashboardService
{
    private readonly IDbConnection _db;

    public DashboardService(IDbConnection db)
    {
        _db = db;
    }

    // 7.1. Listar eventos com métricas
    public async Task<IEnumerable<DashboardEventoListaResponse>> ListarEventosAsync()
    {
        return await _db.QueryAsync<DashboardEventoListaResponse>(
            "SELECT * FROM vw_DashboardEventos ORDER BY DataEvento");
    }

    // 7.2. Dashboard detalhado de um evento
    public async Task<DashboardEventoDetalhadoResponse?> ObterDashboardEventoAsync(int eventoId)
    {
        var evento = await _db.QuerySingleOrDefaultAsync<Evento>(
            "SELECT Id FROM Eventos WHERE Id = @Id",
            new { Id = eventoId });

        if (evento is null)
            return null;

        var eventoDashboard = await _db.QuerySingleOrDefaultAsync<DashboardEventoDetalhadoResponse>(
            "SELECT * FROM vw_DashboardEventos WHERE EventoId = @EventoId",
            new { EventoId = eventoId });

        if (eventoDashboard is null)
        {
            // Se não há dados na view (sem ingressos via TiposIngresso), buscar dados básicos
            var eventoBase = await _db.QuerySingleAsync<Evento>(
                "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos WHERE Id = @Id",
                new { Id = eventoId });

            eventoDashboard = new DashboardEventoDetalhadoResponse
            {
                EventoId = eventoBase.Id,
                NomeEvento = eventoBase.Nome,
                DataEvento = eventoBase.DataEvento,
                CapacidadeTotal = eventoBase.CapacidadeTotal,
                PrecoPadrao = eventoBase.PrecoPadrao,
                TotalIngressosVendidos = 0,
                ReceitaTotal = 0,
                PercentualOcupacao = 0,
                TotalCheckIns = 0,
                PendentesCheckIn = 0,
                TotalCancelados = 0
            };
        }

        // Buscar métricas por lote
        var lotes = await _db.QueryAsync<DashboardLoteResponse>(
            "SELECT * FROM vw_DashboardLotes WHERE EventoId = @EventoId",
            new { EventoId = eventoId });

        eventoDashboard.Lotes = lotes.AsList();

        return eventoDashboard;
    }

    // 7.3. Métricas por lote do evento
    public async Task<IEnumerable<DashboardLoteResponse>?> ListarLotesEventoAsync(int eventoId)
    {
        var evento = await _db.QuerySingleOrDefaultAsync<Evento>(
            "SELECT Id FROM Eventos WHERE Id = @Id",
            new { Id = eventoId });

        if (evento is null)
            return null;

        return await _db.QueryAsync<DashboardLoteResponse>(
            "SELECT * FROM vw_DashboardLotes WHERE EventoId = @EventoId",
            new { EventoId = eventoId });
    }

    // 7.4. Listar todas as reservas (admin)
    public async Task<IEnumerable<AdminReservaResponse>> ListarReservasAdminAsync(
        int? eventoId, string? status, string? cpf)
    {
        var parameters = new { EventoId = eventoId, Status = status, Cpf = cpf };

        var sql = @"
            SELECT
                r.Id                     AS ReservaId,
                r.UsuarioCpf,
                u.Nome                   AS NomeUsuario,
                r.EventoId,
                e.Nome                   AS NomeEvento,
                e.DataEvento,
                i.Id                     AS IngressoId,
                i.CodigoUnico,
                i.Status                 AS StatusIngresso,
                ti.Nome                  AS TipoIngresso,
                i.ValorBruto,
                i.ValorDesconto,
                i.TaxaServico,
                i.ValorFinal,
                r.CupomUtilizado,
                CASE WHEN ci.Id IS NOT NULL THEN 1 ELSE 0 END AS CheckInRealizado
            FROM Reservas r
            INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
            INNER JOIN Eventos e ON e.Id = r.EventoId
            LEFT JOIN Ingressos i ON i.ReservaId = r.Id
            LEFT JOIN TiposIngresso ti ON ti.Id = i.TipoIngressoId
            LEFT JOIN CheckIns ci ON ci.IngressoId = i.Id
            WHERE (@EventoId IS NULL OR r.EventoId = @EventoId)
              AND (@Status IS NULL OR i.Status = @Status)
              AND (@Cpf IS NULL OR r.UsuarioCpf = @Cpf)
            ORDER BY r.Id DESC";

        return await _db.QueryAsync<AdminReservaResponse>(sql, parameters);
    }

    // 8.1. Resumo do evento
    public async Task<EventoResumoResponse?> ObterResumoEventoAsync(int eventoId)
    {
        var evento = await _db.QuerySingleOrDefaultAsync<Evento>(
            "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos WHERE Id = @Id",
            new { Id = eventoId });

        if (evento is null)
            return null;

        var sql = @"
            SELECT
                e.Id              AS EventoId,
                e.Nome            AS NomeEvento,
                e.DataEvento,
                e.CapacidadeTotal,
                ISNULL(COUNT(DISTINCT r.Id), 0)                                                AS TotalReservas,
                e.CapacidadeTotal - ISNULL(SUM(CASE
                    WHEN ig.Status IN ('Confirmada', 'Utilizada') THEN 1 ELSE 0
                END), 0)                                                                        AS IngressosDisponiveis,
                ISNULL(SUM(CASE
                    WHEN ig.Status IN ('Confirmada', 'Utilizada') THEN ig.ValorFinal ELSE 0
                END), 0.00)                                                                     AS ReceitaTotal,
                ISNULL(COUNT(DISTINCT ci.Id), 0)                                                AS TotalCheckIns
            FROM Eventos e
            LEFT JOIN Reservas r ON r.EventoId = e.Id
            LEFT JOIN Ingressos ig ON ig.ReservaId = r.Id
            LEFT JOIN CheckIns ci ON ci.IngressoId = ig.Id
            WHERE e.Id = @EventoId
            GROUP BY e.Id, e.Nome, e.DataEvento, e.CapacidadeTotal";

        return await _db.QuerySingleAsync<EventoResumoResponse>(sql, new { EventoId = eventoId });
    }

    // 8.2. Listar check-ins de um evento (admin)
    public async Task<CheckInListResponse?> ListarCheckInsAdminAsync(int eventoId)
    {
        var evento = await _db.QuerySingleOrDefaultAsync<Evento>(
            "SELECT Id, Nome FROM Eventos WHERE Id = @Id",
            new { Id = eventoId });

        if (evento is null)
            return null;

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

        var checkins = (await _db.QueryAsync<CheckInItemResponse>(sql, new { EventoId = eventoId })).AsList();

        return new CheckInListResponse
        {
            EventoId = eventoId,
            NomeEvento = evento.Nome,
            TotalCheckIns = checkins.Count,
            CheckIns = checkins
        };
    }

    // 8.3. Listar reservas de um evento (admin)
    public async Task<IEnumerable<AdminReservaResponse>?> ListarReservasEventoAsync(int eventoId)
    {
        var evento = await _db.QuerySingleOrDefaultAsync<Evento>(
            "SELECT Id FROM Eventos WHERE Id = @Id",
            new { Id = eventoId });

        if (evento is null)
            return null;

        var sql = @"
            SELECT
                r.Id                     AS ReservaId,
                r.UsuarioCpf,
                u.Nome                   AS NomeUsuario,
                r.EventoId,
                e.Nome                   AS NomeEvento,
                e.DataEvento,
                i.Id                     AS IngressoId,
                i.CodigoUnico,
                i.Status                 AS StatusIngresso,
                ti.Nome                  AS TipoIngresso,
                i.ValorBruto,
                i.ValorDesconto,
                i.TaxaServico,
                i.ValorFinal,
                r.CupomUtilizado,
                CASE WHEN ci.Id IS NOT NULL THEN 1 ELSE 0 END AS CheckInRealizado
            FROM Reservas r
            INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
            INNER JOIN Eventos e ON e.Id = r.EventoId
            LEFT JOIN Ingressos i ON i.ReservaId = r.Id
            LEFT JOIN TiposIngresso ti ON ti.Id = i.TipoIngressoId
            LEFT JOIN CheckIns ci ON ci.IngressoId = i.Id
            WHERE r.EventoId = @EventoId
            ORDER BY r.Id DESC";

        return await _db.QueryAsync<AdminReservaResponse>(sql, new { EventoId = eventoId });
    }
}
