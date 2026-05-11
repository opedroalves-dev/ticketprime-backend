# Requisitos do TicketPrime

## Visão Geral do Sistema

O **TicketPrime** é uma API backend acadêmica voltada à venda de ingressos, cadastro de eventos, gerenciamento de cupons, cadastro de usuários e controle de reservas. O sistema opera com armazenamento persistente em SQL Server e acesso a dados via Dapper com parâmetros nomeados.

### Funcionalidades Principais

- Cadastro e listagem de eventos
- Cadastro de cupons de desconto
- Cadastro de usuários (identificados por CPF)
- Criação e consulta de reservas de ingressos
- Controle de capacidade por evento
- Limite de ingressos por CPF por evento

---

## Histórias de Usuário

### US-01: Cadastro de Evento

**Como** organizador,
**Quero** cadastrar um novo evento informando nome, data, local, descrição e capacidade total,
**Para** disponibilizar ingressos para reserva aos usuários.

#### Critérios de Aceitação (BDD)

**Cenário 1: Cadastro bem-sucedido de evento**

Dado que os dados do evento são válidos (nome, data futura, local, descrição e capacidade positiva),
Quando a requisição de cadastro for enviada para o sistema,
Então o evento deve ser registrado com sucesso.
E o sistema deve retornar os dados do evento criado, incluindo um identificador único.

**Cenário 2: Cadastro com capacidade inválida**

Dado que a capacidade informada é menor ou igual a zero,
Quando a requisição de cadastro for enviada para o sistema,
Então o sistema deve rejeitar o cadastro.
E deve retornar uma mensagem de erro indicando que a capacidade deve ser um valor positivo.

**Cenário 3: Cadastro com data passada**

Dado que a data do evento informada é anterior à data atual,
Quando a requisição de cadastro for enviada para o sistema,
Então o sistema deve rejeitar o cadastro.
E deve retornar uma mensagem de erro informando que a data do evento deve ser futura.

---

### US-02: Cadastro de Usuário

**Como** usuário,
**Quero** realizar meu cadastro no sistema informando CPF e nome,
**Para** poder realizar reservas de ingressos para eventos.

#### Critérios de Aceitação (BDD)

**Cenário 1: Cadastro bem-sucedido de usuário**

Dado que o CPF informado é válido e ainda não está cadastrado no sistema,
Quando a requisição de cadastro for enviada,
Então o usuário deve ser registrado com sucesso.
E o sistema deve retornar os dados do usuário criado, incluindo o CPF como identificador.

**Cenário 2: Cadastro com CPF já existente**

Dado que o CPF informado já está cadastrado no sistema,
Quando a requisição de cadastro for enviada,
Então o sistema deve rejeitar o cadastro.
E deve retornar uma mensagem de erro informando que o CPF já está cadastrado.

**Cenário 3: Cadastro com CPF inválido**

Dado que o CPF informado não possui um formato válido,
Quando a requisição de cadastro for enviada,
Então o sistema deve rejeitar o cadastro.
E deve retornar uma mensagem de erro informando que o CPF é inválido.

---

### US-03: Cadastro de Cupom

**Como** organizador,
**Quero** cadastrar um cupom de desconto informando código, porcentagem de desconto, valor mínimo, data de validade e limite de usos,
**Para** oferecer descontos aos usuários nas reservas de ingressos.

#### Critérios de Aceitação (BDD)

**Cenário 1: Cadastro bem-sucedido de cupom**

Dado que os dados do cupom são válidos (código único, porcentagem de desconto positiva, valor mínimo válido, data de validade futura e limite de usos positivo),
Quando a requisição de cadastro for enviada,
Então o cupom deve ser registrado com sucesso.
E o sistema deve retornar os dados do cupom criado, incluindo seu código.

**Cenário 2: Cadastro com código duplicado**

Dado que já existe um cupom cadastrado com o mesmo código,
Quando a requisição de cadastro for enviada,
Então o sistema deve rejeitar o cadastro.
E deve retornar uma mensagem de erro informando que o código do cupom já existe.

**Cenário 3: Cadastro com porcentagem de desconto inválida**

Dado que a porcentagem de desconto informada é menor ou igual a zero,
Quando a requisição de cadastro for enviada,
Então o sistema deve rejeitar o cadastro.
E deve retornar uma mensagem de erro informando que a porcentagem de desconto deve ser positiva.

**Cenário 4: Cadastro com data de validade no passado**

Dado que a data de validade informada é anterior à data atual,
Quando a requisição de cadastro for enviada,
Então o sistema deve rejeitar o cadastro.
E deve retornar uma mensagem de erro informando que a data de validade deve ser futura.

---

### US-04: Criação de Reserva

**Como** usuário,
**Quero** reservar ingressos para um evento informando meu CPF, o identificador do evento e a quantidade desejada,
**Para** garantir minha entrada no evento.

#### Critérios de Aceitação (BDD)

**Cenário 1: Reserva bem-sucedida**

Dado que o evento existe, possui capacidade disponível e o CPF informado ainda não atingiu o limite de ingressos para este evento,
Quando a requisição de reserva for enviada com quantidade válida,
Então a reserva deve ser criada com sucesso.
E a capacidade disponível do evento deve ser reduzida conforme a quantidade reservada.

**Cenário 2: Reserva com capacidade insuficiente**

Dado que a quantidade de ingressos solicitada excede a capacidade disponível do evento,
Quando a requisição de reserva for enviada,
Então o sistema deve rejeitar a reserva.
E deve retornar uma mensagem de erro informando que não há ingressos suficientes disponíveis.

**Cenário 3: Reserva que excede o limite por CPF**

Dado que o usuário já possui uma quantidade de ingressos reservados para o evento equivalente ao limite por CPF,
Quando a requisição de reserva for enviada com uma quantidade adicional,
Então o sistema deve rejeitar a reserva.
E deve retornar uma mensagem de erro informando que o limite de ingressos por CPF para este evento foi atingido.

**Cenário 4: Reserva para evento inexistente**

Dado que o identificador do evento informado não corresponde a nenhum evento cadastrado,
Quando a requisição de reserva for enviada,
Então o sistema deve rejeitar a reserva.
E deve retornar uma mensagem de erro informando que o evento não foi encontrado.

---

### US-05: Aplicação de Cupom de Desconto na Reserva

**Como** usuário,
**Quero** informar um cupom de desconto no momento da reserva,
**Para** obter um desconto na reserva de ingresso.

#### Critérios de Aceitação (BDD)

**Cenário 1: Cupom válido aplicado com sucesso**

Dado que o cupom informado existe, está ativo, dentro do período de validade e ainda não excedeu o limite de usos,
Quando a reserva for criada com o código do cupom,
Então o desconto deve ser aplicado sobre o total da reserva.
E o sistema deve registrar o uso do cupom, decrementando o limite de usos disponível.

**Cenário 2: Cupom expirado**

Dado que o cupom informado está fora do período de validade (data de expiração já passou),
Quando a reserva for criada com o código do cupom,
Então o sistema deve rejeitar a aplicação do cupom.
E deve retornar uma mensagem de erro informando que o cupom está expirado.

**Cenário 3: Cupom com limite de usos esgotado**

Dado que o cupom informado já atingiu o número máximo de utilizações permitidas,
Quando a reserva for criada com o código do cupom,
Então o sistema deve rejeitar a aplicação do cupom.
E deve retornar uma mensagem de erro informando que o cupom não possui mais usos disponíveis.

**Cenário 4: Cupom inexistente**

Dado que o código do cupom informado não existe no sistema,
Quando a reserva for criada com o código do cupom,
Então o sistema deve rejeitar a aplicação do cupom.
E deve retornar uma mensagem de erro informando que o cupom não foi encontrado.

---

### US-06: Consulta de Reservas por CPF

**Como** usuário,
**Quero** consultar todas as minhas reservas informando meu CPF,
**Para** visualizar os ingressos que reservei para eventos.

#### Critérios de Aceitação (BDD)

**Cenário 1: Consulta com reservas existentes**

Dado que existem reservas registradas para o CPF informado,
Quando a requisição de consulta for enviada,
Então o sistema deve retornar a lista de reservas associadas àquele CPF.
E cada reserva deve conter os detalhes do evento, a quantidade de ingressos e o status da reserva.

**Cenário 2: Consulta sem reservas**

Dado que não existem reservas registradas para o CPF informado,
Quando a requisição de consulta for enviada,
Então o sistema deve retornar uma lista vazia.
E não deve ser considerado um erro.

---

## Glossário

| Termo | Definição |
|---|---|
| **Evento** | Ocorrência cadastrada no sistema com nome, data, local, descrição, capacidade total e preço padrão do ingresso. |
| **Ingresso** | Unidade individual de entrada para um evento, com um preço padrão definido, associada a uma reserva. |
| **Reserva** | Solicitação de um ou mais ingressos para um evento, vinculada a um CPF e opcionalmente a um cupom de desconto. |
| **Cupom** | Código promocional que concede desconto percentual sobre o preço padrão dos ingressos, com valor mínimo de aplicação, período de validade e limite de usos. |
| **CPF** | Documento de identificação do usuário, utilizado como chave para controle de limite de ingressos por evento. |
| **Capacidade** | Número máximo de ingressos disponíveis para um evento. |

---

## Regras de Negócio

1. **Controle de Capacidade** — A quantidade total de ingressos reservados para um evento não pode exceder sua capacidade total.
2. **Limite por CPF** — Cada CPF possui um limite máximo de ingressos que pode reservar para um mesmo evento.
3. **Validade do Cupom** — Cupons só podem ser aplicados dentro de seu período de validade e enquanto houver usos disponíveis.
4. **Data Futura** — Eventos e cupons devem ser cadastrados com data futura em relação ao momento do cadastro.
5. **Reserva Reduz Capacidade** — Cada reserva confirmada reduz a capacidade disponível do evento.
6. **CPF Único** — Cada CPF só pode ser cadastrado uma única vez no sistema.
7. **Código de Cupom Único** — Cada cupom deve possuir um código único no sistema.
