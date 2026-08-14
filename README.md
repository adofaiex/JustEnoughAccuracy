# ADOFAI Multi-Loader Mod Template

A project template for creating A Dance of Fire and Ice (ADOFAI) mods that work with multiple mod loaders: Unity Mod Manager, MelonLoader, BepInEx, and Doorstop standalone.

## Project Structure

```
ProjectRoot/
├── core/
│   ├── AdofaiMod.MultiLoader.Core.csproj   -- Shared mod logic
│   ├── IHandler.cs                         -- Loader abstraction interface
│   ├── Main.cs                             -- Entry point (Initialize)
│   ├── Settings.cs                         -- Serializable settings
│   ├── Patches.cs                          -- Harmony patches
│   └── ResourceLoader.cs                   -- File loading utilities
├── loaders/
│   ├── umm/                                -- Unity Mod Manager adapter
│   ├── melon/                              -- MelonLoader adapter
│   ├── bepinex/                            -- BepInEx adapter
│   └── doorstop/                           -- Doorstop standalone adapter
├── scripts/
│   ├── pack.csx                            -- Distribution zip packer
│   ├── pack.cmd                            -- Windows pack script
│   ├── pack.ps1                            -- PowerShell pack script
│   └── pack.sh                             -- Linux/macOS pack script
├── Resources/                              -- Mod assets (text, images, etc.)
├── ADOFAIMod.targets                       -- MSBuild targets (copy, deploy)
└── Info.json                               -- UMM manifest (only if UMM selected)
```

## Architecture

Each mod loader has its own adapter project that references the shared `core/` project.
The `IHandler` interface abstracts logging, settings, and lifecycle events so the
core mod code never depends on a specific loader.

```
Loader project (e.g. loaders/umm/)
  └── implements IHandler
      └── calls Main.Initialize(handler)
          └── core/ code runs loader-agnostic
```

## Prerequisites

- .NET SDK 6.0 or later
- A Dance of Fire and Ice (Steam)

> [!NOTE]
> Loader dependencies (MelonLoader, BepInEx, UnityModManager, Harmony) are bundled
> in the template's `lib/ModManager/`, so you do **not** need to install any loader
> to build. You only need the game's `GameExePath` so the project can reference the
> Unity engine and game assemblies.

## Install the Template

```bash
# From a local copy of the repository (recommended)
dotnet new install path/to/ADOFAIMod.MultiLoader

# Or from a packed NuGet package
dotnet pack path/to/AdofaiMod.MultiLoader.Template.csproj -o path/to/dist
dotnet new install path/to/dist/AdofaiMod.MultiLoader.1.0.0.nupkg
```

The template is installed as `adofaiml`. Uninstall with `dotnet new uninstall ADOFAIMod.MultiLoader`.

## Create a Project

### Command Line

```bash
# All four loaders (default)
dotnet new adofaiml -n MyMod

# Select specific loaders (disable the ones you don't need)
dotnet new adofaiml -n MyMod --bepinex false --doorstop false

# Specify author and description
dotnet new adofaiml -n MyMod -a "YourName" -d "My first ADOFAI mod"
```

### Point it at your game (`.env`)

Game paths are read from a git-ignored `.env` file at the project root, so your
local installs never end up in version control:

```bash
# copy the example and fill in your path(s)
cp .env.example .env

# .env
ADOFAI_GAME_PATH=C:\Games\ADOFAI\ADanceOfFireAndIce.exe   # default (all loaders)
ADOFAI_GAME_PATH_UMM=...\ADanceOfFireAndIce.exe            # UMM only
ADOFAI_GAME_PATH_ML=...\ADanceOfFireAndIce.exe             # MelonLoader only
ADOFAI_GAME_PATH_BEPINEX=...\ADanceOfFireAndIce.exe        # BepInEx only
ADOFAI_GAME_PATH_DOORSTOP=...\ADanceOfFireAndIce.exe       # Doorstop only
```

Each loader falls back to `ADOFAI_GAME_PATH` when its own key is empty, so a
single default is enough if you use one game install.

### Visual Studio / JetBrains Rider

After installing the template, create a new project and search for "ADOFAI" or
"adofaiml". The new project wizard shows checkboxes for each loader.

### Parameters

| Short | Long | Description |
|---|---|---|
| `-n` | `--name` | Project name (becomes the mod name) |
| `-a` | `--author` | Author name |
| `-d` | `--description` | Mod description |
| `-v` | `--version` | Initial version (default: 1.0.0) |
| `-um` | `--umm` | Include UMM loader (default: true) |
| `-ml` | `--melon` | Include MelonLoader (default: true) |
| `-bx` | `--bepinex` | Include BepInEx (default: true) |
| `-ds` | `--doorstop` | Include Doorstop (default: true) |

## Build and Deploy

### Game path (`GameExePath`)

Game paths are resolved per loader from the git-ignored **`.env`** file at the
project root:

| Key | Used by |
|---|---|
| `ADOFAI_GAME_PATH` | Default — the core project and any loader without its own key |
| `ADOFAI_GAME_PATH_UMM` | UMM loader |
| `ADOFAI_GAME_PATH_ML` | MelonLoader |
| `ADOFAI_GAME_PATH_BEPINEX` | BepInEx |
| `ADOFAI_GAME_PATH_DOORSTOP` | Doorstop |

Resolution order for each loader: its own `.env` key → `ADOFAI_GAME_PATH` →
`ADOFAI_GAME_PATH_<LOADER>` environment variable → `-p:GameExePath=...`.

This lets you point each loader at a different game install (e.g. separate ADOFAI
versions) while keeping every path out of git.

### Build + deploy + launch

```bash
# Build all, deploy to the requested loader's game, and launch it
dotnet build -p:Loader=UMM          # UMM  → GameDir/Mods/{ModName}/
dotnet build -p:Loader=ML           # Melon → GameDir/Mods/
dotnet build -p:Loader=BepInEx      # BepInEx → GameDir/BepInEx/plugins/{ModName}/
dotnet build -p:Loader=Doorstop     # Doorstop → GameDir/

# Build + deploy but do NOT launch the game
dotnet build -p:Loader=UMM -p:AutoLaunchGame=false

# Release: build only (no deploy/launch), outputs flat to out/
dotnet build -c Release
```

Deploy paths by loader:

| Loader | Target Directory |
|---|---|
| `UMM` | `GameDir/Mods/{ModName}/` |
| `ML` | `GameDir/Mods/` |
| `BepInEx` | `GameDir/BepInEx/plugins/{ModName}/` |
| `Doorstop` | `GameDir/` (root, alongside doorstop_config.ini) |

Each project in the solution can be built independently. The `out/` directory
collects all output files in a flat layout, regardless of which loader was built:

```
out/
├── {ModName}.Core.dll
├── {ModName}.Loader.UMM.dll        (if built)
├── {ModName}.Loader.Melon.dll      (if built)
├── {ModName}.Loader.BepInEx.dll    (if built)
├── {ModName}.Loader.Doorstop.dll   (if built)
├── Info.json                        (only if UMM built)
├── doorstop_config.ini              (only if doorstop built)
└── Resources/
```

## Create Distribution Packages

Requires `dotnet script` (install with `dotnet tool install -g dotnet-script`).

```bash
# Build release binaries, then pack
scripts/pack.cmd      # Windows
scripts/pack.sh       # Linux/macOS
scripts/pack.ps1      # PowerShell
```

This produces per-loader ZIP archives in `dist/`:

| File | Structure (relative to game root) |
|---|---|
| `{ModName}_umm.zip` | `Mods/{ModName}/` flat |
| `{ModName}_melon.zip` | `Mods/` flat |
| `{ModName}_bepinex.zip` | `BepInEx/plugins/{ModName}/` flat |
| `{ModName}_doorstop.zip` | root flat (includes doorstop_config.ini) |

Each archive is self-contained: extract directly into the game directory.

## Development

1. Write your mod logic in `core/`. It has no dependency on any specific loader.
2. Use `Main.Handler` for logging, `Main.Settings` for configuration.
3. Add Harmony patch classes under `core/` — they apply automatically when the
   mod is enabled.
4. Place assets in `Resources/` and load them with `ResourceLoader`.
5. Build with your chosen loader to test in-game.

## GitHub Template

This repository can also be used as a GitHub template. Click "Use this template"
on the repository page to create a new repository, then clone it and run the
init script to rename the project to match your repository name:

```bash
# Linux / macOS
chmod +x init.sh
./init.sh

# Windows (PowerShell)
.\init.ps1
```

The init script automatically detects the repository directory name and renames
all files and namespaces accordingly. It also re-initializes the Git history
with a clean initial commit.

## Uninstall the Template

```bash
dotnet new uninstall AdofaiMod.MultiLoader
```

## License

GPL-3.0-or-later
