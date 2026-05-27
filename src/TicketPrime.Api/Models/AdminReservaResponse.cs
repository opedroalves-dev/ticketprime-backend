namespace TicketPrime.Api.Models;

public class AdminReservaResponse
{
    public int ReservaId { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public string NomeUsuario { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
    public int? IngressoId { get; set; }
    public string? CodigoUnico { get; set; }
    public string? StatusIngresso { get; set; }
    public string? TipoIngresso { get; set; }
    public decimal? ValorBruto { get; set; }
    public decimal? ValorDesconto { get; set; }
    public decimal? TaxaServico { get; set; }
    public decimal? ValorFinal { get; set; }
    public string? CupomUtilizado { get; set; }
    public bool CheckInRealizado { get; set; }
}
