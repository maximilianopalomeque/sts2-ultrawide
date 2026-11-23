param([string[]]$Events, [int]$Wait = 9000)
. "$PSScriptRoot\uwtest.ps1"

if (-not (Focus-Game)) { Write-Host "ERROR: sin foco" -ForegroundColor Red; exit 1 }
if (-not (Enter-Run))  { Write-Host "ERROR: no entro a la run" -ForegroundColor Red; exit 1 }

foreach ($e in $Events) {
    $out = "C:\web\sts2-ultrawide\shots\ev_$e.png"
    if (-not (Invoke-Console -Command "event $e" -Wait $Wait)) {
        Write-Host ("{0,-28} COMANDO NO ENTRO" -f $e) -ForegroundColor Red; continue
    }
    if (-not (Capture-Game -Out $out -Shrink 3)) {
        Write-Host ("{0,-28} CAPTURA INVALIDA" -f $e) -ForegroundColor Red; continue
    }
    Write-Host ("{0,-28} capturado" -f $e) -ForegroundColor Green
}
