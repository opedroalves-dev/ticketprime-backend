# Correção AV2 — ticketprime-backend

**Grupo:** Gabriel, Pedro

| # | Item de Avaliação | Nota | Justificativa |
|---|-------------------|:----:|---------------|
| 01 | Padrão AAA nos Testes | 0,0 | Nenhum método com `// Arrange`, `// Act`, `// Assert`; `CupomValidationTests` testa apenas propriedades de modelo |
| 02 | Nomenclatura e Independência | 0,5 | `Cupom_DeveInicializarComValoresPadrao` segue estrutura; zero condicionais |
| 03 | Padrões Arquiteturais | 0,0 | `/docs/analise_arquitetura.md` não existe |
| 04 | Violações Arquiteturais | 0,0 | Arquivo não existe |
| 05 | ADR | 0,0 | `adr.md` existe em `/docs/` mas fora da pasta `/docs/adrs/` exigida |
| 06 | Dívida Técnica | 0,0 | `/docs/registro_divida_tecnica.md` não existe (há `divida-tecnica.md` mas sem as colunas exigidas) |
| 07 | Priorização Dívida | 0,0 | Arquivo não existe no formato exigido |
| 08 | Classificação Manutenção | 0,0 | `/docs/fluxo_manutencao.md` não existe |
| 09 | Pipeline de Liberação | 0,0 | Arquivo não existe |
| 10 | Plano de Iteração | 0,0 | `/docs/plano_iteracao.md` não existe |
| 11 | Quadro Kanban e WIP | 0,0 | Arquivo não existe |
| 12 | Matriz de Riscos | 0,0 | `operacao.md` presente mas sem estrutura de colunas exigida (Risco, Probabilidade, Impacto, Estratégia, Ação Planejada) |
| 13 | Gatilhos de Risco | 0,0 | Estrutura incompleta |
| 14 | Métrica DORA | 0,0 | Sem ficha com 7 campos |
| 15 | Métrica de Qualidade | 0,0 | Segunda métrica não existe |
| 16 | SLO | 0,0 | Sem estrutura de ficha |
| 17 | Error Budget Policy | 0,0 | Sem 3 níveis graduados |
| 18 | Segurança SSDF | 0,5 | Nenhuma credencial hardcoded nos 95 `.cs` |
| 19 | Threat Model e Gates | 0,0 | `/docs/seguranca_ciclo.md` não existe |
| 20 | Topologia Times e DoD | 0,0 | `topologia_times.md` não existe; `release_checklist_final.md` presente mas item requer ambos |

**Nota Final: 1,0 / 10,0**
