<#
  Recorre una lista de pantallas y saca una captura de cada una.

  Cada linea del archivo es "nombre | accion", donde accion es
    cmd:<comando de consola>   -> se verifica contra el log antes de capturar
    keys:<acciones>            -> se manda tal cual a Send-Actions
  Las lineas que empiezan con # se ignoran.
#>
param([string]$ListFile, [int]$Wait = 9000, [switch]$NoEnter)
. "$PSScriptRoot\uwtest.ps1"

if (-not (Focus-Game)) { Write-Host "ERROR: sin foco" -ForegroundColor Red; exit 1 }
if (-not $NoEnter) {
    if (-not (Enter-Run)) { Write-Host "ERROR: no entro a la run" -ForegroundColor Red; exit 1 }
}

$rows = Get-Content $ListFile | Where-Object { $_.Trim() -and -not $_.Trim().StartsWith('#') }
foreach ($row in $rows) {
    $parts = $row -split '\|', 2
    $name = $parts[0].Trim(); $action = $parts[1].Trim()
    $out = "C:\web\sts2-ultrawide\shots\sc_$name.png"

    if ($action.StartsWith('cmd:')) {
        if (-not (Invoke-Console -Command $action.Substring(4).Trim() -Wait $Wait)) {
            Write-Host ("{0,-24} COMANDO NO ENTRO" -f $name) -ForegroundColor Red; continue
        }
    } else {
        Focus-Game | Out-Null
        Send-Actions $action.Substring(5).Trim()
    }

    if (-not (Capture-Game -Out $out -Shrink 3)) {
        Write-Host ("{0,-24} CAPTURA INVALIDA" -f $name) -ForegroundColor Red; continue
    }
    Write-Host ("{0,-24} ok" -f $name) -ForegroundColor Green
}
