# ShoreHue 海岸线 隐私政策

最后更新：2026-08-17

## 1. 简介

ShoreHue 海岸线（ShoreHue）是一款 Windows 桌面边缘面板工具，由 TideHue 开发。本政策说明应用在运行时处理哪些数据，以及这些数据如何存储。

## 2. 我们收集什么

ShoreHue **不要求注册、不要求登录、不含广告、不含第三方分析 SDK**。默认情况下，它不会向任何服务器上传你的个人数据。

### 本地数据（仅存储在你自己的电脑上）

以下数据只保存在本机 `%LOCALAPPDATA%\ShoreHue` 目录中，用于让应用记住你的设置和内容：

- 配置（`config.json`）：面板位置、大小、触发方式、开关状态等设置
- 快捷方式与最近使用记录：任务栏快捷方式、最近打开的程序/文件/网页
- 剪贴板历史、便签、计时器/闹钟等小组件数据
- **AI 助手数据**：`ai.json`（服务商地址、模型、系统提示词、API Key）、`ai_sessions.json`（多会话对话历史，含你上传的图片/文件内容）。API Key 与对话内容**只保存在本机**，卸载时一并清除
- 日志文件（`Logs\`）：用于问题排查，可能包含窗口标题、进程名等调试信息

### 网络请求（仅在可选功能启用时发生）

- **自动更新**：启用后，应用会向 GitHub Releases API 请求最新版本信息，并在必要时下载更新包。GitHub 的服务器日志可能记录你的 IP 地址、User-Agent 和访问时间，这部分数据由 GitHub 处理（见 [GitHub 隐私声明](https://docs.github.com/zh/site-policy/privacy-policies/github-privacy-statement)），ShoreHue 本身不保存这些信息。
- **常用网页**：仅在面板展示时读取你电脑上浏览器（Microsoft Edge / Google Chrome）的本地历史数据库，用于显示最近打开的网页；不会上传这些数据。
- **AI 对话（可选）**：仅当你启用 AI 助手并填写服务商信息后，对话内容（含拖入的图片/文件文本）会由本机**直接发送到你选择的大模型服务商**（如 DeepSeek、OpenAI、本地 Ollama 等），不经过 ShoreHue 的任何服务器。发送的数据受该服务商隐私政策约束；你可在设置中随时关闭 AI 或删除本地对话历史。

### 系统信息

CPU、内存、音量等系统状态仅在面板内本地显示，不收集、不上传。

## 3. 数据存储与删除

- 所有本地数据位于 `%LOCALAPPDATA%\ShoreHue`。
- 商店（Microsoft Store）版卸载应用时，该目录中的数据会一并清除。
- GitHub 版删除该目录即可完全清除所有数据。
- ShoreHue 不收集、不存储云端数据，因此没有云端删除流程。

## 4. 第三方服务

- **GitHub Releases**：仅用于可选的自动更新功能，受 [GitHub 隐私声明](https://docs.github.com/zh/site-policy/privacy-policies/github-privacy-statement) 约束。
- **AI 模型服务商**（可选）：启用 AI 助手后，对话请求直连你选择的服务商。
- 除此之外，ShoreHue 不集成任何第三方 SDK 或在线服务。

## 5. 儿童隐私

ShoreHue不面向儿童提供服务，也不包含任何收集儿童信息的内容。

## 6. 政策变更

本政策如有变更，会在本页面更新并注明生效日期。

## 7. 联系我们

如有疑问，可通过 GitHub 仓库提交 Issue：[timecolors/ShoreHue](https://github.com/timecolors/ShoreHue)
