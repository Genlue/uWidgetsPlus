## uWidgetsPlus (uWidgets+)

<img src=".github/images/icon-light.png#gh-light-mode-only" width="120" alt="Logo" align="right">
<img src=".github/images/icon-dark.png#gh-dark-mode-only" width="120" alt="Logo" align="right">

<div align="center">
  <h3>🎨 Windows 下一代 macOS 风格多功能桌面小组件增强套件</h3>
  <p>基于 Avalonia 11 + .NET 8 打造 · 硬件级实时毛玻璃 · 3D 光学液态玻璃 · 手机锁屏艺术大字时钟 · 灵活桌面网格系统</p>
</div>

<h3 align="center">
  <b><a href="https://github.com/Genlue/uWidgetsPlus/releases">下载最新版本</a></b> ・
  <a href="https://github.com/Genlue/uWidgetsPlus/issues">问题反馈</a> ・
  <a href="项目解构报告.md">项目解构报告</a>
</h3>

<div align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-blue?logo=dotnet"/>
  <img src="https://img.shields.io/badge/Avalonia-11.1-purple?logo=avaloniaui"/>
  <img src="https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows"/>
  <img src="https://img.shields.io/badge/Release-Single--File%20EXE-success"/>
  <img src="https://img.shields.io/badge/Installer-MSI%20Package-blue?logo=windows"/>
</div>

<br />

### 语言版本 / Language
<a href="/README.md"><kbd><img src="https://github.com/yammadev/flag-icons/blob/master/png/US.png?raw=true" height="10" /> English</kbd></a>
<kbd><img src="https://github.com/yammadev/flag-icons/blob/master/png/CN.png?raw=true" height="10" /> 中文 (简体)</kbd>

---

## 🌟 核心特色与重大升级

### 1. 🕒 无边框艺术大字时钟（Frameless Clock）
- **满格顶天立地**：数字直接占据整个小组件单元格，上下严格贴紧边缘（消除字体自带空隙），支持横向自由拉伸（`StretchFill`）与等比居中；
- **精选手机锁屏艺术大字集**：
  - 🔥 **华为锁屏超窄体**（`HarmonyOS Sans Condensed`）：免安装内置打包，专为大字纵向拉伸设计，视觉极其震撼；
  - 🔥 **iOS 16 经典厚重块体**（`Impact`）：美式重型力量感大字；
  - 🔥 **德国工业精工 DIN**（`Bahnschrift`）：严谨规整现代几何窄体；
  - 🔥 **超粗硬核无衬线**（`Arial Black`）：超宽实心黑体，视觉存在感极强；
  - 🔥 **iOS 高定复古衬线**（`Georgia`）：粗细笔触优雅对比；
  - 🔥 **包豪斯极简几何**（`Century Gothic`）：纯粹圆融现代线条；
  - 🔥 **赛博极客终端等宽**（`Cascadia Code`）、**自由随性手写**（`Ink Free`）、**古典罗马体**（`Palatino`）等；
- **全阶梯字重调节**：从 `100 Thin` 纤细至 `900 Black` 浓黑随心切换；
- **独立视觉主题覆盖**：可在该小组件设置中单独指定主题（跟随全局 / 毛玻璃 / 液态玻璃 / 纯色）；
- **自定义遮罩与官方选色器**：支持叠加半透明微光遮罩，配备 Avalonia 官方 `ColorPicker` 选色模块与十六进制文本框双向联动。

### 2. 💎 三大深度适配视觉材质
- 🪟 **毛玻璃（Acrylic Blur）· OS 硬件实时模糊**：
  - 基于 Win32 原生多边形扫描线 `ExtCreateRegion`（`RGNDATA`），将数字字形与卡片实时绑定为物理 HWND Region；
  - Windows DWM 硬件级逐帧对桌面采样合成，对**动态壁纸（Wallpaper Engine 等）、视频壁纸及后台窗口移动实现 60fps/144fps 零延迟实时跟手**；
  - 支持可选高光渐变外描边（颜色与粗细自定义）。
- 💧 **液态玻璃（Liquid Glass）· 3D 光学物理折射**：
  - 2D 欧几里得距离场（EDT）精密计算字符轮廓法线，模拟真实的凹凸透镜折射位移（Lens Refraction）、色散光斑（Dispersion）、3D 镜面高光与微细倒角；
  - **精细化边缘控制**：将折射带宽收窄至 `1.5dp ~ 4.5dp`，数字主体保持水晶般通透平整，杜绝字符过度扭曲；
  - **后台异步预缓存引擎（Background Pre-Caching）**：当前分钟 $T$ 渲染的同时，后台线程静默预渲染下一分钟 $T+1\text{m}$ 帧；**整点切换时 0ms 瞬间命中缓存**，彻底告别渲染卡顿；
  - **严密全自动内存清理（Zero-Leak Auto-Cleanup）**：时间推进自动 Dispose 释放旧帧；窗口移动、缩放、字体/主题更改或组件卸载时立即取消任务并销毁缓存位图，杜绝内存泄漏。
- 🎨 **纯色（Solid Fill）· 纯粹矢量抗锯齿填充**：
  - 纯净抗锯齿矢量填充，支持透明度滑块与色彩定制，零模糊、极度省电。

### 3. 📐 灵活专业桌面网格系统（Grid Management）
- **三大放置模式**：
  - **自定义网格（Manual Grid）**：将桌面划分为 $m \times n$ 个正方形格子，按百分比响应式存储，自适应多分辨率与缩放；
  - **虚拟网格（Virtual Grid）**；
  - **自由拖拽模式（Free Placement）**；
- 组件自适应格子吸附对齐，支持自定义组件间距与圆角大小。

### 4. 🧩 丰富完备的小组件家族
- ⏰ **时钟（Clock）**：指针表盘（3 种风格）、数字时钟、世界时钟（支持多表盘独立自定义城市名称与中心数字时钟联动）、无边框艺术时钟；
- 📁 **文件夹与文件（Folders）**：桌面文件夹快捷入口、实时内容更新监视、单文件启动快捷入口；
- 🌤️ **天气（Weather）**：横向平滑滚轮浏览、7 天详细预报、日出日落、紫外线、空气质量（支持自定义代理）；
- 📊 **系统监视（Monitor）**：单指标轻量仪表、多指标全能看板（CPU / 内存 / 磁盘 / 网络 / 电池）；
- 📝 **便签备忘（Notes）**：桌面随时快捷记录；
- ✅ **待办提醒（Reminders）**：交互式清单与任务计数统计；
- 🎵 **音乐控制（Music）** 与 🔍 **搜索工具（Search）**。

### 5. 🚀 单文件开箱即用（Single-File Executable）
- 编译生成单个 `uWidgets.exe`（~58 MB），内置小组件包内容哈希检测与运行时极速解压，无需复杂安装，即开即用。

---

## 🛠️ 从源码构建（Building from Source）

### 前置条件
- Windows 10 / 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本
- PowerShell 7 (pwsh) 或 Windows PowerShell

### 构建命令
```powershell
# 克隆仓库
git clone https://github.com/Genlue/uWidgetsPlus.git
cd uWidgetsPlus

# 一键编译并生成单文件 EXE
powershell -ExecutionPolicy Bypass -File .\build.ps1

# 一键编译并生成 Windows 标准 MSI 安装包
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Msi

# 产物输出路径
# dist/win-x64/uWidgets.exe
# dist/installer/uWidgetsPlus-1.7.5-win-x64.msi
```

---

## 📄 开源许可与致谢

- 衍生并增强自开源项目 [creewick/uWidgets](https://github.com/creewick/uWidgets)
- 基于 [MIT License](LICENSE) 开源发布
- 感谢 [Avalonia UI](https://avaloniaui.net/) 与 [SkiaSharp](https://github.com/mono/SkiaSharp) 社区提供强大的跨平台图形渲染能力。
