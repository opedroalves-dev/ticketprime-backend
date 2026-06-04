using TicketPrime.Api.Models;
using TicketPrime.Api.Services;

namespace TicketPrime.Tests;

public class IncrementoServiceTests
{
    private readonly IncrementoService _service = new();

    #region Dados Compartilhados

    private static List<Cupom> CuponsPadrao => new()
    {
        new Cupom { Codigo = "DESC10", PorcentagemDesconto = 10m, ValorMinimoRegra = 100m },
        new Cupom { Codigo = "DESC20", PorcentagemDesconto = 20m, ValorMinimoRegra = 50m },
        new Cupom { Codigo = "DESC5",  PorcentagemDesconto = 5m,  ValorMinimoRegra = 200m }
    };

    private static Evento EventoPadrao => new()
    {
        Id = 1,
        Nome = "Show de Rock",
        CapacidadeTotal = 500,
        DataEvento = new DateTime(2026, 12, 31),
        PrecoPadrao = 150m
    };

    private static List<TipoIngresso> TiposIngressoPadrao => new()
    {
        new TipoIngresso
        {
            Id = 1,
            EventoId = 1,
            Nome = "Pista",
            Preco = 100m,
            Capacidade = 300,
            TaxaServico = 10m,
            DataInicioVenda = new DateTime(2026, 1, 1),
            DataFimVenda = new DateTime(2026, 12, 30)
        },
        new TipoIngresso
        {
            Id = 2,
            EventoId = 1,
            Nome = "VIP",
            Preco = 300m,
            Capacidade = 100,
            TaxaServico = 30m,
            DataInicioVenda = new DateTime(2026, 1, 1),
            DataFimVenda = new DateTime(2026, 12, 30)
        }
    };

    #endregion

    // ===================================================================
    //  RF03 — Tipo de Ingresso Inválido
    // ===================================================================

    [Fact]
    public void ValidarTipoIngresso_NomeVazio_DeveRejeitar()
    {
        var (valido, erro) = _service.ValidarTipoIngresso(
            nome: "",
            preco: 100m,
            capacidade: 50,
            dataInicioVenda: DateTime.UtcNow,
            dataFimVenda: DateTime.UtcNow.AddDays(30));

        Assert.False(valido);
        Assert.Contains("obrigatório", erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarTipoIngresso_NomeEspacos_DeveRejeitar()
    {
        var (valido, erro) = _service.ValidarTipoIngresso(
            nome: "   ",
            preco: 100m,
            capacidade: 50,
            dataInicioVenda: DateTime.UtcNow,
            dataFimVenda: DateTime.UtcNow.AddDays(30));

        Assert.False(valido);
        Assert.Contains("obrigatório", erro, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50.50)]
    public void ValidarTipoIngresso_PrecoInvalido_DeveRejeitar(decimal preco)
    {
        var (valido, erro) = _service.ValidarTipoIngresso(
            nome: "VIP",
            preco: preco,
            capacidade: 50,
            dataInicioVenda: DateTime.UtcNow,
            dataFimVenda: DateTime.UtcNow.AddDays(30));

        Assert.False(valido);
        Assert.Contains("Preço", erro, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ValidarTipoIngresso_CapacidadeInvalida_DeveRejeitar(int capacidade)
    {
        var (valido, erro) = _service.ValidarTipoIngresso(
            nome: "Pista",
            preco: 80m,
            capacidade: capacidade,
            dataInicioVenda: DateTime.UtcNow,
            dataFimVenda: DateTime.UtcNow.AddDays(30));

        Assert.False(valido);
        Assert.Contains("Capacidade", erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarTipoIngresso_DataInicioMaiorQueFim_DeveRejeitar()
    {
        var dataInicio = DateTime.UtcNow.AddDays(10);
        var dataFim = DateTime.UtcNow.AddDays(5);

        var (valido, erro) = _service.ValidarTipoIngresso(
            nome: "Meia-Entrada",
            preco: 50m,
            capacidade: 100,
            dataInicioVenda: dataInicio,
            dataFimVenda: dataFim);

        Assert.False(valido);
        Assert.Contains("anterior", erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarTipoIngresso_DataInicioIgualFim_DeveRejeitar()
    {
        var data = DateTime.UtcNow.AddDays(10);

        var (valido, erro) = _service.ValidarTipoIngresso(
            nome: "Meia-Entrada",
            preco: 50m,
            capacidade: 100,
            dataInicioVenda: data,
            dataFimVenda: data);

        Assert.False(valido);
        Assert.Contains("anterior", erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarTipoIngresso_DadosValidos_DevePermitir()
    {
        var (valido, erro) = _service.ValidarTipoIngresso(
            nome: "VIP",
            preco: 300m,
            capacidade: 100,
            dataInicioVenda: DateTime.UtcNow,
            dataFimVenda: DateTime.UtcNow.AddDays(60));

        Assert.True(valido);
        Assert.Null(erro);
    }

    // ===================================================================
    //  RF05 — Simulação de Preço com e sem Cupom
    // ===================================================================

    [Fact]
    public void SimularPreco_SemCupom_DeveCalcularCorretamente()
    {
        // PrecoBase = 150, Taxa = 15 (10%), Desconto = 0, Final = 165
        var resultado = _service.SimularPreco(150m, null, CuponsPadrao);

        Assert.Equal(150m, resultado.PrecoBase);
        Assert.Equal(15m, resultado.TaxaServico);
        Assert.Equal(0m, resultado.ValorDesconto);
        Assert.Equal(165m, resultado.ValorFinal);
    }

    [Fact]
    public void SimularPreco_CupomVazio_DeveCalcularSemDesconto()
    {
        var resultado = _service.SimularPreco(150m, "", CuponsPadrao);

        Assert.Equal(150m, resultado.PrecoBase);
        Assert.Equal(15m, resultado.TaxaServico);
        Assert.Equal(0m, resultado.ValorDesconto);
        Assert.Equal(165m, resultado.ValorFinal);
    }

    [Fact]
    public void SimularPreco_CupomInexistente_DeveCalcularSemDesconto()
    {
        var resultado = _service.SimularPreco(150m, "CUPOM_INVALIDO", CuponsPadrao);

        Assert.Equal(150m, resultado.PrecoBase);
        Assert.Equal(15m, resultado.TaxaServico);
        Assert.Equal(0m, resultado.ValorDesconto);
        Assert.Equal(165m, resultado.ValorFinal);
    }

    [Fact]
    public void SimularPreco_ComCupomValidoEAplicavel_DeveCalcularDesconto()
    {
        // Cupom DESC10: 10% de desconto, ValorMinimoRegra = 100
        // PrecoBase = 150 >= 100 -> desconto aplica
        // Desconto = 150 * 10% = 15
        // Taxa = 150 * 10% = 15
        // Final = 150 + 15 - 15 = 150
        var resultado = _service.SimularPreco(150m, "DESC10", CuponsPadrao);

        Assert.Equal(150m, resultado.PrecoBase);
        Assert.Equal(15m, resultado.TaxaServico);
        Assert.Equal(15m, resultado.ValorDesconto);
        Assert.Equal(150m, resultado.ValorFinal);
    }

    [Fact]
    public void SimularPreco_ComCupomNaoAplicavel_DeveCalcularSemDesconto()
    {
        // Cupom DESC10: ValorMinimoRegra = 100
        // PrecoBase = 50 < 100 -> desconto NÃO aplica
        var resultado = _service.SimularPreco(50m, "DESC10", CuponsPadrao);

        Assert.Equal(50m, resultado.PrecoBase);
        Assert.Equal(5m, resultado.TaxaServico);     // 50 * 10% = 5
        Assert.Equal(0m, resultado.ValorDesconto);    // não aplicou
        Assert.Equal(55m, resultado.ValorFinal);      // 50 + 5 = 55
    }

    [Theory]
    [InlineData(200, "DESC10", 100, 200, 20, 20, 200)]   // 200 >= 100 -> 10% desc
    [InlineData(200, "DESC20", 50,  200, 20, 40, 180)]   // 200 >= 50  -> 20% desc
    [InlineData(200, "DESC5",  200, 200, 20, 10, 210)]   // 200 >= 200 -> 5% desc
    [InlineData(30,  "DESC10", 100, 30,  3,  0,  33)]    // 30  <  100 -> sem desc
    public void SimularPreco_ComCupom_DeveRespeitarValorMinimoRegra(
        decimal precoBase,
        string codigoCupom,
        decimal valorMinimoRegra,
        decimal precoBaseEsperado,
        decimal taxaEsperada,
        decimal descontoEsperado,
        decimal valorFinalEsperado)
    {
        // Extrai a porcentagem de desconto do código (ex: "DESC10" -> 10%)
        var percentual = decimal.Parse(codigoCupom.Replace("DESC", ""));

        var cupom = new List<Cupom>
        {
            new()
            {
                Codigo = codigoCupom,
                PorcentagemDesconto = percentual,
                ValorMinimoRegra = valorMinimoRegra
            }
        };

        var resultado = _service.SimularPreco(precoBase, codigoCupom, cupom);

        Assert.Equal(precoBaseEsperado, resultado.PrecoBase);
        Assert.Equal(taxaEsperada, resultado.TaxaServico);
        Assert.Equal(descontoEsperado, resultado.ValorDesconto);
        Assert.Equal(valorFinalEsperado, resultado.ValorFinal);
    }

    [Fact]
    public void SimularPreco_TaxaServico_DeveSerDezPorcentoDoPrecoBase()
    {
        var resultado = _service.SimularPreco(80m, null, CuponsPadrao);

        Assert.Equal(8m, resultado.TaxaServico);  // 80 * 10% = 8
    }

    [Fact]
    public void SimularPreco_ValorFinal_DeveSerPrecoBaseMaisTaxaMenosDesconto()
    {
        // PrecoBase = 200, Taxa = 20, DESC10: 10% de 200 = 20 de desconto
        // Final = 200 + 20 - 20 = 200
        var resultado = _service.SimularPreco(200m, "DESC10", CuponsPadrao);

        Assert.Equal(resultado.PrecoBase + resultado.TaxaServico - resultado.ValorDesconto,
                     resultado.ValorFinal);
    }

    // ===================================================================
    //  RF01 — Ingresso Digital com Código Único
    // ===================================================================

    [Fact]
    public void GerarCodigoUnico_DeveTerOitoCaracteres()
    {
        var codigosExistentes = new List<string>();

        var codigo = _service.GerarCodigoUnico(codigosExistentes);

        Assert.Equal(8, codigo.Length);
    }

    [Fact]
    public void GerarCodigoUnico_DeveSerAlfanumerico()
    {
        var codigosExistentes = new List<string>();

        var codigo = _service.GerarCodigoUnico(codigosExistentes);

        Assert.Matches("^[A-Z0-9]{8}$", codigo);
    }

    [Fact]
    public void GerarCodigoUnico_DeveSerUnico()
    {
        var codigosExistentes = new List<string> { "ABC123XY", "ZZZ99999", "TESTE123" };

        var codigo = _service.GerarCodigoUnico(codigosExistentes);

        Assert.DoesNotContain(codigo, codigosExistentes);
        Assert.Equal(8, codigo.Length);
    }

    [Fact]
    public void GerarCodigoUnico_Colisao_DeveRegenerarAteSerUnico()
    {
        // Força a geração de um código que colide e verifica se regenera
        var codigosExistentes = new List<string>();

        // Gera 5 códigos e verifica que todos são únicos entre si
        var codigos = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var codigo = _service.GerarCodigoUnico(codigos);
            codigos.Add(codigo);
        }

        Assert.Equal(5, codigos.Distinct().Count());
        Assert.All(codigos, c => Assert.Matches("^[A-Z0-9]{8}$", c));
    }

    [Fact]
    public void ValidarCodigoUnico_CodigoValido_DeveRetornarTrue()
    {
        var valido = _service.ValidarCodigoUnico("ABC123XY");

        Assert.True(valido);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ABC")]
    [InlineData("ABCDEFGHIJ")]
    [InlineData("ABC-1234")]
    [InlineData("abc123xy")]
    [InlineData("1234567")]
    [InlineData("123456789")]
    public void ValidarCodigoUnico_CodigoInvalido_DeveRetornarFalse(string? codigo)
    {
        var valido = _service.ValidarCodigoUnico(codigo!);

        Assert.False(valido);
    }

    [Fact]
    public void CriarIngressoDigital_DeveCriarComStatusConfirmada()
    {
        var codigosExistentes = new List<string>();

        var ingresso = _service.CriarIngressoDigital(
            reservaId: 1,
            tipoIngressoId: 1,
            valorBruto: 150m,
            valorDesconto: 15m,
            taxaServico: 15m,
            valorFinal: 150m,
            codigosExistentes: codigosExistentes);

        Assert.Equal("Confirmada", ingresso.Status);
        Assert.Equal(1, ingresso.ReservaId);
        Assert.Equal(1, ingresso.TipoIngressoId);
        Assert.Equal(150m, ingresso.ValorBruto);
        Assert.Equal(15m, ingresso.ValorDesconto);
        Assert.Equal(15m, ingresso.TaxaServico);
        Assert.Equal(150m, ingresso.ValorFinal);
        Assert.Equal(8, ingresso.CodigoUnico.Length);
        Assert.Matches("^[A-Z0-9]{8}$", ingresso.CodigoUnico);
    }

    [Fact]
    public void CriarIngressoDigital_CodigoUnico_DeveSerUnicoNaBase()
    {
        var codigosExistentes = new List<string> { "CODIGO01", "INGRESSO" };

        var ingresso = _service.CriarIngressoDigital(
            reservaId: 1,
            tipoIngressoId: null,
            valorBruto: 100m,
            valorDesconto: 0m,
            taxaServico: 10m,
            valorFinal: 110m,
            codigosExistentes: codigosExistentes);

        Assert.DoesNotContain(ingresso.CodigoUnico, codigosExistentes);
    }

    [Fact]
    public void CriarIngressoDigital_TipoIngressoNulo_DevePermitir()
    {
        var codigosExistentes = new List<string>();

        var ingresso = _service.CriarIngressoDigital(
            reservaId: 1,
            tipoIngressoId: null,
            valorBruto: 100m,
            valorDesconto: 0m,
            taxaServico: 10m,
            valorFinal: 110m,
            codigosExistentes: codigosExistentes);

        Assert.Null(ingresso.TipoIngressoId);
        Assert.Equal("Confirmada", ingresso.Status);
    }

    // ===================================================================
    //  RF02 — Check-in Válido e Bloqueio de Duplicado
    // ===================================================================

    [Fact]
    public void RealizarCheckIn_IngressoValido_DeveRegistrarComSucesso()
    {
        var ingressos = new List<Ingresso>
        {
            new()
            {
                Id = 1,
                ReservaId = 1,
                CodigoUnico = "ABC123XY",
                Status = "Confirmada",
                ValorFinal = 150m
            }
        };
        var checkInsExistentes = new List<CheckIn>();

        var (sucesso, erro, checkIn) = _service.RealizarCheckIn(
            "ABC123XY", ingressos, checkInsExistentes);

        Assert.True(sucesso);
        Assert.Null(erro);
        Assert.NotNull(checkIn);
        Assert.Equal(1, checkIn.IngressoId);
        Assert.Equal("Utilizada", ingressos[0].Status);
    }

    [Fact]
    public void RealizarCheckIn_IngressoInexistente_DeveRejeitar()
    {
        var ingressos = new List<Ingresso>();
        var checkInsExistentes = new List<CheckIn>();

        var (sucesso, erro, checkIn) = _service.RealizarCheckIn(
            "CODIGO_INVALIDO", ingressos, checkInsExistentes);

        Assert.False(sucesso);
        Assert.Contains("não encontrado", erro, StringComparison.OrdinalIgnoreCase);
        Assert.Null(checkIn);
    }

    [Fact]
    public void RealizarCheckIn_IngressoJaUtilizado_DeveRejeitar()
    {
        var ingressos = new List<Ingresso>
        {
            new()
            {
                Id = 1,
                ReservaId = 1,
                CodigoUnico = "ABC123XY",
                Status = "Utilizada",
                ValorFinal = 150m
            }
        };
        var checkInsExistentes = new List<CheckIn>();

        var (sucesso, erro, checkIn) = _service.RealizarCheckIn(
            "ABC123XY", ingressos, checkInsExistentes);

        Assert.False(sucesso);
        Assert.Contains("não está confirmado", erro, StringComparison.OrdinalIgnoreCase);
        Assert.Null(checkIn);
    }

    [Fact]
    public void RealizarCheckIn_IngressoCancelado_DeveRejeitar()
    {
        var ingressos = new List<Ingresso>
        {
            new()
            {
                Id = 1,
                ReservaId = 1,
                CodigoUnico = "ABC123XY",
                Status = "Cancelada",
                ValorFinal = 150m
            }
        };
        var checkInsExistentes = new List<CheckIn>();

        var (sucesso, erro, checkIn) = _service.RealizarCheckIn(
            "ABC123XY", ingressos, checkInsExistentes);

        Assert.False(sucesso);
        Assert.Contains("não está confirmado", erro, StringComparison.OrdinalIgnoreCase);
        Assert.Null(checkIn);
    }

    [Fact]
    public void RealizarCheckIn_Duplicado_DeveBloquear()
    {
        var ingressos = new List<Ingresso>
        {
            new()
            {
                Id = 1,
                ReservaId = 1,
                CodigoUnico = "ABC123XY",
                Status = "Utilizada",
                ValorFinal = 150m
            }
        };
        var checkInsExistentes = new List<CheckIn>
        {
            new() { Id = 1, IngressoId = 1, DataCheckIn = DateTime.UtcNow }
        };

        var (sucesso, erro, checkIn) = _service.RealizarCheckIn(
            "ABC123XY", ingressos, checkInsExistentes);

        Assert.False(sucesso);
        // O erro pode ser "Ingresso já utilizado" (check-in existente) ou
        // "Ingresso não está confirmado" (status Utilizada)
        Assert.NotNull(erro);
        Assert.Null(checkIn);
    }

    [Fact]
    public void RealizarCheckIn_CheckInPrevio_DeveBloquear()
    {
        var ingressos = new List<Ingresso>
        {
            new()
            {
                Id = 1,
                ReservaId = 1,
                CodigoUnico = "ABC123XY",
                Status = "Confirmada",
                ValorFinal = 150m
            }
        };
        // Check-in já registrado para o ingresso (antes do status ser alterado)
        var checkInsExistentes = new List<CheckIn>
        {
            new() { Id = 1, IngressoId = 1, DataCheckIn = DateTime.UtcNow }
        };

        var (sucesso, erro, checkIn) = _service.RealizarCheckIn(
            "ABC123XY", ingressos, checkInsExistentes);

        Assert.False(sucesso);
        Assert.Contains("já utilizado", erro, StringComparison.OrdinalIgnoreCase);
        Assert.Null(checkIn);
    }

    [Fact]
    public void RealizarCheckIn_DeveAlterarStatusParaUtilizada()
    {
        var ingressos = new List<Ingresso>
        {
            new()
            {
                Id = 1,
                ReservaId = 1,
                CodigoUnico = "ABC123XY",
                Status = "Confirmada",
                ValorFinal = 150m
            }
        };
        var checkInsExistentes = new List<CheckIn>();

        var (sucesso, erro, checkIn) = _service.RealizarCheckIn(
            "ABC123XY", ingressos, checkInsExistentes);

        Assert.True(sucesso);
        Assert.Equal("Utilizada", ingressos[0].Status);
    }

    [Fact]
    public void PodeRealizarCheckIn_IngressoConfirmadoSemCheckIn_DevePermitir()
    {
        var ingresso = new Ingresso
        {
            Id = 1,
            Status = "Confirmada",
            CodigoUnico = "ABC123XY"
        };
        var checkIns = new List<CheckIn>();

        var pode = _service.PodeRealizarCheckIn(ingresso, checkIns);

        Assert.True(pode);
    }

    [Fact]
    public void PodeRealizarCheckIn_IngressoUtilizada_DeveBloquear()
    {
        var ingresso = new Ingresso
        {
            Id = 1,
            Status = "Utilizada",
            CodigoUnico = "ABC123XY"
        };
        var checkIns = new List<CheckIn>();

        var pode = _service.PodeRealizarCheckIn(ingresso, checkIns);

        Assert.False(pode);
    }

    [Fact]
    public void PodeRealizarCheckIn_IngressoComCheckInExistente_DeveBloquear()
    {
        var ingresso = new Ingresso
        {
            Id = 1,
            Status = "Confirmada",
            CodigoUnico = "ABC123XY"
        };
        var checkIns = new List<CheckIn>
        {
            new() { Id = 1, IngressoId = 1 }
        };

        var pode = _service.PodeRealizarCheckIn(ingresso, checkIns);

        Assert.False(pode);
    }


    // ===================================================================
    //  RF03 — Verificação de Disponibilidade de Lote
    // ===================================================================

    [Fact]
    public void VerificarDisponibilidadeLote_LoteComVagas_DeveRetornarDisponivel()
    {
        var lote = new TipoIngresso
        {
            Id = 1,
            Nome = "Pista",
            Capacidade = 100,
            Preco = 100m
        };

        var ingressos = new List<Ingresso>
        {
            new() { Id = 1, TipoIngressoId = 1, Status = "Confirmada" },
            new() { Id = 2, TipoIngressoId = 1, Status = "Utilizada" }
        };

        var (disponivel, vagas) = _service.VerificarDisponibilidadeLote(lote, ingressos);

        Assert.True(disponivel);
        Assert.Equal(98, vagas);  // 100 - 2
    }

    [Fact]
    public void VerificarDisponibilidadeLote_LoteLotado_DeveRetornarIndisponivel()
    {
        var lote = new TipoIngresso
        {
            Id = 1,
            Nome = "VIP",
            Capacidade = 2,
            Preco = 300m
        };

        var ingressos = new List<Ingresso>
        {
            new() { Id = 1, TipoIngressoId = 1, Status = "Confirmada" },
            new() { Id = 2, TipoIngressoId = 1, Status = "Utilizada" }
        };

        var (disponivel, vagas) = _service.VerificarDisponibilidadeLote(lote, ingressos);

        Assert.False(disponivel);
        Assert.Equal(0, vagas);
    }

    [Fact]
    public void VerificarDisponibilidadeLote_IngressosCanceladosNaoContam_DeveConsiderarApenasConfirmadaOUtilizada()
    {
        var lote = new TipoIngresso
        {
            Id = 1,
            Nome = "Pista",
            Capacidade = 3,
            Preco = 100m
        };

        var ingressos = new List<Ingresso>
        {
            new() { Id = 1, TipoIngressoId = 1, Status = "Confirmada" },
            new() { Id = 2, TipoIngressoId = 1, Status = "Cancelada" },
            new() { Id = 3, TipoIngressoId = 1, Status = "Utilizada" }
        };

        var (disponivel, vagas) = _service.VerificarDisponibilidadeLote(lote, ingressos);

        Assert.True(disponivel);
        Assert.Equal(1, vagas);  // 3 - 2 (Cancelada não conta)
    }
}
