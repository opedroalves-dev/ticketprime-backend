-- ============================================================
-- TicketPrime - Script de Criação do Schema Inicial
-- Versão: 1.0.0
-- Descrição: Estrutura inicial do banco de dados TicketPrime.
--            Nenhuma regra de negócio implementada ainda.
-- ============================================================

-- Criação do banco de dados
CREATE DATABASE TicketPrimeDb;
GO

USE TicketPrimeDb;
GO

-- ============================================================
-- Schema principal
-- ============================================================
CREATE SCHEMA [dbo];
GO

-- Tabela de Usuarios
CREATE TABLE [dbo].[Usuarios] (
    [Cpf]   VARCHAR(11)     NOT NULL,
    [Nome]  VARCHAR(100)    NOT NULL,
    [Email] VARCHAR(150)    NOT NULL,
    CONSTRAINT [PK_Usuarios] PRIMARY KEY CLUSTERED ([Cpf])
);

-- Tabela de Eventos
CREATE TABLE [dbo].[Eventos] (
    [Id]             INT IDENTITY(1,1)   NOT NULL,
    [Nome]           VARCHAR(200)        NOT NULL,
    [CapacidadeTotal] INT                NOT NULL,
    [DataEvento]     DATETIME            NOT NULL,
    [PrecoPadrao]    DECIMAL(10,2)       NOT NULL,
    CONSTRAINT [PK_Eventos] PRIMARY KEY CLUSTERED ([Id])
);

PRINT 'Schema TicketPrimeDb criado com sucesso.';
GO
