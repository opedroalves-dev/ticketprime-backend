# Requisitos do TicketPrime

## 1. Visão Geral do Sistema

O **TicketPrime** é uma API backend acadêmica para venda de ingressos, cadastro de eventos, cupons, usuários e reservas, com armazenamento persistente em SQL Server e acesso a dados via Dapper com parâmetros nomeados.

O sistema permite que usuários se cadastrem, consultem eventos disponíveis, realizem reservas de ingressos com controle de capacidade e limite por CPF, e utilizem cupons de desconto sobre o valor final do ingresso.

---

## 2. Entidades do Domínio

### 2.1. Usuarios
Responsável pelo cadastro dos compradores de ingressos.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Cpf` | `VARCHAR(11) NOT NULL` | CPF do usuário (PK) |
| `Nome` | `VARCHAR(100) NOT NULL` | Nome completo |
| `Email` | `VARCHAR(150) NOT NULL` | E-mail para contato |

### 2.2. Eventos
Representa os eventos para os quais ingressos são vendidos.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Id` | `INT IDENTITY(1,1) NOT NULL` | Identificador único do evento (PK) |
| `Nome` | `VARCHAR(200) NOT NULL` | Nome do evento |
| `Data` | `DATETIME NOT NULL` | Data e hora de realização |
| `Local` | `VARCHAR(200) NOT NULL` | Local onde ocorrerá |
| `Capacidade` | `INT NOT NULL` | Número máximo de ingressos |
| `Preco` | `DECIMAL(10,2) NOT NULL` | Valor unitário do ingresso |

### 2.3. Cupons
Cupons de desconto aplicáveis às reservas.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Id` | `INT IDENTITY(1,1) NOT NULL` | Identificador único do cupom (PK) |
| `Codigo` | `VARCHAR(50) NOT NULL` | Código alfanumérico do cupom |
| `Desconto` | `DECIMAL(5,2) NOT NULL` | Percentual de desconto (ex.: 10.00 = 10%) |
| `Validade` | `DATETIME NOT NULL` | Data limite para uso do cupom |

### 2.4. Reservas
Registro central que associa um usuário a um evento, com valor final calculado.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Id` | `INT IDENTITY(1,1) NOT NULL` | Identificador único da reserva (PK) |
| `UsuarioCpf` | `VARCHAR(11) NOT NULL` | FK para Usuarios(Cpf) |
| `EventoId` | `INT NOT NULL` | FK para Eventos(Id) |
| `CupomUtilizado` | `INT NULL` | FK para Cupons(Id) — opcional |
| `ValorFinalPago` | `DECIMAL(10,2) NOT NULL` | Valor final após aplicação de cupom |

---

## 3. Regras de Negócio

1. **Limite por CPF:** cada CPF pode reservar no máximo **1 ingresso por evento**.
2. **Controle de capacidade:** uma reserva só pode ser confirmada se ainda houver ingressos disponíveis para o evento.
3. **Cupons:** um cupom pode ser aplicado opcionalmente a uma reserva. O cupom deve estar dentro da data de validade para ser utilizado.
4. **Cálculo do valor final:** `ValorFinalPago = PrecoDoEvento × (1 - DescontoDoCupom / 100)`. Se nenhum cupom for utilizado, `ValorFinalPago = PrecoDoEvento`.
5. **Integridade referencial:** toda reserva deve estar associada a um usuário e a um evento existentes.

---

## 4. Restrições Técnicas Obrigatórias

- **Sem Entity Framework** — acesso a dados exclusivamente com Dapper.
- **Sem banco em memória** — apenas SQL Server real.
- **Sem SQL injection** — todas as consultas devem utilizar parâmetros nomeados (`@param`).
- **Sem concatenação SQL** — nenhuma string SQL deve ser construída por concatenação.
- **Sem interpolação SQL** — nenhuma string SQL deve utilizar interpolação (`$"..."`).
- **Nomes de pastas fixos** — as pastas `/docs`, `/db`, `/src` e `/tests` não podem ser renomeadas.

---

## 5. Histórias de Usuário

### 5.1. História 1 — Cadastro de Evento

**Como** organizador de eventos,
**Quero** cadastrar um novo evento informando nome, data, local, capacidade e preço,
**Para** que os ingressos fiquem disponíveis para reserva pelos usuários.

#### Critérios de Aceitação (BDD)

**Cenário 1: Cadastro de evento com dados válidos**

Dado que o organizador informou nome "Show da Banda X", data "2026-12-15 20:00", local "Arena Central", capacidade 500 e preço 150.00  
Quando a requisição de cadastro for enviada  
Então o sistema deve criar o evento e retornar os dados cadastrados com um identificador único

**Cenário 2: Cadastro de evento com capacidade inválida**

Dado que o organizador informou capacidade igual a 0  
Quando a requisição de cadastro for enviada  
Então o sistema deve rejeitar o cadastro com mensagem de erro informando que a capacidade deve ser maior que zero

**Cenário 3: Cadastro de evento com preço inválido**

Dado que o organizador informou preço negativo  
Quando a requisição de cadastro for enviada  
Então o sistema deve rejeitar o cadastro com mensagem de erro informando que o preço deve ser maior que zero

---

### 5.2. História 2 — Reserva de Ingresso

**Como** usuário do TicketPrime,
**Quero** reservar um ingresso para um evento informando meu CPF, o evento desejado e, opcionalmente, um cupom de desconto,
**Para** garantir minha vaga no evento com o valor calculado corretamente.

#### Critérios de Aceitação (BDD)

**Cenário 1: Reserva bem-sucedida sem cupom**

Dado que existe um evento "Show da Banda X" com capacidade para 500 pessoas e preço de R$ 150,00  
E que o usuário com CPF "12345678901" está cadastrado  
Quando o usuário solicitar a reserva de 1 ingresso para este evento sem cupom  
Então o sistema deve criar a reserva com valor final de R$ 150,00  
E o sistema deve reduzir a capacidade disponível do evento em 1

**Cenário 2: Reserva bem-sucedida com cupom válido**

Dado que existe um evento "Show da Banda X" com preço de R$ 200,00  
E que o usuário com CPF "12345678901" está cadastrado  
E que existe um cupom "DESCONTO10" com 10% de desconto dentro da validade  
Quando o usuário solicitar a reserva de 1 ingresso utilizando o cupom "DESCONTO10"  
Então o sistema deve criar a reserva com valor final de R$ 180,00  
E o sistema deve associar o cupom à reserva

**Cenário 3: Reserva rejeitada por CPF duplicado no mesmo evento**

Dado que o usuário com CPF "12345678901" já possui uma reserva para o evento "Show da Banda X"  
Quando o mesmo usuário tentar reservar outro ingresso para o mesmo evento  
Então o sistema deve rejeitar a reserva com mensagem de erro informando que o CPF já possui reserva para este evento

**Cenário 4: Reserva rejeitada por capacidade esgotada**

Dado que o evento "Show da Banda X" possui capacidade para 1 pessoa  
E que já existe uma reserva confirmada para este evento  
Quando um novo usuário tentar reservar um ingresso para este evento  
Então o sistema deve rejeitar a reserva com mensagem de erro informando que o evento está com capacidade esgotada

**Cenário 5: Reserva rejeitada por cupom expirado**

Dado que existe um cupom "DESCONTO10" com validade em 01/01/2020  
E que a data atual é superior à data de validade  
Quando o usuário tentar utilizar este cupom em uma reserva  
Então o sistema deve rejeitar o uso do cupom com mensagem de erro informando que o cupom está expirado

**Cenário 6: Reserva rejeitada por usuário não cadastrado**

Dado que o CPF "00000000000" não está cadastrado no sistema  
Quando um usuário tentar realizar uma reserva com este CPF  
Então o sistema deve rejeitar a reserva com mensagem de erro informando que o CPF não está cadastrado

---

### 5.3. História 3 — Consulta de Reservas por CPF

**Como** usuário do TicketPrime,
**Quero** consultar todas as minhas reservas informando meu CPF,
**Para** visualizar os eventos para os quais possuo ingresso e os valores pagos.

#### Critérios de Aceitação (BDD)

**Cenário 1: Consulta de reservas para CPF com reservas existentes**

Dado que o usuário com CPF "12345678901" possui 3 reservas em eventos diferentes  
Quando o sistema consultar as reservas deste CPF  
Então o sistema deve retornar uma lista contendo as 3 reservas  
E cada reserva deve conter os dados do evento, o valor pago e o cupom utilizado (se houver)

**Cenário 2: Consulta de reservas para CPF sem reservas**

Dado que o usuário com CPF "12345678901" está cadastrado  
E que este usuário não possui nenhuma reserva  
Quando o sistema consultar as reservas deste CPF  
Então o sistema deve retornar uma lista vazia

**Cenário 3: Consulta de reservas para CPF não cadastrado**

Dado que o CPF "00000000000" não está cadastrado no sistema  
Quando o sistema consultar as reservas deste CPF  
Então o sistema deve retornar uma mensagem de erro informando que o CPF não foi encontrado

---

### 5.4. História 4 — Cadastro de Cupom de Desconto

**Como** organizador de eventos,
**Quero** cadastrar cupons de desconto com código, percentual e data de validade,
**Para** que os usuários possam obter descontos no valor dos ingressos.

#### Critérios de Aceitação (BDD)

**Cenário 1: Cadastro de cupom com dados válidos**

Dado que o organizador informou código "PROMO20", desconto de 20% e validade para 31/12/2026  
Quando a requisição de cadastro for enviada  
Então o sistema deve criar o cupom e retornar os dados cadastrados com um identificador único

**Cenário 2: Cadastro de cupom com desconto inválido**

Dado que o organizador informou desconto de 0%  
Quando a requisição de cadastro for enviada  
Então o sistema deve rejeitar o cadastro com mensagem de erro informando que o desconto deve ser maior que zero

**Cenário 3: Cadastro de cupom com código duplicado**

Dado que já existe um cupom com código "PROMO20" cadastrado  
Quando o organizador tentar cadastrar outro cupom com o mesmo código  
Então o sistema deve rejeitar o cadastro com mensagem de erro informando que o código já existe

---

### 5.5. História 5 — Cadastro de Usuário

**Como** interessado em adquirir ingressos,
**Quero** me cadastrar no TicketPrime informando CPF, nome e e-mail,
**Para** poder realizar reservas de ingressos em eventos.

#### Critérios de Aceitação (BDD)

**Cenário 1: Cadastro de usuário com dados válidos**

Dado que o interessado informou CPF "12345678901", nome "João Silva" e e-mail "joao@email.com"
Quando a requisição de cadastro for enviada  
Então o sistema deve criar o usuário e retornar os dados cadastrados

**Cenário 2: Cadastro de usuário com CPF já existente**

Dado que já existe um usuário cadastrado com CPF "12345678901"  
Quando um novo cadastro for tentado com o mesmo CPF  
Então o sistema deve rejeitar o cadastro com mensagem de erro informando que o CPF já está cadastrado

**Cenário 3: Cadastro de usuário com dados obrigatórios ausentes**

Dado que o interessado não informou o nome  
Quando a requisição de cadastro for enviada  
Então o sistema deve rejeitar o cadastro com mensagem de erro informando que o nome é obrigatório

---

## 6. Endpoints da API

### AV1

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/eventos` | Cadastrar um novo evento |
| `GET` | `/api/eventos` | Listar todos os eventos |
| `POST` | `/api/cupons` | Cadastrar um novo cupom |
| `POST` | `/api/usuarios` | Cadastrar um novo usuário |

### AV2

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/reservas/{cpf}` | Listar reservas de um usuário pelo CPF |
| `POST` | `/api/reservas` | Criar uma nova reserva |

---

## 7. Glossário

| Termo | Definição |
|-------|-----------|
| **TicketPrime** | Sistema acadêmico de venda de ingressos |
| **Reserva** | Vínculo entre um usuário e um evento, com valor final calculado |
| **Capacidade** | Número máximo de ingressos disponíveis para um evento |
| **Cupom** | Código promocional que concede percentual de desconto |
| **Limite por CPF** | Restrição que permite no máximo 1 reserva por CPF por evento |
| **Valor final** | Preço do ingresso após aplicação opcional de cupom de desconto |

---

## 8. Histórico de Revisões

| Versão | Data | Descrição |
|--------|------|-----------|
| 1.0.0 | 2026-05-12 | Versão inicial dos requisitos do TicketPrime |
