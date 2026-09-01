# 本地化指南（中英双语）

ShoreHue使用 **resx 资源文件 + 运行时切换** 实现本地化，无需第三方库。

## 架构

| 组件 | 位置 | 作用 |
|---|---|---|
| `Strings.resx` | `src/UI/Localization/` | 中性资源（默认 = 中文） |
| `Strings.en-US.resx` | `src/UI/Localization/` | 英文覆盖资源 |
| `LocalizationManager` | `src/UI/Localization/LocalizationManager.cs` | 运行时按 `CultureInfo.CurrentUICulture` 取字符串；实现 `INotifyPropertyChanged` |
| `Language` 配置项 | `SettingsData` | `zh-CN` / `en-US` / 空=跟随系统 |

启动时 `App.OnStartup` 读取配置中的语言并调用 `LocalizationManager.Instance.SetCulture(lang)`。

## XAML 中使用（推荐）

```xml
xmlns:loc="clr-namespace:ShoreHue.UI.Localization"
...
<TextBlock Text="{Binding Item[WidgetTabs_Timer], Source={x:Static loc:LocalizationManager.Instance}}"/>
```

- 用 `{Binding Item[资源Key], Source={x:Static ...}}` 绑定
- 切换语言后所有绑定自动刷新（`PropertyChanged("Item")`）
- 注意：不要用自定义 MarkupExtension 返回 Binding —— BAML 不会对它做绑定特判，会抛
  `"System.Windows.Data.Binding"不是属性"Text"的有效值`（本项目已验证并放弃该方案）

## 代码中使用

```csharp
using ShoreHue.UI.Localization;
...
var text = LocalizationManager.Instance["Timer_FooterHint"];
```

## 新增一个字符串

1. 在 `Strings.resx`（中文）与 `Strings.en-US.resx`（英文）各加一条 `<data name="Key">`
2. XAML/代码引用该 Key
3. 缺失翻译时返回 Key 本身（便于发现漏译）

## 新增一种语言

1. 复制 `Strings.resx` 为 `Strings.xx-XX.resx` 并翻译
2. 在 `ShoreHue.csproj` 的 EmbeddedResource 区追加对应条目（LogicalName 规则同 en-US）
3. 在设置页的「语言」下拉中添加该语言选项（设置 → 常规 → 语言）
