namespace TicketPrime.Api.Models;

public class LoteListaResponse
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int Capacidade { get; set; }
    public decimal TaxaServico { get; set; }
    public DateTime DataInicioVenda { get; set; }
    public DateTime DataFimVenda { get; set; }
    public int IngressosVendidos { get; set; }
    public int CapacidadeRestante { get; set; }
}
