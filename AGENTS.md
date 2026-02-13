# AGENTS.md

## Project Overview
- App name: REA Emergencia
- Stack: ASP.NET Core (MVC), Entity Framework Core, SQL Server
- Target framework: net10.0

## Workflows
- Default UI: MVC with Razor views
- Database: SQL Server LocalDB for dev
- Migrations: use EF Core migrations

## Commands
- Restore: `dotnet restore`
- Build: `dotnet build`
- Run: `dotnet run --project src/REA.Emergencia.Web`
- Tests: `dotnet test`

## Conventions
- Language: C#
- Nullable: enabled
- Use async APIs for DB access
- Use DTOs for form posts
- Keep controllers thin, move logic to services
- Use FluentValidation for input validation

## Database
- Connection string key: `DefaultConnection`
- Provider: `Microsoft.EntityFrameworkCore.SqlServer`
- Migrations folder: `src/REA.Emergencia.Data/Migrations`

## Files & Layout
- Web project: `src/REA.Emergencia.Web`
- Data project: `src/REA.Emergencia.Data`
- Domain models: `src/REA.Emergencia.Domain`

## Test Strategy
- Unit tests in `tests/REA.Emergencia.Tests`
- Prefer xUnit

## Agent Rules
- Do not run destructive git commands.
- Ask before running DB migrations in prod.
- If a requirement is unclear, ask for clarification before coding.
