[CmdletBinding()]
param(
    [timespan]$MaxLifetime = ([timespan]::FromHours(4))
)

$ErrorActionPreference = 'Stop'
if ($MaxLifetime -lt [timespan]::FromMinutes(15) -or $MaxLifetime -gt [timespan]::FromHours(5)) {
    throw 'MaxLifetime must be between 15 minutes and 5 hours.'
}

# Schedule cleanup first so a partial apply is also removed.
$destroyAt = (Get-Date).Add($MaxLifetime)
& (Join-Path $PSScriptRoot 'Schedule-TerraformDestroy.ps1') -At $destroyAt

$repoRoot = Split-Path -Parent $PSScriptRoot
$terraformDir = Join-Path $repoRoot 'teraform'
$terraform = Get-Command terraform -ErrorAction SilentlyContinue
$terraformPath = if ($terraform) { $terraform.Source } else { Join-Path $terraformDir 'terraform.exe' }
if (-not (Test-Path $terraformPath)) { throw 'Terraform is not installed or bundled with this checkout.' }

& $terraformPath "-chdir=$terraformDir" init -input=false
if ($LASTEXITCODE -ne 0) { throw 'terraform init failed.' }
& $terraformPath "-chdir=$terraformDir" apply -auto-approve -input=false
if ($LASTEXITCODE -ne 0) { throw 'terraform apply failed; the scheduled destroy remains active for cleanup.' }

$outputs = & $terraformPath "-chdir=$terraformDir" output -json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'terraform output failed after apply.' }
$jenkinsHome = 'C:\ProgramData\Jenkins\.jenkins'
if (Test-Path $jenkinsHome) {
    $targetFile = Join-Path $jenkinsHome 'secureshare-deployment.env'
    @(
        "DEPLOY_HOST=$($outputs.server_public_ip.value)"
        "APP_PRIVATE_IP=$($outputs.server_private_ip.value)"
        "SERVICES_HOST=$($outputs.services_public_ip.value)"
        "SCANNER_PRIVATE_IP=$($outputs.services_private_ip.value)"
    ) | Set-Content -Path $targetFile -Encoding ascii
    Write-Output "Saved the current Terraform deployment addresses for Jenkins at $targetFile."
}

Write-Output "Environment is ready and will be destroyed no later than $($destroyAt.ToString('yyyy-MM-dd HH:mm:ss zzz'))."
& $terraformPath "-chdir=$terraformDir" output
