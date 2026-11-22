param([string]$Name, [string]$Cmd, [int]$Wait = 11000)
$toggle = [char]42   # la tecla que abre/cierra la consola del juego
$script = "key $toggle; wait 1200; key ^a; wait 200; key {DEL}; wait 200; key $Cmd; wait 900; key {ENTER}; wait $Wait; key $toggle; wait 2500"
powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\act.ps1" -Out "C:\web\sts2-ultrawide\shots\$Name.png" -Script $script -Shrink 3 | Out-Null
python "$PSScriptRoot\bars.py" "C:\web\sts2-ultrawide\shots\$Name.png"
