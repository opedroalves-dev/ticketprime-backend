using TicketPrime.Api.Models;
using TicketPrime.Api.Services;

namespace TicketPrime.Tests;

public class ReservaServiceTests
{
    #region Dados Compartilhados

    private static List<Usuario> UsuariosPadrao => new()
    {
        new Usuario { Cpf = "12345678901", Nome = "João Silva", Email = "joao@email.com" },
        new Usuario { Cpf = "98765432100", Nome = "Maria Souza", Email = "maria@email.com" }
    };

    private static List<Evento> EventosPadrao => new()
    {
        new Evento
        {
            Id = 1,
            Nome = "Show de Rock",
            CapacidadeTotal = 3,
            DataEvento = new DateTime(2026, 12, 31),
            PrecoPadrao = 150m
        },
        new Evento
        {
            Id = 2,
            Nome = "Teatro",
            CapacidadeTotal = 1,
            DataEvento = new DateTime(2026, 11, 15),
            PrecoPadrao = 80m
        }
    };

    private static List<Cupom> CuponsPadrao => new()
    {
        new Cupom { Codigo = "DESC10", PorcentagemDesconto = 10m, ValorMinimoRegra = 100m },
        new Cupom { Codigo = "DESC20", PorcentagemDesconto = 20m, ValorMinimoRegra = 50m },
        new Cupom { Codigo = "DESC5",  PorcentagemDesconto = 5m,  ValorMinimoRegra = 200m }
    };

    #endregion

    // ===================================================================
    //  Regra 1: UsuarioCpf inexistente deve bloquear reserva
    // ===================================================================

    [Fact]
    public void ValidarReserva_CpfInexistente_DeveBloquear()
    {
        var reservas = new List<Reserva>();

        var resultado = RegrasReserva.ValidarReserva(
            usuarioCpf: "00000000000",
            eventoId: 1,
            codigoCupom: null,
            usuarios: UsuariosPadrao,
            eventos: EventosPadrao,
            cupons: CuponsPadrao,
            reservasExistentes: reservas);

        Assert.False(resultado.Sucesso);
        Assert.Contains("CPF", resultado.Erro, StringComparison.OrdinalIgnoreCase);
    }

    // ===================================================================
    //  Regra 2: EventoId inexistente deve bloquear reserva
    // ===================================================================

    [Fact]
    public void ValidarReserva_EventoIdInexistente_DeveBloquear()
    {
        var reservas = new List<Reserva>();

        var resultado = RegrasReserva.ValidarReserva(
            usuarioCpf: "12345678901",
            eventoId: 999,
            codigoCupom: null,
            usuarios: UsuariosPadrao,
            eventos: EventosPadrao,
            cupons: CuponsPadrao,
            reservasExistentes: reservas);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Evento", resultado.Erro, StringComparison.OrdinalIgnoreCase);
    }

    // ===================================================================
    //  Regra 3: Mais de 2 reservas para o mesmo CPF e evento deve bloquear
    // ===================================================================

    [Fact]
    public void ValidarReserva_MaisDeDuasReservasMesmoCpfEEvento_DeveBloquear()
    {
        // Arrange: 2 reservas já existentes para o mesmo CPF e evento
        var reservas = new List<Reserva>
        {
            new() { Id = 1, UsuarioCpf = "12345678901", EventoId = 1, ValorFinalPago = 150m },
            new() { Id = 2, UsuarioCpf = "12345678901", EventoId = 1, ValorFinalPago = 150m }
        };

        // Act: tentar a 3ª reserva
        var resultado = RegrasReserva.ValidarReserva(
            usuarioCpf: "12345678901",
            eventoId: 1,
            codigoCupom: null,
            usuarios: UsuariosPadrao,
            eventos: EventosPadrao,
            cupons: CuponsPadrao,
            reservasExistentes: reservas);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Limite", resultado.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarReserva_DuasReservasMesmoCpfEEvento_DevePermitir()
    {
        // Arrange: apenas 1 reserva existente
        var reservas = new List<Reserva>
        {
            new() { Id = 1, UsuarioCpf = "12345678901", EventoId = 1, ValorFinalPago = 150m }
        };

        // Act: tentar a 2ª reserva (limite é 2, então deve permitir)
        var resultado = RegrasReserva.ValidarReserva(
            usuarioCpf: "12345678901",
            eventoId: 1,
            codigoCupom: null,
            usuarios: UsuariosPadrao,
            eventos: EventosPadrao,
            cupons: CuponsPadrao,
            reservasExistentes: reservas);

        Assert.True(resultado.Sucesso);
    }

    // ===================================================================
    //  Regra 4: Evento lotado deve bloquear reserva
    // ===================================================================

    [Fact]
    public void ValidarReserva_EventoComCapacidadeEsgotada_DeveBloquear()
    {
        // Evento 2 tem CapacidadeTotal = 1 e já tem 1 reserva
        var reservas = new List<Reserva>
        {
            new() { Id = 1, UsuarioCpf = "12345678901", EventoId = 2, ValorFinalPago = 80m }
        };

        var resultado = RegrasReserva.ValidarReserva(
            usuarioCpf: "98765432100",
            eventoId: 2,
            codigoCupom: null,
            usuarios: UsuariosPadrao,
            eventos: EventosPadrao,
            cupons: CuponsPadrao,
            reservasExistentes: reservas);

        Assert.False(resultado.Sucesso);
        Assert.Contains("lotado", resultado.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarReserva_EventoComVagasDisponiveis_DevePermitir()
    {
        var reservas = new List<Reserva>();

        var resultado = RegrasReserva.ValidarReserva(
            usuarioCpf: "12345678901",
            eventoId: 2,
            codigoCupom: null,
            usuarios: UsuariosPadrao,
            eventos: EventosPadrao,
            cupons: CuponsPadrao,
            reservasExistentes: reservas);

        Assert.True(resultado.Sucesso);
        Assert.Equal(80m, resultado.ValorFinalPago);
    }

    // ===================================================================
    //  Regra 5: Cupom aplica desconto apenas quando
    //           PrecoPadrao >= ValorMinimoRegra
    // ===================================================================

    [Theory]
    [InlineData("DESC10", 150, 100, true)]   // Preco >= Minimo -> desconto aplica
    [InlineData("DESC10", 80,  100, false)]  // Preco <  Minimo -> desconto NAO aplica
    [InlineData("DESC20", 80,  50,  true)]   // Preco >= Minimo -> desconto aplica
    [InlineData("DESC5",  150, 200, false)]  // Preco <  Minimo -> desconto NAO aplica
    public void CupomPodeSerAplicado_DeveRespeitarValorMinimoRegra(
        string codigoCupom,
        decimal precoPadrao,
        decimal valorMinimoRegra,
        bool esperado)
    {
        var cupom = new Cupom
        {
            Codigo = codigoCupom,
            PorcentagemDesconto = 10m,
            ValorMinimoRegra = valorMinimoRegra
        };

        var resultado = RegrasReserva.CupomPodeSerAplicado(precoPadrao, cupom);

        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData("DESC10", 150, 100, 135)]    // 150 >= 100 -> 150 - 15 = 135
    [InlineData("DESC10", 80,  100, 80)]     // 80  <  100 -> sem desconto = 80
    [InlineData("DESC20", 80,  50,  64)]     // 80  >= 50  -> 80 - 16 = 64
    [InlineData("DESC5",  150, 200, 150)]    // 150 <  200 -> sem desconto = 150
    public void CalcularValorFinal_CupomSoAplicaQuandoPrecoMaiorOuIgualMinimo(
        string codigoCupom,
        decimal precoPadrao,
        decimal valorMinimoRegra,
        decimal valorFinalEsperado)
    {
        // Extrai a porcentagem de desconto do código (ex: "DESC10" -> 10%)
        var percentual = decimal.Parse(codigoCupom.Replace("DESC", ""));

        var cupons = new List<Cupom>
        {
            new()
            {
                Codigo = codigoCupom,
                PorcentagemDesconto = percentual,
                ValorMinimoRegra = valorMinimoRegra
            }
        };

        var valorFinal = RegrasReserva.CalcularValorFinal(precoPadrao, codigoCupom, cupons);

        Assert.Equal(valorFinalEsperado, valorFinal);
    }

    // ===================================================================
    //  Regra 6: Cálculo de ValorFinalPago deve estar correto
    // ===================================================================

    [Theory]
    [InlineData(200, "DESC10", 180)]   // 200 - 10% = 180
    [InlineData(200, "DESC20", 160)]   // 200 - 20% = 160
    [InlineData(100, "DESC10", 90)]    // 100 - 10% = 90
    [InlineData(50,  null,     50)]    // sem cupom = preco cheio
    [InlineData(50,  "",       50)]    // cupom vazio = preco cheio
    [InlineData(50,  " ",      50)]    // cupom espaco = preco cheio
    public void CalcularValorFinal_DeveRetornarValorCorreto(
        decimal precoPadrao,
        string? codigoCupom,
        decimal valorFinalEsperado)
    {
        var cupons = new List<Cupom>
        {
            new() { Codigo = "DESC10", PorcentagemDesconto = 10m, ValorMinimoRegra = 0m },
            new() { Codigo = "DESC20", PorcentagemDesconto = 20m, ValorMinimoRegra = 0m }
        };

        var valorFinal = RegrasReserva.CalcularValorFinal(precoPadrao, codigoCupom, cupons);

        Assert.Equal(valorFinalEsperado, valorFinal);
    }

    [Fact]
    public void ValidarReserva_ComCupomValido_DeveCalcularValorFinalCorretamente()
    {
        var reservas = new List<Reserva>();

        var resultado = RegrasReserva.ValidarReserva(
            usuarioCpf: "12345678901",
            eventoId: 1,
            codigoCupom: "DESC10",
            usuarios: UsuariosPadrao,
            eventos: EventosPadrao,
            cupons: CuponsPadrao,
            reservasExistentes: reservas);

        // Evento 1 tem PrecoPadrao = 150, DESC10 tem ValorMinimoRegra = 100
        // 150 >= 100 -> desconto aplica
        // ValorFinal = 150 - (150 * 10/100) = 150 - 15 = 135
        Assert.True(resultado.Sucesso);
        Assert.Equal(135m, resultado.ValorFinalPago);
        Assert.Equal("DESC10", resultado.CupomAplicado);
    }

    [Fact]
    public void ValidarReserva_SemCupom_DeveRetornarPrecoCheio()
    {
        var reservas = new List<Reserva>();

        var resultado = RegrasReserva.ValidarReserva(
            usuarioCpf: "12345678901",
            eventoId: 1,
            codigoCupom: null,
            usuarios: UsuariosPadrao,
            eventos: EventosPadrao,
            cupons: CuponsPadrao,
            reservasExistentes: reservas);

        Assert.True(resultado.Sucesso);
        Assert.Equal(150m, resultado.ValorFinalPago);
        Assert.Null(resultado.CupomAplicado);
    }

    // ===================================================================
    //  Testes adicionais: validações combinadas e casos extremos
    // ===================================================================

    [Fact]
    public void ValidarReserva_CupomInexistente_DeveRejeitar()
    {
        var reservas = new List<Reserva>();

        var resultado = RegrasReserva.ValidarReserva(
            usuarioCpf: "12345678901",
            eventoId: 1,
            codigoCupom: "CUPOM_INEXISTENTE",
            usuarios: UsuariosPadrao,
            eventos: EventosPadrao,
            cupons: CuponsPadrao,
            reservasExistentes: reservas);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Cupom", resultado.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarReserva_CpfSemReservasEventoDiferente_DevePermitir()
    {
        // Usuário 12345678901 tem 2 reservas no Evento 1
        // Mas tenta reservar no Evento 2 -> deve permitir
        var reservas = new List<Reserva>
        {
            new() { Id = 1, UsuarioCpf = "12345678901", EventoId = 1, ValorFinalPago = 150m },
            new() { Id = 2, UsuarioCpf = "12345678901", EventoId = 1, ValorFinalPago = 150m }
        };

        var resultado = RegrasReserva.ValidarReserva(
            usuarioCpf: "12345678901",
            eventoId: 2,
            codigoCupom: null,
            usuarios: UsuariosPadrao,
            eventos: EventosPadrao,
            cupons: CuponsPadrao,
            reservasExistentes: reservas);

        Assert.True(resultado.Sucesso);
    }

    // ===================================================================
    //  Teste: Retorno de NomeEvento no GET /api/reservas/{cpf}
    // ===================================================================

    [Fact]
    public void ConstruirReservaResponse_DeveRetornarNomeEvento()
    {
        // Simula uma reserva existente
        var reserva = new Reserva
        {
            Id = 1,
            UsuarioCpf = "12345678901",
            EventoId = 1,
            CupomUtilizado = "DESC10",
            ValorFinalPago = 135m
        };

        var response = RegrasReserva.ConstruirReservaResponse(reserva, EventosPadrao);

        Assert.NotNull(response);
        Assert.Equal("Show de Rock", response.NomeEvento);
        Assert.Equal(1, response.EventoId);
        Assert.Equal("12345678901", response.UsuarioCpf);
        Assert.Equal("DESC10", response.CupomUtilizado);
        Assert.Equal(135m, response.ValorFinalPago);
    }

    [Fact]
    public void ConstruirReservaResponse_EventoInexistente_DeveRetornarNull()
    {
        var reserva = new Reserva
        {
            Id = 1,
            UsuarioCpf = "12345678901",
            EventoId = 999,
            ValorFinalPago = 150m
        };

        var response = RegrasReserva.ConstruirReservaResponse(reserva, EventosPadrao);

        Assert.Null(response);
    }
}
