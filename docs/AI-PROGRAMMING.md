# ShoreHue AI 编程指南（海床）

> 本文档说明：用 AI（如 DeepSeek 网页版）为ShoreHue编写功能时，**能做什么、不能做什么、怎么做**。
> 配套：海床界面 → 小组件/面板功能/状态栏/动画 节点 → 「复制 AI 提示词」按钮获取每类模板。
> 文件放入：`%LOCALAPPDATA%\ShoreHue\seabed\` 对应分类文件夹，自动生效。

---

## 一、总览：能做什么，不能做什么

### 能做的（AI 编程生态）

| 分类 | 放哪 | 文件格式 | 支持完全编程(XAML)? | 效果 |
|------|------|---------|------|------|
| **小组件** | 海床根\面板\小组件\<名字>\ | main.cs，或 .xaml + .xaml.cs + manifest.json | ✅ | 出现在小组件面板标签 |
| **面板功能** | 海床根\面板\面板功能\<名字>\ | main.cs，或 .xaml + .xaml.cs + manifest.json | ✅ | 可作为区域面板（需在设置选区域，显示为 Custom:面板id） |
| **状态栏显示项** | 海床根\状态栏\<名字>\ | main.cs + manifest.json | ❌（仅简单编程） | 出现在状态栏（内置项之后） |
| **动画** | 海床根\动画\<名字>\ | main.cs + manifest.json | ❌（仅简单编程） | 出现在设置→动画 的类型下拉，可选作呼出/隐藏动画 |
| **配置调整** | （海床界面内编辑） | JSON 赋值代码 | — | 修改动画/外观/交互/状态栏参数 |

> 找不到目录时：在海床界面选中对应节点 →「打开文件夹」直达真实位置。

### 两种编程模式（海床「复制 AI 提示词」旁可选）

海床界面「复制 AI 提示词」按钮旁有模式选择：

| 模式 | 产出 | 适用 | 可用分类 |
|------|------|------|------|
| **简单编程**（默认） | 纯 C# 单文件（main.cs） | 快速功能、逻辑为主、界面简单 | 全部 4 类 |
| **完全编程（完全编译）** | XAML + 代码后置（.xaml + .xaml.cs） | 界面复杂、需要精细布局/样式/事件 | 小组件、面板功能 |

- **简单编程**：AI 生成 main.cs（全 C# 构建 UI），放入 <分类>/<名字>/ 生效；
- **完全编程 = 完整 WPF 用户控件编译**：AI 生成 <名字>.xaml + <名字>.xaml.cs 两文件
  （.xaml.cs 里 `public partial class 名字 : UserControl, IWidget`，`CreateView() => this`），
  放入 面板/小组件 或 面板/面板功能 的同名文件夹生效；
- 系统检测到同目录 .xaml + .xaml.cs 就走 **XAML 完全编译**（XAML 与代码后置编入同一程序集，
  事件 / 绑定 / 样式 / 触发器全部可用）；否则回退纯代码编译；
- 界面提供**实时预览**：保存后立即重新编译并刷新，编译报错内联显示（可直接贴回 AI 修改）。
- 状态栏 / 动画只有 main.cs 一种形态（无 XAML），系统会自动强制简单模式。

### 不能做的（边界）

1. **不能修改ShoreHue内核**：边缘检测、面板容器、窗口动画系统、托盘、设置框架是写死的——AI 编的插件只能"长在"内核上，不能改内核行为。
2. **不能碰危险系统 API**（市场发布时会被沙箱拦截）：进程操作、反射、注册表、WMI、全局钩子、模拟输入、屏幕截图、文件写入等。本地自用可以（有弹窗提示），但上架市场不行。
3. **不能做常驻后台任务**：插件只有激活时运行（面板显示/状态栏挂载期间），面板隐藏后 OnDeactivated 会被调用，必须停掉定时器。
4. **动画不能无限自激**：动画驱动必须用 DispatcherTimer 且动画结束就停，禁止长期订阅渲染帧（否则 100% CPU 卡死）。

---

## 二、4 类功能教程

### 1. 小组件（最常见）

**目录**：seabed\小组件\<英文名>\main.cs + manifest.json

**接口契约**（AI 提示词里已给出，要点）：
```csharp
public class 你的类 : UserControl, ShoreHue.UI.Widgets.IWidget
{
    public string Name => "显示名";              // 中文
    public UserControl CreateView() => this;     // 返回 this
    public void OnActivated() { }                // 激活时（启动定时器等）
    public void OnDeactivated() { }              // 切走时（停止定时器）
}
```

**要点**：
- **简单模式**：全部用 C# 代码构建 UI（new StackPanel/TextBlock/Button...），不要 XAML
- **完全模式**：用 .xaml + .xaml.cs 写真实 WPF 布局（见上节「完全编程」），XAML 里可写事件/绑定/样式
- 背景建议深色 #1E1E1E、文字白色（浅色主题下由窗口整体负责）；也可在 XAML 里自由设计
- 定时器用 DispatcherTimer（自动回 UI 线程）
- 联网用 HttpClient + 超时，异常 catch 显示友好提示

**manifest.json**：
```json
{
  "id": "你的英文id",
  "name": "中文名",
  "kind": "Widget",
  "category": "小组件",
  "permissions": []
}
```

### 2. 面板功能

**目录**：seabed\面板功能\<名字>\main.cs + manifest.json（kind = "Panel"）

**与小组件区别**：同样实现 IWidget，但它是"整块面板"而非小组件标签。放入后需在 **设置 → 区域 → 面板类型** 里选它作为某区域的显示内容（选后显示为 "Custom:面板id"）。

### 3. 状态栏显示项

**目录**：seabed\状态栏\<名字>\main.cs + manifest.json（kind = "StatusProvider"）

**接口**：
```csharp
public class 你的类 : ShoreHue.UI.Status.IStatusProvider
{
    public string Name => "CPU 温度";       // 中文名
    public string IconText => "☀";        // 图标（文本符号或文字）
    public string GetText() => "65°C";      // 每秒调用，必须毫秒级返回
    public void OnActivated() { }            // 挂载时
    public void OnDeactivated() { }          // 卸载时（停定时器）
    public bool IsEnabled(ISettingsService s) => true;
}
```

**要点**：
- **不要创建 UI 控件**——系统自动生成「图标+文本」布局，插件只提供数据
- GetText() 每秒调用，禁止耗时操作（WMI/网络放后台线程缓存结果）
- 定时器用 DispatcherTimer，OnDeactivated 必须 Stop

### 4. 动画

**目录**：seabed\动画\<名字>\main.cs + manifest.json（kind = "Animation"）

**接口**：
```csharp
public class 你的类 : ShoreHue.Animation.IAnimation
{
    public string Name => "弹跳";                    // 中文，设置下拉展示
    public string Id => "bounce";                    // 英文唯一标识
    public void AnimateShow(FrameworkElement panel, Window window, double ms, Action onCompleted) { }
    public void AnimateHide(FrameworkElement panel, Window window, double ms, Action onCompleted) { }
}
```

**要点（★ 渲染帧热路径，必须遵守）**：
- 用 DispatcherTimer 逐帧驱动（每帧约 16ms），Tick 里推进进度
- **禁止直接订阅 CompositionTarget.Rendering 而不取消**——会导致 100% CPU 卡死（项目历史教训）
- 进度 = elapsed/ms，到 1 时调 onCompleted（**只调一次**）并停定时器
- 位置动画改 window.Left/Top，透明度动画改 panel.Opacity
- 隐藏动画必须把 panel.Opacity 降到 0
- 所有异常 catch 并仍调 onCompleted（系统有超时兜底）

---

## 三、工作流程

1. **进入海床**：设置 → 海床（编程模式）→ 选择要添加的分类节点（小组件/面板功能/状态栏/动画）
2. **选模式**：小组件/面板功能可在「复制 AI 提示词」旁切换 **简单编程 / 完全编程**（状态栏与动画固定简单编程）
3. **复制提示词**：点「复制 AI 提示词」→ 粘贴到 AI（DeepSeek 等），描述你想要的效果
4. **AI 生成代码**：简单模式输出 main.cs；完全模式输出 .xaml + .xaml.cs（+ manifest.json）
5. **放入文件夹**：把文件存入对应目录（完全模式两个文件必须同名同目录）
6. **自动生效 + 实时预览**：watcher 检测 → 自动编译 → 出现在对应位置并刷新预览；完全模式的报错会内联显示
7. **有报错**：把编译报错信息粘贴回 AI，让它修正（提示词已包含报错处理指引）

**小技巧**：在海床界面选中某节点点「打开文件夹」，可直接定位到该节点目录，方便放文件。

---

## 四、常见错误与排查

| 症状 | 原因 | 解决 |
|------|------|------|
| 小组件不出现 | main.cs 编译失败 | 看日志 "小组件 [id] 编译失败: ..."；把报错贴回 AI |
| 状态栏项不出现 | 接口方法没实现全 / GetText 抛异常 | 检查是否实现 5 个成员；GetText catch 返回 "--" |
| 动画在设置里没有 | Id 冲突或编译失败 | 换唯一 Id；看日志 "动画编译失败" |
| 面板显示 "编译失败" | 源码有问题 | 看日志；确保实现 IWidget 全部成员 |
| XAML 报错 x:Class 不匹配 | .xaml 的 x:Class 与 .xaml.cs 的 partial class 名字不一致 | 两个文件用同一英文类名 |
| XAML 报错根元素错误 | .xaml 根不是 UserControl | 小组件/面板必须根为 UserControl（不要 Window） |
| XAML 找不到类型/命名空间 | 代码后置少了 using | 检查 .xaml.cs 顶部 using；界面引用的自定义样式请内联在窗口资源里 |
| 面板/状态栏闪退 | 定时器没在 OnDeactivated 停止 | 必须 DispatcherTimer + OnDeactivated Stop |
| CPU 100% 卡死 | 动画/更新循环自激 | 动画禁止常驻渲染帧订阅；确保动画结束停定时器 |

**日志位置**：%LOCALAPPDATA%\ShoreHue\Logs\log-*.log（搜索 "编译失败" / "沙箱"）
