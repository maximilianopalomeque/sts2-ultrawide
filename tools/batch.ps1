<#
  Barrido automatico: ejecuta cada comando de consola, captura y verifica que el canvas
  quede cubierto. Solo reporta lo que falla.
#>
param([string]$ListFile, [int]$Wait = 12000)

$toggle = [char]42
$rows = Get-Content $ListFile | Where-Object { $_.Trim() -and -not $_.StartsWith('#') }
$results = @()

foreach ($row in $rows) {
    $parts = $row -split '\|', 2
    $name = $parts[0].Trim()
    $cmd  = $parts[1].Trim()
    $out  = "C:\web\sts2-ultrawide\shots\sweep_$name.png"

    $script = "key $toggle; wait 1100; key ^a; wait 150; key {DEL}; wait 150; key $cmd; wait 800; key {ENTER}; wait $Wait; key $toggle; wait 2200"
    powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\act.ps1" -Out $out -Script $script -Shrink 3 | Out-Null

    $verdict = python "$PSScriptRoot\verify.py" $out
    $results += [pscustomobject]@{ Scene = $name; Verdict = $verdict }
    Write-Host ("{0,-26} {1}" -f $name, ($verdict -replace '^\s*(OK|AVISO)\s+\S+\s+',''))
}

Write-Host ""
Write-Host "=== resumen ===" -ForegroundColor Cyan
$bad = $results | Where-Object { $_.Verdict -notmatch '  OK ' }
if ($bad) { $bad | ForEach-Object { Write-Host ("  REVISAR  " + $_.Scene) -ForegroundColor Yellow } }
else { Write-Host "  todas las escenas cubren el canvas" -ForegroundColor Green }
