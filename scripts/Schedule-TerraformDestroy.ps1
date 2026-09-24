[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [datetime]$At,
    [string]$TaskName = 'SecureShare-Terraform-Destroy'
)

$ErrorActionPreference = 'Stop'
if ($At -le (Get-Date).AddMinutes(2)) { throw 'The destroy time must be at least two minutes in the future.' }
$destroyScript = Join-Path $PSScriptRoot 'Destroy-AwsEnvironment.ps1'
$pwsh = (Get-Command pwsh -ErrorAction Stop).Source
$action = New-ScheduledTaskAction -Execute $pwsh -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$destroyScript`""
$trigger = New-ScheduledTaskTrigger -Once -At $At
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -WakeToRun -ExecutionTimeLimit (New-TimeSpan -Hours 1)
$principal = New-ScheduledTaskPrincipal -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
Write-Output "Terraform destroy scheduled for $($At.ToString('yyyy-MM-dd HH:mm:ss zzz')) in task '$TaskName'."
