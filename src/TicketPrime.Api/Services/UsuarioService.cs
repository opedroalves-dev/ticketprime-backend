using TicketPrime.Api.Models;
using TicketPrime.Api.Repositories;

namespace TicketPrime.Api.Services;

public class UsuarioService
{
    private readonly IUsuarioRepository _repository;

    public UsuarioService(IUsuarioRepository repository)
    {
        _repository = repository;
    }

    public async Task<ResultadoCriacaoUsuario> CriarAsync(UsuarioRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Cpf))
            return new ResultadoCriacaoUsuario { Sucesso = false, Erro = "CPF é obrigatório." };

        if (!request.Cpf.All(char.IsDigit))
            return new ResultadoCriacaoUsuario { Sucesso = false, Erro = "CPF deve conter apenas números." };

        if (request.Cpf.Length != 11)
            return new ResultadoCriacaoUsuario { Sucesso = false, Erro = "CPF deve ter 11 dígitos." };

        if (string.IsNullOrWhiteSpace(request.Nome))
            return new ResultadoCriacaoUsuario { Sucesso = false, Erro = "Nome é obrigatório." };

        if (request.Nome.Length > 100)
            return new ResultadoCriacaoUsuario { Sucesso = false, Erro = "Nome não pode exceder 100 caracteres." };

        if (string.IsNullOrWhiteSpace(request.Email))
            return new ResultadoCriacaoUsuario { Sucesso = false, Erro = "Email é obrigatório." };

        if (request.Email.Length > 150)
            return new ResultadoCriacaoUsuario { Sucesso = false, Erro = "Email não pode exceder 150 caracteres." };

        if (!request.Email.Contains('@') ||
            request.Email.IndexOf('@') == 0 ||
            request.Email.IndexOf('@') == request.Email.Length - 1)
            return new ResultadoCriacaoUsuario { Sucesso = false, Erro = "Email inválido." };

        var existe = await _repository.ExisteAsync(request.Cpf);
        if (existe)
            return new ResultadoCriacaoUsuario { Sucesso = false, Erro = "CPF já cadastrado." };

        var usuario = new Usuario
        {
            Cpf = request.Cpf,
            Nome = request.Nome,
            Email = request.Email
        };

        await _repository.InserirAsync(usuario);

        return new ResultadoCriacaoUsuario
        {
            Sucesso = true,
            Cpf = request.Cpf,
            Usuario = usuario
        };
    }
}
