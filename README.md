# 🕊️ DynamicBird 灵动鸟

<p align="center">
  <img src="packaging/Assets/qr-icon-source.png" width="128" height="128" alt="DynamicBird 灵动鸟"/>
</p>

<p align="center">
  <b>Dynamic Island-style edge panel & taskbar replacement for Windows</b><br/>
  一款 Windows 桌面边缘面板工具：把常用功能收纳到屏幕边缘，鼠标滑过即呼出，用完自动隐去。
</p>

<p align="center">
  <a href="https://github.com/timecolors/DynamicBird/releases">GitHub Releases</a> ·
  <a href="https://apps.microsoft.com/detail/9PBR9CCTQXXN">Microsoft Store</a> ·
  <a href="docs/PRIVACY.md">隐私政策 Privacy Policy</a>
</p>

---

## ✨ 功能特性 / Features

### 边缘任务栏 / Edge Taskbar
- 在屏幕边缘提供快捷方式与运行中的窗口，支持**拖拽排序**、**关闭窗口**、**拖入快捷方式**
- Shortcuts and running windows at the screen edge, with drag-and-drop ordering, window close, and drag-to-pin.

### 应用辅助 / App Assistant
- 与 Windows 应用联动：**媒体控制**（QQ 音乐等）、**窗口镜像 / 窗口嵌入（画中画）**、**本地视频播放**
- Works hand-in-hand with Windows apps: media control (QQ Music, etc.), window mirroring / embedding (picture-in-picture), and local video playback.

### 小组件 / Widgets
- **剪贴板历史**、**便签**、**计时器 / 倒计时**、**标准 / 科学 / 程序员计算器**
- Clipboard history, notes, countdown timer, and standard / scientific / programmer calculator.

### 快捷开关 / Quick Toggles
- **音量与亮度**、**蓝牙**、**Wi-Fi**、**移动热点**、**省电模式**、快速打开 Windows 设置
- Volume & brightness, Bluetooth, Wi-Fi, mobile hotspot, battery saver, and quick access to Windows Settings.

### 通知坞与最近使用 / Notification Dock & Recent Items
- 聚合系统通知；一键打开最近使用的程序、文件与网页
- Aggregate system notifications; reopen recently used apps, files, and web pages in one click.

### 可配置面板 / Configurable Panels
- 屏幕上下左右与四角可**分别配置不同面板**，支持**跟随鼠标 / 固定贴边**两种模式
- Each edge and corner can show a different panel, with follow-mouse or fixed-edge modes.
- 动画顺滑、自适应尺寸、多显示器支持
- Smooth animations, adaptive sizing, and multi-monitor support.

> 所有数据仅保存在本机 `%LOCALAPPDATA%\DynamicBird`，不上传云端；完全免费、无广告。
> All data stays on your device — no cloud uploads, no ads, completely free.

---

## 📸 截图 / Screenshots

| 边缘任务栏 | 应用辅助（画中画） |
| --- | --- |
| ![Edge Taskbar](assets/screenshots/01-Taskbar.png) | ![App Assistant](assets/screenshots/02-AppHelper.png) |

| 小组件 - 计算器 | 小组件 - 计时器 |
| --- | --- |
| ![Widget Calculator](assets/screenshots/03-Widget-Calculator.png) | ![Widget Timer](assets/screenshots/04-Widget-Timer.png) |

---

## 📥 安装 / Installation

### Microsoft Store（推荐）
在 Microsoft Store 搜索 **DynamicBird 灵动鸟**，或直接访问：
<https://apps.microsoft.com/detail/9PBR9CCTQXXN>

### GitHub Releases
从 [Releases](https://github.com/timecolors/DynamicBird/releases) 下载最新版本。

> 要求：Windows 10 1809（10.0.17763）及以上，x64。商店版与 GitHub 版数据目录统一，可无缝切换。

---

## 🚀 快速上手 / Quick Start

1. 启动灵动鸟后，它会驻留在系统托盘。
2. 将鼠标移到屏幕边缘或角落，即可呼出对应面板：
   - **上 / 下 / 左 / 右边缘**：任务栏、应用辅助、小组件等（默认按位置分配）
   - **四角**：快捷开关（左上）、最近使用（左下）、通知坞（右下）；右上角不呼出，避免影响关闭窗口
3. 鼠标离开面板后自动隐藏；也可在设置中调整每个区域的面板类型、尺寸与触发行为。

---

## 🛠 从源码构建 / Build from Source

前置要求：Windows 10/11、[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。

```bash
git clone https://github.com/timecolors/DynamicBird.git
cd DynamicBird

# 调试运行
dotnet run

# 发布单文件（win-x64）
dotnet publish -c Release -p:PublishProfile=win-x64

# 打包 MSIX（商店版，需要 Windows SDK）
.\packaging\build-msix.ps1
```

---

## 🧰 技术栈 / Tech Stack

- C# / .NET 10 / WPF（Windows Presentation Foundation）
- [NAudio](https://github.com/naudio/NAudio)（音量控制 / 音频设备）
- System.Management（系统信息）
- Microsoft.Data.Sqlite（本地数据）
- MSIX 打包，支持 Microsoft Store 分发

第三方组件完整声明见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

---

## 🔒 隐私 / Privacy

灵动鸟不收集、不上传个人数据。所有配置、剪贴板历史、便签等数据仅保存在本机
`%LOCALAPPDATA%\DynamicBird`，卸载商店版时一并清除。详见 [docs/PRIVACY.md](docs/PRIVACY.md)。

---

## 📄 许可证 / License

[MIT](LICENSE) © 2026 TideHue
