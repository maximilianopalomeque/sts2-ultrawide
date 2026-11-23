param([string]$ListFile, [int]$Wait = 13000)
. "$PSScriptRoot\uwtest.ps1"

if (-not (Focus-Game)) { Write-Host "ERROR: no pude enfocar el juego" -ForegroundColor Red; exit 1 }

$rows = Get-Content $ListFile | Where-Object { $_.Trim() -and -not $_.StartsWith('#') }
$bad = @()
foreach ($row in $rows) {
    $parts = $row -split '\|', 2
    $name = $parts[0].Trim(); $cmd = $parts[1].Trim()
    $out = "C:\web\sts2-ultrawide\shots\sw_$name.png"

    if (-not (Focus-Game)) { Write-Host ("{0,-20} SIN FOCO - descartado" -f $name) -ForegroundColor Red; $bad += $name; continue }
    if (-not (Invoke-Console -Command $cmd -Wait $Wait)) {
        Write-Host ("{0,-20} EL COMANDO NO ENTRO - descartado" -f $name) -ForegroundColor Red
        $bad += $name; continue
    }
    if (-not (Capture-Game -Out $out -Shrink 3)) {
        Write-Host ("{0,-20} CAPTURA INVALIDA (el juego no estaba al frente)" -f $name) -ForegroundColor Red
        $bad += $name; continue
    }
    $v = (python "$PSScriptRoot\verify.py" $out) -join ''
    $ok = $v -match '  OK '
    $msg = ($v -replace '^\s*(OK|AVISO)\s+\S+\s+','')
    Write-Host ("{0,-20} {1}" -f $name, $msg) -ForegroundColor $(if ($ok) {'Green'} else {'Yellow'})
    if (-not $ok) { $bad += $name }
}
Write-Host ""
if ($bad) { Write-Host ("A REVISAR: " + ($bad -join ', ')) -ForegroundColor Yellow }
else { Write-Host "todas cubren el canvas" -ForegroundColor Green }
