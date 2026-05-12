using TicketPrime.Api.Models;

namespace TicketPrime.Tests;

public class ModelsTests
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
    public void Usuario_DeveInicializarComValoresPadrao()
    {
        var usuario = new Usuario();

        Assert.Equal(string.Empty, usuario.Cpf);
        Assert.Equal(string.Empty, usuario.Nome);
        Assert.Equal(string.Empty, usuario.Email);
    }

    [Fact]
    public void Usuario_DevePermitirAtribuirPropriedades()
    {
        var usuario = new Usuario
        {
            Cpf = "12345678901",
            Nome = "João Silva",
            Email = "joao@email.com"
        };

        Assert.Equal("12345678901", usuario.Cpf);
        Assert.Equal("João Silva", usuario.Nome);
        Assert.Equal("joao@email.com", usuario.Email);
    }

    [Fact]
    public void Evento_DeveInicializarComValoresPadrao()
    {
        var evento = new Evento();

        Assert.Equal(0, evento.Id);
        Assert.Equal(string.Empty, evento.Nome);
        Assert.Equal(0, evento.CapacidadeTotal);
        Assert.Equal(0m, evento.PrecoPadrao);
    }

    [Fact]
    public void Evento_DevePermitirAtribuirPropriedades()
    {
        var evento = new Evento
        {
            Id = 1,
            Nome = "Show de Rock",
            CapacidadeTotal = 500,
            DataEvento = new DateTime(2025, 12, 31),
            PrecoPadrao = 150m
        };

        Assert.Equal(1, evento.Id);
        Assert.Equal("Show de Rock", evento.Nome);
        Assert.Equal(500, evento.CapacidadeTotal);
        Assert.Equal(new DateTime(2025, 12, 31), evento.DataEvento);
        Assert.Equal(150m, evento.PrecoPadrao);
    }
}
