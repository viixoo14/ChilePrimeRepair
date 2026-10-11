param([Parameter(Mandatory)][string]$Directory)
$ErrorActionPreference = 'Stop'
$releaseDirectory = (Resolve-Path -LiteralPath $Directory).Path
$executable = Join-Path $releaseDirectory 'ChilePrimeRepair.exe'
$signature = Get-AuthenticodeSignature -LiteralPath $executable
if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate) {
    throw ('El ejecutable no tiene una firma Authenticode válida: ' + $signature.Status)
}
if ($signature.SignerCertificate.Subject -notmatch '(^|,\s*)(CN|O)=SignPath Foundation(,|$)') {
    throw 'El titular de la firma no es SignPath Foundation. Verifica el certificado aprobado para el proyecto.'
}
if ($null -eq $signature.TimeStamperCertificate) { throw 'La firma no contiene un sello de tiempo verificable.' }
[xml]$properties = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Directory.Build.props') -Raw
$version = [string]$properties.Project.PropertyGroup.Version
$metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
if ($metadata.ProductName -ne 'Chile Prime Repair' -or $metadata.ProductVersion -ne $version -or
    $metadata.FileVersion -ne ($version + '.0') -or $metadata.CompanyName -ne 'Chile Prime') {
    throw 'Los metadatos del archivo firmado no corresponden al producto y versión del código.'
}
& (Join-Path $PSScriptRoot 'test-ui.ps1') -Executable $executable -Output (Join-Path $PSScriptRoot '..\artifacts\signed-test-results')
$buildInfo = Join-Path $releaseDirectory 'BUILD-INFO.json'
if (-not (Test-Path -LiteralPath $buildInfo)) { throw 'Falta la procedencia de la compilación.' }
$information = Get-Content -LiteralPath $buildInfo -Raw | ConvertFrom-Json
$information.signingStatus = 'signed'
$information | ConvertTo-Json | Set-Content -LiteralPath $buildInfo -Encoding UTF8
$oldChecksum = Join-Path $releaseDirectory 'UNSIGNED-SHA256SUMS.txt'
if (Test-Path -LiteralPath $oldChecksum) { Remove-Item -LiteralPath $oldChecksum }
Get-FileHash -LiteralPath $executable -Algorithm SHA256 |
    ForEach-Object { $_.Hash + '  ChilePrimeRepair.exe' } |
    Set-Content -LiteralPath (Join-Path $releaseDirectory 'SHA256SUMS.txt') -Encoding UTF8
Write-Output ('Firma válida y prueba del binario firmado completada: ' + $metadata.ProductVersion)
