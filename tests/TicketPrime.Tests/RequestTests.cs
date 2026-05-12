namespace TicketPrime.Tests;

public class RequestTests
{
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

    [Fact]
    public void UsuarioRequest_DeveArmazenarValoresCorretamente()
    {
        var request = new UsuarioRequest("12345678901", "João Silva", "joao@email.com");

        Assert.Equal("12345678901", request.Cpf);
        Assert.Equal("João Silva", request.Nome);
        Assert.Equal("joao@email.com", request.Email);
    }

    [Fact]
    public void UsuarioRequest_DeveCompararPorValor()
    {
        var request1 = new UsuarioRequest("12345678901", "João Silva", "joao@email.com");
        var request2 = new UsuarioRequest("12345678901", "João Silva", "joao@email.com");

        Assert.Equal(request1, request2);
    }

    [Fact]
    public void UsuarioRequest_DeveCompararPorValorComDadosDiferentes()
    {
        var request1 = new UsuarioRequest("12345678901", "João Silva", "joao@email.com");
        var request2 = new UsuarioRequest("99999999999", "Maria Souza", "maria@email.com");

        Assert.NotEqual(request1, request2);
    }

    [Fact]
    public void EventoRequest_DeveArmazenarValoresCorretamente()
    {
        var data = new DateTime(2025, 12, 31);
        var request = new EventoRequest("Show de Rock", 500, data, 150m);

        Assert.Equal("Show de Rock", request.Nome);
        Assert.Equal(500, request.CapacidadeTotal);
        Assert.Equal(data, request.DataEvento);
        Assert.Equal(150m, request.PrecoPadrao);
    }

    [Fact]
    public void EventoRequest_DeveCompararPorValor()
    {
        var data = new DateTime(2025, 12, 31);
        var request1 = new EventoRequest("Show de Rock", 500, data, 150m);
        var request2 = new EventoRequest("Show de Rock", 500, data, 150m);

        Assert.Equal(request1, request2);
    }
}
