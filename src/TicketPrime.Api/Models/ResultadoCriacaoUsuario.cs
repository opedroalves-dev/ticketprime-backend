namespace TicketPrime.Api.Models;

public class ResultadoCriacaoUsuario
{
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
    public string? Cpf { get; set; }
    public Usuario? Usuario { get; set; }
}
