# Asset inspection tools

Small standalone scripts used while building the mod. They read the game's own files and
ship no game data. Python 3, no dependencies.

| Script | What it does |
| --- | --- |
| `pck.py` | Reads a Godot `.pck` archive (format 3, as shipped by Godot 4.5). `info`, `list <substring>`, `tree`, `get <path> <dest>`. |
| `cfgb.py` | Decodes `project.binary`, Godot's compiled project settings, to readable key/value pairs. |
| `ctex.py` | Prints the dimensions of a `.ctex` compressed texture or a `.png`. |

```sh
PCK="C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.pck"

python pck.py "$PCK" info
python pck.py "$PCK" list scenes/combat/
python pck.py "$PCK" get project.binary ./project.binary
python cfgb.py ./project.binary display
```

## Verification harness

Used to check that every scene covers the screen, rather than trusting a glance.

| Script | What it does |
| --- | --- |
| `uwtest.ps1` | Focus, input and capture helpers. Everything runs in one process on purpose: spawning child shells steals focus and you end up screenshotting the console instead of the game. `Invoke-Console` confirms against the game log that a command actually ran — if the console toggle misses, the text lands as hotkeys and leaves the game somewhere unexpected. |
| `runsweep.ps1` | Runs a list of console commands, captures each, and reports only what fails. |
| `verify.py` | Flags bare canvas at the screen edges: the engine's clear colour, pure black, or a column with no horizontal gradient, which is the last texel of a background smeared outward. |

```powershell
cd tools
.\runsweep.ps1 -ListFile bosses_all1.txt -Wait 13000
```

`verify.py` screens for candidates; it is not a verdict. A flat sky or a shadowed
cave wall legitimately has no horizontal gradient, so look at anything it flags.
