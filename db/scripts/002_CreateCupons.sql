-- ============================================================
-- TicketPrime - Criação da Tabela Cupons
-- Versão: 1.0.0
-- Descrição: Tabela de cupons de desconto.
-- ============================================================

USE TicketPrimeDb;
GO

-- ============================================================
-- Tabela: Cupons
-- Colunas: Codigo, PorcentagemDesconto, ValorMinimoRegra
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Cupons]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Cupons] (
        [Codigo]              VARCHAR(50)    NOT NULL,
        [PorcentagemDesconto] DECIMAL(5,2)   NOT NULL,
        [ValorMinimoRegra]    DECIMAL(10,2)  NOT NULL,
        CONSTRAINT [PK_Cupons] PRIMARY KEY CLUSTERED ([Codigo])
    );

    PRINT 'Tabela Cupons criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'Tabela Cupons já existe.';
END
GO
