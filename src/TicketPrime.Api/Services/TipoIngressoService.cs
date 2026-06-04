using System.Data;
using TicketPrime.Api.Models;
using TicketPrime.Api.Repositories;

namespace TicketPrime.Api.Services;

/// <summary>
/// Service que orquestra validação, regras de negócio e chamadas aos repositórios
/// para o domínio Lotes/TiposIngresso. Criado na Etapa 7.
/// </summary>
public class TipoIngressoService
{
    private readonly ITipoIngressoRepository _tipoIngressoRepository;
    private readonly IEventoRepository _eventoRepository;
    private readonly IHistoricoPrecoRepository _historicoPrecoRepository;
    private readonly IIngressoRepository _ingressoRepository;
    private readonly IDbConnection _db;

    public TipoIngressoService(
        ITipoIngressoRepository tipoIngressoRepository,
        IEventoRepository eventoRepository,
        IHistoricoPrecoRepository historicoPrecoRepository,
        IIngressoRepository ingressoRepository,
        IDbConnection db)
    {
        _tipoIngressoRepository = tipoIngressoRepository;
        _eventoRepository = eventoRepository;
        _historicoPrecoRepository = historicoPrecoRepository;
        _ingressoRepository = ingressoRepository;
        _db = db;
    }

    // =================================================================
    //  1. POST /api/eventos/{eventoId}/lotes
    // =================================================================

    /// <summary>
    /// Cria um novo lote de ingressos para um evento.
    /// Usa transação para atomicidade entre INSERT em TiposIngresso e HistoricoPrecos.
    /// </summary>
    public async Task<ResultadoCriacaoLote> CriarLoteAsync(int eventoId, CriarLoteRequest request)
    {
        // Validar existência do evento
        var evento = await _eventoRepository.ObterPorIdAsync(eventoId);
        if (evento is null)
            return new ResultadoCriacaoLote { Sucesso = false, StatusCode = 404, Erro = "Evento não encontrado." };

        // Validações inline — mesmas mensagens do endpoint original
        if (string.IsNullOrWhiteSpace(request.Nome))
            return new ResultadoCriacaoLote { Sucesso = false, Erro = "Nome do lote é obrigatório." };

        if (request.Nome.Length > 100)
            return new ResultadoCriacaoLote { Sucesso = false, Erro = "Nome não pode exceder 100 caracteres." };

        if (request.Preco <= 0)
            return new ResultadoCriacaoLote { Sucesso = false, Erro = "Preço deve ser maior que zero." };

        if (request.Capacidade <= 0)
            return new ResultadoCriacaoLote { Sucesso = false, Erro = "Capacidade deve ser maior que zero." };

        if (request.TaxaServico < 0)
            return new ResultadoCriacaoLote { Sucesso = false, Erro = "Taxa de serviço não pode ser negativa." };

        if (request.DataInicioVenda == default)
            return new ResultadoCriacaoLote { Sucesso = false, Erro = "Data de início da venda é obrigatória." };

        if (request.DataFimVenda == default)
            return new ResultadoCriacaoLote { Sucesso = false, Erro = "Data de fim da venda é obrigatória." };

        if (request.DataFimVenda <= request.DataInicioVenda)
            return new ResultadoCriacaoLote { Sucesso = false, Erro = "Data de fim da venda deve ser posterior à data de início." };

        // Transação — atomicidade entre INSERT em TiposIngresso e HistoricoPrecos
        if (_db.State != ConnectionState.Open)
            _db.Open();

        using var transaction = _db.BeginTransaction();

        try
        {
            var tipoIngresso = new TipoIngresso
            {
                EventoId = eventoId,
                Nome = request.Nome,
                Preco = request.Preco,
                Capacidade = request.Capacidade,
                TaxaServico = request.TaxaServico,
                DataInicioVenda = request.DataInicioVenda,
                DataFimVenda = request.DataFimVenda,
                Lote = null
            };

            var loteId = await _tipoIngressoRepository.InserirAsync(tipoIngresso, transaction);

            await _historicoPrecoRepository.InserirHistoricoAsync(
                eventoId, loteId, null, request.Preco,
                "Preço inicial do lote", transaction);

            transaction.Commit();

            var response = new LoteResponse
            {
                Id = loteId,
                EventoId = eventoId,
                Nome = request.Nome,
                Preco = request.Preco,
                Capacidade = request.Capacidade,
                TaxaServico = request.TaxaServico,
                DataInicioVenda = request.DataInicioVenda,
                DataFimVenda = request.DataFimVenda
            };

            return new ResultadoCriacaoLote
            {
                Sucesso = true,
                Id = loteId,
                Lote = response
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // =================================================================
    //  2. GET /api/eventos/{eventoId}/lotes
    // =================================================================

    /// <summary>
    /// Lista todos os lotes de um evento, incluindo ingressos vendidos e capacidade restante.
    /// </summary>
    public async Task<ResultadoListagemLotes> ListarLotesAsync(int eventoId)
    {
        var evento = await _eventoRepository.ObterPorIdAsync(eventoId);
        if (evento is null)
            return new ResultadoListagemLotes { Sucesso = false, StatusCode = 404, Erro = "Evento não encontrado." };

        // Obtém todos os tipos-ingresso do evento
        var tipos = await _tipoIngressoRepository.ObterPorEventoIdAsync(eventoId);

        var lotes = new List<LoteListaResponse>();

        foreach (var ti in tipos)
        {
            var ingressosVendidos = await _ingressoRepository.ContarPorTipoAsync(ti.Id);

            lotes.Add(new LoteListaResponse
            {
                Id = ti.Id,
                EventoId = ti.EventoId,
                Nome = ti.Nome,
                Preco = ti.Preco,
                Capacidade = ti.Capacidade,
                TaxaServico = ti.TaxaServico,
                DataInicioVenda = ti.DataInicioVenda,
                DataFimVenda = ti.DataFimVenda,
                IngressosVendidos = ingressosVendidos,
                CapacidadeRestante = ti.Capacidade - ingressosVendidos
            });
        }

        return new ResultadoListagemLotes { Sucesso = true, Lotes = lotes };
    }

    // =================================================================
    //  3. GET /api/lotes/{loteId}
    // =================================================================

    /// <summary>
    /// Obtém um lote específico pelo Id.
    /// </summary>
    public async Task<ResultadoObterLote> ObterLoteAsync(int loteId)
    {
        var lote = await _tipoIngressoRepository.ObterPorIdAsync(loteId);
        if (lote is null)
            return new ResultadoObterLote { Sucesso = false, StatusCode = 404, Erro = "Lote não encontrado." };

        var response = new LoteResponse
        {
            Id = lote.Id,
            EventoId = lote.EventoId,
            Nome = lote.Nome,
            Preco = lote.Preco,
            Capacidade = lote.Capacidade,
            TaxaServico = lote.TaxaServico,
            DataInicioVenda = lote.DataInicioVenda,
            DataFimVenda = lote.DataFimVenda
        };

        return new ResultadoObterLote { Sucesso = true, Lote = response };
    }

    // =================================================================
    //  4. PUT /api/lotes/{loteId}
    // =================================================================

    /// <summary>
    /// Atualiza os dados de um lote. Se o preço mudou, registra a alteração no histórico.
    /// </summary>
    public async Task<ResultadoAtualizarLote> AtualizarLoteAsync(int loteId, CriarLoteRequest request)
    {
        var lote = await _tipoIngressoRepository.ObterPorIdAsync(loteId);
        if (lote is null)
            return new ResultadoAtualizarLote { Sucesso = false, StatusCode = 404, Erro = "Lote não encontrado." };

        // Validações inline — mesmas mensagens do endpoint original
        if (string.IsNullOrWhiteSpace(request.Nome))
            return new ResultadoAtualizarLote { Sucesso = false, Erro = "Nome do lote é obrigatório." };

        if (request.Nome.Length > 100)
            return new ResultadoAtualizarLote { Sucesso = false, Erro = "Nome não pode exceder 100 caracteres." };

        if (request.Preco <= 0)
            return new ResultadoAtualizarLote { Sucesso = false, Erro = "Preço deve ser maior que zero." };

        if (request.TaxaServico < 0)
            return new ResultadoAtualizarLote { Sucesso = false, Erro = "Taxa de serviço não pode ser negativa." };

        // Verificar se capacidade não é menor que ingressos vendidos
        var ingressosVendidos = await _ingressoRepository.ContarPorTipoAsync(loteId);
        if (request.Capacidade < ingressosVendidos)
            return new ResultadoAtualizarLote { Sucesso = false, Erro = "Capacidade não pode ser menor que a quantidade de ingressos já vendidos para este lote." };

        if (request.DataInicioVenda == default)
            return new ResultadoAtualizarLote { Sucesso = false, Erro = "Data de início da venda é obrigatória." };

        if (request.DataFimVenda == default)
            return new ResultadoAtualizarLote { Sucesso = false, Erro = "Data de fim da venda é obrigatória." };

        if (request.DataFimVenda <= request.DataInicioVenda)
            return new ResultadoAtualizarLote { Sucesso = false, Erro = "Data de fim da venda deve ser posterior à data de início." };

        // Registrar alteração de preço no histórico se mudou
        if (lote.Preco != request.Preco)
        {
            await _historicoPrecoRepository.InserirHistoricoAsync(
                lote.EventoId, loteId, lote.Preco, request.Preco,
                "Alteração de preço do lote");
        }

        // Atualizar lote
        var tipoIngresso = new TipoIngresso
        {
            Id = loteId,
            EventoId = lote.EventoId,
            Nome = request.Nome,
            Preco = request.Preco,
            Capacidade = request.Capacidade,
            TaxaServico = request.TaxaServico,
            DataInicioVenda = request.DataInicioVenda,
            DataFimVenda = request.DataFimVenda
        };

        await _tipoIngressoRepository.AtualizarAsync(tipoIngresso);

        var response = new LoteResponse
        {
            Id = loteId,
            EventoId = lote.EventoId,
            Nome = request.Nome,
            Preco = request.Preco,
            Capacidade = request.Capacidade,
            TaxaServico = request.TaxaServico,
            DataInicioVenda = request.DataInicioVenda,
            DataFimVenda = request.DataFimVenda
        };

        return new ResultadoAtualizarLote
        {
            Sucesso = true,
            Lote = response
        };
    }

    // =================================================================
    //  5. DELETE /api/lotes/{loteId}
    // =================================================================

    /// <summary>
    /// Remove um lote, verificando se não possui ingressos vendidos.
    /// Remove também os registros de histórico associados.
    /// </summary>
    public async Task<ResultadoRemoverLote> RemoverLoteAsync(int loteId)
    {
        var lote = await _tipoIngressoRepository.ObterPorIdAsync(loteId);
        if (lote is null)
            return new ResultadoRemoverLote { Sucesso = false, StatusCode = 404, Erro = "Lote não encontrado." };

        // Verificar se lote possui ingressos vendidos
        var ingressosVendidos = await _ingressoRepository.ContarPorTipoAsync(loteId);
        if (ingressosVendidos > 0)
            return new ResultadoRemoverLote { Sucesso = false, StatusCode = 409, Erro = "Não é possível remover um lote com ingressos vendidos." };

        // Remove registros de histórico associados e depois o lote
        if (_db.State != ConnectionState.Open)
            _db.Open();

        using var transaction = _db.BeginTransaction();

        try
        {
            await _historicoPrecoRepository.ExcluirPorLoteIdAsync(loteId, transaction);
            await _tipoIngressoRepository.RemoverAsync(loteId, transaction);

            transaction.Commit();

            return new ResultadoRemoverLote { Sucesso = true };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // =================================================================
    //  6. POST /api/tipos-ingresso
    // =================================================================

    /// <summary>
    /// Cria um novo tipo de ingresso (via rota /api/tipos-ingresso).
    /// </summary>
    public async Task<ResultadoCriacaoTipoIngresso> CriarTipoIngressoAsync(CriarTipoIngressoRequest request)
    {
        // Valida EventoId existente
        if (request.EventoId <= 0)
            return new ResultadoCriacaoTipoIngresso { Sucesso = false, Erro = "EventoId é obrigatório e deve ser maior que zero." };

        var evento = await _eventoRepository.ObterPorIdAsync(request.EventoId);
        if (evento is null)
            return new ResultadoCriacaoTipoIngresso { Sucesso = false, StatusCode = 404, Erro = "Evento não encontrado." };

        // Valida Nome
        if (string.IsNullOrWhiteSpace(request.Nome))
            return new ResultadoCriacaoTipoIngresso { Sucesso = false, Erro = "Nome é obrigatório." };

        if (request.Nome.Length > 100)
            return new ResultadoCriacaoTipoIngresso { Sucesso = false, Erro = "Nome não pode exceder 100 caracteres." };

        // Valida QuantidadeDisponivel
        if (request.QuantidadeDisponivel <= 0)
            return new ResultadoCriacaoTipoIngresso { Sucesso = false, Erro = "QuantidadeDisponivel deve ser maior que zero." };

        // Valida Preco
        if (request.Preco < 0)
            return new ResultadoCriacaoTipoIngresso { Sucesso = false, Erro = "Preco não pode ser negativo." };

        // Valida Lote
        if (string.IsNullOrWhiteSpace(request.Lote))
            return new ResultadoCriacaoTipoIngresso { Sucesso = false, Erro = "Lote é obrigatório." };

        if (request.Lote.Length > 100)
            return new ResultadoCriacaoTipoIngresso { Sucesso = false, Erro = "Lote não pode exceder 100 caracteres." };

        // Transação — atomicidade entre INSERT em TiposIngresso e HistoricoPrecos
        if (_db.State != ConnectionState.Open)
            _db.Open();

        using var transaction = _db.BeginTransaction();

        try
        {
            var tipoIngresso = new TipoIngresso
            {
                EventoId = request.EventoId,
                Nome = request.Nome,
                Preco = request.Preco,
                Capacidade = request.QuantidadeDisponivel,
                TaxaServico = 0m,
                DataInicioVenda = DateTime.Now,
                DataFimVenda = new DateTime(9999, 12, 31),
                Lote = request.Lote
            };

            var tipoId = await _tipoIngressoRepository.InserirAsync(tipoIngresso, transaction);

            await _historicoPrecoRepository.InserirHistoricoAsync(
                request.EventoId, tipoId, null, request.Preco,
                "Preço inicial do tipo de ingresso", transaction);

            transaction.Commit();

            var response = new TipoIngressoResponse
            {
                Id = tipoId,
                EventoId = request.EventoId,
                Nome = request.Nome,
                QuantidadeDisponivel = request.QuantidadeDisponivel,
                Preco = request.Preco,
                Lote = request.Lote
            };

            return new ResultadoCriacaoTipoIngresso
            {
                Sucesso = true,
                Id = tipoId,
                TipoIngresso = response
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // =================================================================
    //  7. GET /api/eventos/{eventoId}/tipos-ingresso
    // =================================================================

    /// <summary>
    /// Lista os tipos de ingresso de um evento.
    /// </summary>
    public async Task<ResultadoListagemTiposIngresso> ListarTiposIngressoAsync(int eventoId)
    {
        var evento = await _eventoRepository.ObterPorIdAsync(eventoId);
        if (evento is null)
            return new ResultadoListagemTiposIngresso { Sucesso = false, StatusCode = 404, Erro = "Evento não encontrado." };

        var tipos = await _tipoIngressoRepository.ObterPorEventoIdAsync(eventoId);

        var response = tipos.Select(ti => new TipoIngressoResponse
        {
            Id = ti.Id,
            EventoId = ti.EventoId,
            Nome = ti.Nome,
            QuantidadeDisponivel = ti.Capacidade,
            Preco = ti.Preco,
            Lote = ti.Lote ?? string.Empty
        }).ToList();

        return new ResultadoListagemTiposIngresso { Sucesso = true, Tipos = response };
    }
}

// =====================================================================
//  Resultado — classes de retorno com suporte a StatusCode
// =====================================================================

// 2. Listar lotes
public class ResultadoListagemLotes
{
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
    public int? StatusCode { get; set; }
    public IEnumerable<LoteListaResponse> Lotes { get; set; } = Enumerable.Empty<LoteListaResponse>();
}

// 3. Obter lote
public class ResultadoObterLote
{
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
    public int? StatusCode { get; set; }
    public LoteResponse? Lote { get; set; }
}

// 4. Atualizar lote
public class ResultadoAtualizarLote
{
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
    public int? StatusCode { get; set; }
    public LoteResponse? Lote { get; set; }
}

// 5. Remover lote
public class ResultadoRemoverLote
{
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
    public int? StatusCode { get; set; }
}

// 7. Listar tipos ingresso
public class ResultadoListagemTiposIngresso
{
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
    public int? StatusCode { get; set; }
    public IEnumerable<TipoIngressoResponse> Tipos { get; set; } = Enumerable.Empty<TipoIngressoResponse>();
}
