# Game Cheats Manager WinUI 3 移植版

本项目是原版 Python `Game-Cheats-Manager` 的 WinUI 3 移植版本。

原始上游项目：
`https://github.com/dyang886/Game-Cheats-Manager`

## 已移植的功能

- 使用原生 WinUI 3 实现的双栏界面，用于已安装修改器管理和下载搜索
- 本地修改器库的搜索、启动、删除、导入，以及下载目录迁移
- 修改器数据刷新、修改器更新检查，以及下载队列处理
- GCM、FLiNG、小幸、Cheat Tables、Wand、Cheat Engine、Cheat Evolution 等来源管理
- 上传流程、公告和更新检查，以及 Windows Defender 白名单操作

## 后端配置

原始应用依赖一些未提交到源码仓库的私有后端配置。WinUI 版本同样不会把这些值写入受版本控制的文件中，并按以下顺序解析：

1. 环境变量
2. 项目根目录下的 `secret_config.py`
3. 构建后可执行文件同目录下的 `secret_config.py`

支持的变量：

- `SIGNED_URL_DOWNLOAD_ENDPOINT`
- `SIGNED_URL_UPLOAD_ENDPOINT`
- `VERSION_CHECKER_ENDPOINT`
- `PATCH_PATTERNS_ENDPOINT`
- `CLIENT_API_KEY`

如果这些值缺失，WinUI 应用仍然可以构建，本地修改器库相关功能也仍可使用；但依赖原始私有后端的签名下载、上传、版本检查和补丁模式功能将不可用。

## 项目内运行时资源

WinUI 项目在 `Assets` 和 `Dependencies` 目录中自带运行时所需资源，不依赖任何外部本地克隆目录中的文件。

## 构建

```powershell
dotnet restore
dotnet build
```

## 单文件构建

```powershell
dotnet build .\GameCheatsManager.WinUI.csproj -c Release -t:BuildSingleFile
```

单文件可执行文件输出到 `artifacts\release\win-x64\GameCheatsManager.WinUI.exe`。
该构建为 framework-dependent，目标机器需要预先安装 `.NET Desktop Runtime` 和 `Windows App SDK Runtime`。

## 发布包构建

```powershell
dotnet build .\GameCheatsManager.WinUI.csproj -c Release -t:BuildSingleFileBundle
```

该命令会在 `artifacts\release\win-x64\GameCheatsManager.WinUI.zip` 生成压缩后的发布包。
原始 `.exe` 体积仍约为 `38 MB`，但压缩包会小得多，因此更适合作为发布产物。

## GitHub Release 工作流

`.github/workflows/release.yml` 会在 `windows-latest` 上构建压缩后的单文件发布包，并将 `.zip` 与对应的 `.sha256` 文件上传到 GitHub Release 资产列表。
