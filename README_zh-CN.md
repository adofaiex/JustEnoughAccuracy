# ADOFAI 多加载器 Mod 模板

一个用于创建《A Dance of Fire and Ice》（ADOFAI）Mod 的项目模板，支持多种加载器：
Unity Mod Manager、MelonLoader、BepInEx 和 Doorstop 独立模式。

## 项目结构

```
ProjectRoot/
├── core/
│   ├── JustEnoughAccuracy.Core.csproj   -- 共享核心逻辑
│   ├── IHandler.cs                         -- 加载器抽象接口
│   ├── Main.cs                             -- 入口点 (Initialize)
│   ├── Settings.cs                         -- 可序列化的设置
│   ├── Patches.cs                          -- Harmony 补丁
│   └── ResourceLoader.cs                   -- 文件加载工具
├── loaders/
│   ├── umm/                                -- Unity Mod Manager 适配
│   ├── melon/                              -- MelonLoader 适配
│   ├── bepinex/                            -- BepInEx 适配
│   └── doorstop/                           -- Doorstop 独立模式适配
├── scripts/
│   ├── pack.csx                            -- 分发 zip 打包脚本
│   ├── pack.cmd                            -- Windows 打包命令
│   ├── pack.ps1                            -- PowerShell 打包命令
│   └── pack.sh                             -- Linux/macOS 打包命令
├── Resources/                              -- Mod 资源文件（文本、图片等）
├── ADOFAIMod.targets                       -- MSBuild 构建目标
└── Info.json                               -- UMM 清单（仅选择 UMM 时生成）
```

## 架构说明

每个加载器对应一个独立的适配项目，均引用 `core/` 共享项目。
`IHandler` 接口封装了日志、设置和生命周期事件，核心 Mod 代码无需感知具体加载器。

```
加载器项目 (如 loaders/umm/)
  └── 实现 IHandler
      └── 调用 Main.Initialize(handler)
          └── core/ 代码与加载器无关
```

## 环境要求

- .NET SDK 6.0 或更高版本
- A Dance of Fire and Ice (Steam)

> [!NOTE]
> 加载器依赖（MelonLoader、BepInEx、UnityModManager、Harmony）已内置在模板的
> `lib/ModManager/` 中，构建**无需**先安装任何加载器。你只需在 `.env` 中配置
> 游戏路径，供项目引用 Unity 引擎和游戏程序集。

## 安装模板

```bash
# 从本地仓库目录安装（推荐）
dotnet new install path/to/ADOFAIMod.MultiLoader

# 或从打包的 NuGet 包安装
dotnet pack path/to/JustEnoughAccuracy.Template.csproj -o path/to/dist
dotnet new install path/to/dist/JustEnoughAccuracy.1.0.0.nupkg
```

模板安装后名为 `adofaiml`。卸载用 `dotnet new uninstall ADOFAIMod.MultiLoader`。

## 创建项目

### 命令行

```bash
# 全部四个加载器（默认）
dotnet new adofaiml -n MyMod

# 选择部分加载器（关闭不需要的）
dotnet new adofaiml -n MyMod --bepinex false --doorstop false

# 指定作者和描述
dotnet new adofaiml -n MyMod -a "YourName" -d "我的第一个 ADOFAI Mod"
```

### 配置游戏路径（`.env`）

游戏路径从项目根目录的 `.env` 文件读取（该文件已被 git 忽略，本机路径不会进
版本库）：

```bash
# 复制示例文件并填写路径
cp .env.example .env

# .env
ADOFAI_GAME_PATH=C:\Games\ADOFAI\ADanceOfFireAndIce.exe   # 默认（所有加载器）
ADOFAI_GAME_PATH_UMM=...\ADanceOfFireAndIce.exe            # 仅 UMM
ADOFAI_GAME_PATH_ML=...\ADanceOfFireAndIce.exe             # 仅 MelonLoader
ADOFAI_GAME_PATH_BEPINEX=...\ADanceOfFireAndIce.exe        # 仅 BepInEx
ADOFAI_GAME_PATH_DOORSTOP=...\ADanceOfFireAndIce.exe       # 仅 Doorstop
```

某个加载器未配置自己的 key 时，会回退到 `ADOFAI_GAME_PATH`，因此如果只用一个
游戏安装，写一个默认值即可。

### Visual Studio / JetBrains Rider

安装模板后，新建项目并搜索 "ADOFAI" 或 "adofaiml"。
新建项目向导中会为每个加载器显示复选框。

### 参数说明

| 短参数 | 长参数 | 说明 |
|---|---|---|
| `-n` | `--name` | 项目名称（同时也是 Mod 名称） |
| `-a` | `--author` | 作者名称 |
| `-d` | `--description` | Mod 描述 |
| `-v` | `--version` | 初始版本号（默认：1.0.0） |
| `-um` | `--umm` | 包含 UMM 加载器（默认：true） |
| `-ml` | `--melon` | 包含 MelonLoader（默认：true） |
| `-bx` | `--bepinex` | 包含 BepInEx（默认：true） |
| `-ds` | `--doorstop` | 包含 Doorstop（默认：true） |

## 构建与部署

### 游戏路径（`GameExePath`）

各加载器的游戏路径从项目根目录的 **`.env`** 文件（已被 git 忽略）分别解析：

| Key | 用于 |
|---|---|
| `ADOFAI_GAME_PATH` | 默认——核心项目以及未配置专用 key 的加载器 |
| `ADOFAI_GAME_PATH_UMM` | UMM 加载器 |
| `ADOFAI_GAME_PATH_ML` | MelonLoader |
| `ADOFAI_GAME_PATH_BEPINEX` | BepInEx |
| `ADOFAI_GAME_PATH_DOORSTOP` | Doorstop |

每个加载器的解析顺序：自身 `.env` key → `ADOFAI_GAME_PATH` →
`ADOFAI_GAME_PATH_<加载器>` 环境变量 → `-p:GameExePath=...`。

这样可以让每个加载器指向不同的游戏安装（例如不同的 ADOFAI 版本），同时所有
路径都不进 git。

### 构建 + 部署 + 启动

```bash
# 构建全部，部署到指定加载器的游戏目录，并启动游戏
dotnet build -p:Loader=UMM          # UMM → 游戏目录/Mods/{Mod名称}/
dotnet build -p:Loader=ML           # Melon → 游戏目录/Mods/
dotnet build -p:Loader=BepInEx      # BepInEx → 游戏目录/BepInEx/plugins/{Mod名称}/
dotnet build -p:Loader=Doorstop     # Doorstop → 游戏目录/

# 构建 + 部署，但不启动游戏
dotnet build -p:Loader=UMM -p:AutoLaunchGame=false

# Release：仅构建（不部署/启动），扁平输出到 out/
dotnet build -c Release
```

各加载器部署路径：

| 加载器 | 目标目录 |
|---|---|
| `UMM` | `游戏目录/Mods/{Mod名称}/` |
| `ML` | `游戏目录/Mods/` |
| `BepInEx` | `游戏目录/BepInEx/plugins/{Mod名称}/` |
| `Doorstop` | `游戏目录/`（根目录，与 doorstop_config.ini 同级） |

解决方案中的每个项目均可独立构建。无论构建哪个加载器，
`out/` 目录都会以扁平结构收集所有输出文件：

```
out/
├── {Mod名称}.Core.dll
├── {Mod名称}.Loader.UMM.dll        （如果构建了 UMM）
├── {Mod名称}.Loader.Melon.dll      （如果构建了 Melon）
├── {Mod名称}.Loader.BepInEx.dll    （如果构建了 BepInEx）
├── {Mod名称}.Loader.Doorstop.dll   （如果构建了 Doorstop）
├── Info.json                        （仅构建 UMM 时存在）
├── doorstop_config.ini              （仅构建 Doorstop 时存在）
└── Resources/
```

## 制作分发包

需要 `dotnet script`（通过 `dotnet tool install -g dotnet-script` 安装）。

```bash
# 先 Release 构建，再打包
scripts/pack.cmd      # Windows
scripts/pack.sh       # Linux/macOS
scripts/pack.ps1      # PowerShell
```

脚本会在 `dist/` 目录下生成按加载器分类的 ZIP 压缩包：

| 文件 | 内部结构（相对于游戏根目录） |
|---|---|
| `{Mod名称}_umm.zip` | `Mods/{Mod名称}/` 扁平 |
| `{Mod名称}_melon.zip` | `Mods/` 扁平 |
| `{Mod名称}_bepinex.zip` | `BepInEx/plugins/{Mod名称}/` 扁平 |
| `{Mod名称}_doorstop.zip` | 根目录扁平（含 doorstop_config.ini） |

每个压缩包解压到游戏根目录即可直接使用。

## 开发指南

1. 在 `core/` 中编写 Mod 逻辑，代码不与任何特定加载器耦合。
2. 通过 `Main.Handler` 进行日志输出，`Main.Settings` 读写配置。
3. 在 `core/` 中添加 Harmony 补丁类，启用 Mod 时自动应用。
4. 资源文件放入 `Resources/`，使用 `ResourceLoader` 加载。
5. 使用对应加载器构建，在游戏中测试。

## GitHub 模板

本仓库也可以作为 GitHub 模板使用。在仓库页面点击 "Use this template"
创建新仓库，然后克隆并运行初始化脚本，项目会自动按仓库名称重命名：

```bash
# Linux / macOS
chmod +x init.sh
./init.sh

# Windows (PowerShell)
.\init.ps1
```

初始化脚本会自动读取仓库目录名，替换所有文件中的旧名称、重命名文件
和目录，并重新初始化 Git 历史。

## 卸载模板

```bash
dotnet new uninstall JustEnoughAccuracy
```

## 许可证

GPL-3.0-or-later
