# ShoreHue AI 编程指南（海床）

> 本文档说明：用 AI（如 DeepSeek 网页版）为ShoreHue编写功能时，**能做什么、不能做什么、怎么做**。
> 配套：海床界面 → 小组件/面板功能/状态栏/动画 节点 → 「复制 AI 提示词」按钮获取每类模板。
> 文件放入：`%LOCALAPPDATA%\ShoreHue\seabed\` 对应分类文件夹，自动生效。

---

## 一、总览：能做什么，不能做什么

### 能做的（AI 编程生态）

| 分类 | 放哪 | 文件格式 | 效果 |
|------|------|---------|------|
| **小组件** | seabed\小组件\<名字>\ | main.cs（+可选 .xaml）+ manifest.json | 出现在小组件面板标签 |
| **面板功能** | seabed\面板功能\<名字>\ | main.cs + manifest.json | 可作为区域面板（需在设置选区域） |
| **状态栏显示项** | seabed\状态栏\<名字>\ | main.cs + manifest.json | 出现在状态栏（内置项之后） |
| **动画** | seabed\动画\<名字>\ | main.cs + manifest.json | 出现在设置→动画 的类型下拉，可选作呼出/隐藏动画 |
| **配置调整** | （海床界面内编辑） | JSON 赋值代码 | 修改动画/外观/交互/状态栏参数 |

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
- 全部用 C# 代码构建 UI（new StackPanel/TextBlock/Button...），不要 XAML
- 背景深色 #1E1E1E、文字白色，与系统一致
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
2. **复制提示词**：点「复制 AI 提示词」→ 粘贴到 AI（DeepSeek 等），描述你想要的效果
3. **AI 生成代码**：AI 输出完整 C# 源码（实现对应接口）
4. **放入文件夹**：把代码存为 seabed\<分类>\<名字>\main.cs（+ 同目录 manifest.json）
5. **自动生效**：watcher 检测 → 编译 → 出现在对应位置（小组件标签/状态栏/动画下拉）
6. **有报错**：把编译报错信息粘贴回 AI，让它修正（提示词已包含报错处理指引）

**小技巧**：在海床界面选中某节点点「打开文件夹」，可直接定位到该节点目录，方便放文件。

---

## 四、常见错误与排查

| 症状 | 原因 | 解决 |
|------|------|------|
| 小组件不出现 | main.cs 编译失败 | 看日志 "小组件 [id] 编译失败: ..."；把报错贴回 AI |
| 状态栏项不出现 | 接口方法没实现全 / GetText 抛异常 | 检查是否实现 5 个成员；GetText catch 返回 "--" |
| 动画在设置里没有 | Id 冲突或编译失败 | 换唯一 Id；看日志 "动画编译失败" |
| 面板显示 "编译失败" | 源码有问题 | 看日志；确保实现 IWidget 全部成员 |
| 面板/状态栏闪退 | 定时器没在 OnDeactivated 停止 | 必须 DispatcherTimer + OnDeactivated Stop |
| CPU 100% 卡死 | 动画/更新循环自激 | 动画禁止常驻渲染帧订阅；确保动画结束停定时器 |

**日志位置**：%LOCALAPPDATA%\ShoreHue\Logs\log-*.log（搜索 "编译失败" / "沙箱"）
