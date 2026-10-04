Design and implement a complete Public Key Infrastructure (PKI) system for issuing and managing TLS certificates for web servers.

Technology requirements:
- Backend: ASP.NET Core 10
- Frontend: React (professional enterprise-grade UI)
- Deployment: Docker and Docker Compose
- Must be fully containerized
- Must be designed to run later on Kubernetes using persistent volumes
- All state must be stored in files on mounted volumes
- No dependency on external databases

Functional requirements:

1. PKI Scope
- The system acts as a private Certificate Authority.
- It issues certificates exclusively for web servers.
- Support:
  - Root CA
  - Intermediate CA
  - End-entity TLS certificates
- Certificate generation must use modern cryptography defaults.
- Support RSA 4096 and ECDSA P-256.
- Default algorithm configurable.

2. Certificate Management
- Create new server certificates.
- Revoke issued certificates.
- Generate and publish CRLs (Certificate Revocation Lists).
- Display all issued certificates.
- Display revoked certificates.
- Store:
  - Serial number
  - Common Name (CN)
  - SAN entries
  - Creation date
  - Expiration date
  - Revocation date
  - Status (Valid / Revoked / Expired)

3. CRL Configuration
- CRL distribution point base URL must be configurable.
- Every issued certificate must automatically contain the configured CRL Distribution Point extension.
- CRLs must be generated automatically after revocation.
- CRLs must be accessible over HTTP.

4. Configuration
Implement a strongly typed configuration file.

Example configurable settings:

- Default domain suffix
- CRL base URL
- Default certificate validity period
- Default key algorithm
- Default RSA key size
- Default SAN values
- Organization
- Organizational Unit
- Country
- State
- Locality
- Certificate profile defaults

Example:

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

5. Authentication
- Protect the entire web application with a single static administrator password.
- No user management.
- No roles.
- No external identity providers.
- Password hash stored in configuration.
- ASP.NET authentication middleware.
- Session-based authentication.
- Secure logout.
- CSRF protection.

6. Storage
Everything must be stored as files.

Required directory structure:

/data
  /ca
    root.crt
    root.key
    intermediate.crt
    intermediate.key
  /certificates
  /revoked
  /crl
  /audit
  /configuration

Requirements:
- Use PEM format.
- Certificates and keys stored on mounted volumes.
- Audit logs written to files.
- Support backup by simply copying volume contents.
- No database.

7. REST API
Create a complete REST API with OpenAPI support.

Endpoints:
- Login
- Logout
- Get configuration
- Update configuration
- List certificates
- Get certificate details
- Issue certificate
- Revoke certificate
- Download certificate
- Download chain
- Download key
- Generate CRL
- Download current CRL
- Audit log access

8. Frontend
Create a modern professional React application.

Requirements:
- Enterprise-style design
- Responsive layout
- Dark mode
- Light mode
- Dashboard
- Certificate inventory page
- Revocation page
- Configuration page
- Audit page

Dashboard widgets:
- Total certificates
- Active certificates
- Revoked certificates
- Expiring soon
- Latest issued certificates

Certificate inventory:
- Search
- Filtering
- Sorting
- Status badges
- Certificate details dialog

Certificate creation form:
- Common Name
- SAN entries
- Key algorithm
- Key size
- Validity period

Revocation view:
- Certificate selection
- Revocation reason
- Confirmation dialog

9. Security
Implement security best practices:

- HTTP security headers
- Secure cookies
- CSP
- Anti-forgery protection
- File permission validation
- Private keys never exposed in logs
- Audit all administrative actions
- Password hashing via PBKDF2 or Argon2
- Strong input validation

10. Docker
Provide:
- Multi-stage Dockerfile for backend
- Dockerfile for frontend
- Docker Compose configuration
- Health checks
- Volume mappings
- Environment variable support

Example volume:

volumes:
  - ./data:/data

11. Kubernetes Readiness
Design components for future Kubernetes deployment.

Requirements:
- Stateless containers
- PersistentVolume support
- ConfigMap support
- Secret support
- Readiness probes
- Liveness probes

12. Audit Logging
Log all operations:

- Login
- Logout
- Configuration changes
- Certificate issuance
- Certificate revocation
- CRL generation

Store logs in append-only files whenever possible.

13. Architecture Deliverables
Provide:

1. Complete system architecture.
2. Project structure.
3. Directory tree.
4. ASP.NET Core 10 solution structure.
5. React frontend structure.
6. Domain model.
7. Configuration model.
8. Service layer design.
9. Security design.
10. Certificate generation workflow.
11. Certificate revocation workflow.
12. CRL publishing workflow.
13. Docker deployment files.
14. Kubernetes deployment examples.
15. Sample implementation code for critical services.
16. Recommended NuGet packages.
17. Threat model and mitigations.

14. Tests
Use XUnit to prepare unit and integration tests. All tests must pass.

The implementation should follow clean architecture principles, dependency injection, strongly typed configuration, SOLID principles, enterprise coding standards, and production-ready security practices.