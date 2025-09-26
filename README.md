# MusicDistribution

A tiny .NET console tool that **organizes MP3 files into a Jellyfin-friendly folder structure** using their ID3 metadata (via TagLib#).

- Preserves **Unicode filenames** (CJK ideographs, Cyrillic, etc.)
- Safe filename/path sanitization (Windows-safe, reserved names avoided)
- Customizable folder/file pattern (e.g., `Artist/Album (Year)/01 - Title.mp3`)
- Copy or move, overwrite control, and dry-run preview

---

## Contents

- [Requirements](#requirements)
- [Install / Build](#install--build)
- [Usage](#usage)
- [Command Line Options](#command-line-options)
- [Pattern & Placeholders](#pattern--placeholders)
- [Unicode & Sanitization](#unicode--sanitization)
- [Library Structure Recommended by Jellyfin](#library-structure-recommended-by-Jellyfin)
- [Collision Handling](#collision-handling)
- [Dry-Run Safety](#dry-run--safety)
- [Notes & Limitations](#notes--limitations)

---

## Requirements

- **.NET SDK 8.0+** (https://dotnet.microsoft.com/download)
- Windows, Linux, or macOS filesystem that supports Unicode filenames
- NuGet package: **`taglib-sharp`**
- Filesystem that supports Unicode (NTFS, APFS, ext4)
- Your music files are **MP3** (this tool targets `*.mp3`)

> The app targets `net8.0` by default. You can retarget if needed.

---

## Install / Build

You can build it on your own machine:

```powershell
# From the project folder (contains .csproj and Program.cs)
dotnet restore
dotnet run -- --src "D:\Incoming" --dst "E:\Music" --dryrun   # preview plan
dotnet run -- --src "D:\Incoming" --dst "E:\Music" --move     # execute (move)
```

Or run it from build .exe file

```powershell
 .\MusicDistribution.exe --src "D:\Incoming" --dst "E:\Music" --move
```

> When running compiled .exe file, do **not** use `--`

---

## Usage

- Copy into a clean Artist/Album tree

```powershell
.\MusicDistribution.exe --src "D:\Incoming" --dst "E:\Music"
```
- Dry-run to test the output

```powershell
.\MusicDistribution.exe --src "D:\Incoming" --dst "E:\Music" --dryrun
```
- Dry-run with a custom pattern

```powershell
.\MusicDistribution.exe --src "D:\Rip" --dst "E:\Music" --dryrun --pattern "{artist_name}/{album_name}/{track_num} - {track_name}.mp3"
```
- Move instead of copy, and overwrite existing files

```powershell
.\MusicDistribution.exe --src "D:\Downloads" --dst "E:\Music" --move --overwrite
```

---

## Command Line Options

| Option             | Description                                                                        | Default                                                                                       |
| ------------------ | ---------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| `--src`            | Source root folder (recursively scans `*.mp3`)                                     | (req.)                                                                                        |
| `--dst` / `--dest` | Destination music root                                                             | (req.)                                                                                        |
| `--pattern`        | Output pattern using placeholders (see below)                                      | `{artist_name}/{album_name} ({release_year})/{multi_disc_path}{track_num} - {track_name}.mp3` |
| `--move`           | Move files instead of copying                                                      | `false`                                                                                       |
| `--overwrite`      | Overwrite destination if it exists (otherwise a unique name like ` (2)` is chosen) | `false`                                                                                       |
| `--dryrun`         | Print the plan without writing                                                     | `false`                                                                                       |
| `--ascii`          | Strip Latin diacritics to ASCII (CJK/Cyrillic remain as-is)                        | `false`                                                                                       |

> To redirect console output in a log file: `... --dryrun *> log.txt`

---

## Pattern & Placeholders

Use `--pattern` to define your folder/file layout. Available placeholders:

- `{track_name}` — title (`TIT2`)
- `{artist_name}` — first performer; falls back to `"Unknown Artist"`
- `{all_artist_names}` — comma-joined performers
- `{album_name}` — album; falls back to `"Unknown Album"`
- `{track_num}` — 2-digit track number (`01`, `02`), defaults to `00`
- `{release_year}` — numeric year (from `TYER`/ID3 year)
- `{release_date}` — best-effort date; with MP3 this may equal year or be empty
- `{multi_disc_path}` — `CD{disc}/` when `Disc > 1`; otherwise empty
- `{multi_disc_paren}` — `CD{disc}` (no slash)
- Optional (empty by default): `{playlist_name}`, `{context_name}`, `{context_index}`, `{canvas_id}`

**Default pattern**

```powershell
{artist_name}/{album_name} ({release_year})/{multi_disc_path}{track_num} - {track_name}.mp3
```

**Example result**

```powershell
Daft Punk/Discovery (2001)/01 - One More Time.mp3
Metallica/Load (1996)/CD2/03 - Hero of the Day.mp3
Various Artists/OST (2010)/07 - Main Theme.mp3
```

**Playlist layout**
```powershell
Playlists/{playlist_name}/{artist_name} - {track_name}.mp3
```

---

## Unicode & Sanitization

- **Unicode preserved**: CJK ideographs (漢字/汉字/かんじ), Cyrillic, diacritics — all kept.
- **NFC normalization**: filenames/directories normalized to Form C for cross-platform stability.
- **Invalid chars removed**: only filesystem-illegal characters are stripped/replaced (e.g., `:*?"<>|` on Windows).
- **Reserved names avoided**: Windows device names (`CON`, `NUL`, `COM1`…`COM9`, `LPT1`…`LPT9`) are auto-prefixed to stay valid.
- **Console output in UTF-8**: works best in **Windows Terminal / PowerShell 7+**.

`--ascii` **flag**
Optionally strips **Latin** diacritics only (`é → e`, `ñ → n`). Non-Latin scripts are not transliterated.

---

## Library Structure Recommended by Jellyfin

```
Music/
  Artist/
    Album (Year)/
      01 - Title.mp3
      02 - Title.mp3
      ...
```
- Ensure tags are present: **Artist**, **Album**, **Title**, **Track #**, **Year**.
- Multi-disc albums: separate into `CD1/`, `CD2/` (the default pattern does this automatically).
- Singles/EPs: place in their own album folders, or create a `Singles/` collection with a different `--pattern`.

**Jellyfin setup tips**

1. In Jellyfin: **Dashboard → Libraries → Add Library → Music**
2. Point to your `Music/` folder.
3. Metadata fetchers: keep **MusicBrainz** enabled; prefer **ID3 tags** where present.
4. After a large re-org, run **Scan Library** (and optionally **Refresh Metadata** if needed).

---

## Collision Handling

If the destination file exists and `--overwrite` is **not** set, the tool chooses a unique name:
```
"01 - Title.mp3" → "01 - Title (2).mp3" → "01 - Title (3).mp3" → …
```
This avoids accidental overwrites while still placing the track in the correct album folder.

---

## Dry-Run Safety

Use `--dryrun` to preview every action:

- Shows **source → destination** mapping
- No folders are created; no files are copied/moved
- Great for validating your `--pattern` before any changes

Example:
```
.\MusicDistribution.exe --src "D:\Incoming" --dst "E:\Music" --dryrun *> plan.txt
```

Review `plan.txt`, then run without `--dryrun`.

---

## Notes & Limitations

- Focused on **MP3** (`*.mp3`). Other formats are ignored unless you extend the scanner.
- `{release_date}` for MP3 is typically **year only** — ID3 rarely stores full dates consistently.
- Placeholders not present in tags resolve to empty strings; safe fallbacks are used.
- The tool uses **NFC normalization**; on some Samba/NFS mounts you may observe different Unicode behavior.

---
