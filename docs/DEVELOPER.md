# DynamicBird 灵动鸟 — 开发者交接文档

> 本文件供新对话快速接手本项目。先读它，再动手。最后更新：2026-08-17（v1.0.2 发布）。

## 1. 项目状态

- **版本**：v1.0.2（GitHub 已发布测试版；商店版未提交，等 v1.1.0 正式版再交）
- **远程**：https://github.com/timecolors/DynamicBird（SSH: git@github.com:timecolors/DynamicBird.git，走 443 端口）
- **master 已推送**，tag v1.0.2 已推送，CI（GitHub Actions）已跑通
- 代码版本号在 `DynamicBird.csproj`（当前 1.0.2）

## 2. 技术栈与架构

- C# / .NET 10 / WPF（net10.0-windows10.0.19041.0），单实例托盘常驻工具
- `src/core/` — 控制器（EdgeTrigger/Visibility/Size/Content）+ 服务（Settings/Shortcuts/Clipboard/Notes/Ai/Tray）+ 日志（Serilog）
- `src/Infrastructure/` — Win32 封装（窗口捕获、Toast 监听、天气、光标输出、更新）
- `src/UI/` — 面板、设置、AI、主题、引导页
- `tests/DynamicBird.Tests/` — xUnit 单元测试（55 个）
- `tools/` — SmokeTest（冒烟）、AiChatProbe（探针）、mock-ai-server.ps1（mock OpenAI SSE）、ScreenshotGen（商店截图）

## 3. 已实现功能（v1.0.2 全量）

- 边缘面板：上下左右+四角分别配置面板（任务栏/应用辅助/小组件/快捷开关/通知/最近/AI），拖拽排序、关闭窗口、拖入固定
- **AI 助手**（左边缘中间默认）：流式对话、多会话（模型生成标题）、文件上传（图片/文本/代码/docx）、复制/重新生成/编辑/导出、上下文感知（按模型窗口裁剪）、**输出到光标**（瞄准模式：点按钮→点目标窗口→回复直接打进 Word/记事本）、单行可展开输入、快捷指令
- 天气（Open-Meteo 免费无 Key + ipwho.is IP 定位 + 城市联想）、状态栏显示项可配置
- 画中画（窗口镜像/嵌入）、媒体控制、剪贴板历史、便签、计时器、计算器
- CI、Serilog 日志、中英双语本地化（resx + LocalizationManager）、引导页

## 4. 环境限制（重要）

- **本机无法直连 github.com:443**（网络封锁，ping 通但 TCP 443 超时）
- **解决办法**：SSH over 443（`~/.ssh/config` 配了 Host github.com → ssh.github.com:443），SSH key 已生成并加到 GitHub（timecolors 账户）
- push 命令：`git push origin master`（走 SSH 443，免密）
- api.github.com 也常不可达（CI 里的自动更新检查在本地会失败，正常）

## 5. 发版流程

```bash
cd "D:\bird\timecolors\DynamicBird - 蓝色大肥鱼 - 副本"
# 改版本号 DynamicBird.csproj + packaging/AppxManifest.xml
git add -A && git commit -m "..."
git push origin master
git tag -f vX.Y.Z && git push origin :refs/tags/vX.Y.Z 2>/dev/null; git push origin vX.Y.Z
# 等 CI：build-test → publish → msix → Draft Release，然后手动 Publish release
```

⚠️ tag 必须指向**包含最新 workflow 修复**的提交（否则 GitHub 用旧 workflow 校验会失败）
⚠️ workflow 需要 `permissions: contents: write` 才能创建 Release

## 6. 已知坑

- **JS 转义**：用工具写代码时 `\b` `\n` 等会被转成控制字符/换行，破坏 C# 字符串或 YAML（v1.0.2 的 workflow 就中过退格字符的招）。写 C# 字符串里的 `\n` 时用数组拼接或双重转义
- **Clipboard.SetText 必须 STA 线程**（输出到光标功能因此不能放 Task.Run）
- **Win11 新记事本无 TextPattern**：输出到光标时降级为粘贴到当前光标（Word/旧记事本可精确恢复位置）
- 剪贴板监听是应用级常驻（MainWindow 启动 StartListening），不是小组件级
- 残留 DynamicBird 进程会锁 exe，构建前先 `Get-Process -Name DynamicBird | Stop-Process -Force`

## 7. 下一步待办

- [ ] **商店版 1.1.0**：版本 +0.1.0 后才交 Microsoft Store（用户明确要求）
- [ ] 划词 AI：选中文字→快捷键→边缘浮窗翻译/解释（纯文本，所有模型支持）
- [ ] 截图提问：需检测模型是否支持识图（多模态），不支持的给提示
- [ ] 商店截图更新：加 AI 面板 + 天气状态栏截图（用 tools/ScreenshotGen）
- [ ] 长线：AI 工具调用（打开应用/读文件，DSH 仿制品方向，需 function calling）

## 8. 常用命令

```bash
dotnet build DynamicBird.sln -c Debug      # 构建
dotnet test tests/DynamicBird.Tests       # 单元测试
dotnet publish -c Release -p:PublishProfile=win-x64  # 发布单文件
# 冒烟测试：tools/SmokeTest（需桌面会话）
# 探针：tools/AiChatProbe（AI 面板 UI 测试，需先配 ai.json + mock 服务器）
```

## 9. 用户偏好

- 中文交流；尊重"商店不交测试版、只交正式版"的发版策略
- 不要擅自改产品名称（灵动鸟 DynamicBird）与版本格式（x.y.z）
- 用户提到过"蓝色大肥鱼"（指当前模型，不支持图像输入，做多模态功能时要注意）
- AI 功能定位：**桌面 AI 能力层**（本地文件分析/光标输出），不是网页聊天替代品