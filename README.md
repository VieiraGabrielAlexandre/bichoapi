# BichoApi — Guia de Teste & Uso

> Projeto educacional. **Não utilize comercialmente** nem em produção real.

## 📦 Pré-requisitos

* **.NET 8 SDK** (ou runtime 8)
* **Docker** (para subir Postgres rapidamente)
* **curl** ou **Postman** (recomendado)
* Opcional: Rider/VSCode

## 🗂️ Collection do Postman

* Importe a collection em: `docs/BichoApi.postman_collection.json`
* (Opcional) Ambiente sugerido:

    * `baseUrl = http://localhost:5000`
    * `userId` (preenchido após criar usuário)
    * `drawId` (preenchido após criar sorteio)

> As requisições já incluem `Content-Type: application/json` e payloads de exemplo.

---

## 🚀 Subindo local (Postgres + EF + API)

1. **Banco de dados**

```bash
docker run --name pg \
  -e POSTGRES_PASSWORD=localpass \
  -e POSTGRES_USER=appuser \
  -e POSTGRES_DB=bicho \
  -p 5432:5432 -d postgres:16
```

2. **Config (já vem pronta para localhost)**
   `src/BichoApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Db": "Host=localhost;Username=appuser;Password=localpass;Database=bicho"
  }
}
```

3. **Ferramentas EF (se necessário)**

```bash
dotnet tool install -g dotnet-ef --version 8.*
```

4. **Restaurar, migrar e subir**

```bash
dotnet restore
dotnet ef database update --project src/BichoApi
dotnet run --project src/BichoApi
```

5. **Interfaces**

* Swagger: `http://localhost:5000/swagger`
* Health:  `http://localhost:5000/health`

---

## 🧪 Fluxo de teste (rápido)

### 1) Criar usuário

```bash
curl -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -d '{ "name":"Joao", "email":"joao@ex.com" }'
# => { "id": 1 }
```

### 2) Recarregar carteira

```bash
curl -X POST http://localhost:5000/api/wallets/1/recharge \
  -H "Content-Type: application/json" \
  -d '20000'   # R$ 200,00
```

### 3) Criar aposta (ex.: CENTENA\_3X)

```bash
curl -X POST http://localhost:5000/api/bets \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 1,
    "modality": "CENTENA_3X",
    "positions": ["RIGHT"],
    "prizeWindow": "_1_5",
    "stakeCents": 500,
    "payload": { "hundred": "517" }
  }'
# => { "id": <betId> }
```

### 4) Cadastrar sorteio

```bash
curl -X POST http://localhost:5000/api/draws \
  -H "Content-Type: application/json" \
  -d '{
    "market": "PTM",
    "drawDate": "2025-08-26",
    "drawTime": "10:00:00",
    "prizes": ["7517","1234","9876","0017","4551","8888"]
  }'
# => { "id": <drawId> }
```

### 5) Avaliar apostas

```bash
curl -X POST http://localhost:5000/api/draws/<drawId>/evaluate
# => { "count": <resultados> }
```

### 6) Relatórios & saldo

```bash
# payout ratio (stakes vs payouts)
curl "http://localhost:5000/api/reports/payout-ratio?from=2025-08-26T00:00:00Z&to=2025-08-27T00:00:00Z"

# total apostado
curl "http://localhost:5000/api/reports/total-stake?from=2025-08-26T00:00:00Z&to=2025-08-27T00:00:00Z"

# saldo
curl http://localhost:5000/api/wallets/1/balance
```

> Todos os exemplos estão prontos também na **collection do Postman**.

---

## 🔤 Convenções de JSON (importante)

* **Enums como string** (configurado na API):

    * `modality`: `"CENTENA_3X"`, `"MILHAR"`, `"PALPITAO"`, etc.
    * `positions`: `"RIGHT"`, `"LEFT"`, `"MIDDLE"`
    * `prizeWindow`: `"_1_ONLY"`, `"_1_3"`, `"_1_5"`, `"_1_6"`
* **Sempre envie** `Content-Type: application/json`.

Se você ver:

* `The req field is required.` → faltou `Content-Type: application/json`.
* `could not be converted to Modality` → enum escrito diferente do esperado.

---

## 🧠 Modalidades cobertas (resumo)

* **Milhar**: `MILHAR`, `MILHAR_INV`, `MILHAR_E_CT`
* **Centena**: `CENTENA`, `CENTENA_INV`, `CENTENA_3X`, `CENTENA_ESQ`, `CENTENA_INV_ESQ`
* **Dezena/Unidade**: `UNIDADE`, `DEZENA`, `DEZENA_ESQ`, `DEZENA_MEIO`
* **Duques/Ternos de Dezena**: `DUQUE_DE_DEZENA`, `DUQUE_DEZENA_ESQ`, `DUQUE_DEZENA_MEIO`, `TERNO_DZ`, `TERNO_DZ_SECO`, `TERNO_DZ_SECO_ESQ`
* **Grupos**: `GRUPO`, `GRUPO_ESQ`, `GRUPO_MEIO`, `DUQUE_DE_GRUPO`, `DUQUE_DE_GRUPO_ESQ`, `DUQUE_DE_GRUPO_MEIO`, `TERNO_GP`, `TERNO_GP_ESQ`, `TERNO_GP_MEIO`, `QUADRA_GP`, `QUADRA_GP_ESQ`, `QUADRA_GP_MEIO`, `QUINA_GP`, `QUINA_GP_ESQ`, `QUINA_GP_MEIO`, `SENA_GP`, `SENA_GP_ESQ`, `SENA_GP_MEIO`
* **Especiais**: `PALPITAO`, `SENINHA`, `QUININHA`, `LOTINHA`, `PASSE_VAI`, `PASSE_VAI_E_VEM`

> Variações **ESQ/MEIO** são consideradas via `positions` (LEFT/MIDDLE/RIGHT).

---

## 💰 Payouts

A tabela de multiplicadores fica em `payout_tables` (seed inicial simples).
Você pode **ajustar/estender** por modalidade e **chave**:

* exemplos de chaves: `BASE`, `3X`, `MILHAR`, `CENTENA`, `TERNO`, `QUADRA`, `QUINA`, `"3"`, `"4"`, `"5"`, `"6"`, `"15"`, etc.
* o cálculo em `BetEngine` usa essas chaves para cada modalidade.

> Para mudar os multiplicadores, edite o `DbSeeder` ou crie endpoints/admin.

---

## 🛠️ Erros comuns

* **Build ok, run falha pedindo .NET 8**: instale o **runtime 8** (pode coexistir com 9).
* **HealthCheck Postgres não compila**: faltou o pacote `AspNetCore.HealthChecks.NpgSql`.
* **Validação**: se quiser regras rígidas (ex.: `TERNO_DZ_SECO` exige 3 dezenas), habilite/expanda as regras em `CreateBetRequestValidator`.

---

## ☁️ Produção (resumo)

* **Infra**: Terraform (`infra/`) — ALB + EC2 (ASG) + RDS + CloudWatch.
* **Deploy**:

  ```bash
  bash scripts/publish-linux.sh               # gera out/bichoapp.tar.gz
  aws s3 cp out/bichoapp.tar.gz s3://<bucket>/releases/bichoapp.tar.gz
  cd infra && terraform init && terraform apply
  ```
* **Acesso**: use o `alb_dns_name` do `terraform apply`.
* **HTTPS**: adicionar ACM + Listener 443 no ALB.

---

## ⚖️ Aviso Legal

Este projeto é **apenas educacional**. Jogos de azar podem ser **ilegais** na sua jurisdição. Não opere comercialmente.

