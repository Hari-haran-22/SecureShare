param([string]$Domain = 'localhost')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $root '.env'
$secretPath = Join-Path $root 'secrets'
if (Test-Path -LiteralPath $envPath) { throw '.env already exists. Existing secrets were preserved.' }
New-Item -ItemType Directory -Force -Path $secretPath | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root 'backups') | Out-Null
function New-Secret { return [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
$sql = 'Ss9!' + (New-Secret)
$grafana = New-Secret
$certPassword = New-Secret
$certPath = Join-Path $secretPath 'protection.pfx'
$passwordPath = Join-Path $secretPath 'protection-password'
$backupPasswordPath = Join-Path $secretPath 'backup-password'
if ((Test-Path -LiteralPath $certPath) -or (Test-Path -LiteralPath $passwordPath)) { throw 'Certificate files already exist. Restore .env from your secure backup.' }
$rsa = [System.Security.Cryptography.RSA]::Create(4096)
try {
    $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=SecureShare Key Protection', $rsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $cert = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddYears(5))
    [IO.File]::WriteAllBytes($certPath, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $certPassword))
    [IO.File]::WriteAllText($passwordPath, $certPassword)
    [IO.File]::WriteAllText($backupPasswordPath, (New-Secret))
    $cert.Dispose()
} finally { $rsa.Dispose() }
[IO.File]::WriteAllText($envPath, "MSSQL_SA_PASSWORD=$sql" + [Environment]::NewLine +
    "GRAFANA_ADMIN_PASSWORD=$grafana" + [Environment]::NewLine + "DOMAIN=$Domain" + [Environment]::NewLine)
Write-Output 'Created .env and key-protection certificate. Keep .env, secrets/, and backups/ private.'
