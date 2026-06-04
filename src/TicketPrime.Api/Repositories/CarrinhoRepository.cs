using Dapper;
using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Implementação concreta de ICarrinhoRepository usando Dapper.
/// Todos os métodos seguem a convenção C6: transaction é passado ao Dapper quando não-nulo.
/// Nenhuma regra de negócio está presente aqui — apenas SQL com parâmetros nomeados.
/// </summary>
public class CarrinhoRepository : ICarrinhoRepository
{
    private readonly IDbConnection _db;

    public CarrinhoRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<Carrinho?> ObterPorIdAsync(int id, IDbTransaction? transaction = null)
    {
        return await _db.QuerySingleOrDefaultAsync<Carrinho>(
            "SELECT Id, UsuarioCpf, Status, DataCriacao, DataExpiracao FROM Carrinhos WHERE Id = @Id",
            new { Id = id },
            transaction: transaction);
    }

    public async Task<Carrinho?> ObterAtivoPorCpfAsync(string cpf, IDbTransaction? transaction = null)
    {
        return await _db.QuerySingleOrDefaultAsync<Carrinho>(
            "SELECT Id, UsuarioCpf, Status, DataCriacao, DataExpiracao FROM Carrinhos WHERE UsuarioCpf = @Cpf AND Status = 'Ativo'",
            new { Cpf = cpf },
            transaction: transaction);
    }

    public async Task<Carrinho?> ObterAtivoOuExpiradoPorCpfAsync(string cpf, IDbTransaction? transaction = null)
    {
        return await _db.QuerySingleOrDefaultAsync<Carrinho>(
            "SELECT Id, UsuarioCpf, Status, DataCriacao, DataExpiracao FROM Carrinhos WHERE UsuarioCpf = @Cpf AND Status IN ('Ativo', 'Expirado') ORDER BY Id DESC",
            new { Cpf = cpf },
            transaction: transaction);
    }

    public async Task<int> CriarAsync(string usuarioCpf, IDbTransaction? transaction = null)
    {
        return await _db.QuerySingleAsync<int>(
            "INSERT INTO Carrinhos (UsuarioCpf, Status, DataCriacao, DataExpiracao) OUTPUT INSERTED.Id VALUES (@UsuarioCpf, 'Ativo', GETDATE(), DATEADD(MINUTE, 15, GETDATE()))",
            new { UsuarioCpf = usuarioCpf },
            transaction: transaction);
    }

    public async Task AtualizarStatusAsync(int id, string status, IDbTransaction? transaction = null)
    {
        await _db.ExecuteAsync(
            "UPDATE Carrinhos SET Status = @Status WHERE Id = @Id",
            new { Id = id, Status = status },
            transaction: transaction);
    }

    public async Task<bool> ExisteAtivoPorCpfAsync(string cpf, IDbTransaction? transaction = null)
    {
        var count = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Carrinhos WHERE UsuarioCpf = @Cpf AND Status = 'Ativo'",
            new { Cpf = cpf },
            transaction: transaction);
        return count > 0;
    }

    public async Task<IEnumerable<CarrinhoItemResponse>> ObterItensResponseAsync(int carrinhoId, IDbTransaction? transaction = null)
    {
        return await _db.QueryAsync<CarrinhoItemResponse>(@"
            SELECT ci.Id, ci.EventoId, e.Nome AS NomeEvento, ci.TipoIngressoId,
                   ti.Nome AS NomeLote, ci.Quantidade, ci.PrecoUnitario,
                   (ci.Quantidade * ci.PrecoUnitario) AS Subtotal
            FROM CarrinhoItens ci
            INNER JOIN Eventos e ON e.Id = ci.EventoId
            LEFT JOIN TiposIngresso ti ON ti.Id = ci.TipoIngressoId
            WHERE ci.CarrinhoId = @CarrinhoId",
            new { CarrinhoId = carrinhoId },
            transaction: transaction);
    }

    public async Task InserirItemAsync(CarrinhoItem item, IDbTransaction? transaction = null)
    {
        await _db.ExecuteAsync(
            "INSERT INTO CarrinhoItens (CarrinhoId, EventoId, TipoIngressoId, Quantidade, PrecoUnitario) VALUES (@CarrinhoId, @EventoId, @TipoIngressoId, @Quantidade, @PrecoUnitario)",
            new { item.CarrinhoId, item.EventoId, item.TipoIngressoId, item.Quantidade, item.PrecoUnitario },
            transaction: transaction);
    }

    public async Task RemoverItensAsync(int carrinhoId, IDbTransaction? transaction = null)
    {
        await _db.ExecuteAsync(
            "DELETE FROM CarrinhoItens WHERE CarrinhoId = @CarrinhoId",
            new { CarrinhoId = carrinhoId },
            transaction: transaction);
    }

    public async Task<int> ContarItensAsync(int carrinhoId, IDbTransaction? transaction = null)
    {
        return await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM CarrinhoItens WHERE CarrinhoId = @CarrinhoId",
            new { CarrinhoId = carrinhoId },
            transaction: transaction);
    }

    public async Task<int> ObterQuantidadeReservadaPorTipoEmCarrinhosAtivosAsync(
        int tipoIngressoId, int carrinhoIdExcluir, IDbTransaction? transaction = null)
    {
        return await _db.ExecuteScalarAsync<int>(@"
            SELECT ISNULL(SUM(ci.Quantidade), 0)
            FROM CarrinhoItens ci
            INNER JOIN Carrinhos c ON c.Id = ci.CarrinhoId
            WHERE ci.TipoIngressoId = @TipoIngressoId AND c.Status = 'Ativo' AND c.Id != @CarrinhoIdExcluir",
            new { TipoIngressoId = tipoIngressoId, CarrinhoIdExcluir = carrinhoIdExcluir },
            transaction: transaction);
    }
}
