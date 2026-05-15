# Operação do TicketPrime

## 1. Matriz de Riscos Operacionais

A matriz a seguir identifica os principais riscos operacionais do sistema TicketPrime, associando cada risco à sua probabilidade de ocorrência, impacto no sistema, ação de mitigação e gatilho que o dispara.

| Risco | Probabilidade | Impacto | Ação | Gatilho |
|-------|---------------|---------|------|---------|
| SQL Injection via parâmetros maliciosos | Baixa | Alto | Utilizar exclusivamente parâmetros nomeados (`@param`) em todas as consultas Dapper, conforme estabelecido no [`ADR-001`](docs/adr.md:13); realizar code review obrigatório em todo Pull Request que contenhaSQL. | Tentativa de inserção de caracteres de escape ou comandos SQL nos campos de entrada (CPF, nome, código do cupom). |
| Excesso de reservas por condição de corrida | Média | Alto | Implementar bloqueio pessimista (`WITH (UPDLOCK, ROWLOCK)` ou `SERIALIZABLE`) na consulta de capacidade dentro de uma transação explícita no endpoint [`POST /api/reservas`](src/TicketPrime.Api/Program.cs:197). | Duas ou mais requisições simultâneas de reserva para o mesmo evento com apenas 1 ingresso restante. |
| Indisponibilidade do banco SQL Server | Baixa | Crítico | Configurar `ConnectRetryCount` na string de conexão em [`appsettings.json`](src/TicketPrime.Api/appsettings.json); monitorar conectividade com health check no endpoint raiz [`GET /`](src/TicketPrime.Api/Program.cs:108). | Falha de rede, reinicialização do servidor de banco ou esgotamento de conexões simultâneas. |
| Falha no cálculo de cupons de desconto | Baixa | Médio | Validar o cálculo de [`ValorFinalPago`](src/TicketPrime.Api/Program.cs:256) com testes unitários em [`ReservaService.CalcularValorFinal`](src/TicketPrime.Api/Services/ReservaService.cs:66) cobrindo cenários de cupom válido, expirado, com valor mínimo não atingido e sem cupom. | Alteração na lógica de porcentagem de desconto ou inclusão de nova regra de valor mínimo sem cobertura de testes. |
| Violação de integridade referencial em reservas | Baixa | Alto | Manter as constraints `FK_Reservas_Usuarios`, `FK_Reservas_Eventos` e `FK_Reservas_Cupons` conforme definido no schema [`001_CreateSchema.sql`](db/scripts/001_CreateSchema.sql:88-103); Nunca executarDELETE em tabelas pai sem verificar reservas associadas. | Tentativa de exclusão de um usuário, evento ou cupom que possua reservas vinculadas. |
| Degradação de performance em consultas de reservas | Média | Médio | Monitorar o plano de execução da query [`GET /api/reservas/{cpf}`](src/TicketPrime.Api/Program.cs:332) com `INNER JOIN` entre Reservas e Eventos; criar índices não clusterizados nas colunas `UsuarioCpf` e `EventoId` da tabela Reservas. | Acúmulo de milhares de registros na tabela Reservas sem índices adequados. |
| Inconsistência entre capacidade total e reservas confirmadas | Média | Alto | Substituir a verificação de capacidade por `COUNT(1)` na tabela Reservas, conforme linha [`src/TicketPrime.Api/Program.cs:234`](src/TicketPrime.Api/Program.cs:234), e testar com cenários de concorrência em [`ReservaServiceTests.cs`](tests/TicketPrime.Tests/ReservaServiceTests.cs). | Atualização manual da capacidade de um evento sem revalidar as reservas existentes. |

---

## 2. Métrica Operacional

### Taxa de Sucesso de Reservas (TSR)

Métrica que monitora a proporção de requisições de reserva concluídas com sucesso em relação ao total de tentativas realizadas no período.

| Campo | Valor |
|-------|-------|
| **Fórmula** | `TSR = (Total de reservas criadas com sucesso ÷ Total de requisições POST /api/reservas) × 100` |
| **Fonte de Dados** | Logs estruturados do endpoint [`POST /api/reservas`](src/TicketPrime.Api/Program.cs:197) — capturar `status code` (201 Created vs. 400 BadRequest) e armazenar em tabela de auditoria ou sistema de logging |
| **Frequência** | Diária, com apuração ao final de cada dia às 23:59 (UTC-3) |
| **Ação se Violado** | Se `TSR < 95%` em um período de 24 horas, acionar alerta no canal de comunicação da equipe e iniciar investigação para identificar a causa raiz — revisar logs de erro, validar regras de negócio (limite por CPF, capacidade do evento) e verificar disponibilidade do banco SQL Server |

---

## 3. Service Level Objective (SLO)

**SLO:** 99,5% de disponibilidade do endpoint de criação de reservas (`POST /api/reservas`) em uma janela de 30 dias corridos.

*Definição:* disponibilidade é calculada como o percentual de requisições ao endpoint que retornam uma resposta HTTP válida (2xx ou 4xx — respostas esperadas do sistema) em relação ao total de requisições recebidas, excluindo janelas de manutenção programada.

---

## 4. Error Budget Policy

O Error Budget (orçamento de falhas) é o limite aceitável de indisponibilidade do sistema dentro da janela do SLO. Para o SLO de 99,5% em 30 dias, o error budget corresponde a **0,5% do tempo** — aproximadamente **3 horas e 36 minutos** de indisponibilidade tolerada por mês.

**Procedimentos quando o Error Budget for violado:**

1. **Congelamento de entregas:** toda e qualquer implantação de novas funcionalidades no ambiente de produção é suspensa imediatamente. Apenas correções críticas (bugs que afetam a integridade dos dados ou a funcionalidade core de reservas) podem ser deployadas.

2. **Investigação forense:** a equipe deve realizar uma Análise de Causa Raiz (RCA) no prazo máximo de 48 horas úteis, documentando o incidente, o tempo de impacto, os endpoints afetados e as ações corretivas necessárias.

3. **Plano de remediação:** com base na RCA, a equipe elabora e executa um plano de ações corretivas com prazos definidos, priorizando estabilidade sobre novas features. O plano deve ser registrado como item no backlog e revisado na sprint seguinte.

4. **Revisão do SLO:** após a recuperação, a equipe reavalia se o SLO de 99,5% permanece adequado ou se ajustes na janela de medição ou no percentual são necessários, considerando a maturidade operacional atual do sistema.

5. **Comunicação:** o responsável técnico comunica o status do error budget e as ações em andamento às partes interessadas (stakeholders acadêmicos/orientadores), garantindo transparência sobre o impacto e as medidas adotadas.

---

## 5. Histórico de Revisões

| Versão | Data | Descrição |
|--------|------|-----------|
| 1.0.0 | 2026-05-15 | Versão inicial do documento de operação do TicketPrime |
