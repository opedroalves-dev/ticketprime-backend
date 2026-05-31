-- ============================================================
-- TicketPrime - Script Incremental de Novos Recursos
-- Versão: 1.0.0
-- Descrição: Adiciona tabelas para os 6 novos recursos sem
--            alterar as tabelas obrigatórias existentes
--            (Usuarios, Eventos, Cupons, Reservas).
-- ============================================================

-- ============================================================
-- RF03 — Tipos/Lotes de Ingresso (TiposIngresso)
-- ============================================================
-- Permite que um evento tenha múltiplos tipos/lotes de ingresso
-- (ex.: Pista, VIP, Meia-Entrada) com preços e capacidades
-- independentes.
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TiposIngresso')
BEGIN
    CREATE TABLE TiposIngresso (
        Id              INT IDENTITY(1,1)   NOT NULL,
        EventoId        INT                 NOT NULL,
        Nome            VARCHAR(100)        NOT NULL,
        Preco           DECIMAL(10,2)       NOT NULL,
        Capacidade      INT                 NOT NULL,
        TaxaServico     DECIMAL(10,2)       NOT NULL DEFAULT 0.00,
        DataInicioVenda DATETIME            NOT NULL,
        DataFimVenda    DATETIME            NOT NULL,
        Lote            VARCHAR(100)        NULL,
        CONSTRAINT PK_TiposIngresso PRIMARY KEY (Id),
        CONSTRAINT FK_TiposIngresso_Eventos FOREIGN KEY (EventoId)
            REFERENCES Eventos(Id)
    );

    PRINT 'Tabela TiposIngresso criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'Tabela TiposIngresso ja existe.';
END
GO

-- ============================================================
-- RF01 — Ingresso Digital com Código Único (Ingressos)
-- ============================================================
-- Cada linha representa um ingresso digital vinculado a uma
-- reserva existente. Contém código único de 8 caracteres,
-- status do ingresso e discriminação completa dos valores
-- (bruto, desconto, taxa de serviço, final).
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Ingressos')
BEGIN
    CREATE TABLE Ingressos (
        Id              INT IDENTITY(1,1)   NOT NULL,
        ReservaId       INT                 NOT NULL,
        TipoIngressoId  INT                 NULL,
        CodigoUnico     VARCHAR(8)          NOT NULL,
        Status          VARCHAR(20)         NOT NULL DEFAULT 'Confirmada',
        ValorBruto      DECIMAL(10,2)       NOT NULL,
        ValorDesconto   DECIMAL(10,2)       NOT NULL DEFAULT 0.00,
        TaxaServico     DECIMAL(10,2)       NOT NULL DEFAULT 0.00,
        ValorFinal      DECIMAL(10,2)       NOT NULL,
        DataCriacao     DATETIME            NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_Ingressos PRIMARY KEY (Id),
        CONSTRAINT UQ_Ingressos_CodigoUnico UNIQUE (CodigoUnico),
        CONSTRAINT FK_Ingressos_Reservas FOREIGN KEY (ReservaId)
            REFERENCES Reservas(Id),
        CONSTRAINT FK_Ingressos_TiposIngresso FOREIGN KEY (TipoIngressoId)
            REFERENCES TiposIngresso(Id),
        CONSTRAINT CK_Ingressos_Status CHECK (
            Status IN ('Confirmada', 'Utilizada', 'Cancelada')
        ),
        CONSTRAINT CK_Ingressos_CodigoUnico_Tamanho CHECK (
            LEN(CodigoUnico) = 8
        )
    );

    PRINT 'Tabela Ingressos criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'Tabela Ingressos ja existe.';
END
GO

-- Índice para consulta rápida de ingressos por reserva
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Ingressos_ReservaId')
BEGIN
    CREATE INDEX IX_Ingressos_ReservaId ON Ingressos(ReservaId);
    PRINT 'Indice IX_Ingressos_ReservaId criado.';
END
GO

-- Índice para consulta de status (dashboard)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Ingressos_Status')
BEGIN
    CREATE INDEX IX_Ingressos_Status ON Ingressos(Status);
    PRINT 'Indice IX_Ingressos_Status criado.';
END
GO

-- ============================================================
-- RF02 — Check-in de Ingresso (CheckIns)
-- ============================================================
-- Registra a validação do ingresso na entrada do evento.
-- A constraint UNIQUE em IngressoId garante que cada ingresso
-- só possa realizar check-in uma única vez.
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CheckIns')
BEGIN
    CREATE TABLE CheckIns (
        Id              INT IDENTITY(1,1)   NOT NULL,
        IngressoId      INT                 NOT NULL,
        DataCheckIn     DATETIME            NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_CheckIns PRIMARY KEY (Id),
        CONSTRAINT UQ_CheckIns_IngressoId UNIQUE (IngressoId),
        CONSTRAINT FK_CheckIns_Ingressos FOREIGN KEY (IngressoId)
            REFERENCES Ingressos(Id)
    );

    PRINT 'Tabela CheckIns criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'Tabela CheckIns ja existe.';
END
GO

-- ============================================================
-- RF04 — Carrinho/Reserva Temporária (Carrinhos + CarrinhoItens)
-- ============================================================
-- Carrinho: representa uma sessão de compra temporária por CPF,
-- com status (Ativo, Expirado, Confirmado) e data de expiração.
-- CarrinhoItens: itens (ingressos) adicionados ao carrinho,
-- com quantidade e preço unitário congelado no momento da adição.
-- ============================================================

-- Tabela Carrinhos
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Carrinhos')
BEGIN
    CREATE TABLE Carrinhos (
        Id              INT IDENTITY(1,1)   NOT NULL,
        UsuarioCpf      VARCHAR(11)         NOT NULL,
        Status          VARCHAR(20)         NOT NULL DEFAULT 'Ativo',
        DataCriacao     DATETIME            NOT NULL DEFAULT GETDATE(),
        DataExpiracao   DATETIME            NOT NULL,
        CONSTRAINT PK_Carrinhos PRIMARY KEY (Id),
        CONSTRAINT FK_Carrinhos_Usuarios FOREIGN KEY (UsuarioCpf)
            REFERENCES Usuarios(Cpf),
        CONSTRAINT CK_Carrinhos_Status CHECK (
            Status IN ('Ativo', 'Expirado', 'Confirmado')
        )
    );

    PRINT 'Tabela Carrinhos criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'Tabela Carrinhos ja existe.';
END
GO

-- Índice para buscar carrinho ativo por CPF
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Carrinhos_UsuarioCpf_Status')
BEGIN
    CREATE INDEX IX_Carrinhos_UsuarioCpf_Status
        ON Carrinhos(UsuarioCpf, Status)
        WHERE Status = 'Ativo';
    PRINT 'Indice IX_Carrinhos_UsuarioCpf_Status criado.';
END
GO

-- Tabela CarrinhoItens
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CarrinhoItens')
BEGIN
    CREATE TABLE CarrinhoItens (
        Id              INT IDENTITY(1,1)   NOT NULL,
        CarrinhoId      INT                 NOT NULL,
        EventoId        INT                 NOT NULL,
        TipoIngressoId  INT                 NULL,
        Quantidade      INT                 NOT NULL DEFAULT 1,
        PrecoUnitario   DECIMAL(10,2)       NOT NULL,
        CONSTRAINT PK_CarrinhoItens PRIMARY KEY (Id),
        CONSTRAINT FK_CarrinhoItens_Carrinhos FOREIGN KEY (CarrinhoId)
            REFERENCES Carrinhos(Id),
        CONSTRAINT FK_CarrinhoItens_Eventos FOREIGN KEY (EventoId)
            REFERENCES Eventos(Id),
        CONSTRAINT FK_CarrinhoItens_TiposIngresso FOREIGN KEY (TipoIngressoId)
            REFERENCES TiposIngresso(Id),
        CONSTRAINT CK_CarrinhoItens_Quantidade CHECK (
            Quantidade > 0
        )
    );

    PRINT 'Tabela CarrinhoItens criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'Tabela CarrinhoItens ja existe.';
END
GO

-- ============================================================
-- RF05 — Transparência de Preço (HistoricoPrecos)
-- ============================================================
-- Registra toda alteração de preço de eventos e tipos de ingresso.
-- Append-only: uma vez inserido, o registro não pode ser alterado
-- ou excluído via API (apenas SELECT).
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HistoricoPrecos')
BEGIN
    CREATE TABLE HistoricoPrecos (
        Id              INT IDENTITY(1,1)   NOT NULL,
        EventoId        INT                 NULL,
        TipoIngressoId  INT                 NULL,
        PrecoAnterior   DECIMAL(10,2)       NULL,
        PrecoNovo       DECIMAL(10,2)       NOT NULL,
        DataAlteracao   DATETIME            NOT NULL DEFAULT GETDATE(),
        Motivo          VARCHAR(200)        NULL,
        CONSTRAINT PK_HistoricoPrecos PRIMARY KEY (Id),
        CONSTRAINT FK_HistoricoPrecos_Eventos FOREIGN KEY (EventoId)
            REFERENCES Eventos(Id),
        CONSTRAINT FK_HistoricoPrecos_TiposIngresso FOREIGN KEY (TipoIngressoId)
            REFERENCES TiposIngresso(Id)
    );

    PRINT 'Tabela HistoricoPrecos criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'Tabela HistoricoPrecos ja existe.';
END
GO

-- Índice para consulta de histórico por evento
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HistoricoPrecos_EventoId')
BEGIN
    CREATE INDEX IX_HistoricoPrecos_EventoId
        ON HistoricoPrecos(EventoId, DataAlteracao DESC);
    PRINT 'Indice IX_HistoricoPrecos_EventoId criado.';
END
GO

-- Índice para consulta de histórico por tipo de ingresso
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HistoricoPrecos_TipoIngressoId')
BEGIN
    CREATE INDEX IX_HistoricoPrecos_TipoIngressoId
        ON HistoricoPrecos(TipoIngressoId, DataAlteracao DESC);
    PRINT 'Indice IX_HistoricoPrecos_TipoIngressoId criado.';
END
GO

-- ============================================================
-- RF06 — Dashboard/Admin
-- ============================================================
-- O dashboard não requer tabelas adicionais. As consultas são
-- feitas diretamente sobre as tabelas existentes e as novas
-- tabelas criadas neste script:
--
--   - Eventos        (dados do evento)
--   - Reservas       (total de vendas)
--   - Ingressos      (status dos ingressos, código único)
--   - CheckIns       (check-ins realizados)
--   - TiposIngresso  (métricas por lote)
--   - Carrinhos      (carrinhos ativos/expirados)
--
-- Abaixo, views auxiliares para simplificar as consultas
-- de dashboard.
-- ============================================================
--
-- ATENÇÃO: As views abaixo usam LEFT JOIN com caminhos que
-- dependem do TipoIngressoId na tabela Ingressos. Ingressos
-- sem TipoIngressoId (migração/compatibilidade) não aparecem
-- nas views. Se necessário, criar views alternativas via
-- Reservas → Eventos para contemplar todos os ingressos.

-- View: Métricas agregadas por evento
-- JOIN: Eventos -> TiposIngresso -> Ingressos -> CheckIns
-- NOTA: Ingressos sem TipoIngressoId não são contemplados aqui.
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_DashboardEventos')
    DROP VIEW vw_DashboardEventos;
GO

CREATE VIEW vw_DashboardEventos
AS
SELECT
    e.Id                           AS EventoId,
    e.Nome                         AS NomeEvento,
    e.DataEvento,
    e.CapacidadeTotal,
    e.PrecoPadrao,

    -- Ingressos vendidos (Confirmada + Utilizada)
    ISNULL(SUM(CASE
        WHEN ig.Status IN ('Confirmada', 'Utilizada')
        THEN 1 ELSE 0
    END), 0)                        AS TotalIngressosVendidos,

    -- Receita total (soma dos valores finais)
    ISNULL(SUM(CASE
        WHEN ig.Status IN ('Confirmada', 'Utilizada')
        THEN ig.ValorFinal ELSE 0
    END), 0.00)                     AS ReceitaTotal,

    -- Percentual de ocupação
    CASE
        WHEN e.CapacidadeTotal > 0
        THEN ROUND(
            CAST(ISNULL(SUM(CASE
                WHEN ig.Status IN ('Confirmada', 'Utilizada')
                THEN 1 ELSE 0
            END), 0) AS DECIMAL(10,2)) / e.CapacidadeTotal * 100, 2)
        ELSE 0.00
    END                             AS PercentualOcupacao,

    -- Check-ins realizados
    ISNULL(COUNT(DISTINCT ci.Id), 0) AS TotalCheckIns,

    -- Ingressos pendentes de check-in (apenas Confirmada, sem check-in)
    ISNULL(SUM(CASE
        WHEN ig.Status = 'Confirmada' THEN 1 ELSE 0
    END), 0)                        AS PendentesCheckIn,

    -- Ingressos cancelados
    ISNULL(SUM(CASE
        WHEN ig.Status = 'Cancelada' THEN 1 ELSE 0
    END), 0)                        AS TotalCancelados

FROM Eventos e
LEFT JOIN TiposIngresso ti ON ti.EventoId = e.Id
LEFT JOIN Ingressos ig ON ig.TipoIngressoId = ti.Id
LEFT JOIN CheckIns ci ON ci.IngressoId = ig.Id
GROUP BY e.Id, e.Nome, e.DataEvento, e.CapacidadeTotal, e.PrecoPadrao;
GO

PRINT 'View vw_DashboardEventos criada (métricas por evento via TiposIngresso).';
GO

-- View: Métricas por tipo de ingresso (lote)
-- JOIN: TiposIngresso -> Ingressos -> CheckIns
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_DashboardLotes')
    DROP VIEW vw_DashboardLotes;
GO

CREATE VIEW vw_DashboardLotes
AS
SELECT
    ti.Id                           AS TipoIngressoId,
    ti.EventoId,
    ti.Nome                         AS NomeLote,
    ti.Preco                        AS PrecoAtual,
    ti.Capacidade                   AS CapacidadeLote,
    ti.TaxaServico,
    ti.DataInicioVenda,
    ti.DataFimVenda,

    -- Ingressos vendidos para este lote
    ISNULL(SUM(CASE
        WHEN ig.Status IN ('Confirmada', 'Utilizada')
        THEN 1 ELSE 0
    END), 0)                        AS IngressosVendidos,

    -- Capacidade restante do lote
    ti.Capacidade - ISNULL(SUM(CASE
        WHEN ig.Status IN ('Confirmada', 'Utilizada')
        THEN 1 ELSE 0
    END), 0)                        AS CapacidadeRestante,

    -- Receita do lote
    ISNULL(SUM(CASE
        WHEN ig.Status IN ('Confirmada', 'Utilizada')
        THEN ig.ValorFinal ELSE 0
    END), 0.00)                     AS ReceitaLote,

    -- Check-ins realizados neste lote
    ISNULL(COUNT(DISTINCT ci.Id), 0) AS CheckInsRealizados

FROM TiposIngresso ti
LEFT JOIN Ingressos ig ON ig.TipoIngressoId = ti.Id
LEFT JOIN CheckIns ci ON ci.IngressoId = ig.Id
GROUP BY ti.Id, ti.EventoId, ti.Nome, ti.Preco, ti.Capacidade,
         ti.TaxaServico, ti.DataInicioVenda, ti.DataFimVenda;
GO

PRINT 'View vw_DashboardLotes criada (métricas por lote/tipo de ingresso).';
GO

-- ============================================================
-- Resumo da Carga Executada
-- ============================================================
PRINT '';
PRINT '============================================================';
PRINT ' TicketPrime - Script Incremental concluido com sucesso.';
PRINT '------------------------------------------------------------';
PRINT ' Tabelas criadas:';
PRINT '   - TiposIngresso    (lotes/tipos de ingresso)';
PRINT '   - Ingressos        (ingresso digital com codigo unico)';
PRINT '   - CheckIns         (check-in de ingresso)';
PRINT '   - Carrinhos        (carrinho de compras temporario)';
PRINT '   - CarrinhoItens    (itens do carrinho)';
PRINT '   - HistoricoPrecos  (transparencia de precos)';
PRINT '------------------------------------------------------------';
PRINT ' Views criadas:';
PRINT '   - vw_DashboardEventos  (metricas agregadas por evento)';
PRINT '   - vw_DashboardLotes    (metricas por tipo de ingresso)';
PRINT '============================================================';
GO
