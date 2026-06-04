using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Interface para o repositório de Ingressos.
/// Expandida na Etapa 8 com métodos CRUD, consultas detalhadas e geração de código único.
/// </summary>
public interface IIngressoRepository
{
    /// <summary>
    /// Retorna a quantidade de ingressos vendidos (status Confirmada ou Utilizada)
    /// para um determinado tipo-ingresso/lote.
    /// </summary>
    Task<int> ContarPorTipoAsync(int tipoIngressoId,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna um ingresso pelo ID da reserva.
    /// Usado para verificar se a reserva já possui ingresso.
    /// </summary>
    Task<Ingresso?> ObterPorReservaIdAsync(int reservaId,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna um ingresso pelo código único.
    /// Reservado para uso na Etapa 11b (consulta direta por código no contexto de admin/cancelamento).
    /// </summary>
    Task<Ingresso?> ObterPorCodigoAsync(string codigo,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Insere um novo ingresso e retorna o Id gerado + DataCriacao.
    /// </summary>
    Task<(int Id, DateTime DataCriacao)> InserirAsync(Ingresso ingresso,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Consulta detalhada de ingresso por ReservaId com dados do evento
    /// (JOIN com Reservas e Eventos). Retorna IngressoPorReservaResponse.
    /// </summary>
    Task<IngressoPorReservaResponse?> ObterDetalhadoPorReservaIdAsync(
        int reservaId, IDbTransaction? transaction = null);

    /// <summary>
    /// Consulta detalhada de ingresso por código único com dados do
    /// tipo-ingresso, evento e usuário (multi-mapping JOIN com 4 tabelas).
    /// Retorna IngressoDetalhadoResponse.
    /// </summary>
    Task<IEnumerable<IngressoDetalhadoResponse>> ObterDetalhadoPorCodigoAsync(
        string codigo, IDbTransaction? transaction = null);

    /// <summary>
    /// Gera um código único de 8 caracteres (A-Z, 2-9, sem caracteres
    /// ambíguos como I, O, 0, 1) verificando colisões no banco.
    /// MOVIDO de Program.cs.
    /// </summary>
    Task<string> GerarCodigoUnicoAsync(IDbTransaction? transaction = null,
        int? commandTimeout = 30);
}
