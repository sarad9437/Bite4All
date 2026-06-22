# Bite4All

Bite4All is a .NET API for coordinating food donations between hospitality partners, charity organizations, drivers, and platform administrators.

## Tech Stack

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server / LocalDB
- ASP.NET Core Identity
- JWT authentication
- xUnit tests

## Getting Started

1. Install the .NET 10 SDK.
2. Create a local development settings file:

```powershell
Copy-Item Bite4All.API/appsettings.Development.example.json Bite4All.API/appsettings.Development.json
```

3. Open `Bite4All.API/appsettings.Development.json` and replace `Jwt:Key` with a private value of at least 32 bytes.
4. Restore and build:

```powershell
dotnet restore
dotnet build
```

5. Run the API:

```powershell
dotnet run --project Bite4All.API
```

In development, the app seeds demo users and exposes the OpenAPI/Scalar API reference.

## Tests

```powershell
dotnet test
```

## Configuration

Do not commit local secrets. The repository intentionally ignores `appsettings.Development.json`, production settings, database files, build output, IDE metadata, and test artifacts.

Use one of these for real values:

- `Bite4All.API/appsettings.Development.json` for local development
- environment variables for deployed environments
- user secrets or a secret manager in CI/CD

JWT configuration can be overridden with environment variables such as:

```powershell
$env:Jwt__Key = "your-private-key-at-least-32-bytes-long"
```

## Repository Hygiene

Before pushing to GitHub, make sure only source files, migrations, tests, and safe example configuration files are staged. Generated folders such as `bin/`, `obj/`, `.vs/`, and local settings should stay untracked.
