# Carga datos demo en PostgreSQL (esquema acl).
# - Si existe psql (PATH o instalación típica en Windows), lo usa.
# - Si no, usa la herramienta .NET tools/RunSqlSeeds (Npgsql), sin necesidad de psql.
#
# Uso (desde la raíz del repo):
#   .\infra\seeds\Apply-AclSeeds.ps1
#   .\infra\seeds\Apply-AclSeeds.ps1 -Password "tu_clave"
#   .\infra\seeds\Apply-AclSeeds.ps1 -SkipExtraUsers

param(
    [string] $PgHost = "localhost",
    [int] $Port = 5432,
    [string] $User = "postgres",
    [string] $Database = "comunaclick_db",
    [string] $Password = "admin123",
    [switch] $SkipExtraUsers
)

$ErrorActionPreference = "Stop"

function Get-PsqlExecutable {
    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $bases = @(
        ${env:ProgramFiles},
        ${env:ProgramFiles(x86)}
    ) | Where-Object { $_ -and (Test-Path $_) }

    foreach ($base in $bases) {
        $pgRoot = Join-Path $base "PostgreSQL"
        if (-not (Test-Path $pgRoot)) { continue }

        $candidates = Get-ChildItem -Path $pgRoot -Directory -ErrorAction SilentlyContinue |
            ForEach-Object { Join-Path $_.FullName "bin\psql.exe" } |
            Where-Object { Test-Path $_ }

        if ($candidates) {
            return ($candidates | Sort-Object -Descending | Select-Object -First 1)
        }
    }

    return $null
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$seedDir = Join-Path $repoRoot "infra\seeds"
$psqlExe = Get-PsqlExecutable

if ($psqlExe) {
    Write-Host "Usando psql: $psqlExe"
    $env:PGPASSWORD = $Password

    Write-Host "Aplicando acl_seed.sql ..."
    & $psqlExe -h $PgHost -p $Port -U $User -d $Database -v ON_ERROR_STOP=1 -f (Join-Path $seedDir "acl_seed.sql")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (-not $SkipExtraUsers) {
        Write-Host "Aplicando acl_seed_test_users_all_roles.sql ..."
        & $psqlExe -h $PgHost -p $Port -U $User -d $Database -v ON_ERROR_STOP=1 -f (Join-Path $seedDir "acl_seed_test_users_all_roles.sql")
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}
else {
    Write-Host "No se encontró psql. Usando herramienta .NET (Npgsql) en tools\RunSqlSeeds ..."
    $conn = "Host=$PgHost;Port=$Port;Database=$Database;Username=$User;Password=$Password"
    $env:COMUNACLICK_DB = $conn
    try {
        $proj = Join-Path $repoRoot "tools\RunSqlSeeds\RunSqlSeeds.csproj"
        $f1 = Join-Path $seedDir "acl_seed.sql"
        if ($SkipExtraUsers) {
            dotnet run --project $proj -c Release -- $f1
        }
        else {
            $f2 = Join-Path $seedDir "acl_seed_test_users_all_roles.sql"
            dotnet run --project $proj -c Release -- $f1 $f2
        }
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    finally {
        Remove-Item Env:COMUNACLICK_DB -ErrorAction SilentlyContinue
    }
}

Write-Host "Listo. Usuarios demo: ver README. Contraseña común: test123"
