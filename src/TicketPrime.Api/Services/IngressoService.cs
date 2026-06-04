using TicketPrime.Api.Models;
using TicketPrime.Api.Repositories;

namespace TicketPrime.Api.Services;

/// <summary>
/// Service de Ingressos.
/// Orquestra validação, regras de negócio e chamadas aos repositórios.
/// Criado na Etapa 8 para extrair os 3 endpoints de Ingressos do Program.cs.
/// </summary>
public class IngressoService
{
    private readonly IIngressoRepository _ingressoRepository;
    private readonly IReservaRepository _reservaRepository;
    private readonly IEventoRepository _eventoRepository;
    private readonly ICupomRepository _cupomRepository;

    public IngressoService(
        IIngressoRepository ingressoRepository,
        IReservaRepository reservaRepository,
        IEventoRepository eventoRepository,
        ICupomRepository cupomRepository)
    {
        _ingressoRepository = ingressoRepository;
        _reservaRepository = reservaRepository;
        _eventoRepository = eventoRepository;
        _cupomRepository = cupomRepository;
    }

    /// <summary>
    /// Gera um ingresso para uma reserva existente.
    /// Corresponde ao endpoint POST /api/reservas/{id}/ingresso.
    /// </summary>
    /// <returns>
    /// (Response, Erro, StatusCode) onde:
    /// - Response é o IngressoResponse em caso de sucesso (201)
    /// - Erro e StatusCode indicam o motivo da falha (404, 409)
    /// </returns>
    public async Task<(IngressoResponse? Response, string? Erro, int StatusCode)> GerarIngressoAsync(int reservaId)
    {
        // 1. Verificar se reserva existe
        var reserva = await _reservaRepository.ObterPorIdAsync(reservaId);

        if (reserva is null)
            return (null, "Reserva não encontrada.", 404);

        // 2. Verificar se reserva já possui ingresso
        var ingressoExistente = await _ingressoRepository.ObterPorReservaIdAsync(reservaId);

        if (ingressoExistente is not null)
            return (null, "Reserva já possui ingresso gerado.", 409);

        // 3. Obter evento vinculado
        var evento = await _eventoRepository.ObterPorIdAsync(reserva.EventoId);

        if (evento is null)
            return (null, "Evento vinculado à reserva não encontrado.", 404);

        // 4. Gerar código único
        var codigoUnico = await _ingressoRepository.GerarCodigoUnicoAsync();

        // 5. Calcular valores
        decimal valorBruto = evento.PrecoPadrao;
        decimal valorDesconto = 0;
        decimal taxaServico = 0;
        decimal valorFinal = valorBruto;

        // 6. Se a reserva usou cupom, calcular desconto
        if (!string.IsNullOrWhiteSpace(reserva.CupomUtilizado))
        {
            var cupom = await _cupomRepository.ObterPorCodigoAsync(reserva.CupomUtilizado);

            if (cupom is not null && evento.PrecoPadrao >= cupom.ValorMinimoRegra)
            {
                valorDesconto = evento.PrecoPadrao * cupom.PorcentagemDesconto / 100m;
            }
        }

        valorFinal = valorBruto - valorDesconto;

        // 7. Inserir ingresso
        var ingresso = new Ingresso
        {
            ReservaId = reservaId,
            TipoIngressoId = null,
            CodigoUnico = codigoUnico,
            Status = "Confirmada",
            ValorBruto = valorBruto,
            ValorDesconto = valorDesconto,
            TaxaServico = taxaServico,
            ValorFinal = valorFinal
        };

        var (ingressoId, dataCriacao) = await _ingressoRepository.InserirAsync(ingresso);

        // 8. Montar response
        var response = new IngressoResponse
        {
            Id = ingressoId,
            ReservaId = reservaId,
            TipoIngressoId = null,
            CodigoUnico = codigoUnico,
            Status = "Confirmada",
            ValorBruto = valorBruto,
            ValorDesconto = valorDesconto,
            TaxaServico = taxaServico,
            ValorFinal = valorFinal,
            DataCriacao = dataCriacao
        };

        return (response, null, 201);
    }

    /// <summary>
    /// Consulta ingresso por código único (8 caracteres) ou por ID da reserva (numérico).
    /// Corresponde ao endpoint GET /api/ingressos/{param}.
    /// </summary>
    public async Task<(object? Response, string? Erro, int StatusCode)> ConsultarIngressoAsync(string param)
    {
        // Se o parâmetro for numérico, consulta por ReservaId
        if (int.TryParse(param, out int reservaId))
        {
            // Verificar se a reserva existe
            var reserva = await _reservaRepository.ObterPorIdAsync(reservaId);

            if (reserva is null)
                return (null, "Reserva não encontrada.", 404);

            // Buscar ingresso vinculado à reserva com dados do evento
            var ingresso = await _ingressoRepository.ObterDetalhadoPorReservaIdAsync(reservaId);

            if (ingresso is null)
                return (null, "Nenhum ingresso gerado para esta reserva.", 404);

            return (ingresso, null, 200);
        }

        // Caso contrário, consulta pelo código único de 8 caracteres
        if (param.Length != 8)
            return (null, "Código deve ter 8 caracteres.", 400);

        var resultados = await _ingressoRepository.ObterDetalhadoPorCodigoAsync(param);
        var ingressoDetalhado = resultados.FirstOrDefault();

        if (ingressoDetalhado is null)
            return (null, "Ingresso não encontrado.", 404);

        return (ingressoDetalhado, null, 200);
    }

    /// <summary>
    /// Consulta ingresso por reserva.
    /// Corresponde ao endpoint GET /api/reservas/{id}/ingresso.
    /// </summary>
    public async Task<(IngressoResponse? Response, string? Erro, int StatusCode)> ObterPorReservaAsync(int reservaId)
    {
        var reserva = await _reservaRepository.ObterPorIdAsync(reservaId);

        if (reserva is null)
            return (null, "Reserva não encontrada.", 404);

        var ingresso = await _ingressoRepository.ObterPorReservaIdAsync(reservaId);

        if (ingresso is null)
            return (null, "Nenhum ingresso gerado para esta reserva.", 404);

        var response = new IngressoResponse
        {
            Id = ingresso.Id,
            ReservaId = ingresso.ReservaId,
            TipoIngressoId = ingresso.TipoIngressoId,
            CodigoUnico = ingresso.CodigoUnico,
            Status = ingresso.Status,
            ValorBruto = ingresso.ValorBruto,
            ValorDesconto = ingresso.ValorDesconto,
            TaxaServico = ingresso.TaxaServico,
            ValorFinal = ingresso.ValorFinal,
            DataCriacao = ingresso.DataCriacao
        };

        return (response, null, 200);
    }
}
