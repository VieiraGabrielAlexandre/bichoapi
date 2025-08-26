# BichoApi (Simulador de Apostas)

> Projeto educacional com regras inspiradas em loterias não oficiais. **Não utilize comercialmente.**

## Tecnologias
- .NET 8 (ASP.NET Core)
- Entity Framework Core (Npgsql) *(troque para Pomelo/MySQL se quiser)*
- Serilog
- Swagger / OpenAPI

## Rodando localmente (Postgres via Docker)
```bash
docker run --name pg -e POSTGRES_PASSWORD=localpass -e POSTGRES_USER=appuser \
  -e POSTGRES_DB=bicho -p 5432:5432 -d postgres:16
dotnet tool install --global dotnet-ef
dotnet restore
dotnet ef database update --project src/BichoApi
dotnet run --project src/BichoApi
