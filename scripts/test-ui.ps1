param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
$executablePath = (Resolve-Path -LiteralPath $Executable).Path
$runDirectory = Join-Path ([IO.Path]::GetFullPath($Output)) ('ui-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$process = Start-Process -FilePath $executablePath -ArgumentList @('--smoke-test', ('"' + $runDirectory + '"')) `
    -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(120000)) {
        $process.Kill()
        throw 'La prueba del asistente superó su tiempo máximo.'
    }
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw ('Falló la prueba del asistente. Revisa: ' + $runDirectory) }
    $report = Join-Path $runDirectory 'ui-smoke-result.txt'
    if (-not (Test-Path -LiteralPath $report) -or -not (Get-Content -LiteralPath $report -Raw).StartsWith('PASS:')) {
        throw 'La aplicación no generó un resultado aprobado de la prueba del asistente.'
    }
    Get-Content -LiteralPath $report
}
finally { $process.Dispose() }
