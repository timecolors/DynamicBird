# ShoreHue 海岸线 — 安全说明 / Security

> 本文档如实说明 ShoreHue 的安全模型、已实现的防线、已知边界与威胁模型。
> 目标：让用户与安全研究者能在**事实**基础上评估本项目的风险，而非猜测。
> 最后更新：2026-09

---

## 一、威胁模型（本项目防什么 / 不防什么）

ShoreHue 允许用户**从在线市场安装功能包**，也允许**在本地海床编写/由 AI 生成 C# 与 XAML 代码并编译执行**。
这是本产品"可编程外骨骼"的核心能力，也是安全设计的出发点。

- **防御目标 A**：市场下载的第三方功能包、AI 生成的代码，**不得**访问文件系统写入、注册表、进程管理、
  反射/动态执行、网络监听、输入注入等敏感能力——除非用户明确授权。
- **防御目标 B**：用户的 API 密钥、本地数据**不得**未经用户操作就被外发。
- **明确不防**：用户**主动**粘贴到 AI 输入框的内容、用户**手动**授予的权限、
  以及"用户本人就是攻击者"（本地代码编辑器永远无法防住所有者自己）——这不在任何沙箱的责任范围内。

---

## 二、已实现的防线（可对照源码核实）

### 1. 动态编译双层沙箱 — src/UI/Widgets/Dynamic/WidgetCompiler.cs

市场来源代码（TrustedSource=false）编译前经**两道独立检查**，任何一道命中即拒绝编译：

**第一层：文本扫描（CheckSandbox）**
- 词边界匹配危险 API 名（防子串误伤，如 Dispatcher.Invoke 不误判）。

**第二层：符号级检查（CheckSandboxSymbols）— 关键防线**
- 使用 Roslyn 编译管道，在**语法符号层**（而非字符串）检查引用。
- 类型级黑名单（命中即整类型拦截）：
  - System.Diagnostics.Process（进程启动/管理）
  - System.Reflection（反射——换皮绕过的主要通道）
  - System.Runtime.InteropServices（P/Invoke 原生调用）
  - System.Management（WMI）、System.DirectoryServices（AD）
  - Microsoft.Win32.Registry*（注册表）
  - System.IO.FileStream / StreamWriter / BinaryWriter / RandomAccess / FileInfo / DirectoryInfo（写流与文件系统信息）
  - System.Windows.Forms.SendKeys（输入注入）、TextWriterTraceListener / EventLog / PerformanceCounter、System.AppDomain
- 成员级拦截：System.IO.File 的 Write/Append/Open/Create/Delete/Move/Copy 等写操作全部拦截（读允许）；
  System.Environment.Exit/FailFast 等终止类调用拦截。
- **符号级为何不可绕过**：只要代码引用了被拦截类型/成员，无论怎么改名、加壳、间接调用，
  符号表里都会出现该类型 → 必然命中。这是与"关键词黑名单"的本质区别。

> ★ 有意设计：Clipboard（剪贴板）不硬拦——它是权限声明类（见下），且剪贴板小组件是核心功能；
> 是否允许由安装时的权限检测 + 用户确认决定，而非编译期一刀切。

### 2. 权限检测与安装确认 — src/UI/Widgets/Dynamic/WidgetPermissions.cs

- 功能包上传/导出市场时，自动检测源码所需的 7 类权限：
  network / clipboard / file / process / system / window / screen。
- **安装时重新检测**（不信任包内声明的 manifest）→ 风险权限弹窗逐项确认 → 用户同意后才落盘。
- 不信任声明、不静默授予：权限以"当前源码实际检测结果"为准。

### 3. 密钥与敏感数据保护

- **API Key 不以明文落盘**：磁盘上以 **DPAPI（DataProtectionScope.CurrentUser）加密** 存储
  （src/core/Services/Ai/AiSettingsStore.cs、GitHubMarketService.cs 的 github_token.dat 同机制）；
  解密仅在需要发起请求时于内存中进行。
- **凭证不进模型上下文**：GitHub/服务商的凭证请求独立于 AI 聊天会话，不拼接进发送给模型的提示词。
- **AI 请求内容边界**：发送给 AI 的只有——用户手动输入框的文本、用户主动拖入的图片/文件。
  剪贴板历史（ClipboardManager.History）是独立数据流，仅供剪贴板小组件使用，**不进入 AI 请求**。
  AI 面板对剪贴板的唯一操作是 Clipboard.SetText（把回答复制出去）。

### 4. 数据本地化

- 配置、剪贴板历史、会话记录、预设、海床源码全部存于 %LOCALAPPDATA%\ShoreHue，不自动上传。
- 联网行为仅发生在用户主动触发的场景：AI 请求、市场浏览/下载、更新检查。

---

## 三、已知边界（诚实声明，不回避）

1. **沙箱是编译期静态防线，不是运行时进程隔离**。
   它拦截"直接引用危险类型"的代码，但理论上无法 100% 证明任意代码在运行期不借助
   极端技巧（如利用宿主自身暴露的合法 API 链）造成越权。当前拦截面覆盖已知的
   反射/PInvoke/进程/文件写等全部主流逃逸通道，并在持续补洞（历史补洞记录见源码注释）。
   **未来方向**：将市场插件/第三方代码迁移到独立低权限子进程 + IPC 网关，实现运行时强制隔离。
2. **权限检测是启发式**：文本特征匹配可能漏报或误报，以安装时用户确认为最终裁决。
3. **AI 内容外发取决于用户**：用户主动粘贴的内容会按所选服务商发送；请勿向 AI 输入机密信息。
4. **Windows 系统级集成**（钩子/窗口消息）依赖系统行为，杀软可能误报，这是此类工具的共性。

---

## 四、报告漏洞 / 安全反馈

- 请通过 GitHub Issues（标签 security）私密描述问题，或发送邮件至仓库主页维护者邮箱。
- 建议附带：复现步骤、受影响版本、缓解建议。我们承诺及时响应并在修复后致谢（可选署名）。
