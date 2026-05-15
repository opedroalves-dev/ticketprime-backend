using TicketPrime.Api.Models;

namespace TicketPrime.Tests;

public class CupomValidationTests
{
    [Fact]
    public void Cupom_DeveInicializarComValoresPadrao()
    {
        var cupom = new Cupom();

        Assert.Equal(string.Empty, cupom.Codigo);
        Assert.Equal(0m, cupom.PorcentagemDesconto);
        Assert.Equal(0m, cupom.ValorMinimoRegra);
    }

    [Fact]
    public void Cupom_DevePermitirAtribuirPropriedades()
    {
        var cupom = new Cupom
        {
            Codigo = "DESC10",
            PorcentagemDesconto = 10m,
            ValorMinimoRegra = 50m
        };

        Assert.Equal("DESC10", cupom.Codigo);
        Assert.Equal(10m, cupom.PorcentagemDesconto);
        Assert.Equal(50m, cupom.ValorMinimoRegra);
    }

    [Fact]
    public void CupomRequest_DeveArmazenarValoresCorretamente()
    {
        var request = new CupomRequest("DESC10", 10m, 50m);

        Assert.Equal("DESC10", request.Codigo);
        Assert.Equal(10m, request.PorcentagemDesconto);
        Assert.Equal(50m, request.ValorMinimoRegra);
    }

    [Fact]
    public void CupomRequest_DeveCompararPorValor()
    {
        var request1 = new CupomRequest("DESC10", 10m, 50m);
        var request2 = new CupomRequest("DESC10", 10m, 50m);

        Assert.Equal(request1, request2);
    }

    [Fact]
    public void CupomRequest_DeveRetornarStringsDiferentesParaCodigosDiferentes()
    {
        var request1 = new CupomRequest("DESC10", 10m, 50m);
        var request2 = new CupomRequest("DESC20", 20m, 100m);

        Assert.NotEqual(request1.ToString(), request2.ToString());
    }
}
