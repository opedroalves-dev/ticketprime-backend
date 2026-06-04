using TicketPrime.Api.Models;
using TicketPrime.Api.Repositories;

namespace TicketPrime.Api.Services;

public class CupomService
{
    private readonly ICupomRepository _repository;

    public CupomService(ICupomRepository repository)
    {
        _repository = repository;
    }

    public async Task<ResultadoCriacaoCupom> CriarAsync(CupomRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
            return new ResultadoCriacaoCupom { Sucesso = false, Erro = "Código é obrigatório." };

        if (request.Codigo.Length > 50)
            return new ResultadoCriacaoCupom { Sucesso = false, Erro = "Código não pode exceder 50 caracteres." };

        if (request.PorcentagemDesconto <= 0)
            return new ResultadoCriacaoCupom { Sucesso = false, Erro = "PorcentagemDesconto deve ser maior que zero." };

        if (request.ValorMinimoRegra < 0)
            return new ResultadoCriacaoCupom { Sucesso = false, Erro = "ValorMinimoRegra não pode ser negativo." };

        var existe = await _repository.ExisteAsync(request.Codigo);
        if (existe)
            return new ResultadoCriacaoCupom { Sucesso = false, Erro = "Código já existe." };

        var cupom = new Cupom
        {
            Codigo = request.Codigo,
            PorcentagemDesconto = request.PorcentagemDesconto,
            ValorMinimoRegra = request.ValorMinimoRegra
        };

        await _repository.InserirAsync(cupom);

        return new ResultadoCriacaoCupom
        {
            Sucesso = true,
            Codigo = request.Codigo,
            Cupom = cupom
        };
    }
}
