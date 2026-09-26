[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$NgrokPath,

    [int]$Port = 8081,

    [string]$LogPath = (Join-Path $PSScriptRoot '..\logs\ngrok.log')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $NgrokPath -PathType Leaf)) {
    throw "ngrok was not found at '$NgrokPath'."
}

$existingTunnel = Get-CimInstance Win32_Process -Filter "Name = 'ngrok.exe'" |
    Where-Object { $_.CommandLine -match "\bhttp\s+$Port\b" } |
    Select-Object -First 1

if ($existingTunnel) {
    return
}

$logDirectory = Split-Path -Parent $LogPath
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

$ngrokArguments = @(
    'http'
    $Port.ToString()
    "--log=$LogPath"
    '--log-format=json'
)

Start-Process -FilePath $NgrokPath -ArgumentList $ngrokArguments -WindowStyle Hidden
