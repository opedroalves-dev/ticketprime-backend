-- ============================================================
-- TicketPrime - Script Completo do Schema do Banco de Dados
-- Versão: 2.0.0
-- Descrição: Estrutura completa do banco de dados TicketPrime
--            conforme enunciado oficial e compatível com a API.
-- ============================================================

-- ============================================================
-- Tabela: Usuarios
-- ============================================================
CREATE TABLE Usuarios (
    Cpf         VARCHAR(11)     NOT NULL,
    Nome        VARCHAR(100)    NOT NULL,
    Email       VARCHAR(150)    NOT NULL,
    CONSTRAINT PK_Usuarios PRIMARY KEY (Cpf)
);

-- ============================================================
-- Tabela: Eventos
-- ============================================================
CREATE TABLE Eventos (
    Id               INT IDENTITY(1,1)   NOT NULL,
    Nome             VARCHAR(200)        NOT NULL,
    CapacidadeTotal  INT                 NOT NULL,
    DataEvento       DATETIME            NOT NULL,
    PrecoPadrao      DECIMAL(10,2)       NOT NULL,
    CONSTRAINT PK_Eventos PRIMARY KEY (Id)
);

-- ============================================================
-- Tabela: Cupons
-- ============================================================
CREATE TABLE Cupons (
    Codigo              VARCHAR(50)    NOT NULL,
    PorcentagemDesconto DECIMAL(5,2)   NOT NULL,
    ValorMinimoRegra    DECIMAL(10,2)  NOT NULL,
    CONSTRAINT PK_Cupons PRIMARY KEY (Codigo)
);

-- ============================================================
-- Tabela: Reservas
-- ============================================================
CREATE TABLE Reservas (
    Id              INT IDENTITY(1,1)   NOT NULL,
    UsuarioCpf      VARCHAR(11)         NOT NULL,
    EventoId        INT                 NOT NULL,
    CupomUtilizado  VARCHAR(50)         NULL,
    ValorFinalPago  DECIMAL(10,2)       NOT NULL,
    CONSTRAINT PK_Reservas PRIMARY KEY (Id),
    CONSTRAINT FK_Reservas_Usuarios FOREIGN KEY (UsuarioCpf)
        REFERENCES Usuarios(Cpf),
    CONSTRAINT FK_Reservas_Eventos FOREIGN KEY (EventoId)
        REFERENCES Eventos(Id),
    CONSTRAINT FK_Reservas_Cupons FOREIGN KEY (CupomUtilizado)
        REFERENCES Cupons(Codigo)
);
