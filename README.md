# SecureShare

SecureShare is a .NET 10 file-sharing application with authenticated encryption, expiring links, password protection, and a private owner dashboard. SQL Server Express stores metadata; encrypted files and key material use persistent Docker volumes.

## Features

- Upload files up to 100 MB with a 1–168 hour expiry and 1–100 allowed downloads.
- Optional passwords, hashed with PBKDF2 through ASP.NET Core Identity.
- AES-256-GCM in bounded 64 KiB frames authenticates ciphertext, frame order, and the end of the file.
- File keys are protected with ASP.NET Core Data Protection. Production requires a separate wrapping certificate.
- Owner-only listing, download history, revocation, deletion, and recovery on another device.
- Atomic download claims with transactional audit logging and retry idempotency.
- ClamAV scanning in Docker; scanner outages reject uploads.
- Server-side input validation, CSRF protection, IP rate limiting, and storage quotas.
- Cleanup removes unavailable file data and keys every 60 seconds; orphan reconciliation runs after 24 hours.
- Health endpoints, Prometheus metrics, Grafana, and Alertmanager.
- Verified, encrypted backups and a CI pipeline with tests, security scans, readiness verification, and application-image rollback.

A recovery code is a private management credential. Anyone with the code can manage that owner's files. Anyone with a share link can inspect its file metadata and download it unless a file password is required. There is no email account system or recipient identity verification.

## Local Docker setup

Requirements: Docker Desktop with Linux containers, Docker Compose 2.24.4 or newer, and PowerShell 7 for the Windows initialization script. Allow sufficient Docker memory for SQL Server and ClamAV; the full stack is intended for a host with at least 8 GB RAM and spare disk capacity for working files and backups.

Windows:

```powershell
./scripts/Initialize.ps1
docker compose up -d --build --wait --wait-timeout 600
```

Linux, on a fresh checkout:

```bash
sudo bash scripts/initialize.sh localhost
sudo docker compose up -d --build --wait --wait-timeout 600
```

Initialization generates fresh passwords and a wrapping certificate in ignored local files. It refuses to overwrite an existing `.env`. Preserve those secrets when updating an installation; changing the SQL password in Compose does not change an existing SQL Server login.

Services:

| Service | Local address |
| --- | --- |
| Upload portal and private dashboard | http://localhost:8080 |
| Swagger, development only | http://localhost:8080/swagger |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| Alertmanager | http://localhost:9093 |
| API liveness | http://localhost:8080/health/live |
| API readiness | http://localhost:8080/health/ready |

Grafana's password is generated in `.env`; its username is `admin`. SQL Server and ClamAV have no published host ports. All published development services bind to localhost.

ClamAV may need several minutes to download signatures on first startup. Uploads remain unavailable if scanning is not ready.

## Development and testing

Requirements: .NET 10 SDK. The project uses SQL Server; local `dotnet run` defaults to Windows SQL Express in `appsettings.json`. Supply `ConnectionStrings__DefaultConnection` for a different SQL Server.

```powershell
dotnet restore SecureShare.slnx --locked-mode
dotnet test SecureShare.slnx -c Release --no-restore
dotnet run --project SecureShare.API
```

Tests use an isolated SQLite database, temporary file storage, and ASP.NET Core's test server. They verify frame boundaries and tampering, concurrent download claims, passwords, owner isolation, CSRF, recovery, quotas, scanner failures, cleanup, missing files, and legacy upgrades. SQL Server and ClamAV require a separate Docker integration check.

Direct `dotnet run` in Development does not enable scanning by default. Docker enables it explicitly. Production enables scanning by default and requires certificate-protected Data Protection keys. Never expose Development to the internet.

## Configuration

Environment variables use ASP.NET Core's `__` separator.

| Setting | Default |
| --- | --- |
| Storage__MaxFileBytes | 104857600 |
| Storage__OwnerQuotaBytes | 524288000 |
| Storage__TotalQuotaBytes | 5368709120 |
| Storage__MaxFilesPerOwner | 100 |
| Storage__MaxTotalFiles | 10000 |
| Storage__MaxExpiryHours | 168 |
| Storage__MaxDownloads | 100 |
| Storage__CleanupSeconds | 60 |
| Storage__AuditRetentionDays | 30 |
| Storage__Path | SecureUploads |
| DataProtection__KeyPath | DataProtectionKeys |
| Scanner__Enabled | true in Production, false for direct Development |
| Scanner__Host / Scanner__Port | clamav / 3310 |
| Database__MigrateOnStartup | true in Development, false in Production |

ClamAV's `StreamMaxLength` and `MaxFileSize` must be adjusted in `deploy/clamd.conf` if the application upload limit is increased. Server processing uses temporary disk files; reserve headroom beyond the retained-file quota.

Requests are limited to 120 per minute per IP, with additional limits of 10 uploads and 10 download attempts per minute. At most eight application requests run concurrently; health and metrics requests are excluded from that concurrency cap. These limiters are local to one application process. This deployment supports one API instance with local persistent storage.

## API flow

1. GET `/api/session` establishes an HttpOnly owner cookie and returns a CSRF token, recovery code, and limits.
2. Send the CSRF token in `X-CSRF-TOKEN` for every POST or DELETE.
3. POST multipart data to `/api/files/upload` with `file`, `expiryHours`, `maxDownloads`, and optional `password`.
4. Share the returned relative URL, resolved against the public site origin.
5. GET `/api/files/{id}/info` checks availability without consuming a download.
6. POST JSON `{"password": null}` or a password to `/api/files/{id}/download` to receive the file.

Owner endpoints: GET `/api/files`, GET `/api/files/{id}/logs`, POST `/api/files/{id}/revoke`, and DELETE `/api/files/{id}`. POST `/api/session/restore` accepts a recovery code; fetch a new session token after changing identities.

The original GET `/api/files/{id}` links redirect to the download page. A GET, link preview, or password failure never consumes an allowance. Missing or corrupt stored files are rejected before claiming an allowance.

A count records an authorized transfer, not confirmation that the recipient saved the complete file. A disconnect after authorization still consumes the allowance. Range requests are disabled to preserve predictable count semantics.

## Expiry and deletion

Unavailable links are blocked immediately. The cleanup worker deletes encrypted file data, protected encryption keys, IVs, and password hashes during its next cycle. Explicit deletion removes the owner's metadata and logs as well.

Access logs are retained for 30 days. Inactive metadata is removed 30 days after its expiry, or upload time for legacy records without expiry. Backups retain previous snapshots for their configured retention period; deletion from active storage does not erase existing backup archives.

Startup migration upgrades active legacy AES-CBC files to authenticated GCM without changing their IDs. Unreadable legacy files are retired. Old uploads have no owner identity because the original schema did not record one; they cannot be assigned to a browser safely. Back up the database and file storage before upgrading.

## Production deployment

To replace an unavailable AWS account, follow [Switch AWS accounts](docs/aws-account-switch.md). Terraform requires the intended 12-digit `expected_account_id`; use `scripts/Use-AwsAccount.ps1` to verify and save a named profile for this project.

The production Compose override adds Caddy with automatic HTTPS, removes the API's host port, and trusts only the configured Caddy proxy address for forwarded headers. Monitoring stays on localhost; access it through SSH tunnels or AWS Session Manager.

1. Provision infrastructure using `teraform/main.tf`. Run `terraform plan` and review it before applying.
2. Clone the repository to `/opt/secureshare` on the server. Generate server secrets with `sudo bash scripts/initialize.sh your-domain.example`.
3. Point the domain's DNS to the provisioned public IP.
4. Build or pull the API image and run the explicit migration job before starting Production:

```bash
sudo docker compose -f docker-compose.yml -f docker-compose.production.yml build secureshare-api
sudo docker compose -f docker-compose.yml -f docker-compose.production.yml up -d db clamav
sudo docker compose -f docker-compose.yml -f docker-compose.production.yml run --rm secureshare-api --migrate-only
sudo docker compose -f docker-compose.yml -f docker-compose.production.yml up -d --no-build --wait --wait-timeout 600
```

Use a fresh server key volume for Production; development Data Protection keys are not encrypted at rest. Production secrets must be readable by container UID/GID 1654; the Linux initialization script sets that ownership. Keep the wrapping certificate and password for as long as files or backups protected by it are retained.

Terraform defaults to a Free Tier eligible `t3.micro`, a 30 GB encrypted gp3 root volume, IMDSv2, public ports 80/443, and an SSM role. It uses the instance's assigned public IP instead of allocating an Elastic IP. SSH is disabled unless trusted CIDRs and a key pair are configured. Monitoring, SQL, and port 8080 are not opened by Terraform. Remote Terraform state storage is deployment-specific and must be configured before team use.

Existing installations must back up their current database and upload directory before switching to named volumes. The old Compose configuration had no volumes; new empty volumes cannot automatically recover data from old containers. Restore the old database and files into the new persistent storage before running migrations.

## CI/CD

Jenkins requires a Linux agent labelled `linux-docker-dotnet` with Docker, .NET 10, EF CLI 10, Terraform, and Trivy. Required plugins include Credentials Binding and SSH Credentials; both are installed on the inspected Jenkins instance.

Credentials: `docker-hub-id`, `aws-ssh-key-id`, and a file credential `aws-known-hosts` containing independently verified server host keys. The pipeline reads the deployment username from `aws-ssh-key-id`. That user needs permission to operate Docker and update `/opt/secureshare`.

For AWS account `593602867169`, Jenkins credential `aws-ssh-key-id` is an **SSH Username with private key** credential for user `ubuntu`. `DEPLOY_HOST` defaults to the Terraform-managed server, and `deploy/known_hosts` pins that server's SSH host key. The private key stays in Jenkins. This SSH credential cannot run AWS API or Terraform commands; local AWS profile `hariharan` is used for Terraform commands run on this computer. See [AWS account switch](docs/aws-account-switch.md).

The pipeline restores locked packages, runs tests, checks migration completeness, scans source/configuration and the image, and pushes a commit-tagged image. Deployment uses the pinned host key, a verified backup, explicit migrations, and container readiness checks. Override `DEPLOY_HOST` with a blank value when a manual build should skip deployment.

Updates use a brief maintenance window. On failure, a previous image advertising encryption format 2 can be restored. The original application cannot read protected keys or GCM files; rollback across the first upgrade requires restoring the pre-upgrade database and file archive together. Database migrations are not automatically reversed. The included schema migration is additive, while legacy encryption conversion changes stored data.

## Backups and recovery

Create a consistent, verified, encrypted backup:

```powershell
./scripts/Backup.ps1
# Add -Production for the production Compose override.
```

Linux:

```bash
sudo bash scripts/backup.sh --production
```

The API pauses during the snapshot. The archive includes a SQL backup, encrypted uploads, the Data Protection key ring, and the wrapping certificate/password. GPG encrypts it using `secrets/backup-password`, which is excluded from the archive. Save that password separately in a secure password manager. Losing the password makes the archive unrecoverable.

For daily production backups, install the provided service and timer:

```bash
sudo cp deploy/secureshare-backup.service deploy/secureshare-backup.timer /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now secureshare-backup.timer
```

Local Linux archives expire after 30 days. Configure `BACKUP_S3_URI` in the backup service environment and install/configure AWS CLI to copy encrypted archives off the server. The copy does not delete remote archives; configure remote retention separately.

Recovery should first be rehearsed on an isolated deployment:

1. Decrypt the selected `.tar.gz.gpg` archive with GPG using your separately saved backup password.
2. Stop the target API. Restore `uploads/` and `keys/` into their matching Docker volumes, and the wrapping certificate/password into the target `secrets/`.
3. Copy `database/secureshare.bak` into SQL Server's backup volume. Run `RESTORE VERIFYONLY`, then `RESTORE DATABASE [SecureShareDb]` with SQL Server in single-user mode.
4. Apply any required forward migrations, start the API, and verify a known file download and owner recovery code.
5. Remove temporary decrypted archives after verification.

Do not remove persistent volumes during updates. Keep secrets and off-server backups separate from the public repository.

## Monitoring

Prometheus scrapes `/metrics`; Caddy blocks that route publicly. Grafana provisions the included runtime dashboard and a SecureShare dashboard. Alert rules cover API availability, HTTP server errors, storage usage, and stalled cleanup.

Alerts are visible in the local Alertmanager UI. Configure an external receiver in `deploy/alertmanager.yml` for email or webhook delivery; no messages are sent by the default configuration.

## Project layout

```text
SecureShare.API/        API, infrastructure services, migrations, and browser portal
SecureShare.Core/       Domain entities and service contracts
SecureShare.Tests/      Security and file-flow regression tests
deploy/                HTTPS, scanning, alerts, backup image, and timer configuration
scripts/               Initialization, backup, and deployment scripts
grafana/               Provisioned dashboards and data source
teraform/              AWS infrastructure (original directory name retained)
```
