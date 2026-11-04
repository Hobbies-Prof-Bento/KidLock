<#
.SYNOPSIS
    Build, testes, publicação e instalador do KidLock.
.EXAMPLE
    .\build.ps1                         # build + testes unitários + instalador
.EXAMPLE
    .\build.ps1 -Target IntegrationTest # testes com ganchos reais (precisa de desktop interativo)
.EXAMPLE
    .\build.ps1 -Target Installer -Version 1.2.0
.EXAMPLE
    .\build.ps1 -Target Publish -PublishProfile win-arm64
.NOTES
    Instalador (artifacts\installer\KidLock-Setup-<versão>.exe): leva o runtime .NET junto, roda em
    qualquer Windows 10/11 e exige o Inno Setup 6 só na máquina que gera o instalador
    (winget install JRSoftware.InnoSetup).

    Versões portáteis (src/KidLock.App/Properties/PublishProfiles), um único KidLock.exe:
      win-x64       .NET embutido (~70 MB)
      win-arm64     idem, para notebooks ARM
      win-x64-lite  pequeno; exige o ".NET Desktop Runtime 8" instalado
#>
[CmdletBinding()]
param(
    [ValidateSet('Build', 'Test', 'IntegrationTest', 'Publish', 'Installer', 'All')]
    [string]$Target = 'All',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64', 'win-x64-lite')]
    [string]$PublishProfile = 'win-x64',

    # Padrão: <Version> do Directory.Build.props
    [string]$Version,

    # Endereço do repositório, exibido em "Aplicativos instalados" do Windows
    [string]$RepoUrl
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$solution = Join-Path $root 'KidLock.sln'
$app = Join-Path $root 'src\KidLock.App\KidLock.App.csproj'
$results = Join-Path $root 'artifacts\test-results'

if (-not $Version) {
    $props = [xml](Get-Content (Join-Path $root 'Directory.Build.props'))
    $Version = @($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
}
$versionArgs = @("-p:Version=$Version")

function Invoke-Dotnet {
    Write-Host "> dotnet $($args -join ' ')" -ForegroundColor Cyan
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args[0]) falhou (codigo $LASTEXITCODE)." }
}

function Invoke-Build {
    Invoke-Dotnet build $solution -c $Configuration -nologo @versionArgs
}

function Invoke-Tests([string]$filter, [string]$name) {
    Invoke-Dotnet test $solution -c $Configuration --no-build -nologo `
        --filter $filter `
        --logger "trx;LogFilePrefix=$name" `
        --results-directory $results `
        --collect 'XPlat Code Coverage'
}

function Invoke-Publish([string]$profileName) {
    Invoke-Dotnet publish $app -nologo "-p:PublishProfile=$profileName" @versionArgs
    $exe = Join-Path $root "artifacts\publish\$profileName\KidLock.exe"
    $sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "Publicado: $exe ($sizeMb MB)" -ForegroundColor Green
}

function Find-InnoCompiler {
    $candidates = @(
        $env:INNO_SETUP_COMPILER,
        (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source,
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $found = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $found) {
        throw 'Inno Setup 6 nao encontrado. Instale com: winget install JRSoftware.InnoSetup'
    }
    return $found
}

function Invoke-Installer {
    $iscc = Find-InnoCompiler
    $payload = Join-Path $root 'artifacts\publish\installer-win-x86'
    if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
    Invoke-Publish 'installer-win-x86'

    # O Windows só aceita versão numérica nos metadados do .exe (ex.: 1.2.0, sem "-ci.5").
    $numericVersion = ($Version -split '[-+]')[0]
    $defines = @("/DAppVersion=$Version", "/DNumericVersion=$numericVersion")
    if ($RepoUrl) { $defines += "/DRepoUrl=$RepoUrl" }

    Write-Host "> ISCC $($defines -join ' ')" -ForegroundColor Cyan
    & $iscc /Q @defines (Join-Path $root 'installer\KidLock.iss')
    if ($LASTEXITCODE -ne 0) { throw "ISCC falhou (codigo $LASTEXITCODE)." }

    $setup = Join-Path $root "artifacts\installer\KidLock-Setup-$Version.exe"
    $sizeMb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
    Write-Host "Instalador: $setup ($sizeMb MB)" -ForegroundColor Green
}

switch ($Target) {
    'Build' { Invoke-Build }
    'Test' { Invoke-Build; Invoke-Tests 'Category!=Integration' 'unit' }
    'IntegrationTest' { Invoke-Build; Invoke-Tests 'Category=Integration' 'integration' }
    'Publish' { Invoke-Publish $PublishProfile }
    'Installer' { Invoke-Installer }
    'All' { Invoke-Build; Invoke-Tests 'Category!=Integration' 'unit'; Invoke-Installer }
}
