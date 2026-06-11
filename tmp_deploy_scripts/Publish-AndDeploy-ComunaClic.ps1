# Publica y despliega servicios ComunaClic al EC2 (Windows / Git Bash).
# Ejemplo (solo app / frontend):
#   $env:SSH_KEY = "...\tmp_deploy_scripts\ubuntu-doc-alfacloud.pem"
#   .\tmp_deploy_scripts\Publish-AndDeploy-ComunaClic.ps1 -Targets app

param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet("api", "acl", "app", "admin")]
    [string[]] $Targets,

    [string] $HostName = "3.92.248.0",
    [string] $SshUser = "ubuntu",
    [string] $SshKey = $env:SSH_KEY
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($SshKey)) {
    throw "Define la llave SSH: `$env:SSH_KEY = 'C:\ruta\llave.pem' o pasa -SshKey"
}

$bash = Get-Command bash -ErrorAction SilentlyContinue
if (-not $bash) {
    $gitBash = "${env:ProgramFiles}\Git\bin\bash.exe"
    if (Test-Path $gitBash) { $bash = @{ Source = $gitBash } }
}
if (-not $bash) {
    throw "Instala Git Bash para ejecutar los scripts de deploy (bash no encontrado en PATH)."
}
$bashExe = if ($bash.Source) { $bash.Source } else { $bash.Path }

$env:HOST = $HostName
$env:SSH_USER = $SshUser
$env:SSH_KEY = $SshKey
$env:BASE_REPO = $RepoRoot

$targetArgs = $Targets -join " "
& $bashExe -lc "cd '$RepoRoot' && bash tmp_deploy_scripts/publish_and_deploy_comunaclic.sh $targetArgs"

Write-Host ""
Write-Host "Listo. Abre https://app.comunaclic.cl y fuerza recarga (Ctrl+F5)." -ForegroundColor Green
