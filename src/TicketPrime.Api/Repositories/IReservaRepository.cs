using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Interface para operações de banco relacionadas a Reservas.
/// Expandida na Etapa 10b com métodos CRUD completos seguindo a convenção C6.
/// </summary>
public interface IReservaRepository
{
    /// <summary>
    /// Retorna uma reserva pelo ID.
    /// </summary>
    Task<Reserva?> ObterPorIdAsync(int id, IDbTransaction? transaction = null);

    /// <summary>
    /// Insere uma reserva e retorna o Id gerado.
    /// </summary>
    Task<int> InserirAsync(Reserva reserva, IDbTransaction? transaction = null);

    /// <summary>
    /// Conta reservas de um CPF em um evento específico (limite de 2).
    /// </summary>
    Task<int> ContarPorCpfEEventoAsync(string cpf, int eventoId, IDbTransaction? transaction = null);

    /// <summary>
    /// Conta reservas de um evento (verificação de capacidade).
    /// </summary>
    Task<int> ContarPorEventoAsync(int eventoId, IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna todas as reservas de um CPF.
    /// </summary>
    Task<IEnumerable<Reserva>> ObterPorCpfAsync(string cpf, IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna todas as reservas de um evento.
    /// </summary>
    Task<IEnumerable<Reserva>> ObterPorEventoIdAsync(int eventoId, IDbTransaction? transaction = null);
}
