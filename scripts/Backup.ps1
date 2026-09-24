param([switch]$Production)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
$compose = @('compose', '-f', 'docker-compose.yml')
if ($Production) { $compose += @('-f', 'docker-compose.production.yml') }
$wasRunning = $false
try {
    & docker @compose --profile tools build backup
    if ($LASTEXITCODE -ne 0) { throw 'Could not build backup image.' }
    $runningContainer = & docker @compose ps --status running -q secureshare-api
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect API status.' }
    $wasRunning = -not [string]::IsNullOrWhiteSpace($runningContainer)
    if ($wasRunning) {
        & docker @compose stop secureshare-api
        if ($LASTEXITCODE -ne 0) { throw 'Could not stop API for consistent backup.' }
    }
    $query = "BACKUP DATABASE [SecureShareDb] TO DISK = '/var/opt/mssql/backups/secureshare.bak' WITH INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK = '/var/opt/mssql/backups/secureshare.bak' WITH CHECKSUM;"
    & docker @compose exec -T db bash -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "$1"' -- $query
    if ($LASTEXITCODE -ne 0) { throw 'Database backup verification failed.' }
    & docker @compose --profile tools run --rm --no-deps backup
    if ($LASTEXITCODE -ne 0) { throw 'Backup archive creation failed.' }
} finally {
    if ($wasRunning) { & docker @compose start secureshare-api }
    Pop-Location
}
