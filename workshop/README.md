# Publishing to the Steam Workshop

Slay the Spire 2 loads Workshop items but has no uploader of its own, so the upload goes
through SteamCMD.

## One-time setup

1. Download SteamCMD: <https://developer.valvesoftware.com/wiki/SteamCMD> and unpack it
   somewhere, e.g. `C:\steamcmd`.
2. Build and stage the mod from the repo root:

   ```powershell
   .\build.ps1
   ```

   That leaves the exact folder Steam will upload at `release\content\ultrawide\`.
3. Open `workshop\ultrawide.vdf` and check `contentfolder` and `previewfile` point at this
   checkout. They are absolute paths.

## First upload

`publishedfileid` is `0`, which tells Steam to create a new item. `visibility` is `2`
(private) on purpose — upload it, look at it on Steam, and only then make it public.

```powershell
C:\steamcmd\steamcmd.exe +login YOUR_STEAM_ACCOUNT +workshop_build_item "C:\web\sts2-ultrawide\workshop\ultrawide.vdf" +quit
```

You will be asked for your password and Steam Guard code. On success SteamCMD prints the
new item's id.

**Put that id into `ultrawide.vdf` as `publishedfileid`** and commit it — every later
upload updates that same item instead of creating duplicates.

## Later updates

1. Bump `version` in `mod/ultrawide.json`.
2. Update `changenote` in `workshop/ultrawide.vdf`.
3. `.\build.ps1`
4. Re-run the same SteamCMD command.

## Going public

Steam requires you to accept the Workshop Legal Agreement once before an item can be
public. Open the item's page on Steam, accept if prompted, then switch visibility to
Public there — or set `"visibility" "0"` in the VDF and re-upload.

## Checklist before going public

- [ ] `min_game_version` in `mod/ultrawide.json` matches the oldest build you tested.
- [ ] `affects_gameplay` is still `false` — this is what lets unmodded friends play with you.
- [ ] Tested at your own resolution, and at 16:9 and 21:9 to confirm the mod stays inert.
- [ ] `preview.png` is under 1 MB.
