using System.Data;
using TicketPrime.Api.Models;

namespace TicketPrime.Api.Repositories;

/// <summary>
/// Repositório para operações CRUD de Carrinhos e CarrinhoItens.
/// Todos os métodos seguem a convenção C6: IDbTransaction? transaction = null como último parâmetro.
/// Criado na Etapa 11a para suportar operações não transacionais do carrinho.
/// </summary>
public interface ICarrinhoRepository
{
    /// <summary>
    /// Retorna um carrinho pelo ID (inclui todos os campos).
    /// Usado pelo POST /api/carrinho/{id}/itens.
    /// </summary>
    Task<Carrinho?> ObterPorIdAsync(int id, IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna o carrinho ativo de um CPF (Status = 'Ativo').
    /// Usado pelo DELETE /api/carrinho/{cpf} e validação de carrinho ativo.
    /// </summary>
    Task<Carrinho?> ObterAtivoPorCpfAsync(string cpf, IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna o carrinho mais recente (ativo ou expirado) de um CPF.
    /// Usado pelo GET /api/carrinho/{cpf} para visualizar o carrinho.
    /// </summary>
    Task<Carrinho?> ObterAtivoOuExpiradoPorCpfAsync(string cpf, IDbTransaction? transaction = null);

    /// <summary>
    /// Insere um novo carrinho com Status='Ativo' e DataExpiracao = NOW + 15min.
    /// Retorna o Id gerado.
    /// </summary>
    Task<int> CriarAsync(string usuarioCpf, IDbTransaction? transaction = null);

    /// <summary>
    /// Atualiza o status de um carrinho.
    /// Usado para expirar carrinhos.
    /// </summary>
    Task AtualizarStatusAsync(int id, string status, IDbTransaction? transaction = null);

    /// <summary>
    /// Verifica se já existe um carrinho ativo para o CPF.
    /// Retorna true se existir.
    /// </summary>
    Task<bool> ExisteAtivoPorCpfAsync(string cpf, IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna os itens de um carrinho com dados do evento e lote,
    /// incluindo Subtotal calculado. Usado para montar CarrinhoResponse.
    /// </summary>
    Task<IEnumerable<CarrinhoItemResponse>> ObterItensResponseAsync(int carrinhoId, IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna os itens crus de um carrinho (sem JOINs), incluindo
    /// Id, CarrinhoId, EventoId, TipoIngressoId, Quantidade, PrecoUnitario.
    /// Usado pelo fluxo de confirmação (CarrinhoService.ConfirmarAsync)
    /// para processar cada item dentro da transação.
    /// </summary>
    Task<IEnumerable<CarrinhoItem>> ObterItensPorCarrinhoIdAsync(int carrinhoId, IDbTransaction? transaction = null);

    /// <summary>
    /// Insere um item no carrinho.
    /// </summary>
    Task InserirItemAsync(CarrinhoItem item, IDbTransaction? transaction = null);

    /// <summary>
    /// Remove todos os itens de um carrinho.
    /// Usado pelo DELETE /api/carrinho/{cpf} (limpar carrinho).
    /// </summary>
    Task RemoverItensAsync(int carrinhoId, IDbTransaction? transaction = null);

    /// <summary>
    /// Conta quantos itens existem em um carrinho.
    /// Usado para validação.
    /// </summary>
    Task<int> ContarItensAsync(int carrinhoId, IDbTransaction? transaction = null);

    /// <summary>
    /// Retorna a soma das quantidades de um TipoIngressoId em carrinhos ativos,
    /// excluindo o carrinho informado. Usado para verificar disponibilidade de lote.
    /// </summary>
    Task<int> ObterQuantidadeReservadaPorTipoEmCarrinhosAtivosAsync(
        int tipoIngressoId, int carrinhoIdExcluir, IDbTransaction? transaction = null);
}
