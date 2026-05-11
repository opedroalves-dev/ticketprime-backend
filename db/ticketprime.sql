-- ============================================================
-- TicketPrime - Script Completo do Schema do Banco de Dados
-- Versão: 1.0.0
-- Descrição: Estrutura completa do banco de dados TicketPrime
--            conforme especificação do trabalho.
-- ============================================================

-- ============================================================
-- Tabela: Usuarios
-- ============================================================
CREATE TABLE Usuarios (
    Cpf         VARCHAR(11)     NOT NULL,
    Nome        VARCHAR(100)    NOT NULL,
    Email       VARCHAR(150)    NOT NULL,
    Telefone    VARCHAR(20)     NULL,
    CONSTRAINT PK_Usuarios PRIMARY KEY (Cpf)
);

-- ============================================================
-- Tabela: Eventos
-- ============================================================
CREATE TABLE Eventos (
    Id          INT IDENTITY(1,1)   NOT NULL,
    Nome        VARCHAR(200)        NOT NULL,
    Data        DATETIME            NOT NULL,
    Local       VARCHAR(200)        NOT NULL,
    Capacidade  INT                 NOT NULL,
    Preco       DECIMAL(10,2)       NOT NULL,
    CONSTRAINT PK_Eventos PRIMARY KEY (Id)
);

-- ============================================================
-- Tabela: Cupons
-- ============================================================
CREATE TABLE Cupons (
    Id          INT IDENTITY(1,1)   NOT NULL,
    Codigo      VARCHAR(50)         NOT NULL,
    Desconto    DECIMAL(5,2)        NOT NULL,
    Validade    DATETIME            NOT NULL,
    CONSTRAINT PK_Cupons PRIMARY KEY (Id)
);

-- ============================================================
-- Tabela: Reservas (tabela central)
-- ============================================================
CREATE TABLE Reservas (
    Id              INT IDENTITY(1,1)   NOT NULL,
    UsuarioCpf      VARCHAR(11)         NOT NULL,
    EventoId        INT                 NOT NULL,
    CupomUtilizado  INT                 NULL,
    ValorFinalPago  DECIMAL(10,2)       NOT NULL,
    CONSTRAINT PK_Reservas PRIMARY KEY (Id),
    CONSTRAINT FK_Reservas_Usuarios FOREIGN KEY (UsuarioCpf)
        REFERENCES Usuarios(Cpf),
    CONSTRAINT FK_Reservas_Eventos FOREIGN KEY (EventoId)
        REFERENCES Eventos(Id),
    CONSTRAINT FK_Reservas_Cupons FOREIGN KEY (CupomUtilizado)
        REFERENCES Cupons(Id)
);
