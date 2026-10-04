# Copilot Instructions — MiniPKI

## Project Overview

MiniPKI is a private Certificate Authority (PKI) system for issuing and managing TLS certificates. It consists of an ASP.NET Core 10 backend and a React frontend, deployed via Docker and Kubernetes. All state is file-based — there is no external database.

Requirements specification: `doc/overview.md`
Implementation plan: `doc/plan.md`

## Build, Test, and Run Commands

### Backend (.NET 10)

```bash
# Build entire solution
dotnet build src/backend/MiniPKI.slnx

# Run all tests
dotnet test src/backend/MiniPKI.slnx

# Run a single test class or method
dotnet test src/backend/MiniPKI.slnx --filter "FullyQualifiedName~ClassName"
dotnet test src/backend/MiniPKI.slnx --filter "FullyQualifiedName~ClassName.MethodName"

# Run the API (Development mode, http://localhost:5284)
dotnet run --project src/backend/MiniPKI.Api
```

### Frontend (when scaffolded under `src/frontend/`)

```bash
cd src/frontend
npm install
npm run dev      # Vite dev server
npm run build    # Production build to dist/
```

### Docker

```bash
docker compose build
docker compose up
```

## Build Configuration

- **SDK**: .NET 10.0.303, pinned in `global.json` with `rollForward: latestFeature`.
- **Solution format**: `.slnx` (XML-based), not `.sln`. Always reference `src/backend/MiniPKI.slnx`.
- **`Directory.Build.props`** (root-level) applies to all projects:
  - `TargetFramework=net10.0`
  - `TreatWarningsAsErrors=true` — any compiler warning fails the build. Fix warnings rather than suppressing them.
  - `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`

## Architecture — Clean Architecture

Four projects in `src/backend/`, layered by dependency direction:

| Project | Depends On | Purpose |
|---------|-----------|---------|
| `MiniPKI.Core` | (none) | Domain models, service interfaces, configuration model |
| `MiniPKI.Infrastructure` | Core | BouncyCastle crypto implementations, file-based storage, audit logging |
| `MiniPKI.Api` | Core, Infrastructure | ASP.NET Core 10 Web API: controllers, middleware, auth |
| `MiniPKI.Tests` | Core, Infrastructure, Api | xUnit unit and integration tests |

**Dependency rule**: Core never references Infrastructure or Api. Infrastructure depends on Core. Api depends on both.

## Key Conventions

### Naming and Structure
- Service interfaces live in `MiniPKI.Core.Services/` and are prefixed with `I` (e.g., `ICertificateService`).
- Domain models live in `MiniPKI.Core.Domain/` (e.g., `CertificateRecord`, `AuditEntry`).
- Configuration models live in `MiniPKI.Core.Configuration/`.
- Async methods use the `Async` suffix (e.g., `IssueAsync`, `GetBySerialAsync`).
- All public types and members have `///` XML doc comments.

### Cryptography
- All crypto operations use **BouncyCastle.Cryptography** (NuGet 2.5.1), not `System.Security.Cryptography`.
- `MiniPKI.Infrastructure.Crypto.PemHelper` provides static helpers for PEM encoding/decoding of certificates, private keys, and CRLs.
- Supported algorithms: RSA (default 4096-bit) and ECDSA (P-256 / `SecP256R1`).
- All certificates, keys, and CRLs are stored in **PEM format**.

### File-Based Storage
- All runtime state lives under the `data/` directory (mapped to `/data` in containers).
- Directory layout: `data/ca/` (root + intermediate CA), `data/certificates/` (issued certs + keys + metadata JSON), `data/revoked/` (revoked cert files), `data/crl/` (CRL file), `data/audit/` (daily audit logs), `data/configuration/` (runtime PKI config).
- The `data/` directory is **gitignored** — never commit keys, certificates, or runtime data.
- Audit logs are append-only, JSON-lines format, one file per day (`audit-{YYYY-MM-DD}.log`).

### Configuration
- `PkiConfiguration` is strongly-typed, bound from `appsettings.json` under the `Pki` section.
- Runtime configuration is persisted as JSON at `data/configuration/pki.json`.
- Configuration overrides via environment variables use `__` separator (e.g., `Pki__DefaultDomain`).

### Testing
- xUnit with global `Using Include="Xunit"` in the test csproj.
- Tests live in `MiniPKI.Tests/` with subdirectories mirroring the layer (`Crypto/`, `Storage/`, `Api/`).
- File-based storage tests use temporary directories, cleaned up per test.

### Security
- Private keys are never logged or exposed in API responses (except the explicit download-key endpoint).
- Session-based auth with PBKDF2 password hashing.
- CSRF protection via `SameSite=Strict` cookies + `X-Requested-With` header requirement on state-changing requests.
- Security headers set by middleware: CSP, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, HSTS.

## Project Status

The backend solution structure and Core layer (domain models, service interfaces, configuration) are implemented. The Infrastructure layer has `PemHelper` but the full crypto/storage implementations, API layer, frontend, Docker, and tests are not yet built. See `doc/plan.md` for the complete implementation roadmap.
