using System.Data;
using System.Linq;
using TicketPrime.Api.Middleware;
using TicketPrime.Api.Models;
using TicketPrime.Api.Repositories;

namespace TicketPrime.Api.Services;

/// <summary>
/// Service responsável pelas operações CRUD não transacionais do domínio Carrinho.
/// Não retorna IResult (preserva separação de responsabilidades com a camada de endpoints).
/// Não contém SQL (delegado ao repositório).
/// Criado na Etapa 11a — faz parte da correção C1 (separar CRUD de confirmação).
/// Expandido na Etapa 11b com ConfirmarAsync (fluxo transacional de confirmação).
/// </summary>
public class CarrinhoService
{
    private readonly ICarrinhoRepository _carrinhoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IEventoRepository _eventoRepository;
    private readonly ITipoIngressoRepository _tipoIngressoRepository;
    private readonly IIngressoRepository _ingressoRepository;
    private readonly IReservaRepository _reservaRepository;
    private readonly ICupomRepository _cupomRepository;
    private readonly IDbConnection _db;

    public CarrinhoService(
        ICarrinhoRepository carrinhoRepository,
        IUsuarioRepository usuarioRepository,
        IEventoRepository eventoRepository,
        ITipoIngressoRepository tipoIngressoRepository,
        IIngressoRepository ingressoRepository,
        IReservaRepository reservaRepository,
        ICupomRepository cupomRepository,
        IDbConnection db)
    {
        _carrinhoRepository = carrinhoRepository;
        _usuarioRepository = usuarioRepository;
        _eventoRepository = eventoRepository;
        _tipoIngressoRepository = tipoIngressoRepository;
        _ingressoRepository = ingressoRepository;
        _reservaRepository = reservaRepository;
        _cupomRepository = cupomRepository;
        _db = db;
    }

    // ---------------------------------------------------------------
    // a) Criar carrinho vazio (substitui POST /api/carrinho)
    // ---------------------------------------------------------------
    public async Task<(CarrinhoResponse? Response, string? Erro)> CriarAsync(CriarCarrinhoRequest request)
    {
        // 1. Validar CPF
        if (string.IsNullOrWhiteSpace(request.UsuarioCpf))
            return (null, "CPF do usuário é obrigatório.");

        if (request.UsuarioCpf.Length != 11 || !request.UsuarioCpf.All(char.IsDigit))
            return (null, "CPF deve conter 11 dígitos numéricos.");

        // 2. Buscar usuário
        var usuario = await _usuarioRepository.ObterPorCpfAsync(request.UsuarioCpf);
        if (usuario is null)
            return (null, "Usuário não encontrado para o CPF informado.");

        // 3. Verificar carrinho ativo existente
        if (await _carrinhoRepository.ExisteAtivoPorCpfAsync(request.UsuarioCpf))
        {
            var carrinhoAtivo = await _carrinhoRepository.ObterAtivoPorCpfAsync(request.UsuarioCpf);

            if (carrinhoAtivo is not null)
            {
                // Se expirou, marcar como expirado e permitir criar novo
                if (carrinhoAtivo.DataExpiracao <= DateTime.Now)
                {
                    await _carrinhoRepository.AtualizarStatusAsync(carrinhoAtivo.Id, "Expirado");
                }
                else
                {
                    return (null, "Já existe um carrinho ativo para este CPF.");
                }
            }
        }

        // 4. Criar novo carrinho
        var carrinhoId = await _carrinhoRepository.CriarAsync(request.UsuarioCpf);

        // 5. Montar response
        var response = await ConstruirResponseAsync(carrinhoId);
        return (response, null);
    }

    // ---------------------------------------------------------------
    // b) Adicionar itens ao carrinho (substitui POST /api/carrinho/{id}/itens)
    // ---------------------------------------------------------------
    public async Task<(CarrinhoResponse? Response, string? Erro, int StatusCode)> AdicionarItensAsync(
        int carrinhoId, AdicionarItensRequest request)
    {
        // 1. Buscar carrinho
        var carrinho = await _carrinhoRepository.ObterPorIdAsync(carrinhoId);
        if (carrinho is null)
            return (null, "Carrinho não encontrado.", 404);

        // 2. Validar status
        if (carrinho.Status != "Ativo")
            return (null, "Carrinho não está ativo.", 400);

        // 3. Validar expiração
        if (carrinho.DataExpiracao <= DateTime.Now)
        {
            await _carrinhoRepository.AtualizarStatusAsync(carrinho.Id, "Expirado");
            return (null, "Carrinho expirado. Crie um novo carrinho.", 400);
        }

        // 4. Validar lista de itens
        if (request.Itens is null || request.Itens.Count == 0)
            return (null, "Carrinho deve conter ao menos um item.", 400);

        // 5. Processar cada item
        for (int i = 0; i < request.Itens.Count; i++)
        {
            var item = request.Itens[i];

            // 5a. Validar EventoId
            if (item.EventoId <= 0)
                return (null, "EventoId é obrigatório para cada item.", 400);

            // 5b. Buscar evento
            var evento = await _eventoRepository.ObterPorIdAsync(item.EventoId);
            if (evento is null)
                return (null, "Evento não encontrado para o Id informado.", 400);

            // 5c. Validar Quantidade
            if (item.Quantidade <= 0)
                return (null, "Quantidade deve ser maior que zero.", 400);

            // 5d. Determinar PrecoUnitario
            decimal precoUnitario = evento.PrecoPadrao;

            if (item.TipoIngressoId.HasValue)
            {
                var tipoIngresso = await _tipoIngressoRepository.ObterPorIdAsync(item.TipoIngressoId.Value);
                if (tipoIngresso is null)
                    return (null, "Tipo de ingresso não encontrado.", 400);

                if (tipoIngresso.EventoId != item.EventoId)
                    return (null, "Tipo de ingresso não pertence ao evento informado.", 400);

                // Verificar disponibilidade do lote
                var vendidos = await _ingressoRepository.ContarPorTipoAsync(item.TipoIngressoId.Value);
                var reservadosCarrinho = await _carrinhoRepository
                    .ObterQuantidadeReservadaPorTipoEmCarrinhosAtivosAsync(item.TipoIngressoId.Value, carrinho.Id);

                if (vendidos + reservadosCarrinho + item.Quantidade > tipoIngresso.Capacidade)
                    return (null, "Capacidade insuficiente no lote informado.", 400);

                precoUnitario = tipoIngresso.Preco;
            }

            // 5e. Verificar limite de 2 reservas por CPF por evento
            var reservasCpfEvento = await _reservaRepository.ContarPorCpfEEventoAsync(
                carrinho.UsuarioCpf, item.EventoId);

            if (reservasCpfEvento >= 2)
                return (null, $"CPF já possui o limite máximo de 2 reservas para o evento {evento.Id}.", 400);

            // 5f. Inserir item
            var novoItem = new CarrinhoItem
            {
                CarrinhoId = carrinho.Id,
                EventoId = item.EventoId,
                TipoIngressoId = item.TipoIngressoId,
                Quantidade = item.Quantidade,
                PrecoUnitario = precoUnitario
            };

            await _carrinhoRepository.InserirItemAsync(novoItem);
        }

        // 6. Montar response
        var response = await ConstruirResponseAsync(carrinho.Id);
        return (response, null, 200);
    }

    // ---------------------------------------------------------------
    // c) Visualizar carrinho ativo (substitui GET /api/carrinho/{cpf})
    // ---------------------------------------------------------------
    public async Task<(CarrinhoResponse? Response, string? Erro)> ObterAtivoAsync(string cpf)
    {
        // 1. Validar formato do CPF
        if (cpf.Length != 11 || !cpf.All(char.IsDigit))
            return (null, "CPF deve conter 11 dígitos numéricos.");

        // 2. Buscar carrinho (ativo ou expirado)
        var carrinho = await _carrinhoRepository.ObterAtivoOuExpiradoPorCpfAsync(cpf);
        if (carrinho is null)
            return (null, "Nenhum carrinho ativo encontrado para este CPF.");

        // 3. Se expirou, atualizar status
        if (carrinho.Status == "Ativo" && carrinho.DataExpiracao <= DateTime.Now)
        {
            await _carrinhoRepository.AtualizarStatusAsync(carrinho.Id, "Expirado");
            carrinho.Status = "Expirado";
        }

        // 4. Montar response
        var response = await ConstruirResponseAsync(carrinho.Id);

        if (carrinho.Status == "Expirado")
        {
            response.Mensagem = "Carrinho expirado. Crie um novo carrinho para continuar.";
            response.MinutosRestantes = 0;
        }

        return (response, null);
    }

    // ---------------------------------------------------------------
    // d) Cancelar/limpar carrinho (substitui DELETE /api/carrinho/{cpf})
    // ---------------------------------------------------------------
    public async Task<(bool Sucesso, string? Erro)> CancelarAsync(string cpf)
    {
        // 1. Validar formato do CPF
        if (string.IsNullOrWhiteSpace(cpf))
            return (false, "CPF do usuário é obrigatório.");

        if (cpf.Length != 11 || !cpf.All(char.IsDigit))
            return (false, "CPF deve conter 11 dígitos numéricos.");

        // 2. Buscar carrinho ativo
        var carrinho = await _carrinhoRepository.ObterAtivoPorCpfAsync(cpf);
        if (carrinho is null)
            return (false, "Nenhum carrinho ativo encontrado para este CPF.");

        // 3. Validar expiração
        if (carrinho.DataExpiracao <= DateTime.Now)
        {
            await _carrinhoRepository.AtualizarStatusAsync(carrinho.Id, "Expirado");
            return (false, "Carrinho já expirou.");
        }

        // 4. Remover itens e marcar carrinho como expirado
        await _carrinhoRepository.RemoverItensAsync(carrinho.Id);
        await _carrinhoRepository.AtualizarStatusAsync(carrinho.Id, "Expirado");

        return (true, null);
    }

    // ---------------------------------------------------------------
    // e) Confirmar carrinho (substitui POST /api/carrinho/{cpf}/confirmar)
    // ---------------------------------------------------------------
    public async Task<(CarrinhoConfirmacaoResponse? Response, string? Erro, int StatusCode)>
        ConfirmarAsync(string cpf, ConfirmarCarrinhoRequest? request)
    {
        // ---------------------------------------------------------------
        // Validações pré-transação (fora da transação)
        // ---------------------------------------------------------------

        // 1. Validar CPF
        if (cpf.Length != 11 || !cpf.All(char.IsDigit))
            return (null, "CPF deve conter 11 dígitos numéricos.", 400);

        // 2. Extrair cupom (request é opcional)
        var cupomUtilizado = request?.CupomUtilizado;

        // 3. Buscar carrinho ativo
        var carrinho = await _carrinhoRepository.ObterAtivoPorCpfAsync(cpf);
        if (carrinho is null)
            return (null, "Nenhum carrinho ativo encontrado para este CPF.", 404);

        // 4. Validar expiração
        if (carrinho.DataExpiracao <= DateTime.Now)
            return (null, "Carrinho expirado. Crie um novo carrinho.", 400);

        // 5. Verificar se carrinho possui itens
        var totalItens = await _carrinhoRepository.ContarItensAsync(carrinho.Id);
        if (totalItens == 0)
            return (null, "Carrinho vazio. Adicione itens antes de confirmar.", 400);

        // ---------------------------------------------------------------
        // Transação
        // ---------------------------------------------------------------
        if (_db.State != ConnectionState.Open)
            _db.Open();

        using var transaction = _db.BeginTransaction();
        try
        {
            // 6. Validar cupom se informado
            Cupom? cupom = null;
            if (!string.IsNullOrWhiteSpace(cupomUtilizado))
            {
                cupom = await _cupomRepository.ObterPorCodigoAsync(cupomUtilizado, transaction);
                if (cupom is null)
                    throw new ValidationException("Cupom não encontrado.");
            }

            // 7. Obter itens do carrinho (via repository — sem SQL direto)
            var itensCarrinho = await _carrinhoRepository
                .ObterItensPorCarrinhoIdAsync(carrinho.Id, transaction);

            var reservasCriadas = new List<ReservaConfirmadaResponse>();
            decimal totalPago = 0;

            // 8. Processar cada item
            foreach (var item in itensCarrinho)
            {
                // 8a. Buscar evento
                var evento = await _eventoRepository.ObterPorIdAsync(item.EventoId, transaction);
                if (evento is null)
                    throw new ValidationException($"Evento {item.EventoId} não encontrado.");

                // 8b. Obter nome do lote se TipoIngressoId informado
                string nomeLote = "";
                if (item.TipoIngressoId.HasValue)
                {
                    var lote = await _tipoIngressoRepository
                        .ObterPorIdAsync(item.TipoIngressoId.Value, transaction);
                    nomeLote = lote?.Nome ?? "";
                }

                // 8c. Para cada unidade do item
                for (int q = 0; q < item.Quantidade; q++)
                {
                    // 8c.1. Verificar limite de 2 reservas por CPF/evento
                    var reservasCpfEvento = await _reservaRepository
                        .ContarPorCpfEEventoAsync(carrinho.UsuarioCpf, item.EventoId, transaction);

                    if (reservasCpfEvento >= 2)
                        throw new ValidationException(
                            $"CPF já possui o limite máximo de 2 reservas para o evento {item.EventoId}.");

                    // 8c.2. Verificar capacidade do lote
                    if (item.TipoIngressoId.HasValue)
                    {
                        var lote = await _tipoIngressoRepository
                            .ObterPorIdAsync(item.TipoIngressoId.Value, transaction);

                        if (lote is not null)
                        {
                            var vendidos = await _ingressoRepository
                                .ContarPorTipoAsync(item.TipoIngressoId.Value, transaction);

                            if (vendidos >= lote.Capacidade)
                                throw new ValidationException(
                                    $"Capacidade insuficiente no lote {item.TipoIngressoId}.");
                        }
                    }

                    // 8c.3. Calcular valor
                    decimal valorBruto = item.PrecoUnitario;
                    decimal valorDesconto = 0;
                    decimal taxaServico = 0;
                    decimal valorFinal = valorBruto;

                    if (cupom is not null && evento.PrecoPadrao >= cupom.ValorMinimoRegra)
                    {
                        valorDesconto = valorBruto * cupom.PorcentagemDesconto / 100m;
                        valorFinal = valorBruto - valorDesconto;
                    }

                    // 8c.4. Inserir reserva
                    var reserva = new Reserva
                    {
                        UsuarioCpf = carrinho.UsuarioCpf,
                        EventoId = item.EventoId,
                        CupomUtilizado = cupom?.Codigo,
                        ValorFinalPago = valorFinal
                    };

                    var reservaId = await _reservaRepository.InserirAsync(reserva, transaction);

                    // 8c.5. Gerar código único para o ingresso
                    var codigoUnico = await _ingressoRepository
                        .GerarCodigoUnicoAsync(transaction, 30);

                    // 8c.6. Inserir ingresso
                    var ingresso = new Ingresso
                    {
                        ReservaId = reservaId,
                        TipoIngressoId = item.TipoIngressoId,
                        CodigoUnico = codigoUnico,
                        Status = "Confirmada",
                        ValorBruto = valorBruto,
                        ValorDesconto = valorDesconto,
                        TaxaServico = taxaServico,
                        ValorFinal = valorFinal,
                        DataCriacao = DateTime.Now
                    };

                    var (ingressoId, _) = await _ingressoRepository.InserirAsync(ingresso, transaction);

                    // 8c.7. Adicionar ao response
                    reservasCriadas.Add(new ReservaConfirmadaResponse
                    {
                        ReservaId = reservaId,
                        IngressoId = ingressoId,
                        CodigoUnico = codigoUnico,
                        EventoId = item.EventoId,
                        NomeEvento = evento.Nome,
                        TipoIngresso = nomeLote,
                        ValorFinal = valorFinal,
                        Status = "Confirmada"
                    });

                    totalPago += valorFinal;
                }
            }

            // 9. Marcar carrinho como Confirmado
            await _carrinhoRepository.AtualizarStatusAsync(carrinho.Id, "Confirmado", transaction);

            // 10. Limpar itens do carrinho
            await _carrinhoRepository.RemoverItensAsync(carrinho.Id, transaction);

            // 11. Commit
            transaction.Commit();

            // 12. Montar response
            var response = new CarrinhoConfirmacaoResponse
            {
                Mensagem = "Carrinho confirmado com sucesso.",
                CarrinhoId = carrinho.Id,
                ReservasCriadas = reservasCriadas,
                TotalPago = totalPago
            };

            return (response, null, 201);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // ---------------------------------------------------------------
    // Método auxiliar: Construir CarrinhoResponse
    // (extraído da função inline ConstruirCarrinhoResponseAsync do Program.cs)
    // ---------------------------------------------------------------
    private async Task<CarrinhoResponse> ConstruirResponseAsync(int carrinhoId)
    {
        var carrinho = await _carrinhoRepository.ObterPorIdAsync(carrinhoId)
            ?? throw new InvalidOperationException($"Carrinho {carrinhoId} não encontrado ao construir response.");

        var itens = (await _carrinhoRepository.ObterItensResponseAsync(carrinhoId)).ToList();
        var total = itens.Sum(i => i.Subtotal);

        var minutosRestantes = carrinho.Status == "Ativo"
            ? Math.Max(0, (int)(carrinho.DataExpiracao - DateTime.Now).TotalMinutes)
            : 0;

        return new CarrinhoResponse
        {
            CarrinhoId = carrinho.Id,
            UsuarioCpf = carrinho.UsuarioCpf,
            Status = carrinho.Status,
            DataCriacao = carrinho.DataCriacao,
            DataExpiracao = carrinho.DataExpiracao,
            MinutosRestantes = minutosRestantes,
            Itens = itens,
            Total = total
        };
    }
}
