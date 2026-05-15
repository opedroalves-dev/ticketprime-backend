using TicketPrime.Api.Models;

namespace TicketPrime.Tests;

public class UsuarioValidationTests
{
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
}
