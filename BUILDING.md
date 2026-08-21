# uWidgets 构建与模块化说明（个人使用版）

基于官方 [creewick/uWidgets](https://github.com/creewick/uWidgets) v0.6.0 的本地 fork。
仅保留 **简体中文 + 英语** 语言，并增设了自定义 **Folders**（文件夹）小组件。

## 目录结构（模块化）

```
├── Directory.Packages.props   ← 集中包版本管理 (CPM)，所有包版本只在这里改
├── build.ps1                  ← 一键构建脚本（单 exe / 便携 zip）
├── scripts/
│   └── build-widgets-bundle.ps1 ← widget 打包脚本（被 MSBuild 目标调用）
└── src/
    ├── uWidgets.sln
    ├── uWidgets/              ← 主程序（Avalonia 桌面应用）
    │   └── Services/WidgetBundle.cs ← 单文件模式自解压逻辑
    ├── uWidgets.Core/         ← 小组件 SDK（插件契约：WidgetInfo/IAssemblyProvider/…）
    └── Widgets/               ← 小组件，每个目录一个模块
        ├── Clock/ Calendar/ Weather/ Monitor/ Notes/ Reminders/
        └── Folders/           ← 自定义：文件夹快捷方式 / 监视文件夹实时刷新
```

### 模块边界
- 每个小组件是独立 `csproj`，产出 DLL，由主程序通过 `AssemblyLoadContext` 从 `Widgets/` 目录动态加载（`uWidgets.Core/Services/AssemblyProvider.cs`）。
- 小组件**不携带** Avalonia/Core 副本（`PrivateAssets=all` + `ExcludeAssets=runtime`），运行时由宿主提供。
- 新增小组件：复制 `Widgets/Clock/` 结构，修改 `AssemblyInfo.cs` 里的 `[WidgetInfo(...)]` 与 `[Locale(...)]`，加入 sln 即可（打包目标自动发现）。

## 打包：单文件 exe

`build.ps1` 默认产出**框架依赖单文件 exe**（需目标机安装 .NET 8 运行时）：

```
./build.ps1                      # dist\win-x64\uWidgets.exe
./build.ps1 -Runtime win-arm64   # ARM64
./build.ps1 -Portable            # 额外产出官方式便携 zip（exe + Widgets + json）
```

### 原理
1. `dotnet publish -r <rid> --self-contained false`，csproj 已开启 `PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract`（Avalonia 原生库随 exe 打包，运行时自动解压到临时目录）。
2. MSBuild 目标 `BuildWidgetsBundle`（挂 `AssignTargetPaths`）发布全部小组件 → 压缩为 `widgets.zip` → 作为内嵌资源打入 exe；`appSettings.json` / `layout.json` 默认值同样内嵌。
3. 启动时 `WidgetBundle.ExtractIfNeeded()`（`Program.Main` 最先执行）：
   - **便携模式**：exe 旁存在 `Widgets/` 文件夹 → 全部走本地文件（官方/开发模式，行为不变）。
   - **打包模式**：解压到 `%LocalAppData%\uWidgets\`，`.version` 标记记录 exe 版本，版本变化才重新解压。

### 热更新
打包模式下 widget 是普通磁盘文件（`%LocalAppData%\uWidgets\Widgets\`），直接替换 DLL 即可热更新，无需重新打包 exe。

### 注意
- **不要开启 Trim**（`PublishTrimmed`）：widget 类型靠反射激活，裁剪会破坏插件机制。
- `dotnet publish` 裸命令也能出包，但推荐 `build.ps1`（会清理散落文件，保证 dist 里只有一个 exe）。

## 配置迁移（从旧便携版）
把旧目录的 `layout.json` 和 `appSettings.json` 复制到 `%LocalAppData%\uWidgets\` 即可无缝迁移（旧版 widget 位置/设置全部保留）；或直接把新 exe 放入旧目录走便携模式。

## 版本管理
- 主程序版本：`src/uWidgets/AssemblyInfo.cs`（`AssemblyVersion`），同时作为 widget 包重解压标记。
- 小组件版本：各自 `AssemblyInfo.cs`。
- 包版本：`Directory.Packages.props` 一处集中修改。
