using System.Data;

namespace TicketPrime.Api.Repositories;

public interface IHistoricoPrecoRepository
{
    /// <summary>
    /// Registra o preço inicial de um evento no histórico (RF05).
    /// Este é o único método necessário nesta etapa.
    /// Na Etapa 6, esta interface será complementada com métodos de consulta.
    /// </summary>
    Task InserirPrecoInicialAsync(int eventoId, decimal precoNovo,
        IDbTransaction? transaction = null);
}
