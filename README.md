# MiniPKI

A private Certificate Authority (PKI) for issuing and managing TLS certificates.
Built with ASP.NET Core 10, BouncyCastle 2.7.0, and React 18.

## Overview

MiniPKI implements a two-tier CA hierarchy:

```
Root CA (self-signed, RSA 4096, 20 years)
  └── Intermediate CA (signed by root, 10 years)
        └── End-entity TLS certificates (RSA 4096 or ECDSA P-256)
```

**Features:**
- Issue, revoke, and list TLS certificates
- Issue certificates from CSR (Certificate Signing Request) files
- Download certificates as PEM, PEM bundle (chain), P12, or private key
- Certificate subject DN includes Organization, OU, Country, State, and Locality
- CRL Distribution Point extension added when CRL Base URL is configured
- Automatic CRL (Certificate Revocation List) generation
- File-based storage (no database)
- Session-based admin authentication (PBKDF2)
- First-run password setup and password change from UI
- Reset CA from configuration page (regenerates Root + Intermediate CA)
- Audit logging of all operations
- Docker deployment with nginx reverse proxy

**Architecture:**

```
Browser ──► nginx (port 80)
              ├── /        → React SPA (static files)
              ├── /api/    → ASP.NET Core backend (port 8080)
              └── /crl/    → CRL endpoint (public, no auth)
```

## Build

### Prerequisites
- Docker & Docker Compose
- (For local dev) .NET 10 SDK, Node.js 22

### Docker (recommended)
```bash
docker compose build
```

### Local development
```bash
# Backend
dotnet build src/backend/MiniPKI.slnx
dotnet run --project src/backend/MiniPKI.Api

# Frontend
cd src/frontend
npm install
npm run dev
```

## Run

### Docker Compose
```bash
docker compose up -d
```

Services:
| Service  | Container           | Port | Description                    |
|----------|---------------------|------|--------------------------------|
| nginx    | minipki-nginx-1     | 80   | Reverse proxy + frontend SPA   |
| backend  | minipki-backend-1   | -    | ASP.NET Core API (internal)    |

Data is persisted in `./data/`:
```
data/
├── ca/              # Root & Intermediate CA certs + keys
├── certificates/    # Issued certificates (.crt, .key, .json)
├── revoked/         # Revoked certificates
├── crl/             # Certificate Revocation List (ca.crl)
├── audit/           # Daily audit logs (JSON-lines)
└── configuration/   # Runtime PKI configuration (pki.json)
```

### Configuration

Environment variables (set in `docker-compose.yml`):

| Variable                    | Default     | Description                          |
|-----------------------------|-------------|--------------------------------------|
| `STORAGE__DATAPATH`         | `/data`     | Data directory path                  |
| `Auth__PasswordHash`        | (see below) | PBKDF2 hash of admin password        |
| `ASPNETCORE__ENVIRONMENT`   | `Production`| ASP.NET Core environment             |

**Default admin password: `admin123`**

On first run, the system will prompt you to set a new admin password.
You can change the password at any time from the UI (Account → Change Password).

## Usage

### Access the web UI
Open `http://localhost` in your browser. Log in with the admin password.

### Certificate operations

| Action | How |
|--------|-----|
| Issue certificate | Certificates → Issue Certificate |
| Issue from CSR | Certificates → Issue from CSR |
| Download PEM | Certificate row → ⋮ → Download PEM |
| Download Bundle (chain) | Certificate row → ⋮ → Download Bundle |
| Download Key | Certificate row → ⋮ → Download Key |
| Download P12 | Certificate row → ⋮ → Download P12 |
| Revoke certificate | Certificate row → Revoke |

### Reset CA

The Configuration page includes a **Reset CA** button that:
- Deletes the Root CA certificate and key
- Deletes the Intermediate CA certificate and key
- Deletes all issued certificates and their private keys
- Deletes all revoked certificates
- Deletes the current CRL
- Generates new Root CA and Intermediate CA certificates using the current configuration settings

> ⚠️ This action cannot be undone. All previously issued certificates will become invalid.

### API endpoints

| Method | Endpoint                          | Description              | Auth |
|--------|-----------------------------------|--------------------------|------|
| POST   | `/api/auth/login`                 | Login                    | No   |
| POST   | `/api/auth/logout`                | Logout                   | Yes  |
| GET    | `/api/auth/status`                | Check session            | No   |
| POST   | `/api/auth/set-password`          | Set initial password     | No   |
| POST   | `/api/auth/change-password`       | Change password          | Yes  |
| GET    | `/api/certificates`               | List certificates        | Yes  |
| GET    | `/api/certificates/{serial}`      | Get certificate details  | Yes  |
| POST   | `/api/certificates`               | Issue new certificate    | Yes  |
| POST   | `/api/certificates/csr`           | Issue from CSR           | Yes  |
| POST   | `/api/certificates/{serial}/revoke` | Revoke certificate     | Yes  |
| GET    | `/api/certificates/{serial}/download` | Download cert PEM   | Yes  |
| GET    | `/api/certificates/{serial}/bundle`   | Download PEM bundle | Yes  |
| GET    | `/api/certificates/{serial}/chain`    | Download CA chain   | Yes  |
| GET    | `/api/certificates/{serial}/key`      | Download private key| Yes  |
| GET    | `/api/certificates/{serial}/p12`      | Download P12 file   | Yes  |
| GET    | `/api/configuration`              | Get PKI configuration    | Yes  |
| PUT    | `/api/configuration`              | Update configuration     | Yes  |
| POST   | `/api/configuration/reset-ca`     | Reset CA (regenerate)    | Yes  |
| GET    | `/api/crl/current`                | Download current CRL     | No   |

### Example: Issue a certificate

```bash
# Login (saves session cookie)
curl -c cookies.txt -X POST http://localhost/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"password":"admin123"}'

# Issue a certificate
curl -b cookies.txt -X POST http://localhost/api/certificates \
  -H "Content-Type: application/json" \
  -d '{
    "commonName": "web.example.com",
    "sanEntries": ["web.example.com", "www.example.com"],
    "keyAlgorithm": "RSA",
    "keySize": 4096,
    "validityDays": 365
  }'

# List all certificates
curl -b cookies.txt http://localhost/api/certificates

# Download certificate PEM
curl -b cookies.txt http://localhost/api/certificates/{serial}/download

# Download P12 file (password-protected)
curl -b cookies.txt "http://localhost/api/certificates/{serial}/p12?password=secret123" -o cert.p12

# Revoke a certificate
curl -b cookies.txt -X POST http://localhost/api/certificates/{serial}/revoke \
  -H "Content-Type: application/json" \
  -d '{"reason":"keyCompromise"}'

# Download CRL (no auth required)
curl http://localhost/api/crl/current
```

### Example: Issue from CSR

```bash
# Issue a certificate from an existing CSR
curl -b cookies.txt -X POST http://localhost/api/certificates/csr \
  -H "Content-Type: application/json" \
  -d '{
    "csrPem": "-----BEGIN CERTIFICATE REQUEST-----\n...\n-----END CERTIFICATE REQUEST-----",
    "validityDays": 365
  }'
```

### Example: Reset CA

```bash
# Reset CA (deletes all certs/keys/CRL, generates new CA)
curl -b cookies.txt -X POST http://localhost/api/configuration/reset-ca
```

## Project Structure

```
MiniPKI/
├── src/
│   ├── backend/
│   │   ├── MiniPKI.Core/           # Domain models, interfaces, config
│   │   ├── MiniPKI.Infrastructure/ # BouncyCastle implementations, storage
│   │   ├── MiniPKI.Api/            # ASP.NET Core Web API
│   │   └── MiniPKI.Tests/          # xUnit unit + integration tests
│   └── frontend/                   # React + Vite + TypeScript + MUI
├── docker/
│   ├── backend.Dockerfile          # Multi-stage: build .NET → aspnet runtime
│   ├── nginx.Dockerfile            # Multi-stage: build frontend → nginx
│   └── nginx.conf                  # Nginx reverse proxy config
├── docker-compose.yml              # nginx + backend services
├── data/                           # Runtime data (gitignored)
└── global.json                     # .NET SDK version pinning
```

## AI Credits

This project was developed with assistance from [GitHub Copilot](https://github.com/features/copilot),
an AI pair programmer. Copilot helped with:

- Code generation and refactoring
- Test writing and debugging
- Documentation and code reviews

All AI-generated code was reviewed, tested, and verified by the developer.

## License

MIT License
Refer LICENSE.md. 

## Data Directory Structure

All runtime state is stored under `./data/` (gitignored). The directory is created
automatically on first run.

```
data/
├── ca/                              # Certificate Authority
│   ├── root.crt                     # Root CA certificate (PEM)
│   ├── root.key                     # Root CA private key (PEM)
│   ├── intermediate.crt             # Intermediate CA certificate (PEM)
│   └── intermediate.key             # Intermediate CA private key (PEM)
│
├── certificates/                    # Active end-entity certificates
│   ├── {serial}.crt                 # Certificate (PEM)
│   ├── {serial}.key                 # Private key (PEM)
│   └── {serial}.json                # Metadata (CN, SAN, algorithm, dates, etc.)
│
├── revoked/                         # Revoked certificates (moved from certificates/)
│   ├── {serial}.crt
│   ├── {serial}.key
│   └── {serial}.json                # Metadata includes RevokedAt + RevocationReason
│
├── crl/                             # Certificate Revocation List
│   └── ca.crl                       # Current CRL (PEM), regenerated on revocation
│
├── audit/                           # Append-only audit logs (JSON-lines)
│   └── audit-{yyyy-MM-dd}.jsonl     # One file per day, each line is a JSON object
│
└── configuration/                   # Runtime PKI configuration
    ├── pki.json                     # Persisted configuration (editable via API/UI)
    └── password.hash                # Admin password hash (PBKDF2-HMAC-SHA256)
```

### File Formats

| Type | Format | Description |
|------|--------|-------------|
| Certificates (`.crt`) | PEM | `-----BEGIN CERTIFICATE-----` |
| Private keys (`.key`) | PEM | `-----BEGIN PRIVATE KEY-----` (PKCS#8) |
| CRL (`.crl`) | PEM | `-----BEGIN X509 CRL-----` |
| Metadata (`.json`) | JSON | Indented JSON with all certificate fields |
| Audit logs (`.jsonl`) | JSON-lines | One JSON object per line, non-indented |
| Configuration (`pki.json`) | JSON | Indented JSON with all PKI settings |
| Password hash (`password.hash`) | Text | `pbkdf2-sha256:{iterations}:{base64-salt}:{base64-hash}` |

### Backup

To back up the entire PKI state, simply copy the `data/` directory:

```bash
# Stop the services first
docker compose down

# Create a backup
cp -r data/ backup-$(date +%Y%m%d)/

# Restart
docker compose up -d
```
