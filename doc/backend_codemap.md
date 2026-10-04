# Backend Codemap

MiniPKI backend — a .NET 10 PKI management application built on BouncyCastle.Cryptography 2.7.0.
Implements a two-tier CA hierarchy (Root CA → Intermediate CA → end-entity TLS certificates),
CRL generation, file-based storage, session-based authentication, and an append-only audit log.

## Solution Structure

```
src/backend/
├── MiniPKI.slnx                          # Solution file (XML-based .slnx format)
├── MiniPKI.Core/                         # Domain models, service interfaces, configuration
├── MiniPKI.Infrastructure/               # BouncyCastle implementations, file storage, crypto
├── MiniPKI.Api/                          # ASP.NET Core 10 Web API
└── MiniPKI.Tests/                        # xUnit unit + integration tests
```

### Project Dependency Graph

```
MiniPKI.Api ──► MiniPKI.Infrastructure ──► MiniPKI.Core
MiniPKI.Tests ──► MiniPKI.Core
                ──► MiniPKI.Infrastructure
                ──► MiniPKI.Api
```

### Target Frameworks & Key Packages

| Project | Target | Key Dependencies |
|---------|--------|------------------|
| MiniPKI.Core | net10.0 | (none) |
| MiniPKI.Infrastructure | net10.0 | BouncyCastle.Cryptography 2.7.0 |
| MiniPKI.Api | net10.0 | Microsoft.AspNetCore.OpenApi 10.0.12 |
| MiniPKI.Tests | net10.0 | xunit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, coverlet.collector 6.0.4 |

---

## MiniPKI.Core

The innermost layer: domain models, service interfaces, and configuration.
Has no external dependencies — only standard .NET BCL.

### Configuration/

#### `PkiConfiguration`
**File:** `Configuration/PkiConfiguration.cs`

Strongly-typed PKI configuration. Bound from `appsettings.json` under the `Pki` section
and also persisted as a runtime-editable JSON file at `data/configuration/pki.json`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultDomain` | `string` | `"company.local"` | Default domain for issued certificates |
| `CrlUrl` | `string` | `"http://pki.company.local{API_CRL}"` | CRL distribution point URL. `{API_CRL}` placeholder is resolved to `/api/crl/current` at issuance time. |
| `DefaultValidityDays` | `int` | `825` | Default certificate validity period in days |
| `Algorithm` | `KeyAlgorithm` | `RSA` | Default key algorithm (serialized as string via `JsonStringEnumConverter`) |
| `RsaKeySize` | `int` | `4096` | RSA key size in bits |
| `Organization` | `string` | `"Company"` | Organization (O) field in certificate subject |
| `OrganizationalUnit` | `string` | `"IT"` | Organizational Unit (OU) field |
| `Country` | `string` | `"PL"` | Country (C) field — 2-letter ISO code |
| `State` | `string` | `"Greater Poland"` | State/province (ST) field |
| `Locality` | `string` | `"Poznan"` | Locality (L) field |

### Domain/

#### `CertificateRecord`
**File:** `Domain/CertificateRecord.cs`

Represents an issued TLS certificate with its metadata. This is the primary domain entity
for certificate management.

| Property | Type | Description |
|----------|------|-------------|
| `SerialNumber` | `string` | Unique hex serial number (128-bit, uppercase) |
| `CommonName` | `string` | CN field — typically the primary domain |
| `SanEntries` | `List<string>` | Subject Alternative Names (DNS names) |
| `KeyAlgorithm` | `KeyAlgorithm` | RSA or ECDSA |
| `KeySize` | `int` | Key size in bits (2048/4096 for RSA, 256 for ECDSA) |
| `CreatedAt` | `DateTime` | When the certificate was issued |
| `ExpiresAt` | `DateTime` | When the certificate expires |
| `RevokedAt` | `DateTime?` | When the certificate was revoked (null if not revoked) |
| `RevocationReason` | `string?` | Reason for revocation (e.g., "keyCompromise") |
| `Issuer` | `string` | Issuer name (typically "MiniPKI Intermediate CA") |
| `Status` | `CertificateStatus` | **Computed** — see logic below |

**Status Computation Logic:**
```csharp
if (RevokedAt.HasValue)
    return CertificateStatus.Revoked;    // Revoked takes priority
if (ExpiresAt < DateTime.UtcNow)
    return CertificateStatus.Expired;    // Then check expiration
return CertificateStatus.Valid;          // Otherwise valid
```

#### `CertificateStatus` (enum)
**File:** `Domain/CertificateStatus.cs`

Lifecycle status of an issued certificate.

| Value | Description |
|-------|-------------|
| `Valid` | Certificate is active and not expired |
| `Revoked` | Certificate has been revoked |
| `Expired` | Certificate has passed its expiration date |

#### `KeyAlgorithm` (enum)
**File:** `Domain/KeyAlgorithm.cs`

Supported key algorithms for certificate generation.

| Value | Description |
|-------|-------------|
| `RSA` | RSA algorithm (default key size: 4096 bits) |
| `ECDSA` | ECDSA on P-256 curve (256-bit key) |

#### `AuditEntry`
**File:** `Domain/AuditEntry.cs`

A single audit log entry. Stored in append-only files.

| Property | Type | Description |
|----------|------|-------------|
| `Timestamp` | `DateTime` | When the event occurred (defaults to `DateTime.UtcNow`) |
| `Action` | `string` | Event type (e.g., "ISSUE", "REVOKE", "CRL_GENERATE") |
| `Details` | `string` | Human-readable description of the event |
| `Actor` | `string?` | Who triggered the event (optional) |

### Services/ (Interfaces)

#### `ICertificateAuthorityService`
**File:** `Services/ICertificateAuthorityService.cs`

Manages the Root CA and Intermediate CA lifecycle.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `EnsureCaInitializedAsync()` | `Task` | Ensures root and intermediate CA certificates/keys exist on disk. Generates them if missing. |
| `GetRootCaPemAsync()` | `Task<string>` | Returns PEM-encoded root CA certificate |
| `GetIntermediateCaPemAsync()` | `Task<string>` | Returns PEM-encoded intermediate CA certificate |
| `GetIntermediateCaKeyPemAsync()` | `Task<string>` | Returns PEM-encoded intermediate CA private key (used by CertificateService and CrlService for signing) |
| `GetCaChainPemAsync()` | `Task<string>` | Returns full CA chain (root + intermediate) in PEM format |

#### `ICertificateService`
**File:** `Services/ICertificateService.cs`

Issues, revokes, lists, and retrieves end-entity TLS certificates.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `ListAsync()` | `Task<List<CertificateRecord>>` | Lists all issued certificates (valid and revoked) |
| `GetBySerialAsync(serialNumber)` | `Task<CertificateRecord?>` | Retrieves a certificate record by serial number |
| `IssueAsync(commonName, sanEntries, algorithm, keySize, validityDays)` | `Task<CertificateRecord>` | Issues a new end-entity certificate signed by the Intermediate CA |
| `RevokeAsync(serialNumber, reason)` | `Task<bool>` | Revokes a certificate by serial number. Regenerates the CRL. |
| `DownloadAsync(serialNumber)` | `Task<(string certificatePem, string keyPem, string chainPem)>` | Downloads the certificate, private key, and CA chain for the given serial |

#### `ICrlService`
**File:** `Services/ICrlService.cs`

Generates and publishes Certificate Revocation Lists (CRLs).

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GenerateCrlAsync()` | `Task<string>` | Regenerates the CRL from the current revoked-certificate list. Returns the PEM-encoded CRL. |
| `GetCurrentCrlPemAsync()` | `Task<string>` | Returns the current CRL in PEM format, generating it if it does not exist. |

#### `IAuditService`
**File:** `Services/IAuditService.cs`

Append-only audit logging to files.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `LogAsync(action, details, actor?)` | `Task` | Appends an audit entry to today's audit log file |
| `GetEntriesAsync(limit = 100)` | `Task<List<AuditEntry>>` | Reads all audit entries from all audit log files, sorted by timestamp descending (newest first) |

#### `IConfigurationService`
**File:** `Services/IConfigurationService.cs`

Reads and updates the runtime PKI configuration.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetAsync()` | `Task<PkiConfiguration>` | Gets the current PKI configuration |
| `UpdateAsync(configuration)` | `Task<PkiConfiguration>` | Updates the PKI configuration |

---

## MiniPKI.Infrastructure

Implements all Core interfaces using BouncyCastle.Cryptography 2.7.0.
Contains crypto helpers, file-based storage, and service implementations.

### Crypto/

#### `CertificateGenerator` (static)
**File:** `Crypto/CertificateGenerator.cs`

Builds X.509 v3 certificates for Root CA, Intermediate CA, and end-entity TLS.
All certificates use SHA-256 signatures.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GenerateRootCaCertificate(keyPair, commonName, organization, organizationalUnit, country, state, locality)` | `X509Certificate` | Generates a self-signed Root CA certificate (20-year validity, BasicConstraints CA=true, KeyUsage=KeyCertSign+CrlSign, SubjectKeyIdentifier) |
| `GenerateIntermediateCaCertificate(keyPair, rootCert, rootPrivateKey, commonName, organization, organizationalUnit, country, state, locality)` | `X509Certificate` | Generates an Intermediate CA certificate signed by the Root CA (10-year validity, BasicConstraints CA=true with pathlen=0, KeyUsage=KeyCertSign+CrlSign, SubjectKeyIdentifier, AuthorityKeyIdentifier) |
| `GenerateEndEntityCertificate(keyPair, serialNumber, commonName, organization, organizationalUnit, country, state, locality, sanEntries, validityDays, crlDistributionPointUrl, issuerCert, issuerPrivateKey)` | `X509Certificate` | Generates an end-entity TLS certificate signed by the Intermediate CA (BasicConstraints CA=false, KeyUsage=DigitalSignature+KeyEncipherment, ExtendedKeyUsage=serverAuth, SubjectAlternativeName, CrlDistributionPoints, SubjectKeyIdentifier, AuthorityKeyIdentifier) |

**Key Implementation Details:**
- Root CA: serial=1, self-signed, 20-year validity
- Intermediate CA: serial=2, signed by root, 10-year validity, pathlen=0
- End-entity: serial from `KeyGenerator.GenerateSerialNumber()`, signed by intermediate
- Subject DN includes CN, O, OU, L, ST, C — only non-empty fields are included
- CRL Distribution Point extension is only added when `crlDistributionPointUrl` is non-empty
- Uses `X509ExtensionUtilities.CreateSubjectKeyIdentifier()` and `CreateAuthorityKeyIdentifier()`
- Uses `KeyPurposeID.id_kp_serverAuth`
- Signature algorithm derived from issuer's key type via `KeyGenerator.GetSignatureAlgorithm()`

#### `KeyGenerator` (static)
**File:** `Crypto/KeyGenerator.cs`

Generates key pairs (RSA, ECDSA P-256) and random serial numbers.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GenerateRsaKeyPair(keySize)` | `AsymmetricCipherKeyPair` | Generates an RSA key pair of the specified size (typically 2048 or 4096) |
| `GenerateEcdsaKeyPair()` | `AsymmetricCipherKeyPair` | Generates an ECDSA key pair on the P-256 (secp256r1) curve |
| `GenerateSerialNumber()` | `string` | Generates a random 128-bit positive serial number as an uppercase hex string |
| `GetSignatureAlgorithm(key)` | `string` | Returns the signature algorithm name for the given key type: `"SHA256withRSA"` for RSA keys, `"SHA256withECDSA"` for ECDSA keys |

#### `PemHelper` (static)
**File:** `Crypto/PemHelper.cs`

PEM encoding/decoding helpers for certificates, private keys, CRLs, CSRs, and PKCS#12 archives.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `WriteCertificatePem(cert)` | `string` | Writes an X509Certificate as a PEM string |
| `WritePrivateKeyPem(privateKey)` | `string` | Writes a private key as a PKCS#8 PEM string (`"PRIVATE KEY"` type) |
| `WriteCrlPem(crl)` | `string` | Writes an X509Crl as a PEM string |
| `ReadCertificatePem(pem)` | `X509Certificate` | Reads a PEM-encoded certificate |
| `ReadPrivateKeyPem(pem)` | `AsymmetricKeyParameter` | Reads a PEM-encoded private key (handles both `AsymmetricKeyParameter` and `AsymmetricCipherKeyPair` return types) |
| `ReadCrlPem(pem)` | `X509Crl` | Reads a PEM-encoded CRL |
| `ReadCsrPem(pem)` | `Pkcs10CertificationRequest` | Parses a PEM-encoded Certificate Signing Request (CSR) |
| `ConvertToBouncyCastle(cert)` | `X509Certificate` | Converts a .NET `X509Certificate2` to a BouncyCastle `X509Certificate` |
| `ConvertToBouncyCastlePrivateKey(rsa)` | `AsymmetricKeyParameter` | Converts a .NET `RSA` instance to BouncyCastle `RsaPrivateCrtKeyParameters` |
| `WriteDerAsPem(derData, type)` | `string` | Wraps raw DER bytes in PEM format with the given type label |
| `WritePkcs12(cert, privateKey, chain, password)` | `byte[]` | Creates a PKCS#12 (PFX) archive containing the certificate, private key, and CA chain |

### Storage/

#### `DataPathProvider`
**File:** `Storage/DataPathProvider.cs`

Centralizes all file path logic for the PKI data directory.

**Directory Structure:**
```
{BasePath}/
├── ca/                    # CA certificates and keys
│   ├── root.crt
│   ├── root.key
│   ├── intermediate.crt
│   └── intermediate.key
├── certificates/          # Active end-entity certificates
│   ├── {serial}.crt
│   ├── {serial}.key
│   └── {serial}.json      # Metadata
├── revoked/               # Revoked certificates (moved from certificates/)
│   ├── {serial}.crt
│   ├── {serial}.key
│   └── {serial}.json      # Metadata with revocation info
├── crl/                   # Certificate Revocation Lists
│   └── ca.crl
├── audit/                 # Append-only audit logs (daily files)
│   └── audit-{yyyy-MM-dd}.jsonl
└── configuration/         # Runtime PKI configuration
    └── pki.json
```

| Property | Type | Description |
|----------|------|-------------|
| `BasePath` | `string` | Root data directory path |
| `CaPath` | `string` | `{BasePath}/ca` |
| `CertificatesPath` | `string` | `{BasePath}/certificates` |
| `RevokedPath` | `string` | `{BasePath}/revoked` |
| `CrlPath` | `string` | `{BasePath}/crl` |
| `AuditPath` | `string` | `{BasePath}/audit` |
| `ConfigurationPath` | `string` | `{BasePath}/configuration` |
| `RootCertPath` | `string` | `{CaPath}/root.crt` |
| `RootKeyPath` | `string` | `{CaPath}/root.key` |
| `IntermediateCertPath` | `string` | `{CaPath}/intermediate.crt` |
| `IntermediateKeyPath` | `string` | `{CaPath}/intermediate.key` |
| `CrlFilePath` | `string` | `{CrlPath}/ca.crl` |
| `ConfigurationFilePath` | `string` | `{ConfigurationPath}/pki.json` |
| `PasswordFilePath` | `string` | `{ConfigurationPath}/password.hash` |

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetCertificatePath(serial)` | `string` | `{CertificatesPath}/{serial}.crt` |
| `GetKeyPath(serial)` | `string` | `{CertificatesPath}/{serial}.key` |
| `GetMetadataPath(serial)` | `string` | `{CertificatesPath}/{serial}.json` |
| `GetRevokedCertificatePath(serial)` | `string` | `{RevokedPath}/{serial}.crt` |
| `GetRevokedKeyPath(serial)` | `string` | `{RevokedPath}/{serial}.key` |
| `GetRevokedMetadataPath(serial)` | `string` | `{RevokedPath}/{serial}.json` |
| `EnsureDirectoriesExist()` | `void` | Creates all required directories under the data path |

#### `FileCertificateStore`
**File:** `Storage/FileCertificateStore.cs`

File-based store for certificate records (metadata + PEM files).

| Method | Return Type | Description |
|--------|-------------|-------------|
| `SaveAsync(record, certificatePem, privateKeyPem)` | `Task` | Saves a certificate record: metadata JSON, certificate PEM, and private key PEM |
| `ListAsync()` | `Task<List<CertificateRecord>>` | Lists all certificate records (both `certificates/` and `revoked/` directories) |
| `GetBySerialAsync(serialNumber)` | `Task<CertificateRecord?>` | Retrieves a certificate record by serial number. Checks both active and revoked directories. |
| `GetCertificatePemAsync(serialNumber)` | `Task<string>` | Reads the PEM-encoded certificate for the given serial |
| `GetPrivateKeyPemAsync(serialNumber)` | `Task<string>` | Reads the PEM-encoded private key for the given serial |
| `RevokeAsync(serialNumber, reason)` | `Task<bool>` | Marks a certificate as revoked: moves files to revoked/ and updates metadata |
| `ListRevokedAsync()` | `Task<List<CertificateRecord>>` | Lists all revoked certificate records |

**Key Implementation Details:**
- Metadata is stored as JSON using `StorageJsonContext` (indented format)
- Revocation moves `.crt`, `.key`, and `.json` files from `certificates/` to `revoked/`
- `ListAsync()` reads all `*.json` files from both `certificates/` and `revoked/` directories
- `GetBySerialAsync()` checks `certificates/` first, then `revoked/`

#### `FileAuditService`
**File:** `Storage/FileAuditService.cs`

Append-only audit log service. Writes JSON-lines to daily files.
Implements `IAuditService`.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `LogAsync(action, details, actor?)` | `Task` | Appends an audit entry to today's audit log file |
| `GetEntriesAsync(limit = 100)` | `Task<List<AuditEntry>>` | Reads all audit entries from all audit log files, sorted by timestamp descending (newest first) |

**Key Implementation Details:**
- Audit log files are named `audit-{yyyy-MM-dd}.jsonl` (one per day)
- Each entry is a single-line JSON object (JSON-lines format)
- Uses `AuditJsonContext` with `WriteIndented = false` to ensure each entry fits on one line
- Thread-safe via `SemaphoreSlim`

#### `FileConfigurationStore`
**File:** `Storage/FileConfigurationStore.cs`

File-based configuration store. Loads from JSON file, falls back to defaults.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `LoadAsync()` | `Task<PkiConfiguration>` | Loads configuration from file, or returns defaults if file doesn't exist |
| `SaveAsync(config)` | `Task` | Saves configuration to file |

**Key Implementation Details:**
- Configuration file: `{ConfigurationPath}/pki.json`
- Falls back to `PkiConfiguration` defaults passed in constructor if file doesn't exist
- Thread-safe via `SemaphoreSlim`

#### `StoredMetadata`
**File:** `Storage/StoredMetadata.cs`

JSON-serializable metadata record persisted alongside certificate/key PEM files.

| Property | Type | Description |
|----------|------|-------------|
| `SerialNumber` | `string` | Unique hex serial number |
| `CommonName` | `string` | CN field |
| `SanEntries` | `List<string>` | Subject Alternative Names |
| `KeyAlgorithm` | `KeyAlgorithm` | RSA or ECDSA |
| `KeySize` | `int` | Key size in bits |
| `CreatedAt` | `DateTime` | When the certificate was issued |
| `ExpiresAt` | `DateTime` | When the certificate expires |
| `Issuer` | `string` | Issuer name |
| `RevokedAt` | `DateTime?` | When the certificate was revoked (null if not revoked) |
| `RevocationReason` | `string?` | Reason for revocation |

#### `StoredConfiguration`
**File:** `Storage/FileConfigurationStore.cs`

JSON-serializable configuration record persisted in the configuration directory.

| Property | Type | Description |
|----------|------|-------------|
| `DefaultDomain` | `string` | Default domain for issued certificates |
| `CrlUrl` | `string` | CRL distribution point URL (supports `{API_CRL}` placeholder) |
| `DefaultValidityDays` | `int` | Default certificate validity period in days |
| `Algorithm` | `KeyAlgorithm` | Default key algorithm |
| `RsaKeySize` | `int` | RSA key size in bits |
| `Organization` | `string` | Organization (O) field |
| `OrganizationalUnit` | `string` | Organizational Unit (OU) field |
| `Country` | `string` | Country (C) field |
| `State` | `string` | State/province (ST) field |
| `Locality` | `string` | Locality (L) field |

#### `StoredAuditEntry`
**File:** `Storage/FileAuditService.cs`

JSON-serializable audit entry persisted in daily audit log files.

| Property | Type | Description |
|----------|------|-------------|
| `Timestamp` | `DateTime` | When the event occurred |
| `Action` | `string` | Event type (e.g., "ISSUE", "REVOKE") |
| `Details` | `string` | Human-readable description |
| `Actor` | `string?` | Who triggered the event |

#### `StorageJsonContext` (partial)
**File:** `Storage/StoredMetadata.cs`

Source-generated JSON context for storage serialization.
Uses `WriteIndented = true` for human-readable metadata files.

**Registered Types:**
- `StoredMetadata`
- `List<StoredMetadata>`
- `StoredConfiguration`
- `StoredAuditEntry`
- `List<StoredAuditEntry>`
- `Dictionary<string, string>`

#### `AuditJsonContext` (partial)
**File:** `Storage/StoredMetadata.cs`

Source-generated JSON context for audit log entries.
Uses `WriteIndented = false` so each entry fits on a single line (JSON-lines format).

**Registered Types:**
- `StoredAuditEntry`
- `List<StoredAuditEntry>`

### Services/

#### `BouncyCastleCertificateAuthorityService`
**File:** `Services/BouncyCastleCertificateAuthorityService.cs`

BouncyCastle-based Certificate Authority service.
Manages Root CA and Intermediate CA lifecycle (creation, loading, retrieval, reset).
Implements `ICertificateAuthorityService`.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `EnsureCaInitializedAsync()` | `Task` | Initializes the CA: loads existing CA files or generates new ones |
| `GetRootCaPemAsync()` | `Task<string>` | Returns the PEM-encoded Root CA certificate |
| `GetIntermediateCaPemAsync()` | `Task<string>` | Returns the PEM-encoded Intermediate CA certificate |
| `GetIntermediateCaKeyPemAsync()` | `Task<string>` | Returns the PEM-encoded Intermediate CA private key |
| `GetCaChainPemAsync()` | `Task<string>` | Returns the full CA chain (intermediate + root) in PEM format |
| `GetIntermediateCertificate()` | `X509Certificate` | Returns the BouncyCastle X509Certificate object for the intermediate CA |
| `GetIntermediatePrivateKey()` | `AsymmetricKeyParameter` | Returns the BouncyCastle private key for the intermediate CA |
| `ResetCaAsync()` | `Task` | Resets the CA: clears in-memory objects, deletes all CA files, and regenerates Root CA and Intermediate CA using current runtime configuration |

**Key Implementation Details:**
- Thread-safe via `SemaphoreSlim` (prevents concurrent CA initialization)
- `EnsureCaInitializedAsync()` checks if all 4 CA files exist (root cert/key, intermediate cert/key)
- If files exist: loads them into memory via `PemHelper`
- If files don't exist: generates new Root CA and Intermediate CA via `CertificateGenerator`
- CA certificates are generated using RSA with the configured key size
- Root CA subject: `"MiniPKI Root CA"`, Intermediate CA subject: `"MiniPKI Intermediate CA"`
- `GenerateCaAsync()` loads runtime configuration from `FileConfigurationStore` to get O, OU, C, ST, L fields

#### `BouncyCastleCertificateService`
**File:** `Services/BouncyCastleCertificateService.cs`

BouncyCastle-based certificate service.
Issues, revokes, lists, and retrieves end-entity certificates.
Implements `ICertificateService`.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `ListAsync()` | `Task<List<CertificateRecord>>` | Lists all issued certificates (valid and revoked) |
| `GetBySerialAsync(serialNumber)` | `Task<CertificateRecord?>` | Retrieves a certificate record by serial number |
| `IssueAsync(commonName, sanEntries, algorithm, keySize, validityDays)` | `Task<CertificateRecord>` | Issues a new end-entity certificate signed by the Intermediate CA |
| `RevokeAsync(serialNumber, reason)` | `Task<bool>` | Revokes a certificate by serial number. **Regenerates the CRL** after successful revocation. |
| `DownloadAsync(serialNumber)` | `Task<(string certificatePem, string keyPem, string chainPem)>` | Downloads the certificate, private key, and CA chain for the given serial |

**Key Implementation Details:**

`IssueAsync()`:
1. Loads runtime configuration from `FileConfigurationStore`
2. Generates key pair based on algorithm (RSA with specified key size, or ECDSA P-256)
3. Generates random serial number via `KeyGenerator.GenerateSerialNumber()`
4. Gets intermediate CA cert and key from `BouncyCastleCertificateAuthorityService`
5. Builds CRL distribution point URL from `config.CrlUrl`, resolving `{API_CRL}` → `/api/crl/current`
6. Generates certificate via `CertificateGenerator.GenerateEndEntityCertificate()`
7. Converts cert and key to PEM format
8. Creates `CertificateRecord` with metadata
9. Saves to store via `FileCertificateStore.SaveAsync()`
10. Logs audit entry: action="ISSUE", details="Issued certificate for {commonName} (serial: {serial})"
11. Returns the created `CertificateRecord`

`RevokeAsync()`:
1. Gets record from store
2. Returns false if record not found or already revoked
3. Calls `FileCertificateStore.RevokeAsync()` to move files and update metadata
4. Logs audit entry: action="REVOKE", details="Revoked certificate {serialNumber}: {reason}"
5. **Regenerates the CRL** via `_crlService.GenerateCrlAsync()` so the revoked serial appears immediately
6. Returns true on success

`DownloadAsync()`:
1. Gets cert PEM and key PEM from store
2. Gets CA chain PEM from `BouncyCastleCertificateAuthorityService.GetCaChainPemAsync()`
3. Returns tuple of (certPem, keyPem, chainPem)

#### `BouncyCastleCrlService`
**File:** `Services/BouncyCastleCrlService.cs`

BouncyCastle-based CRL service.
Generates and publishes Certificate Revocation Lists (CRLs).
Implements `ICrlService`.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GenerateCrlAsync()` | `Task<string>` | Generates a new CRL containing all revoked certificates. Writes the CRL to the CRL directory as `ca.crl`. |
| `GetCurrentCrlPemAsync()` | `Task<string>` | Returns the current CRL in PEM format, generating it if it does not exist. |
| `GetRevokedCertificatesAsync()` | `Task<List<CertificateRecord>>` | Returns the list of revoked certificate records |

**Key Implementation Details:**
- Uses `X509V2CrlGenerator` (high-level API)
- CRL issuer: intermediate CA's subject DN
- CRL validity: `ThisUpdate=now`, `NextUpdate=now+1day`
- Adds revoked certificates: serial number (parsed as hex `BigInteger`), revocation date, reason code 0
- Signs CRL with intermediate CA's private key using `KeyGenerator.GetSignatureAlgorithm()`
- Writes CRL to `{CrlPath}/ca.crl`
- Logs audit entry: action="CRL_GENERATE", details="Generated CRL with {count} revoked certificates"

#### `ConfigurationService`
**File:** `Services/ConfigurationService.cs`

Configuration service that reads and updates the runtime PKI configuration.
Implements `IConfigurationService`.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetAsync()` | `Task<PkiConfiguration>` | Gets the current PKI configuration |
| `UpdateAsync(configuration)` | `Task<PkiConfiguration>` | Updates the PKI configuration |

---

## MiniPKI.Api

ASP.NET Core 10 Web API. Provides REST endpoints for certificate management,
authentication, configuration, CRL access, audit logs, and dashboard statistics.

### `Program.cs`

Application entry point. Configures DI, middleware pipeline, and JSON serialization.

**DI Registrations:**
- `DataPathProvider` — singleton, reads `Storage:DataPath` from configuration
- `PkiConfiguration` — singleton, bound from `appsettings.json` `Pki` section
- `FileConfigurationStore` — singleton
- `FileCertificateStore` — singleton
- `SessionStore` — singleton
- `FileAuditService` — singleton, aliased as `IAuditService`
- `PasswordService` — singleton
- `BouncyCastleCertificateAuthorityService` — singleton
- `BouncyCastleCertificateService` — singleton
- `BouncyCastleCrlService` — singleton
- `CaInitializerService` — hosted service (initializes CA on startup)

**JSON Serialization:**
- Enums serialized as strings via `JsonStringEnumConverter`
- Configured via `AddControllers().AddJsonOptions()`

**Middleware Pipeline (order matters):**
1. `SecurityHeadersMiddleware` — sets CSP, X-Frame-Options, X-Content-Type-Options, HSTS
2. `AuthenticationMiddleware` — validates session cookie for protected endpoints
3. `HttpsRedirection`
4. `MapControllers`
5. Health endpoint: `GET /health`

### Auth/

#### `PasswordHasher` (static)
**File:** `Auth/PasswordHasher.cs`

PBKDF2-HMAC-SHA256 password hasher.
Hash format: `pbkdf2-sha256:{iterations}:{base64-salt}:{base64-hash}`

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Hash(password, iterations = 100000)` | `string` | Hashes a plaintext password using PBKDF2-HMAC-SHA256 |
| `Verify(password, storedHash)` | `bool` | Verifies a plaintext password against a stored hash using constant-time comparison |

#### `PasswordService`
**File:** `Auth/PasswordService.cs`

Manages admin password: load from file, change, first-run setup.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetPasswordHash()` | `string` | Returns the current password hash. Loads from file if available, otherwise falls back to configuration (`Auth:PasswordHash`). |
| `IsFirstRun()` | `bool` | Returns true if the password file doesn't exist (first run) |
| `ChangePassword(currentPassword, newPassword)` | `bool` | Changes the admin password. Returns true on success, false if current password is incorrect. |
| `SetInitialPassword(newPassword)` | `bool` | Sets the initial password on first run. Returns false if not first run. |

#### `SessionStore`
**File:** `Auth/SessionStore.cs`

In-memory session store with expiration.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `CreateSession()` | `string` | Creates a new session and returns the session token (Base64-encoded 32 random bytes) |
| `IsValid(token)` | `bool` | Validates a session token. Returns false if invalid, expired, or null. |
| `RemoveSession(token)` | `void` | Removes a session (logout). |

#### `CaInitializerService`
**File:** `Auth/CaInitializerService.cs`

Hosted service that initializes the Certificate Authority on application startup.
Calls `BouncyCastleCertificateAuthorityService.EnsureCaInitializedAsync()`.

### Controllers/

#### `AuthController`
**File:** `Controllers/AuthController.cs`

Authentication endpoints.

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/auth/login` | Login with password. Sets session cookie. |
| `POST` | `/api/auth/logout` | Logout. Removes session. |
| `GET` | `/api/auth/status` | Check session status and first-run flag. |
| `POST` | `/api/auth/set-password` | Set initial password (first run only). |
| `POST` | `/api/auth/change-password` | Change password (requires current password). |

#### `CertificatesController`
**File:** `Controllers/CertificatesController.cs`

Certificate management endpoints.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/certificates` | List all certificates (valid and revoked) |
| `GET` | `/api/certificates/{serial}` | Get certificate details by serial number |
| `POST` | `/api/certificates` | Issue a new certificate |
| `POST` | `/api/certificates/csr` | Issue a certificate from a CSR (Certificate Signing Request) |
| `POST` | `/api/certificates/{serial}/revoke` | Revoke a certificate by serial number |
| `GET` | `/api/certificates/{serial}/download` | Download certificate PEM |
| `GET` | `/api/certificates/{serial}/bundle` | Download PEM bundle (certificate + CA chain) |
| `GET` | `/api/certificates/{serial}/chain` | Download CA chain PEM |
| `GET` | `/api/certificates/{serial}/key` | Download private key PEM |
| `GET` | `/api/certificates/{serial}/p12` | Download PKCS#12 file (requires password query parameter) |

#### `ConfigurationController`
**File:** `Controllers/ConfigurationController.cs`

PKI configuration endpoints.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/configuration` | Get current PKI configuration |
| `PUT` | `/api/configuration` | Update PKI configuration |
| `POST` | `/api/configuration/reset-ca` | Reset CA: deletes all issued/revoked certs, CRL, and CA certs/keys, then regenerates new CA using current configuration |

#### `CrlController`
**File:** `Controllers/CrlController.cs`

CRL (Certificate Revocation List) endpoints.

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/crl/generate` | Force CRL regeneration |
| `GET` | `/api/crl/current` | Download current CRL in PEM format |

#### `DashboardController`
**File:** `Controllers/DashboardController.cs`

Dashboard statistics endpoint.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/dashboard/stats` | Returns dashboard statistics: total, active, revoked, expiring soon, and latest issued certificates |

#### `AuditController`
**File:** `Controllers/AuditController.cs`

Audit log endpoint.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/audit` | Returns audit log entries (sorted by timestamp descending, newest first) |

### Middleware/

#### `AuthenticationMiddleware`
**File:** `Middleware/AuthenticationMiddleware.cs`

Validates session cookie for protected API endpoints.

**Public paths (no auth required):**
- `/api/auth/login`
- `/api/auth/status`
- `/api/auth/set-password`
- `/api/crl/current`
- `/health`
- All non-`/api` paths (static files, etc.)

**Behavior:**
- For protected paths: checks `minipki_session` cookie against `SessionStore`
- Returns 401 Unauthorized if session is invalid or missing

#### `SecurityHeadersMiddleware`
**File:** `Middleware/SecurityHeadersMiddleware.cs`

Sets HTTP security headers on all responses:
- `Content-Security-Policy`
- `X-Frame-Options: DENY`
- `X-Content-Type-Options: nosniff`
- `Strict-Transport-Security` (HSTS)

### Models/

#### `Dtos.cs`
**File:** `Models/Dtos.cs`

Data Transfer Objects for API requests and responses.

**Request DTOs:**
- `LoginRequest` — password field
- `IssueCertificateRequest` — commonName, sanEntries, keyAlgorithm, keySize, validityDays
- `IssueCsrRequest` — csrPem, validityDays
- `RevokeCertificateRequest` — reason
- `UpdateConfigurationRequest` — all PKI configuration fields
- `SetPasswordRequest` — password
- `ChangePasswordRequest` — currentPassword, newPassword

**Response DTOs:**
- `LoginResponse` — success, error
- `AuthStatusResponse` — authenticated, firstRun
- `CertificateResponse` — serialNumber, commonName, sanEntries, keyAlgorithm, keySize, createdAt, expiresAt, revokedAt, revocationReason, status, issuer
- `AuditEntryResponse` — timestamp, action, details, actor
- `DashboardStatsResponse` — total, active, revoked, expiringSoon, latestIssued

---

## MiniPKI.Tests

xUnit test project with unit, integration, and API tests.

### Test Structure

```
MiniPKI.Tests/
├── Unit/
│   ├── CertificateGeneratorTests.cs     # Certificate generation tests
│   ├── CertificateRecordTests.cs        # CertificateRecord status logic + DataPathProvider tests
│   └── KeyGeneratorTests.cs             # Key generation and serial number tests
├── Integration/
│   ├── AuditServiceTests.cs             # Audit log creation and retrieval
│   ├── CertificateAuthorityServiceTests.cs  # CA initialization, PEM retrieval, reset
│   ├── CertificateServiceTests.cs       # Certificate issuance, revocation, CRL URL, download
│   ├── ConfigurationServiceTests.cs     # Configuration read/write
│   ├── CrlServiceTests.cs              # CRL generation, revoked cert inclusion
│   └── FullPkiWorkflowTests.cs          # End-to-end PKI workflow
└── Api/
    ├── TestWebApplicationFactory.cs     # Custom WebApplicationFactory with temp data dir
    ├── AuditApiTests.cs                # Audit API endpoint tests
    ├── AuthFlowTests.cs                # Authentication flow tests
    ├── CertificateLifecycleTests.cs    # Certificate lifecycle API tests
    ├── ConfigurationApiTests.cs        # Configuration API tests
    └── CrlApiTests.cs                  # CRL API tests
```

### Build & Test Commands

#### Build Solution
```powershell
dotnet build src\backend\MiniPKI.slnx
```

#### Run All Tests
```powershell
dotnet test src\backend\MiniPKI.slnx --verbosity normal
```

#### Run Specific Test Class
```powershell
dotnet test src\backend\MiniPKI.slnx --filter "FullyQualifiedName~CertificateServiceTests" --verbosity normal
```

#### Run Only Unit Tests
```powershell
dotnet test src\backend\MiniPKI.slnx --filter "FullyQualifiedName~Unit" --verbosity normal
```

#### Run Only Integration Tests
```powershell
dotnet test src\backend\MiniPKI.slnx --filter "FullyQualifiedName~Integration" --verbosity normal
```

---

## Architecture Notes

### Clean Architecture Layers

1. **Core** (`MiniPKI.Core`): Domain models, service interfaces, configuration.
   No external dependencies. This is the innermost layer.

2. **Infrastructure** (`MiniPKI.Infrastructure`): Implements all Core interfaces using
   BouncyCastle.Cryptography 2.7.0. Contains crypto helpers, file-based storage, and
   service implementations.

3. **API** (`MiniPKI.Api`): ASP.NET Core 10 Web API. Provides REST endpoints for
   certificate management, authentication, configuration, CRL access, audit logs,
   and dashboard statistics.

4. **Tests** (`MiniPKI.Tests`): xUnit test project with unit, integration, and API tests.

### Dependency Direction

```
API → Infrastructure → Core
Tests → Core, Infrastructure, API
```

Dependencies always point inward toward Core. Core has no knowledge of Infrastructure or API.

### Key Design Decisions

1. **File-based storage**: All PKI data (CA certs, end-entity certs, CRLs, audit logs,
   configuration) is stored on the filesystem. No database is used.

2. **Two-tier CA hierarchy**: Root CA (self-signed, 20-year validity) → Intermediate CA
   (signed by root, 10-year validity, pathlen=0) → end-entity TLS certificates (signed by
   intermediate, configurable validity).

3. **BouncyCastle 2.7.0**: Uses the latest BouncyCastle API:
   - `X509ExtensionUtilities.CreateSubjectKeyIdentifier()` and `CreateAuthorityKeyIdentifier()`
   - `KeyPurposeID.id_kp_serverAuth`
   - `X509V2CrlGenerator` (high-level CRL API)
   - `CustomNamedCurves.GetByName("P-256")` for ECDSA key generation

4. **JSON-lines audit log**: Audit entries are written as single-line JSON objects to
   daily files (`audit-{yyyy-MM-dd}.jsonl`). This format is append-only and easy to parse.

5. **CRL regeneration on revocation**: When a certificate is revoked via
   `BouncyCastleCertificateService.RevokeAsync()`, the CRL is automatically regenerated
   so the revoked serial number appears immediately in the CRL.

6. **CRL URL with `{API_CRL}` placeholder**: The `CrlUrl` configuration field supports
   a `{API_CRL}` placeholder that is resolved to `/api/crl/current` at certificate
   issuance time. This allows the CRL distribution point URL to point to the MiniPKI
   CRL endpoint regardless of the host/port configuration.

7. **Session-based authentication**: Admin authentication uses PBKDF2-HMAC-SHA256
   password hashing and session-based authentication with secure cookies.
   First-run password setup is supported.

8. **Certificate subject DN**: End-entity certificate subjects include CN, O, OU, L, ST, C
   from the runtime configuration. Only non-empty fields are included in the subject DN.
