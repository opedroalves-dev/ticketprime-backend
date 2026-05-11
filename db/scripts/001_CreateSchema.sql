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

-- Placeholder: as tabelas de domínio serão criadas aqui
-- quando as regras de negócio forem implementadas.

-- Exemplo futuro:
-- CREATE TABLE [dbo].[Tickets] (
--     [Id]            INT IDENTITY(1,1)   NOT NULL,
--     [Title]         NVARCHAR(200)       NOT NULL,
--     [Description]   NVARCHAR(2000)      NULL,
--     [CreatedAt]     DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
--     CONSTRAINT [PK_Tickets] PRIMARY KEY CLUSTERED ([Id])
-- );

PRINT 'Schema TicketPrimeDb criado com sucesso.';
GO
