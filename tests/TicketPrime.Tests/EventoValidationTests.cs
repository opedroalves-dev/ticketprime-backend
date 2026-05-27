using TicketPrime.Api.Models;

namespace TicketPrime.Tests;

public class EventoValidationTests
{
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

    [Fact]
    public void EventoRequest_DeveArmazenarValoresCorretamente()
    {
        var data = new DateTime(2025, 12, 31);
        var request = new EventoRequest
        {
            Nome = "Show de Rock",
            CapacidadeTotal = 500,
            DataEvento = data,
            PrecoPadrao = 150m
        };

        Assert.Equal("Show de Rock", request.Nome);
        Assert.Equal(500, request.CapacidadeTotal);
        Assert.Equal(data, request.DataEvento);
        Assert.Equal(150m, request.PrecoPadrao);
    }
}
