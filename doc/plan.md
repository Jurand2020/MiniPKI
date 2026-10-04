# MiniPKI — Implementation Plan

Derived from `doc/overview.md`. This document describes the architecture, project structure, domain model, workflows, deployment strategy, and task breakdown for the MiniPKI private Certificate Authority system.

---

## 1. System Architecture

```
┌─────────────────────────────────────────────────────────┐
│                      Browser (User)                      │
└────────────────────────┬────────────────────────────────┘
                         │ HTTPS
┌────────────────────────▼────────────────────────────────┐
│                 Nginx (Reverse Proxy)                    │
│   - Terminates TLS (optional, self-signed in dev)        │
│   - Serves React static build from /usr/share/nginx/html │
│   - Proxies /api/* → backend                             │
│   - Proxies /crl/* → backend CRL endpoint                │
└──────────┬──────────────────────────────┬───────────────┘
           │ /api/*                        │ static
┌──────────▼──────────────────┐  ┌────────▼────────┐
│   ASP.NET Core 10 Backend    │  │  React Frontend  │
│  ┌──────────────────────┐   │  │  (Vite + TS +    │
│  │   API Controllers    │   │  │   MUI)           │
│  ├──────────────────────┤   │  └──────────────────┘
│  │  Auth Middleware     │   │
│  ├──────────────────────┤   │
│  │   Service Layer      │   │
│  │  (CA, Cert, CRL,     │   │
│  │   Config, Audit)     │   │
│  ├──────────────────────┤   │
│  │  Infrastructure      │   │
│  │  (BouncyCastle,      │   │
│  │   File Storage)      │   │
│  └──────────────────────┘   │
└──────────┬──────────────────┘
           │
┌──────────▼──────────────────┐
│      /data (Volume)          │
│  /ca       /certificates     │
│  /revoked  /crl   /audit     │
│  /configuration              │
└──────────────────────────────┘
```

**Key design principles:**
- **Clean Architecture**: Core (domain + interfaces) ← Infrastructure (implementations) ← API (controllers + middleware).
- **Stateless containers**: All state lives on mounted volumes under `/data`.
- **No external databases**: Everything is file-based (JSON metadata + PEM certificates/keys).
- **Kubernetes-ready**: Stateless containers + PersistentVolume claims.

---

## 2. Project Structure

```
MiniPKI/
├── doc/
│   ├── overview.md              # Requirements specification
│   └── plan.md                  # This file
├── src/
│   ├── backend/
│   │   ├── MiniPKI.sln
│   │   ├── Directory.Build.props
│   │   ├── MiniPKI.Core/        # Domain models, interfaces, configuration
│   │   │   ├── Domain/
│   │   │   │   ├── CertificateRecord.cs
│   │   │   │   ├── CertificateStatus.cs
│   │   │   │   ├── KeyAlgorithm.cs
│   │   │   │   └── AuditEntry.cs
│   │   │   ├── Configuration/
│   │   │   │   └── PkiConfiguration.cs
│   │   │   └── Services/        # Interface contracts
│   │   │       ├── ICertificateAuthorityService.cs
│   │   │       ├── ICertificateService.cs
│   │   │       ├── ICrlService.cs
│   │   │       ├── IConfigurationService.cs
│   │   │       └── IAuditService.cs
│   │   ├── MiniPKI.Infrastructure/  # Implementations
│   │   │   ├── Crypto/
│   │   │   │   ├── PemHelper.cs
│   │   │   │   ├── KeyGenerator.cs
│   │   │   │   └── CertificateGenerator.cs
│   │   │   ├── Storage/
│   │   │   │   ├── DataPathProvider.cs
│   │   │   │   ├── FileCertificateStore.cs
│   │   │   │   ├── FileConfigurationStore.cs
│   │   │   │   └── DirectoryInitializer.cs
│   │   │   ├── Audit/
│   │   │   │   └── FileAuditService.cs
│   │   │   ├── BouncyCastleCertificateAuthorityService.cs
│   │   │   ├── BouncyCastleCertificateService.cs
│   │   │   └── BouncyCastleCrlService.cs
│   │   ├── MiniPKI.Api/         # ASP.NET Core 10 Web API
│   │   │   ├── Program.cs
│   │   │   ├── appsettings.json
│   │   │   ├── Controllers/
│   │   │   │   ├── AuthController.cs
│   │   │   │   ├── CertificatesController.cs
│   │   │   │   ├── ConfigurationController.cs
│   │   │   │   ├── CrlController.cs
│   │   │   │   └── AuditController.cs
│   │   │   ├── Middleware/
│   │   │   │   ├── AuthenticationMiddleware.cs
│   │   │   │   └── SecurityHeadersMiddleware.cs
│   │   │   ├── Auth/
│   │   │   │   ├── PasswordHasher.cs
│   │   │   │   └── SessionStore.cs
│   │   │   └── Models/
│   │   │       ├── LoginRequest.cs
│   │   │       ├── IssueCertificateRequest.cs
│   │   │       ├── RevokeCertificateRequest.cs
│   │   │       └── UpdateConfigurationRequest.cs
│   │   └── MiniPKI.Tests/       # XUnit tests
│   │       ├── Crypto/
│   │       ├── Storage/
│   │       └── Api/
│   └── frontend/                # React + Vite + TypeScript + MUI
│       ├── package.json
│       ├── vite.config.ts
│       ├── tsconfig.json
│       ├── index.html
│       └── src/
│           ├── main.tsx
│           ├── App.tsx
│           ├── theme.ts
│           ├── api/
│           │   └── client.ts
│           ├── components/
│           │   ├── Layout.tsx
│           │   ├── StatusBadge.tsx
│           │   └── ConfirmDialog.tsx
│           └── pages/
│               ├── Login.tsx
│               ├── Dashboard.tsx
│               ├── Certificates.tsx
│               ├── Revocation.tsx
│               ├── Configuration.tsx
│               └── Audit.tsx
├── docker/
│   ├── backend.Dockerfile
│   ├── frontend.Dockerfile
│   └── nginx.conf
├── k8s/
│   ├── namespace.yaml
│   ├── backend-deployment.yaml
│   ├── frontend-deployment.yaml
│   ├── pvc.yaml
│   ├── configmap.yaml
│   ├── secret.yaml
│   └── service.yaml
├── data/                        # Runtime data (gitignored)
├── docker-compose.yml
├── global.json
├── Directory.Build.props
└── .gitignore
```

---

## 3. Domain Model

### 3.1 Core Entities

| Entity | Description |
|--------|-------------|
| `CertificateRecord` | Metadata for an issued certificate: serial, CN, SANs, algorithm, key size, creation/expiration/revocation dates, issuer. |
| `AuditEntry` | Single audit log line: timestamp, action, details, actor. |
| `PkiConfiguration` | Strongly-typed runtime configuration (domain suffix, CRL base URL, validity, algorithm, org info). |

### 3.2 Enums

| Enum | Values |
|------|--------|
| `KeyAlgorithm` | `RSA`, `ECDSA` |
| `CertificateStatus` | `Valid`, `Revoked`, `Expired` |

### 3.3 Service Interfaces (Core)

| Interface | Responsibility |
|-----------|---------------|
| `ICertificateAuthorityService` | Initialize Root CA + Intermediate CA; provide CA certificates/chain. |
| `ICertificateService` | Issue, revoke, list, retrieve end-entity TLS certificates. |
| `ICrlService` | Generate and publish Certificate Revocation Lists. |
| `IConfigurationService` | Read and update runtime PKI configuration. |
| `IAuditService` | Append-only audit logging to files. |

---

## 4. Configuration Model

Strongly-typed configuration bound from `appsettings.json` and persisted as a runtime-editable JSON file at `/data/configuration/pki.json`.

```json
{
  "Pki": {
    "DefaultDomain": "company.local",
    "CrlBaseUrl": "http://pki.company.local/crl/",
    "DefaultValidityDays": 825,
    "Algorithm": "RSA",
    "RsaKeySize": 4096,
    "Organization": "Company",
    "OrganizationalUnit": "IT",
    "Country": "PL",
    "State": "Greater Poland",
    "Locality": "Poznan"
  }
}
```

Additional configuration sections:
- `Auth:PasswordHash` — PBKDF2/Argon2 hash of the admin password.
- `Auth:SessionTimeoutMinutes` — Session expiration.
- `Storage:DataPath` — Root data directory (default `/data`).

---

## 5. Service Layer Design

### 5.1 Certificate Authority Service (`BouncyCastleCertificateAuthorityService`)

**Responsibility:** Manage Root CA and Intermediate CA lifecycle.

**Initialization flow:**
1. Check if `/data/ca/root.crt` and `/data/ca/root.key` exist.
2. If not, generate Root CA self-signed certificate (RSA 4096, 20-year validity).
3. Check if `/data/ca/intermediate.crt` and `/data/ca/intermediate.key` exist.
4. If not, generate Intermediate CA certificate signed by Root CA (RSA 4096, 10-year validity).
5. Write all files in PEM format with restricted permissions.

**Key generation:**
- RSA: `RsaKeyPairGenerator` with specified key size (default 4096).
- ECDSA: `ECKeyPairGenerator` with P-256 curve (`SecP256R1`).

### 5.2 Certificate Service (`BouncyCastleCertificateService`)

**Responsibility:** Issue, revoke, list, and retrieve end-entity TLS certificates.

**Issue certificate flow:**
1. Generate key pair using configured algorithm (RSA 4096 or ECDSA P-256).
2. Build `X509Name` for subject from configuration (O, OU, C, ST, L) + provided CN.
3. Generate random serial number (128-bit).
4. Set validity period (default 825 days, configurable per request).
5. Add Subject Alternative Names (DNS names from request + default domain suffix).
6. Add CRL Distribution Point extension from configuration.
7. Add Basic Constraints (CA:FALSE), Key Usage (digitalSignature, keyEncipherment), Extended Key Usage (serverAuth).
8. Sign with Intermediate CA private key.
9. Store certificate PEM at `/data/certificates/{serial}.crt`.
10. Store private key PEM at `/data/certificates/{serial}.key`.
11. Store metadata JSON at `/data/certificates/{serial}.json`.
12. Log to audit.

**Revoke certificate flow:**
1. Find certificate metadata by serial number.
2. Set `RevokedAt` to current UTC time and store revocation reason.
3. Move certificate files to `/data/revoked/` (or mark in metadata).
4. Update metadata JSON.
5. Trigger CRL regeneration.
6. Log to audit.

**List certificates flow:**
1. Scan `/data/certificates/*.json` and `/data/revoked/*.json`.
2. Deserialize each metadata file.
3. Return sorted list (newest first).

**Download certificate flow:**
1. Read certificate PEM from disk.
2. Read private key PEM from disk.
3. Build chain PEM (end-entity + intermediate + root).
4. Return tuple of (certificate, key, chain).

### 5.3 CRL Service (`BouncyCastleCrlService`)

**Responsibility:** Generate and publish Certificate Revocation Lists.

**CRL generation flow:**
1. Create `X509V2CrlGenerator`.
2. Set issuer to Intermediate CA subject.
3. Set `ThisUpdate` to current time, `NextUpdate` to +30 days.
4. For each revoked certificate, add `AddCrlEntry(serialNumber, revocationDate, reason)`.
5. Add CRL Number extension (incrementing).
6. Add Authority Key Identifier extension.
7. Sign with Intermediate CA private key.
8. Write CRL PEM to `/data/crl/ca.crl`.
9. Log to audit.

### 5.4 Configuration Service (`FileConfigurationStore`)

**Responsibility:** Read and update runtime PKI configuration.

- On startup: load from `/data/configuration/pki.json`, or fall back to `appsettings.json` defaults.
- On update: validate all fields, write to `/data/configuration/pki.json`, log to audit.

### 5.5 Audit Service (`FileAuditService`)

**Responsibility:** Append-only audit logging to files.

- Each entry: `{ "timestamp": "...", "action": "...", "details": "...", "actor": "..." }`
- Written to `/data/audit/audit-{YYYY-MM-DD}.log` (one file per day, JSON-lines format).
- Read: parse files in reverse chronological order, return latest N entries.

---

## 6. API Layer Design

### 6.1 REST API Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | `/api/auth/login` | Authenticate admin | No |
| POST | `/api/auth/logout` | Destroy session | Yes |
| GET | `/api/auth/status` | Check session status | No |
| GET | `/api/configuration` | Get current PKI configuration | Yes |
| PUT | `/api/configuration` | Update PKI configuration | Yes |
| GET | `/api/certificates` | List all certificates | Yes |
| GET | `/api/certificates/{serial}` | Get certificate details | Yes |
| POST | `/api/certificates` | Issue new certificate | Yes |
| POST | `/api/certificates/{serial}/revoke` | Revoke certificate | Yes |
| GET | `/api/certificates/{serial}/download` | Download certificate (PEM) | Yes |
| GET | `/api/certificates/{serial}/chain` | Download CA chain (PEM) | Yes |
| GET | `/api/certificates/{serial}/key` | Download private key (PEM) | Yes |
| POST | `/api/crl/generate` | Force CRL regeneration | Yes |
| GET | `/api/crl/current` | Download current CRL (PEM) | No |
| GET | `/api/audit` | Get audit log entries | Yes |

### 6.2 Authentication & Security

**Authentication flow:**
1. `POST /api/auth/login` with `{ "password": "..." }`.
2. Server verifies password against `Auth:PasswordHash` using PBKDF2 (or Argon2 via `BCrypt-Net`).
3. On success: create session token (cryptographically random 256-bit), store in `SessionStore` (in-memory `ConcurrentDictionary` with expiration).
4. Set session cookie: `HttpOnly`, `Secure` (in production), `SameSite=Strict`, `Path=/`.
5. Log login to audit.

**Middleware:**
- `AuthenticationMiddleware`: For all `/api/*` paths except `/api/auth/login`, `/api/auth/status`, `/api/crl/current`, validate session cookie. Return 401 if invalid.
- `SecurityHeadersMiddleware`: Sets `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `X-XSS-Protection: 0`, `Referrer-Policy: strict-origin-when-cross-origin`, `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'`, `Strict-Transport-Security: max-age=31536000; includeSubDomains`.

**CSRF protection:**
- Since the API uses cookie-based session auth, CSRF is a concern.
- Approach: `SameSite=Strict` cookie + require custom header `X-Requested-With: XMLHttpRequest` on all state-changing requests (browsers won't send custom headers in cross-origin CSRF attacks without preflight).

**Password hashing:**
- Use PBKDF2 with HMAC-SHA256, 100,000 iterations, 128-bit salt.
- Hash format: `pbkdf2-sha256:{iterations}:{base64-salt}:{base64-hash}`.
- Default admin password hash pre-configured (password: `admin123` — should be changed in production).

### 6.3 Request/Response Models

```csharp
// LoginRequest
{ "password": "string" }

// IssueCertificateRequest
{
  "commonName": "string",
  "sanEntries": ["string"],
  "keyAlgorithm": "RSA" | "ECDSA",  // optional, defaults to config
  "keySize": 4096,                  // optional, defaults to config
  "validityDays": 825              // optional, defaults to config
}

// RevokeCertificateRequest
{ "reason": "string" }

// UpdateConfigurationRequest
{ /* same shape as PkiConfiguration */ }
```

---

## 7. Certificate Generation Workflow

```
User issues certificate via API
        │
        ▼
┌─────────────────────┐
│ Validate request     │  - CN not empty
│ (IssueCertificate    │  - SANs are valid DNS names
│  Request)            │  - Algorithm is supported
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Load configuration   │  - Get org info, CRL base URL,
│                      │    default validity, algorithm
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Generate key pair    │  - RSA: RsaKeyPairGenerator (4096-bit)
│                      │  - ECDSA: ECKeyPairGenerator (P-256)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Build certificate    │  - Subject: CN + O, OU, C, ST, L
│                      │  - Serial: random 128-bit
│                      │  - Validity: now → now + validityDays
│                      │  - SAN: DNS names from request
│                      │  - CRL Distribution Point from config
│                      │  - Basic Constraints: CA:FALSE
│                      │  - Key Usage: digitalSignature, keyEncipherment
│                      │  - Extended Key Usage: serverAuth
│                      │  - Authority Key Identifier
│                      │  - Subject Key Identifier
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Sign with            │  - Use Intermediate CA private key
│ Intermediate CA      │  - Signature algorithm: SHA256withRSA
│                      │    or SHA256withECDSA
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Store to disk        │  - /data/certificates/{serial}.crt (PEM)
│                      │  - /data/certificates/{serial}.key (PEM)
│                      │  - /data/certificates/{serial}.json (metadata)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Audit log            │  - "Certificate issued: {CN} ({serial})"
└─────────────────────┘
```

---

## 8. Certificate Revocation Workflow

```
User revokes certificate via API
        │
        ▼
┌─────────────────────┐
│ Validate request     │  - Serial exists
│ (RevokeCertificate   │  - Certificate is not already revoked
│  Request)            │  - Reason is provided
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Update metadata      │  - Set RevokedAt = DateTime.UtcNow
│                      │  - Set RevocationReason
│                      │  - Write updated metadata JSON
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Move files to        │  - Move {serial}.crt, {serial}.key,
│ /data/revoked/       │    {serial}.json to /data/revoked/
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Regenerate CRL       │  - Call ICrlService.GenerateCrlAsync()
│                      │  - New CRL includes this certificate
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Audit log            │  - "Certificate revoked: {serial}, reason: {reason}"
└─────────────────────┘
```

---

## 9. CRL Publishing Workflow

```
CRL generation triggered (after revocation or manual API call)
        │
        ▼
┌─────────────────────┐
│ Load revoked certs   │  - Scan /data/revoked/*.json
│                      │  - Build list of (serial, revokedAt, reason)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Create CRL generator │  - X509V2CrlGenerator
│                      │  - Issuer: Intermediate CA subject DN
│                      │  - ThisUpdate: now
│                      │  - NextUpdate: now + 30 days
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Add revoked entries  │  - For each revoked cert:
│                      │    AddCrlEntry(serial, revokedAt, reason)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Add extensions        │  - CRL Number (incrementing)
│                      │  - Authority Key Identifier
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Sign CRL             │  - Sign with Intermediate CA private key
│                      │  - SHA256withRSA or SHA256withECDSA
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Write to disk        │  - /data/crl/ca.crl (PEM format)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Audit log            │  - "CRL generated: {N} revoked entries"
└─────────────────────┘
```

**CRL accessibility:**
- CRL is served via `GET /api/crl/current` (no auth required, as CRLs are public).
- Nginx proxies `/crl/current` → backend.
- Every issued certificate contains the CRL Distribution Point extension pointing to the configured `CrlBaseUrl`.

---

## 10. Storage Design

### 10.1 Directory Structure

```
/data/
├── ca/
│   ├── root.crt              # Root CA certificate (PEM)
│   ├── root.key              # Root CA private key (PEM)
│   ├── intermediate.crt      # Intermediate CA certificate (PEM)
│   └── intermediate.key      # Intermediate CA private key (PEM)
├── certificates/
│   ├── {serial}.crt          # End-entity certificate (PEM)
│   ├── {serial}.key          # End-entity private key (PEM)
│   └── {serial}.json         # End-entity metadata (JSON)
├── revoked/
│   ├── {serial}.crt          # Revoked certificate (moved from certificates/)
│   ├── {serial}.key          # Revoked private key (moved)
│   └── {serial}.json         # Revoked metadata (updated)
├── crl/
│   └── ca.crl                # Current CRL (PEM)
├── audit/
│   └── audit-{YYYY-MM-DD}.log  # Daily audit log (JSON-lines)
└── configuration/
    └── pki.json              # Runtime PKI configuration
```

### 10.2 File Formats

- **Certificates/keys**: PEM format (Base64-encoded DER with `-----BEGIN CERTIFICATE-----` headers).
- **Metadata**: JSON files with `CertificateRecord` fields.
- **CRL**: PEM-encoded CRL.
- **Audit logs**: JSON-lines format (one JSON object per line), append-only.

### 10.3 File Permissions

- Private key files (`.key`): `0600` (owner read/write only).
- Certificate files (`.crt`): `0644` (world-readable, owner write).
- Configuration files: `0600`.
- Audit log files: `0640` (owner read/write, group read).
- Directory permissions: `0750` for `/data/ca/`, `0750` for `/data/revoked/`, etc.

On Windows (development), file permissions are managed via ACLs but the application should not fail if it cannot set Unix-style permissions.

---

## 11. Frontend Design

### 11.1 Technology Stack

- **React 19** with TypeScript
- **Vite 6** as build tool
- **Material-UI (MUI) v6** for enterprise-grade UI components
- **React Router v7** for navigation
- **Axios** for API calls
- **date-fns** for date formatting

### 11.2 Pages

| Page | Route | Description |
|------|-------|-------------|
| Login | `/login` | Single password field, form validation |
| Dashboard | `/` | Summary cards: total, active, revoked, expiring soon; latest issued certificates table |
| Certificates | `/certificates` | Searchable/filterable/sortable table; issue certificate dialog; certificate details dialog |
| Revocation | `/revocation` | Certificate selection dropdown; revocation reason input; confirmation dialog |
| Configuration | `/configuration` | Editable form for all PKI configuration fields; save button |
| Audit | `/audit` | Paginated table of audit entries; filter by action type |

### 11.3 Theme

- Dark mode and light mode support via MUI's `ThemeProvider`.
- Theme toggle in the app bar.
- Primary color: deep blue (`#1976d2`).
- Enterprise-style typography (Roboto).

### 11.4 API Client

- Axios instance with `baseURL: '/api'`.
- Request interceptor: attach session cookie (automatic with `withCredentials: true`).
- Response interceptor: on 401, redirect to `/login`.
- All state-changing requests include `X-Requested-With: XMLHttpRequest` header for CSRF protection.

---

## 12. Docker Deployment

### 12.1 Backend Dockerfile (Multi-stage)

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/backend/MiniPKI.Api/MiniPKI.Api.csproj", "src/backend/MiniPKI.Api/"]
COPY ["src/backend/MiniPKI.Core/MiniPKI.Core.csproj", "src/backend/MiniPKI.Core/"]
COPY ["src/backend/MiniPKI.Infrastructure/MiniPKI.Infrastructure.csproj", "src/backend/MiniPKI.Infrastructure/"]
RUN dotnet restore "src/backend/MiniPKI.Api/MiniPKI.Api.csproj"
COPY src/backend/ .
WORKDIR "/src/src/backend/MiniPKI.Api"
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV STORAGE__DATAPATH=/data
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "MiniPKI.Api.dll"]
```

### 12.2 Frontend Dockerfile (Multi-stage)

```dockerfile
# Stage 1: Build
FROM node:22-alpine AS build
WORKDIR /app
COPY src/frontend/package*.json ./
RUN npm ci
COPY src/frontend/ .
RUN npm run build

# Stage 2: Nginx
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget -q --spider http://localhost/ || exit 1
```

### 12.3 Docker Compose

```yaml
version: '3.8'

services:
  backend:
    build:
      context: .
      dockerfile: docker/backend.Dockerfile
    ports:
      - "8080:8080"
    volumes:
      - ./data:/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Auth__PasswordHash=pbkdf2-sha256:100000:...
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 3s
      retries: 3

  frontend:
    build:
      context: .
      dockerfile: docker/frontend.Dockerfile
    ports:
      - "80:80"
    depends_on:
      backend:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "wget", "-q", "--spider", "http://localhost/"]
      interval: 30s
      timeout: 3s
      retries: 3
```

### 12.4 Nginx Configuration

```nginx
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    # SPA fallback
    location / {
        try_files $uri $uri/ /index.html;
    }

    # API proxy
    location /api/ {
        proxy_pass http://backend:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # CRL proxy (public, no auth)
    location /crl/ {
        proxy_pass http://backend:8080/api/crl/;
        proxy_set_header Host $host;
    }
}
```

---

## 13. Kubernetes Deployment

### 13.1 Persistent Volume Claim

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: minipki-data
  namespace: minipki
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 1Gi
  storageClassName: standard
```

### 13.2 Backend Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: minipki-backend
  namespace: minipki
spec:
  replicas: 1
  selector:
    matchLabels:
      app: minipki-backend
  template:
    metadata:
      labels:
        app: minipki-backend
    spec:
      containers:
        - name: backend
          image: minipki-backend:latest
          ports:
            - containerPort: 8080
          volumeMounts:
            - name: data
              mountPath: /data
          envFrom:
            - configMapRef:
                name: minipki-config
            - secretRef:
                name: minipki-secrets
          readinessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 15
            periodSeconds: 20
      volumes:
        - name: data
          persistentVolumeClaim:
            claimName: minipki-data
```

### 13.3 ConfigMap and Secret

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: minipki-config
  namespace: minipki
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  STORAGE__DATAPATH: "/data"
  Pki__DefaultDomain: "company.local"
  Pki__CrlBaseUrl: "http://pki.company.local/crl/"
---
apiVersion: v1
kind: Secret
metadata:
  name: minipki-secrets
  namespace: minipki
type: Opaque
stringData:
  Auth__PasswordHash: "pbkdf2-sha256:100000:..."
```

---

## 14. Security Design

### 14.1 Threat Model

| Threat | Mitigation |
|--------|------------|
| **Private key compromise** | Keys stored with `0600` permissions; never logged; CA keys generated once and kept on volume. |
| **Unauthorized API access** | Session-based auth with PBKDF2 password hashing; all endpoints except login/CRL require valid session. |
| **CSRF attacks** | `SameSite=Strict` cookies; `X-Requested-With` header required on state-changing requests. |
| **XSS attacks** | CSP headers restricting script sources; React's built-in XSS protection (no `dangerouslySetInnerHTML`). |
| **Clickjacking** | `X-Frame-Options: DENY`; CSP `frame-ancestors 'none'`. |
| **Session hijacking** | `Secure` + `HttpOnly` cookies; session tokens are 256-bit cryptographically random. |
| **Brute force login** | Rate limiting on login endpoint (configurable, default 5 attempts/minute). |
| **MITM attacks** | HSTS header; production should use TLS (via Nginx or ingress controller). |
| **Audit log tampering** | Append-only files; daily rotation; logs include timestamps and actor information. |
| **Configuration tampering** | Configuration file stored with `0600` permissions; updates require authentication and are audit-logged. |

### 14.2 Security Headers

All responses include:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'`
- `Strict-Transport-Security: max-age=31536000; includeSubDomains` (only when HTTPS is detected)

### 14.3 Password Hashing

Using PBKDF2 with HMAC-SHA256:
- **Iterations**: 100,000
- **Salt**: 128-bit cryptographically random
- **Key**: 256-bit
- **Format**: `pbkdf2-sha256:{iterations}:{base64-salt}:{base64-hash}`

A utility endpoint or CLI command can generate the hash from a plaintext password.

---

## 15. Recommended NuGet Packages

| Package | Purpose |
|---------|---------|
| `BouncyCastle.Cryptography` | Cryptographic operations: X.509 certificate generation, CRL generation, key pair generation. |
| `Microsoft.AspNetCore.Authentication.Cookies` | Cookie-based session authentication. |
| `Swashbuckle.AspNetCore` | OpenAPI/Swagger documentation generation. |
| `xunit` + `xunit.runner.visualstudio` | Unit testing framework and test runner. |
| `Microsoft.NET.Test.Sdk` | Test SDK for building and running tests. |
| `Moq` | Mocking framework for unit tests. |

---

## 16. Testing Strategy

### 16.1 Unit Tests (`MiniPKI.Tests`)

**Crypto layer tests:**
- `KeyGeneratorTests`: Verify RSA key generation produces correct key size; ECDSA key generation uses P-256 curve.
- `CertificateGeneratorTests`: Verify generated certificates have correct subject, issuer, validity, extensions (SAN, CRL DP, Key Usage, Extended Key Usage, Basic Constraints).
- `PemHelperTests`: Round-trip serialization/deserialization of certificates and private keys.

**Storage layer tests:**
- `FileCertificateStoreTests`: Verify issue, revoke, list, get operations; verify file creation and metadata persistence.
- `FileConfigurationStoreTests`: Verify load, save, and fallback to defaults.
- `FileAuditServiceTests`: Verify append-only behavior and entry retrieval.

**CRL service tests:**
- `BouncyCastleCrlServiceTests`: Verify CRL generation includes all revoked certificates; verify CRL number increments; verify CRL is signed by intermediate CA.

### 16.2 Integration Tests

**API integration tests:**
- `AuthFlowTests`: Login → authenticated request → logout → unauthenticated request.
- `CertificateLifecycleTests`: Issue → list → get details → download → revoke → verify in revoked list → verify CRL updated.
- `ConfigurationTests`: Get → update → verify persisted.
- `CrlAccessTests`: Verify CRL is accessible without authentication.

### 16.3 Test Infrastructure

- Use `WebApplicationFactory<Program>` for in-memory integration testing of the API.
- Use temporary directories (`Path.GetTempPath()`) for file-based storage tests, cleaned up after each test.
- Mock `ICertificateAuthorityService` in unit tests where CA initialization is not needed.

---

## 17. Task Breakdown

### Phase 1: Backend Foundation
1. **Set up backend solution structure** — Create `MiniPKI.sln`, `Directory.Build.props`, `global.json`, four projects (Core, Infrastructure, Api, Tests), project references, BouncyCastle package. ✅
2. **Implement Core domain layer** — Domain models (`CertificateRecord`, `AuditEntry`, enums), `PkiConfiguration`, all service interfaces. ✅
3. **Implement Infrastructure crypto layer** — `PemHelper`, `KeyGenerator`, `CertificateGenerator`, `BouncyCastleCertificateAuthorityService`, `BouncyCastleCertificateService`, `BouncyCastleCrlService`. ✅ (in progress)
4. **Implement Infrastructure storage layer** — `DataPathProvider`, `DirectoryInitializer`, `FileCertificateStore`, `FileConfigurationStore`, `FileAuditService`.
5. **Implement API layer** — `Program.cs` (DI, middleware pipeline, Kestrel config), controllers (Auth, Certificates, Configuration, CRL, Audit), middleware (Authentication, SecurityHeaders), auth utilities (`PasswordHasher`, `SessionStore`), request/response models, `appsettings.json`, health endpoint.
6. **Write XUnit tests** — Unit tests for crypto/storage/config layers; integration tests for API endpoints using `WebApplicationFactory`.

### Phase 2: Frontend
7. **Scaffold React frontend** — Vite + React + TypeScript, MUI, React Router, Axios. Install dependencies, configure Vite proxy.
8. **Implement frontend pages** — Login, Dashboard, Certificates (with issue dialog + details dialog), Revocation, Configuration, Audit. Implement theme (dark/light), layout, API client, status badges, confirm dialogs.

### Phase 3: Deployment
9. **Create Docker deployment files** — Multi-stage `backend.Dockerfile`, multi-stage `frontend.Dockerfile`, `nginx.conf`, `docker-compose.yml` with health checks and volume mappings.
10. **Create Kubernetes manifests** — Namespace, PVC, ConfigMap, Secret, backend Deployment + Service, frontend Deployment + Service, readiness/liveness probes.

### Phase 4: Verification
11. **Build and verify** — `dotnet build`, `dotnet test`, `npm run build`, `docker compose build`. Fix any errors. Verify the application starts and basic flows work.

---

## 18. Architecture Deliverables Summary

| # | Deliverable | Status |
|---|-------------|--------|
| 1 | Complete system architecture | ✅ Section 1 |
| 2 | Project structure | ✅ Section 2 |
| 3 | Directory tree | ✅ Section 2 |
| 4 | ASP.NET Core 10 solution structure | ✅ Section 2 |
| 5 | React frontend structure | ✅ Section 2 |
| 6 | Domain model | ✅ Section 3 |
| 7 | Configuration model | ✅ Section 4 |
| 8 | Service layer design | ✅ Section 5 |
| 9 | Security design | ✅ Section 14 |
| 10 | Certificate generation workflow | ✅ Section 7 |
| 11 | Certificate revocation workflow | ✅ Section 8 |
| 12 | CRL publishing workflow | ✅ Section 9 |
| 13 | Docker deployment files | ✅ Section 12 |
| 14 | Kubernetes deployment examples | ✅ Section 13 |
| 15 | Sample implementation code for critical services | ✅ In `src/backend/` |
| 16 | Recommended NuGet packages | ✅ Section 15 |
| 17 | Threat model and mitigations | ✅ Section 14 |
