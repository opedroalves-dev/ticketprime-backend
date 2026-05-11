# Release Checklist Final - TicketPrime

## Pré-Release

### Código e Arquitetura

- [ ] **Code Review**: todo o código revisado por pares
- [ ] **Regras de Negócio**: todas as regras implementadas e validadas
- [ ] **Endpoints**: todos os endpoints da Minimal API mapeados e funcionais
- [ ] **Dapper**: todas as consultas utilizam parâmetros nomeados (`@param`)
- [ ] **Sem EF**: nenhuma referência ao Entity Framework nos projetos
- [ ] **Sem InMemory**: nenhum uso de banco em memória
- [ ] **Sem SQL interpolation**: nenhuma string SQL interpolada ou concatenada
- [ ] **Nomenclatura**: nomes de pastas e rotas mantidos conforme definido

### Banco de Dados

- [ ] **Migration Scripts**: scripts de banco versionados em `/db/scripts/`
- [ ] **Schema Final**: schema do banco reflete o modelo de dados completo
- [ ] **Índices**: índices necessários criados para performance
- [ ] **Rollback**: script de rollback disponível para cada migration

### Testes

- [ ] **Unit Tests**: cobertura mínima de 80% nas camadas de domínio e aplicação
- [ ] **Integration Tests**: testes de integração com banco real (SQL Server)
- [ ] **Testes de API**: testes de contrato dos endpoints
- [ ] **Todos verdes**: `dotnet test` executa sem falhas

### Configuração

- [ ] **appsettings.json**: connection string atualizada para produção
- [ ] **appsettings.Production.json**: configurado para ambiente produtivo
- [ ] **Variáveis de ambiente**: segredos e connection strings externalizados
- [ ] **CORS**: configuração de CORS ajustada para produção

## Release

### Build

- [ ] Build de Release compila sem erros: `dotnet build -c Release`
- [ ] Publicação gerada: `dotnet publish -c Release`
- [ ] Versão do assembly atualizada (AssemblyInfo / csproj)

### Deploy

- [ ] **Banco**: scripts executados no banco de produção
- [ ] **Aplicação**: binários publicados no ambiente destino
- [ ] **Health Check**: endpoint de saúde respondendo (quando implementado)
- [ ] **Logs**: sistema de logging configurado e funcional

### Documentação

- [ ] **README.md**: atualizado com instruções de deploy
- [ ] **Swagger/OpenAPI**: documentação da API disponível (quando implementado)
- [ ] **CHANGELOG.md**: registro de mudanças da versão

## Pós-Release

- [ ] Monitoramento de erros ativo
- [ ] Backup do banco de dados verificado
- [ ] Rollback plan documentado e testado
