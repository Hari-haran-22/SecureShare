param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Profile,
    [Parameter(Mandatory)][ValidatePattern('^[0-9]{12}$')][string]$AccountId,
    [ValidatePattern('^[a-z]{2}(?:-[a-z]+)+-[0-9]+$')][string]$Region = 'us-east-1'
)
$ErrorActionPreference = 'Stop'
Get-Command aws -ErrorAction Stop | Out-Null
$root = Split-Path -Parent $PSScriptRoot
$terraformRoot = Join-Path $root 'teraform'
$selectionPath = Join-Path $terraformRoot 'account.auto.tfvars.json'
$statePath = Join-Path $terraformRoot 'terraform.tfstate'
$workspacePath = Join-Path $terraformRoot '.terraform/environment'
if ((Test-Path -LiteralPath $workspacePath) -and
    ([IO.File]::ReadAllText($workspacePath).Trim() -ne 'default')) {
    throw 'Select the default Terraform workspace before switching, or use a separately configured state directory.'
}
$actualAccount = & aws sts get-caller-identity --profile $Profile --query Account --output text --no-cli-pager --cli-connect-timeout 10 --cli-read-timeout 15
if ($LASTEXITCODE -ne 0) { throw "AWS authentication failed. Log in to the '$Profile' profile locally first." }
if (($actualAccount | Out-String).Trim() -ne $AccountId) {
    throw 'The selected AWS profile belongs to a different account. Project settings were not changed.'
}
$previous = $null
if (Test-Path -LiteralPath $selectionPath) {
    $previous = Get-Content -LiteralPath $selectionPath -Raw | ConvertFrom-Json
}
if (Test-Path -LiteralPath $statePath) {
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if (@($state.resources).Count -gt 0 -and
        ($null -eq $previous -or $previous.expected_account_id -ne $AccountId)) {
        throw 'Existing Terraform state contains resources from an unverified account. Preserve it and configure separate state before switching accounts.'
    }
}
$selection = [ordered]@{
    aws_profile = $Profile
    expected_account_id = $AccountId
    region = $Region
}
[IO.File]::WriteAllText($selectionPath, ($selection | ConvertTo-Json) + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))
Write-Output "SecureShare now selects AWS profile '$Profile', account $AccountId, region $Region."
Write-Output 'No AWS resources were created. Run Terraform plan to review the new infrastructure.'
