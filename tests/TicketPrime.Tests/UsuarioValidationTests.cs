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
        var request = new UsuarioRequest
        {
            Cpf = "12345678901",
            Nome = "João Silva",
            Email = "joao@email.com"
        };

        Assert.Equal("12345678901", request.Cpf);
        Assert.Equal("João Silva", request.Nome);
        Assert.Equal("joao@email.com", request.Email);
    }

    [Fact]
    public void UsuarioRequest_DeveCompararPropriedades()
    {
        var request1 = new UsuarioRequest
        {
            Cpf = "12345678901",
            Nome = "João Silva",
            Email = "joao@email.com"
        };
        var request2 = new UsuarioRequest
        {
            Cpf = "12345678901",
            Nome = "João Silva",
            Email = "joao@email.com"
        };

        Assert.Equal(request1.Cpf, request2.Cpf);
        Assert.Equal(request1.Nome, request2.Nome);
        Assert.Equal(request1.Email, request2.Email);
    }

    [Fact]
    public void UsuarioRequest_DeveCompararPropriedadesDiferentes()
    {
        var request1 = new UsuarioRequest
        {
            Cpf = "12345678901",
            Nome = "João Silva",
            Email = "joao@email.com"
        };
        var request2 = new UsuarioRequest
        {
            Cpf = "99999999999",
            Nome = "Maria Souza",
            Email = "maria@email.com"
        };

        Assert.NotEqual(request1.Cpf, request2.Cpf);
        Assert.NotEqual(request1.Nome, request2.Nome);
        Assert.NotEqual(request1.Email, request2.Email);
    }
}
