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

}
