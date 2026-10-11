param(
    [string]$Dotnet = 'dotnet',
    [string]$Output = (Join-Path $PSScriptRoot 'artifacts\unsigned'),
    [string]$TestOutput = (Join-Path $PSScriptRoot 'artifacts\test-results')
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$outputDirectory = [IO.Path]::GetFullPath($Output)
$testDirectory = [IO.Path]::GetFullPath($TestOutput)
New-Item -ItemType Directory -Path $outputDirectory, $testDirectory -Force | Out-Null
Push-Location $PSScriptRoot
try {
    & $Dotnet run --project 'tests\ChilePrimeRepair.Tests' --configuration Release 2>&1 |
        Tee-Object -FilePath (Join-Path $testDirectory 'core-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas del núcleo. No se publicó la aplicación.' }
    & $Dotnet publish 'src\ChilePrimeRepair.App' --configuration Release --runtime win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:ContinuousIntegrationBuild=true `
        -p:DebugType=None -p:DebugSymbols=false --output $outputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación.' }
    foreach ($document in @('README.md', 'LICENSE', 'PRIVACY.md', 'CODE_SIGNING.md', 'UNINSTALL.md', 'THIRD_PARTY_NOTICES.md')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $document) -Destination $outputDirectory -Force
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $outputDirectory -Recurse -Force
    $metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $outputDirectory 'ChilePrimeRepair.exe'))
    $sdkVersion = & $Dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo identificar el SDK de la compilación.' }
    [ordered]@{
        product = $metadata.ProductName
        version = $metadata.ProductVersion
        sdk = $sdkVersion.Trim()
        commit = $env:GITHUB_SHA
        repository = $env:GITHUB_REPOSITORY
        workflowRun = $env:GITHUB_RUN_ID
        signingStatus = 'unsigned'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDirectory 'BUILD-INFO.json') -Encoding UTF8
    & (Join-Path $PSScriptRoot 'scripts\test-ui.ps1') -Executable (Join-Path $outputDirectory 'ChilePrimeRepair.exe') -Output $testDirectory
    Get-FileHash -LiteralPath (Join-Path $outputDirectory 'ChilePrimeRepair.exe') -Algorithm SHA256 |
        ForEach-Object { $_.Hash + '  ChilePrimeRepair.exe' } |
        Set-Content -LiteralPath (Join-Path $outputDirectory 'UNSIGNED-SHA256SUMS.txt') -Encoding UTF8
    Write-Output ('Pruebas completadas. Artefacto SIN FIRMA: ' + $outputDirectory)
}
finally { Pop-Location }
