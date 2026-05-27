namespace TicketPrime.Api.Models;

public class IngressoDetalhadoResponse
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public TipoIngressoResumo? TipoIngresso { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ValorBruto { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal TaxaServico { get; set; }
    public decimal ValorFinal { get; set; }
    public DateTime DataCriacao { get; set; }
    public EventoResumo? Evento { get; set; }
    public UsuarioResumo? Usuario { get; set; }
}

public class TipoIngressoResumo
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
}

public class EventoResumo
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
}

public class UsuarioResumo
{
    public string Cpf { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
}
