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
