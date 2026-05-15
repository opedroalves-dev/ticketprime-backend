# ADR-001: Arquitetura de Acesso a Dados com Dapper e SQL Manual

## Contexto

O TicketPrime é um sistema acadêmico de venda de ingressos que exige armazenamento persistente em SQL Server, controle explícito das regras de negócio (limite por CPF, capacidade de evento, cálculo de desconto, integridade referencial) e proteção contra SQL Injection. O enunciado oficial do projeto determina que o acesso a dados deve ser realizado exclusivamente com Dapper e SQL manual, sendo vedado o uso de Entity Framework ou banco em memória.

Diante disso, faz-se necessário definir a estratégia arquitetural para a camada de persistência que atenda aos requisitos funcionais e restrições técnicas impostas.

## Decisão

Adotar **.NET 8 Minimal API** como framework de apresentação, **Dapper** como micro-ORM e **SQL manual** como estratégia de acesso a dados, com as seguintes diretrizes:

1.  **Acesso a dados exclusivamente via Dapper:** todas as operações de leitura e escrita no banco SQL Server utilizam o método [`Dapper.SqlMapper`](https://github.com/DapperLib/Dapper) (`QueryAsync`, `ExecuteAsync`) encapsulado em serviços específicos.
2.  **SQL manual com parâmetros nomeados:** todas as instruções SQL são escritas manualmente em strings literais, utilizando parâmetros nomeados (`@param`) para vinculação de valores, eliminando concatenação e interpolação de strings.
3.  **Controle explícito de regras de negócio nas consultas:** as regras de limite por CPF, verificação de capacidade, validação de cupom e cálculo do valor final são implementadas diretamente nos serviços da aplicação, que orquestram as chamadas ao banco via Dapper.
4.  **Camada de serviços como orquestradores:** a lógica de negócio reside em classes de serviço ([`ReservaService`](src/TicketPrime.Api/Services/ReservaService.cs), etc.) que injetam [`SqlConnection`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection) e executam os comandos Dapper, mantendo a API (endpoints Minimal API enxutos).

## Consequências

### Prós:

- **Segurança contra SQL Injection:** o uso obrigatório de parâmetros nomeados (`@param`) impede a injeção maliciosa de comandos SQL, conforme determina o enunciado.
- **Simplicidade e baixo acoplamento:** Dapper é uma biblioteca leve que mapeia resultados de consultas para objetos sem abstrações complexas (Unit of Work, Change Tracking), reduzindo a curva de aprendizado e o código boilerplate.
- **Controle explícito do SQL:** a equipe mantém domínio total sobre as consultas executadas, permitindo otimizações finas e depuração direta dos comandos enviados ao banco.
- **Conformidade com o enunciado acadêmico:** a arquitetura atende rigorosamente às restrições de "sem Entity Framework", "sem banco em memória" e "sem concatenação/interpolação SQL".
- **Performance previsível:** por ser um thin wrapper sobre ADO.NET, Dapper adiciona overhead mínimo, resultando em tempos de resposta próximos ao acesso bruto ao banco.

### Contras:

- **Maior carga de escrita manual de SQL:** cada operação de persistência exige a redação manual da instrução SQL, aumentando o volume de código comparado a ORMs com geração automática de consultas.
- **Ausência de migrations automáticas:** não há mecanismo embutido para versionamento de schema; os scripts SQL devem ser gerenciados manualmente na pasta [`db/scripts/`](db/scripts/).
- **Responsabilidade explícita por integridade referencial:** como não há Unit of Work, o desenvolvedor deve coordenar manualmente transações e garantir a consistência entre múltiplas operações (ex.: reserva e atualização de capacidade).
