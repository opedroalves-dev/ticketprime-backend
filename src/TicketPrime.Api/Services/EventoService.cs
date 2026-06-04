using System.Data;
using TicketPrime.Api.Models;
using TicketPrime.Api.Repositories;

namespace TicketPrime.Api.Services;

public class EventoService
{
    private readonly IEventoRepository _eventoRepository;
    private readonly IHistoricoPrecoRepository _historicoPrecoRepository;
    private readonly IDbConnection _db;

    public EventoService(
        IEventoRepository eventoRepository,
        IHistoricoPrecoRepository historicoPrecoRepository,
        IDbConnection db)
    {
        _eventoRepository = eventoRepository;
        _historicoPrecoRepository = historicoPrecoRepository;
        _db = db;
    }

    public async Task<ResultadoCriacaoEvento> CriarAsync(EventoRequest request)
    {
        // Validações de entrada
        if (string.IsNullOrWhiteSpace(request.Nome))
            return new ResultadoCriacaoEvento { Sucesso = false, Erro = "Nome é obrigatório." };

        if (request.Nome.Length > 200)
            return new ResultadoCriacaoEvento { Sucesso = false, Erro = "Nome não pode exceder 200 caracteres." };

        if (request.CapacidadeTotal <= 0)
            return new ResultadoCriacaoEvento { Sucesso = false, Erro = "CapacidadeTotal deve ser maior que zero." };

        if (request.PrecoPadrao < 0)
            return new ResultadoCriacaoEvento { Sucesso = false, Erro = "PrecoPadrao não pode ser negativo." };

        // Abrir transação — necessária por usar 2 repositórios (atomicidade)
        if (_db.State != ConnectionState.Open)
            _db.Open();

        using var transaction = _db.BeginTransaction();

        try
        {
            var evento = new Evento
            {
                Nome = request.Nome,
                CapacidadeTotal = request.CapacidadeTotal,
                DataEvento = request.DataEvento,
                PrecoPadrao = request.PrecoPadrao
            };

            // Inserir evento e obter Id
            var id = await _eventoRepository.InserirAsync(evento, transaction);
            evento.Id = id;

            // Registrar preço inicial no histórico (RF05)
            await _historicoPrecoRepository.InserirPrecoInicialAsync(id, request.PrecoPadrao, transaction);

            transaction.Commit();

            return new ResultadoCriacaoEvento
            {
                Sucesso = true,
                Id = id,
                Evento = evento
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<Evento>> ListarTodosAsync()
    {
        return await _eventoRepository.ObterTodosAsync();
    }
}
