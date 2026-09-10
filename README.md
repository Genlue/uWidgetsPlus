## uWidgetsPlus (uWidgets+)

<img src=".github/images/icon-light.png#gh-light-mode-only" width="120" alt="Logo" align="right">
<img src=".github/images/icon-dark.png#gh-dark-mode-only" width="120" alt="Logo" align="right">

<div align="center">
  <h3>🎨 Next-Generation macOS-Style Desktop Widgets Suite for Windows</h3>
  <p>Built with Avalonia 11 + .NET 8 · Hardware Acrylic Blur · 3D Liquid Glass Optics · Lockscreen Art-Font Clock · Flexible Desktop Grid Engine</p>
</div>

<h3 align="center">
  <b><a href="https://github.com/Genlue/uWidgetsPlus/releases">Download Latest Release</a></b> ・
  <a href="https://github.com/Genlue/uWidgetsPlus/issues">Report an Issue</a> ・
  <a href="项目解构报告.md">Architecture Report (中文)</a>
</h3>

<div align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-blue?logo=dotnet"/>
  <img src="https://img.shields.io/badge/Avalonia-11.1-purple?logo=avaloniaui"/>
  <img src="https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows"/>
  <img src="https://img.shields.io/badge/Release-Single--File%20EXE-success"/>
  <img src="https://img.shields.io/badge/Installer-MSI%20Package-blue?logo=windows"/>
</div>

<br />

### Languages
<kbd><img src="https://github.com/yammadev/flag-icons/blob/master/png/US.png?raw=true" height="10" /> English</kbd>
<a href="/README.ZH-HANS.md"><kbd><img src="https://github.com/yammadev/flag-icons/blob/master/png/CN.png?raw=true" height="10" /> 中文 (简体)</kbd></a>

---

## 🌟 Key Highlights & Major Enhancements

### 1. 🕒 Frameless Display Clock
- **Full Cell Stretched Bounds**: Numerals directly occupy the entire widget grid unit without card margins or extra font leading gaps ($y=0$ to $y=H$). Supports non-uniform stretching (`StretchFill`) or uniform proportional centering;
- **Curated Mobile Lockscreen Display Fonts**:
  - 🔥 **HarmonyOS Sans Condensed**: Embedded in assembly, purpose-built for dramatic vertical expansion without clipping;
  - 🔥 **Impact**: Heavyweight, bold, punchy classic iOS lockscreen numerals;
  - 🔥 **Bahnschrift (DIN)**: Precision German industrial geometric condensed design;
  - 🔥 **Arial Black**: Ultra-wide heavyweight grotesque sans-serif;
  - 🔥 **Georgia**: Sophisticated contrast editorial lockscreen serif;
  - 🔥 **Century Gothic**: Bauhaus geometric curves;
  - 🔥 **Cascadia Code**, **Ink Free**, **Palatino Linotype**, and all Windows system installed fonts;
- **Full Font-Weight Spectrum**: Seamless selection across 100 Thin to 900 Black;
- **Per-Widget Theme Override**: Select individual theme mode (Follow Global / Acrylic / Liquid Glass / Solid) directly from widget settings;
- **Color Overlay & Native ColorPicker**: Features standard Avalonia `ColorPicker` for color overlay tinting with live bidirectional hex `#RRGGBB` synchronization.

### 2. 💎 Three Deeply Adapted Visual Materials
- 🪟 **Acrylic Blur (OS-Level Live Hardware Blur)**:
  - Utilizes Win32 `ExtCreateRegion` (`RGNDATA`) to dynamically bind numeral glyph scanline spans directly to the HWND region;
  - Windows DWM hardware samples desktop background underneath at **60fps/144fps zero-latency**, tracking Wallpaper Engine live wallpapers and background video playback seamlessly;
  - Optional specular gradient outline rim.
- 💧 **Liquid Glass (3D Optical Refraction Model)**:
  - Euclidean Distance Transform (EDT) computes accurate surface normals across stroke contours, rendering authentic convex/concave lens displacement, chromatic dispersion, 3D specular glints, and bevel lines;
  - **Refined Edge Optics**: Refraction width is strictly clamped to a delicate `1.5dp ~ 4.5dp` rim, keeping numeral centers crystal clear and flat;
  - **Background Pre-Caching Engine**: Asynchronously pre-renders the next minute frame ($T+1\text{m}$) in background threads (`Task.Run` + `CancellationToken`), achieving **instant 0ms cache-hit switching** on the minute tick;
  - **Zero-Leak Automatic Cleanup**: Evicts and disposes expired Bitmaps on every tick; immediately flushes and disposes cached textures upon window move, resize, font/theme change, or widget unload.
- 🎨 **Solid Fill (Vector Anti-Aliased Fill)**:
  - Pure geometric anti-aliased fill with configurable opacity slider and theme accent colors.

### 3. 📐 Advanced Desktop Grid Management
- **Three Placement Modes**:
  - **Manual Grid**: Divides desktop into $m \times n$ square cells, stored as percentages for responsive multi-resolution and DPI adaptation;
  - **Virtual Grid**;
  - **Free Placement**;
- Widgets snap to cells with customizable margins and corner radiuses.

### 4. 🧩 Complete Widget Ecosystem
- ⏰ **Clock**: Analog (3 styles), Digital, World Clock (independent dual/quad face custom city naming + synchronized center digital readout), Frameless Clock;
- 📁 **Folders & Files**: Desktop folder quick launcher, real-time file change monitoring, single-file desktop launcher;
- 🌤️ **Weather**: Smooth horizontal wheel scrolling, 7-day forecast, sunrise/sunset, UV index, air quality (with HTTP proxy support);
- 📊 **System Monitor**: Lightweight single metric dials, multi-dashboard overview (CPU, RAM, Disk, Network, Battery);
- 📝 **Notes**: Quick desktop notes;
- ✅ **Reminders**: Interactive checklist with task counters;
- 🎵 **Music Controls** & 🔍 **Search Utility**.

### 5. 🚀 Standalone Single-File Distribution
- Builds to a self-contained `uWidgets.exe` (~58 MB) with content-hash-verified embedded widget bundle extraction on first launch.

---

## 🛠️ Building from Source

### Prerequisites
- Windows 10 / 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- PowerShell 7 (pwsh) or Windows PowerShell

### Build Command
```powershell
# Clone the repository
git clone https://github.com/Genlue/uWidgetsPlus.git
cd uWidgetsPlus

# Compile and package single-file EXE
powershell -ExecutionPolicy Bypass -File .\build.ps1

# Compile and package Windows standard MSI Installer
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Msi

# Output binary location:
# dist/win-x64/uWidgets.exe
# dist/installer/uWidgetsPlus-1.7.5-win-x64.msi
```

---

## 📄 License & Credits

- Derived and enhanced from [creewick/uWidgets](https://github.com/creewick/uWidgets)
- Licensed under the [MIT License](LICENSE)
- Special thanks to the [Avalonia UI](https://avaloniaui.net/) and [SkiaSharp](https://github.com/mono/SkiaSharp) communities.
