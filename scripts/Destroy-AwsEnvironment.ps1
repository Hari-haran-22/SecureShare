[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$terraformDir = Join-Path $repoRoot 'teraform'
$terraform = Get-Command terraform -ErrorAction SilentlyContinue
if (-not $terraform) {
    $bundled = Join-Path $terraformDir 'terraform.exe'
    if (-not (Test-Path $bundled)) { throw 'Terraform is not installed or bundled with this checkout.' }
    $terraformPath = $bundled
} else {
    $terraformPath = $terraform.Source
}

$logDir = Join-Path $repoRoot 'logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir 'terraform-destroy.log'
"[$(Get-Date -Format o)] Starting scheduled Terraform destroy." | Tee-Object -FilePath $log -Append
& $terraformPath "-chdir=$terraformDir" init -input=false 2>&1 | Tee-Object -FilePath $log -Append
if ($LASTEXITCODE -ne 0) { throw 'terraform init failed.' }
& $terraformPath "-chdir=$terraformDir" destroy -auto-approve -input=false 2>&1 | Tee-Object -FilePath $log -Append
if ($LASTEXITCODE -ne 0) { throw 'terraform destroy failed.' }
$remaining = @(& $terraformPath "-chdir=$terraformDir" state list 2>&1)
if ($LASTEXITCODE -ne 0) { throw 'terraform state list failed after destroy.' }
if ($remaining.Count -gt 0) {
    $remaining | Tee-Object -FilePath $log -Append
    throw 'Terraform destroy completed but tracked resources remain in state.'
}
$targetFile = 'C:\ProgramData\Jenkins\.jenkins\secureshare-deployment.env'
if (Test-Path $targetFile) {
    Remove-Item -LiteralPath $targetFile -Force
    "[$(Get-Date -Format o)] Removed the obsolete Jenkins deployment target file." | Tee-Object -FilePath $log -Append
}
"[$(Get-Date -Format o)] Verified that Terraform state contains no managed resources." | Tee-Object -FilePath $log -Append
"[$(Get-Date -Format o)] Terraform destroy completed." | Tee-Object -FilePath $log -Append
