<#
  Arnes de testeo en un solo proceso. Lanzar PowerShell hijos roba el foco y termina
  capturando la consola en vez del juego, asi que todo vive aca adentro.
#>
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public class UW {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int c);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,IntPtr p);
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a,uint b,bool f);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint dx,uint dy,int d,int e);
}
"@

function Get-GameWindow {
    $p = Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue
    if (-not $p) { return [IntPtr]::Zero }
    return $p.MainWindowHandle
}

function Focus-Game {
    param([int]$Tries = 4)
    $h = Get-GameWindow
    if ($h -eq [IntPtr]::Zero) { return $false }
    for ($i = 0; $i -lt $Tries; $i++) {
        $fg  = [UW]::GetForegroundWindow()
        if ($fg -eq $h) { return $true }
        $tid = [UW]::GetWindowThreadProcessId($fg, [IntPtr]::Zero)
        $cur = [UW]::GetCurrentThreadId()
        [UW]::AttachThreadInput($cur, $tid, $true) | Out-Null
        [UW]::ShowWindow($h, 9) | Out-Null
        [UW]::BringWindowToTop($h) | Out-Null
        [UW]::SetForegroundWindow($h) | Out-Null
        [UW]::AttachThreadInput($cur, $tid, $false) | Out-Null
        Start-Sleep -Milliseconds 500
    }
    return ([UW]::GetForegroundWindow() -eq $h)
}

function Send-Actions {
    param([string]$Script)
    foreach ($line in ($Script -split ';')) {
        $t = $line.Trim(); if (-not $t) { continue }
        $a = $t -split '\s+', 2
        switch ($a[0]) {
            'click' { $xy = $a[1] -split '\s+'
                      [void][UW]::SetCursorPos([int]$xy[0], [int]$xy[1]); Start-Sleep -Milliseconds 250
                      [UW]::mouse_event(0x0002,0,0,0,0); Start-Sleep -Milliseconds 60
                      [UW]::mouse_event(0x0004,0,0,0,0) }
            'key'   { [System.Windows.Forms.SendKeys]::SendWait($a[1]); Start-Sleep -Milliseconds 250 }
            'wheel' { [UW]::mouse_event(0x0800,0,0,[int]$a[1],0); Start-Sleep -Milliseconds 150 }
            'hwheel'{ [UW]::mouse_event(0x1000,0,0,[int]$a[1],0); Start-Sleep -Milliseconds 150 }
            'move'  { $xy = $a[1] -split '\s+'
                      [void][UW]::SetCursorPos([int]$xy[0], [int]$xy[1]); Start-Sleep -Milliseconds 150 }
            'drag'  { $xy = $a[1] -split '\s+'
                      [void][UW]::SetCursorPos([int]$xy[0], [int]$xy[1]); Start-Sleep -Milliseconds 250
                      [UW]::mouse_event(0x0002,0,0,0,0); Start-Sleep -Milliseconds 150
                      $steps = 20
                      for ($k = 1; $k -le $steps; $k++) {
                          $ix = [int]$xy[0] + ([int]$xy[2] - [int]$xy[0]) * $k / $steps
                          $iy = [int]$xy[1] + ([int]$xy[3] - [int]$xy[1]) * $k / $steps
                          [void][UW]::SetCursorPos([int]$ix, [int]$iy); Start-Sleep -Milliseconds 25
                      }
                      Start-Sleep -Milliseconds 150
                      [UW]::mouse_event(0x0004,0,0,0,0) }
            'wait'  { Start-Sleep -Milliseconds ([int]$a[1]) }
        }
    }
}

function Capture-Game {
    param([string]$Out, [int]$Shrink = 3)
    # Re-afirmar el foco JUSTO antes de capturar: cualquier cosa pudo robarlo durante las
    # acciones, y una captura de otra ventana pasa cualquier verificacion por accidente.
    if (-not (Focus-Game)) { return $false }
    Start-Sleep -Milliseconds 400
    if ([UW]::GetForegroundWindow() -ne (Get-GameWindow)) { return $false }

    $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size); $g.Dispose()
    if ($Shrink -gt 1) {
        $w = [int]($b.Width/$Shrink); $h = [int]($b.Height/$Shrink)
        $s = New-Object System.Drawing.Bitmap($w, $h)
        $g2 = [System.Drawing.Graphics]::FromImage($s)
        $g2.InterpolationMode = 'HighQualityBicubic'
        $g2.DrawImage($bmp, 0, 0, $w, $h); $g2.Dispose(); $bmp.Dispose(); $bmp = $s
    }
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    return $true
}

function Invoke-Console {
    <#
      Manda un comando a la consola del juego y CONFIRMA contra el log que se ejecuto.

      Si el toggle de consola no prende, el texto se va como pulsaciones sueltas al juego
      y cada letra dispara su hotkey: la A abre la pila de robo, la M el mapa. Asumir que
      el comando entro deja el juego en cualquier estado y las capturas no valen nada.
    #>
    param([string]$Command, [int]$Wait = 12000, [int]$Tries = 3)

    $log = "C:/Users/maxim/AppData/Roaming/SlayTheSpire2/logs/godot.log"
    $toggle = [char]42

    for ($i = 1; $i -le $Tries; $i++) {
        $before = if (Test-Path $log) { (Select-String -Path $log -Pattern "DevConsole:" -ErrorAction SilentlyContinue).Count } else { 0 }

        Focus-Game | Out-Null
        # Vaciar el buffer con retrocesos: Ctrl+A no selecciona todo en el LineEdit de Godot.
        Send-Actions "key $toggle; wait 900; key {BS 90}; wait 200"
        Send-Actions "key $Command; wait 700; key {ENTER}; wait 600"

        $after = (Select-String -Path $log -Pattern "DevConsole:" -ErrorAction SilentlyContinue).Count
        if ($after -gt $before) {
            $last = (Select-String -Path $log -Pattern "DevConsole:" | Select-Object -Last 1).Line
            if ($last -match [regex]::Escape($Command)) {
                Send-Actions "wait $Wait; key $toggle; wait 2200"
                return $true
            }
            Write-Host ("    intento {0}: la consola recibio '{1}'" -f $i, ($last -replace '.*DevConsole: ','')) -ForegroundColor DarkYellow
        } else {
            Write-Host ("    intento {0}: la consola no registro nada" -f $i) -ForegroundColor DarkYellow
        }
        Send-Actions "key $toggle; wait 600"
    }
    return $false
}

function Enter-Run {
    <#
      Entra a la partida guardada y CONFIRMA que cargo.

      Un solo click en Continue no siempre prende, y si falla todo lo que sigue corre
      contra el menu principal: los comandos de consola no hacen nada y las capturas
      muestran el menu.

      El resultado de un comando de consola sale por la consola del juego y no por el
      log, asi que no sirve para verificar. Lo que si queda escrito es el pase de
      nitidez del mod, que se instala al cargar la run y en ningun otro momento.
    #>
    param([int]$Tries = 4)

    $log = "C:/Users/maxim/AppData/Roaming/SlayTheSpire2/logs/godot.log"
    $marker = "Sharpening pass enabled"

    for ($i = 1; $i -le $Tries; $i++) {
        $before = (Select-String -Path $log -Pattern $marker -ErrorAction SilentlyContinue).Count

        Focus-Game | Out-Null
        Send-Actions "click 2312 912; wait 1500; click 2312 912; wait 8000"

        # El log se vacia a disco con retraso: preguntar una sola vez da falsos negativos.
        for ($t = 0; $t -lt 24; $t++) {
            $after = (Select-String -Path $log -Pattern $marker -ErrorAction SilentlyContinue).Count
            if ($after -gt $before) { return $true }
            Start-Sleep -Milliseconds 500
        }

        Write-Host ("    intento {0}: no entro a la run" -f $i) -ForegroundColor DarkYellow
        Send-Actions "key {ESC}; wait 1500"
    }
    return $false
}

function Start-Drag {
    <# Aprieta el boton en (x1,y1) y arrastra hasta (x2,y2) SIN soltar. #>
    param([int]$X1, [int]$Y1, [int]$X2, [int]$Y2)
    [void][UW]::SetCursorPos($X1, $Y1); Start-Sleep -Milliseconds 300
    [UW]::mouse_event(0x0002, 0, 0, 0, 0); Start-Sleep -Milliseconds 200
    for ($k = 1; $k -le 25; $k++) {
        $ix = $X1 + ($X2 - $X1) * $k / 25
        $iy = $Y1 + ($Y2 - $Y1) * $k / 25
        [void][UW]::SetCursorPos([int]$ix, [int]$iy); Start-Sleep -Milliseconds 25
    }
    Start-Sleep -Milliseconds 400
}

function Cancel-Drag {
    <# Vuelve abajo y suelta, para no jugar la carta. #>
    param([int]$X, [int]$Y)
    for ($k = 1; $k -le 15; $k++) { [void][UW]::SetCursorPos($X, $Y); Start-Sleep -Milliseconds 20 }
    Start-Sleep -Milliseconds 200
    [UW]::mouse_event(0x0004, 0, 0, 0, 0); Start-Sleep -Milliseconds 400
}
