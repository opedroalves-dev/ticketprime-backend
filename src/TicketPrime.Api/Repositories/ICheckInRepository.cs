using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Interface para o repositório de CheckIn.
/// Criada na Etapa 9 com 4 métodos para operações CRUD e consultas na tabela CheckIns.
/// </summary>
public interface ICheckInRepository
{
    /// <summary>
    /// Insere um novo check-in e retorna o Id gerado + DataCheckIn.
    /// Usado por POST /api/ingressos/{codigo}/checkin e POST /api/checkin.
    /// </summary>
    Task<(int Id, DateTime DataCheckIn)> InserirAsync(int ingressoId,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Verifica se já existe check-in para um ingresso.
    /// Usado para bloquear check-in duplicado.
    /// </summary>
    Task<bool> ExistePorIngressoIdAsync(int ingressoId,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Lista todos os check-ins de um evento, com dados do ingresso,
    /// usuário e tipo-ingresso (JOINs múltiplos).
    /// Usado por GET /api/eventos/{eventoId}/checkins.
    /// </summary>
    Task<IEnumerable<CheckInItemResponse>> ListarPorEventoIdAsync(int eventoId,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna estatísticas de check-in de um evento:
    /// total vendidos, total check-ins, pendentes.
    /// Usado por GET /api/eventos/{eventoId}/checkins/stats.
    /// </summary>
    Task<CheckInStatsRaw> ObterStatsPorEventoIdAsync(int eventoId,
        IDbTransaction? transaction = null);
}
