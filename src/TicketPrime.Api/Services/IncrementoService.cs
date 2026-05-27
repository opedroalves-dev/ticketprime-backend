using TicketPrime.Api.Models;

namespace TicketPrime.Api.Services;

/// <summary>
/// Serviço com as regras de negócio dos incrementos do TicketPrime.
/// Não utiliza Entity Framework, banco em memória ou TestContainers.
/// </summary>
public class IncrementoService
{
    private static readonly Random _random = new();
    private const string CaracteresPermitidos = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    // ===================================================================
    //  RF01 — Ingresso Digital com Código Único
    // ===================================================================

    /// <summary>
    /// Gera um código único de 8 caracteres alfanuméricos (A-Z, 0-9).
    /// Se houver colisão com códigos existentes, regenera automaticamente.
    /// </summary>
    public string GerarCodigoUnico(List<string> codigosExistentes)
    {
        string codigo;
        do
        {
            codigo = new string(Enumerable.Range(0, 8)
                .Select(_ => CaracteresPermitidos[_random.Next(CaracteresPermitidos.Length)])
                .ToArray());
        } while (codigosExistentes.Contains(codigo, StringComparer.OrdinalIgnoreCase));

        return codigo;
    }

    /// <summary>
    /// Cria um ingresso digital com código único, status "Confirmada" e valores discriminados.
    /// </summary>
    public Ingresso CriarIngressoDigital(
        int reservaId,
        int? tipoIngressoId,
        decimal valorBruto,
        decimal valorDesconto,
        decimal taxaServico,
        decimal valorFinal,
        List<string> codigosExistentes)
    {
        return new Ingresso
        {
            ReservaId = reservaId,
            TipoIngressoId = tipoIngressoId,
            CodigoUnico = GerarCodigoUnico(codigosExistentes),
            Status = "Confirmada",
            ValorBruto = valorBruto,
            ValorDesconto = valorDesconto,
            TaxaServico = taxaServico,
            ValorFinal = valorFinal,
            DataCriacao = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Valida se o código único gerado tem exatamente 8 caracteres alfanuméricos.
    /// </summary>
    public bool ValidarCodigoUnico(string codigo)
    {
        if (string.IsNullOrEmpty(codigo) || codigo.Length != 8)
            return false;

        return codigo.All(c => CaracteresPermitidos.Contains(c));
    }

    // ===================================================================
    //  RF02 — Check-in de Ingresso
    // ===================================================================

    /// <summary>
    /// Realiza o check-in de um ingresso pelo código único.
    /// Regras:
    ///   - Ingresso deve existir e estar com Status = "Confirmada"
    ///   - Cada ingresso só pode ter um único check-in
    ///   - Após check-in, status do ingresso passa para "Utilizada"
    /// </summary>
    public (bool Sucesso, string? Erro, CheckIn? CheckIn) RealizarCheckIn(
        string codigoUnico,
        List<Ingresso> ingressos,
        List<CheckIn> checkInsExistentes)
    {
        // 1. Verificar se o ingresso existe pelo código único
        var ingresso = ingressos.FirstOrDefault(i =>
            i.CodigoUnico.Equals(codigoUnico, StringComparison.OrdinalIgnoreCase));

        if (ingresso is null)
            return (false, "Ingresso não encontrado.", null);

        // 2. Verificar se o status é "Confirmada"
        if (ingresso.Status != "Confirmada")
            return (false, "Ingresso não está confirmado para check-in.", null);

        // 3. Verificar se já existe check-in para este ingresso
        var checkInExistente = checkInsExistentes
            .FirstOrDefault(c => c.IngressoId == ingresso.Id);

        if (checkInExistente is not null)
            return (false, "Ingresso já utilizado.", null);

        // 4. Registrar check-in
        var checkIn = new CheckIn
        {
            IngressoId = ingresso.Id,
            DataCheckIn = DateTime.UtcNow
        };

        // 5. Atualizar status do ingresso para "Utilizada"
        ingresso.Status = "Utilizada";

        return (true, null, checkIn);
    }

    /// <summary>
    /// Verifica se um ingresso pode realizar check-in (status "Confirmada" e sem check-in prévio).
    /// </summary>
    public bool PodeRealizarCheckIn(Ingresso ingresso, List<CheckIn> checkInsExistentes)
    {
        if (ingresso.Status != "Confirmada")
            return false;

        return !checkInsExistentes.Any(c => c.IngressoId == ingresso.Id);
    }

    // ===================================================================
    //  RF03 — Tipos/Lotes de Ingresso
    // ===================================================================

    /// <summary>
    /// Valida os dados de um tipo/lote de ingresso.
    /// </summary>
    public (bool Valido, string? Erro) ValidarTipoIngresso(
        string nome,
        decimal preco,
        int capacidade,
        DateTime dataInicioVenda,
        DateTime dataFimVenda)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return (false, "Nome do tipo de ingresso é obrigatório.");

        if (preco <= 0)
            return (false, "Preço do ingresso deve ser maior que zero.");

        if (capacidade <= 0)
            return (false, "Capacidade do lote deve ser maior que zero.");

        if (dataInicioVenda >= dataFimVenda)
            return (false, "Data de início de venda deve ser anterior à data de fim de venda.");

        return (true, null);
    }

    /// <summary>
    /// Verifica se um lote tem capacidade disponível.
    /// </summary>
    public (bool Disponivel, int VagasRestantes) VerificarDisponibilidadeLote(
        TipoIngresso lote,
        List<Ingresso> ingressosDoLote)
    {
        var reservados = ingressosDoLote
            .Count(i => i.Status == "Confirmada" || i.Status == "Utilizada");

        var vagas = lote.Capacidade - reservados;
        return (vagas > 0, Math.Max(0, vagas));
    }

    // ===================================================================
    //  RF05 — Transparência de Preço / Simulação
    // ===================================================================

    /// <summary>
    /// Simula o preço de uma reserva com discriminação completa dos valores.
    /// Regras:
    ///   - PrecoBase = valor base do ingresso
    ///   - TaxaServico = PrecoBase × 0,10 (10%)
    ///   - ValorDesconto = aplicado conforme regra de cupom
    ///   - ValorFinal = PrecoBase + TaxaServico - ValorDesconto
    /// </summary>
    public SimulacaoPrecoResponse SimularPreco(
        decimal precoBase,
        string? codigoCupom,
        List<Cupom> cupons)
    {
        decimal taxaServico = Math.Round(precoBase * 0.10m, 2);
        decimal valorDesconto = 0m;

        if (!string.IsNullOrWhiteSpace(codigoCupom))
        {
            var cupom = cupons.FirstOrDefault(c =>
                c.Codigo.Equals(codigoCupom, StringComparison.OrdinalIgnoreCase));

            if (cupom is not null && precoBase >= cupom.ValorMinimoRegra)
            {
                valorDesconto = Math.Round(precoBase * (cupom.PorcentagemDesconto / 100m), 2);
            }
        }

        decimal valorFinal = precoBase + taxaServico - valorDesconto;

        return new SimulacaoPrecoResponse
        {
            PrecoBase = precoBase,
            TaxaServico = taxaServico,
            ValorDesconto = valorDesconto,
            ValorFinal = Math.Round(valorFinal, 2)
        };
    }

    // ===================================================================
    //  RF04 — Carrinho/Reserva Temporária
    // ===================================================================

    /// <summary>
    /// Valida se um carrinho pode ser confirmado.
    /// Regras:
    ///   - Carrinho deve estar com Status = "Ativo"
    ///   - DataExpiracao deve ser posterior ao momento atual
    /// </summary>
    public (bool Valido, string? Erro) ValidarCarrinhoParaConfirmacao(Carrinho carrinho)
    {
        if (carrinho.Status != "Ativo")
            return (false, $"Carrinho não está ativo. Status atual: {carrinho.Status}");

        if (carrinho.DataExpiracao <= DateTime.UtcNow)
            return (false, "Carrinho expirado. O prazo de 15 minutos para confirmação foi ultrapassado.");

        return (true, null);
    }

    /// <summary>
    /// Verifica se o carrinho está expirado comparando a data de expiração com o momento atual.
    /// </summary>
    public bool CarrinhoEstaExpirado(Carrinho carrinho)
    {
        return carrinho.Status != "Ativo" || carrinho.DataExpiracao <= DateTime.UtcNow;
    }

    // ===================================================================
    //  RF06 — Dashboard/Admin de Eventos
    // ===================================================================

    /// <summary>
    /// Calcula as métricas de dashboard para um evento específico.
    /// Métricas: total de ingressos vendidos, receita, % ocupação, check-ins.
    /// </summary>
    public DashboardEventoDetalhadoResponse CalcularDashboardEvento(
        Evento evento,
        List<TipoIngresso> tiposIngresso,
        List<Ingresso> ingressos,
        List<CheckIn> checkIns)
    {
        var ingressosVendidos = ingressos
            .Where(i => i.Status == "Confirmada" || i.Status == "Utilizada")
            .ToList();

        int totalVendidos = ingressosVendidos.Count;
        decimal receitaTotal = ingressosVendidos.Sum(i => i.ValorFinal);
        decimal percentualOcupacao = evento.CapacidadeTotal > 0
            ? Math.Round((decimal)totalVendidos / evento.CapacidadeTotal * 100, 2)
            : 0m;

        int totalCheckIns = checkIns.Count;

        int pendentesCheckIn = ingressos
            .Count(i => i.Status == "Confirmada"
                && !checkIns.Any(c => c.IngressoId == i.Id));

        int totalCancelados = ingressos.Count(i => i.Status == "Cancelada");

        var lotesResponse = tiposIngresso.Select(ti =>
        {
            var ingressosLote = ingressos
                .Where(i => i.TipoIngressoId == ti.Id)
                .ToList();

            var vendidosLote = ingressosLote
                .Where(i => i.Status == "Confirmada" || i.Status == "Utilizada")
                .ToList();

            var checkInsLote = checkIns
                .Where(c => ingressosLote.Any(i => i.Id == c.IngressoId))
                .ToList();

            return new DashboardLoteResponse
            {
                TipoIngressoId = ti.Id,
                NomeLote = ti.Nome,
                PrecoAtual = ti.Preco,
                CapacidadeLote = ti.Capacidade,
                TaxaServico = ti.TaxaServico,
                IngressosVendidos = vendidosLote.Count,
                CapacidadeRestante = ti.Capacidade - vendidosLote.Count,
                ReceitaLote = vendidosLote.Sum(i => i.ValorFinal),
                CheckInsRealizados = checkInsLote.Count
            };
        }).ToList();

        return new DashboardEventoDetalhadoResponse
        {
            EventoId = evento.Id,
            NomeEvento = evento.Nome,
            DataEvento = evento.DataEvento,
            CapacidadeTotal = evento.CapacidadeTotal,
            PrecoPadrao = evento.PrecoPadrao,
            TotalIngressosVendidos = totalVendidos,
            ReceitaTotal = receitaTotal,
            PercentualOcupacao = percentualOcupacao,
            TotalCheckIns = totalCheckIns,
            PendentesCheckIn = pendentesCheckIn,
            TotalCancelados = totalCancelados,
            Lotes = lotesResponse
        };
    }

    /// <summary>
    /// Calcula as métricas resumidas de lista de eventos para o dashboard.
    /// </summary>
    public List<DashboardEventoListaResponse> CalcularDashboardLista(
        List<Evento> eventos,
        List<TipoIngresso> tiposIngresso,
        List<Ingresso> ingressos,
        List<CheckIn> checkIns)
    {
        return eventos.Select(e =>
        {
            var tiposDoEvento = tiposIngresso.Where(t => t.EventoId == e.Id).ToList();
            var idsTipos = tiposDoEvento.Select(t => t.Id).ToList();
            var ingressosDoEvento = ingressos
                .Where(i => i.TipoIngressoId.HasValue && idsTipos.Contains(i.TipoIngressoId.Value))
                .ToList();

            var vendidos = ingressosDoEvento
                .Where(i => i.Status == "Confirmada" || i.Status == "Utilizada")
                .ToList();

            var idsIngressosEvento = ingressosDoEvento.Select(i => i.Id).ToList();
            var checkInsEvento = checkIns
                .Where(c => idsIngressosEvento.Contains(c.IngressoId))
                .ToList();

            return new DashboardEventoListaResponse
            {
                EventoId = e.Id,
                NomeEvento = e.Nome,
                DataEvento = e.DataEvento,
                CapacidadeTotal = e.CapacidadeTotal,
                PrecoPadrao = e.PrecoPadrao,
                TotalIngressosVendidos = vendidos.Count,
                ReceitaTotal = vendidos.Sum(i => i.ValorFinal),
                PercentualOcupacao = e.CapacidadeTotal > 0
                    ? Math.Round((decimal)vendidos.Count / e.CapacidadeTotal * 100, 2)
                    : 0m,
                TotalCheckIns = checkInsEvento.Count,
                PendentesCheckIn = ingressosDoEvento
                    .Count(i => i.Status == "Confirmada"
                        && !checkIns.Any(c => c.IngressoId == i.Id)),
                TotalCancelados = ingressosDoEvento.Count(i => i.Status == "Cancelada")
            };
        }).ToList();
    }
}
