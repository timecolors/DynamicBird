using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShoreHue.Animation;
using ShoreHue.Infrastructure.Utils;
using ShoreHue.UI.Status;

namespace ShoreHue.UI.Widgets.Dynamic
{
    /// <summary>已安装的 C# 插件小组件（manifest + 源码）。</summary>
    public class WidgetPlugin
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Permissions { get; set; } = new();
        /// <summary>分组名（对应 widgets/ 下的子文件夹，如 小组件/面板功能）。</summary>
        public string Group { get; set; } = "小组件";
        [JsonIgnore]
        public string Source { get; set; } = "";
        /// <summary>XAML 形态（可选）：<id>.xaml 界面源码；与 XamlCs 配套，存在时编译走 CompileXaml。</summary>
        [JsonIgnore]
        public string Xaml { get; set; } = "";
        /// <summary>XAML 代码后置（可选）：<id>.xaml.cs（partial class + 事件处理器）。</summary>
        [JsonIgnore]
        public string XamlCs { get; set; } = "";
        /// <summary>是否信任来源（跳过沙箱）。默认 true = 本地代码（本地编程不检测，见 HANDOFF）；
        /// 市场安装/系统内置副本按 manifest 标记。false = 每次加载前过沙箱。</summary>
        [JsonIgnore]
        public bool TrustedSource { get; set; } = true;
        /// <summary>类型（Widget/Panel/Config/Category；无 manifest 的旧文件夹项为空串）。
        /// WidgetSwitcher 只把 Widget 类当作小组件标签，Panel 类走区域面板下拉。</summary>
        [JsonIgnore]
        public string Kind { get; set; } = "";
    }

    /// <summary>
    /// 本地插件仓库：每个小组件一个目录
    /// %LOCALAPPDATA%\ShoreHue\widgets\<id>\（main.cs 源码 + manifest.json 元信息）。
    /// </summary>
    public static class WidgetPluginStore
    {
        /// <summary>校验小组件 id（仅英文/数字/下划线/连字符）。</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 2 || id.Length > 32) return false;
            foreach (char c in id)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-')) return false;
            }
            return true;
        }

        /// <summary>ShoreHue 内置小组件 id（官方随附，删除可能导致运行异常）。</summary>
        private static readonly HashSet<string> _builtinIds = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "timer", "calculator", "clipboard", "note", "textai", "web"
        };

        /// <summary>是否为 ShoreHue 内置文件（官方 author 或内置 id 清单）。</summary>
        public static bool IsBuiltin(WidgetPlugin plugin)
        {
            if (plugin == null) return false;
            if (_builtinIds.Contains(plugin.Id)) return true;
            return string.Equals(plugin.Author, "timecolors", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>权限 → 显示标签。</summary>
        public static string PermissionLabel(string p) => p switch
        {
            "network" => "联网",
            "clipboard" => "剪贴板",
            "file" => "本地文件",
            _ => "无权限"
        };

        /// <summary>列表变化（安装/删除）时触发，供 WidgetSwitcher 重建标签。</summary>
        public static event Action? Changed;

        // ★ 文件夹变化监听：用户在系统文件夹增删文件时，自动刷新小组件列表（双向同步）
        private static FileSystemWatcher? _watcher;
        private static readonly object _watcherLock = new object();

        /// <summary>开始监听小组件文件夹（应用启动时调用一次；Watcher 生命周期随进程）。
        /// ★ 职责边界（2026-09-01 明确）：**只检测文件/目录的增删**（用户手工放 .dbp 包 / .cs
        ///   单文件、删除文件夹 → 海床自动识别更新）。**内容修改不归 watcher 管**：应用内保存
        ///   （Save/SaveNodeToFolder）已显式 Reload+Changed，用户改内容也走海床界面。
        /// ★ 防死循环关键：NotifyFilter **只留 FileName|DirectoryName，去掉 LastWrite**——
        ///   应用自身覆盖写文件（main.cs/manifest.json）不再触发事件，只有新建/删除/重命名触发；
        ///   配合 800ms 防抖 + Reload 期间暂停 + 应用写盘走 WithWatcherSuspended，切断
        ///   "应用写盘 → 事件 → Reload → 又写盘"的自触发链（曾实测 CPU 78%/100% 卡死）。</summary>
        private static System.Threading.Timer? _watchDebounceTimer;
        private static readonly object _watchDebounceLock = new object();
        private static bool _watchDebouncePending;

        /// <summary>开始监听小组件文件夹（应用启动时调用一次；Watcher 生命周期随进程）。
        /// ★ VS Code 风格：文件事件 → 时间窗口聚合（防抖合并，不丢事件）→ 窗口结束统一 Reload。
        ///    - 事件只触发"扫描更新缓存"（ReloadCore 轻量，不编译），编译在使用时按需进行；
        ///    - 应用自身写盘走 WithWatcherSuspended（暂停 watcher），不引发事件链；
        ///    - NotifyFilter 只留 FileName|DirectoryName（去掉 LastWrite），应用覆盖写不触发。
        /// ★ 相比旧版（800ms 直接丢弃后续事件）：聚合窗口内收集全部事件，窗口结束处理一次，
        ///   避免"丢真实变更"；且 Reload 串行化 + 暂停 watcher 防自触发循环（曾实测 CPU 卡死）。</summary>
        public static void StartWatching()
        {
            lock (_watcherLock)
            {
                if (_watcher != null) return;
                try
                {
                    EnsureSkeleton();
                    _watcher = new FileSystemWatcher(RootDir)
                    {
                        IncludeSubdirectories = true,
                        // ★ 只监听增删：内容写入（LastWrite）不触发，应用自身写盘不再引发事件链
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                    };
                    // VS Code 时间窗口聚合：事件 → 标记 pending → 防抖定时器（120ms）到点统一 Reload
                    FileSystemEventHandler handler = (_, _) => ScheduleWatchReload();
                    _watcher.Created += handler;
                    _watcher.Deleted += handler;
                    _watcher.Changed += handler;
                    _watcher.Renamed += (_, _) => ScheduleWatchReload();
                    _watcher.EnableRaisingEvents = true;
                }
                catch { }
            }
        }

        /// <summary>聚合 watcher 事件：窗口内标记 pending，定时器到点统一 Reload（不丢事件、不风暴）。</summary>
        private static void ScheduleWatchReload()
        {
            lock (_watchDebounceLock)
            {
                if (_watchDebouncePending) return;   // 已有挂起的刷新，只重置定时器
                _watchDebouncePending = true;
            }
            _watchDebounceTimer?.Dispose();
            _watchDebounceTimer = new System.Threading.Timer(_ =>
            {
                lock (_watchDebounceLock) _watchDebouncePending = false;
                try
                {
                    // ★ 暂停 watcher：Reload 的目录扫描会产生文件系统事件，防"事件→Reload→事件"死循环
                    if (_watcher != null) _watcher.EnableRaisingEvents = false;
                    try
                    {
                        Reload();
                        Changed?.Invoke();
                    }
                    finally
                    {
                        if (_watcher != null) _watcher.EnableRaisingEvents = true;
                    }
                }
                catch { }
            }, null, 120, System.Threading.Timeout.Infinite);   // 120ms 窗口聚合
        }

        /// <summary>
        /// 应用自身写盘路径的统一包装：写盘期间暂停 watcher，避免应用的新建/删除/写文件
        /// 触发 watcher 事件链。应用内保存已显式 Reload+Changed，watcher 只需响应**用户**的增删。
        /// watcher 未启动（如 Seeder 在 StartWatching 之前运行）时无副作用。
        /// </summary>
        public static void WithWatcherSuspended(Action action)
        {
            if (action == null) return;
            var watcher = _watcher;
            if (watcher == null) { action(); return; }
            try { watcher.EnableRaisingEvents = false; }
            catch { }
            try { action(); }
            finally
            {
                try { watcher.EnableRaisingEvents = true; } catch { }
            }
        }

        /// <summary>海床项目文件夹根（= 海床树的物理投影）：小组件/面板设计/动画/... 各分组子文件夹。</summary>
        public static string RootDir => Path.Combine(AppPaths.DataRoot, "seabed");

        /// <summary>旧版本 widgets/ 目录 → seabed/ 迁移（首次运行执行一次）。</summary>
        private static void MigrateLegacyWidgetsDir()
        {
            try
            {
                string old = Path.Combine(AppPaths.DataRoot, "widgets");
                if (Directory.Exists(old) && !Directory.Exists(RootDir))
                {
                    Directory.Move(old, RootDir);
                }
            }
            catch { }
        }

        private static List<WidgetPlugin>? _cache;

        public static List<WidgetPlugin> Installed
        {
            get
            {
                if (_cache == null) Reload();
                return _cache ?? new List<WidgetPlugin>();
            }
        }

        // ★ Reload 串行化：watcher 恢复后会在线程池线程触发 Reload，与 UI 线程的
        //   Save/Delete/Installed 并发——文件移动/解包/目录扫描不能并行（会互相踩文件）。
        private static readonly object _reloadLock = new object();

        public static void Reload()
        {
            lock (_reloadLock)
            {
                ReloadCore();
                // ★ 状态栏/动画插件缓存随 Reload 一起刷新（watcher 增删文件、应用保存后都会走到这里）
                ReloadStatusProviders();
                ReloadAnimations();
            }
        }

        private static void ReloadCore()
        {
            var list = new List<WidgetPlugin>();
            try
            {
                EnsureSkeleton();   // 不存在时创建分组骨架
                // ★ 分组 = 根目录下的子文件夹（小组件/面板功能/面板设计/动画/外观/交互/状态栏）
                foreach (var groupDir in Directory.GetDirectories(RootDir))
                {
                    string group = Path.GetFileName(groupDir);
                    // ① 分组目录下的 .cs 单文件 → 归一化为 <id>/main.cs（自动包裹）
                    foreach (var csFile in Directory.GetFiles(groupDir, "*.cs"))
                    {
                        try { NormalizeSingleCs(csFile, groupDir); } catch { }
                    }
                    // ② 分组目录下的 .dbp 包 → 自动解包为 <id>/ 目录
                    foreach (var dbpFile in Directory.GetFiles(groupDir, "*.dbp"))
                    {
                        try { NormalizeDbp(dbpFile, groupDir); } catch { }
                    }
                    // ③ 标准目录（main.cs + manifest.json；或 XAML 形态 <id>.xaml + <id>.xaml.cs）
                    foreach (var dir in Directory.GetDirectories(groupDir))
                    {
                        try
                        {
                            string id = Path.GetFileName(dir);
                            string dirName = Path.GetFileName(dir);
                            string main = Path.Combine(dir, "main.cs");
                            bool hasMain = File.Exists(main);
                            // ★ XAML 形态：<id>.xaml + <id>.xaml.cs（无 main.cs 时也可运行，走 CompileXaml）
                            string xamlFile = Path.Combine(dir, dirName + ".xaml");
                            string xamlCsFile = Path.Combine(dir, dirName + ".xaml.cs");
                            bool hasXaml = File.Exists(xamlFile) && File.Exists(xamlCsFile);
                            if (!hasMain && !hasXaml) continue;   // 目录里既无 main.cs 也无 XAML → 跳过
                            string source = hasMain ? File.ReadAllText(main) : "";
                            string xaml = hasXaml ? File.ReadAllText(xamlFile) : "";
                            string xamlCs = hasXaml ? File.ReadAllText(xamlCsFile) : "";
                            string name = id, author = "", desc = "";
                            var perms = new List<string>();
                            string mf = Path.Combine(dir, "manifest.json");
                            bool isSystem = false;
                            bool? trustedFromManifest = null;
                            string kind = "";
                            if (File.Exists(mf))
                            {
                                // ★★★ 2026-09-01 修复（重构回归）：新写盘端（BuiltinTemplateSeeder /
                                //   SaveNodeToFolder）用小写 Dictionary 键写 manifest（"name"/"system"/"kind"…），
                                //   而旧写盘端（WidgetPluginStore.Save 序列化 WidgetManifest）是 PascalCase 键
                                //   （"Name"/"System"…）。默认 Deserialize 大小写敏感 → 小写键全部匹配失败 →
                                //   system/kind 恒空 → 内置模板不被跳过、面板功能被当小组件编译（13 标签布局
                                //   循环卡死 + 激活 2.9s 冷编译）。必须大小写不敏感，兼容两种格式。
                                var m = JsonSerializer.Deserialize<WidgetManifest>(File.ReadAllText(mf),
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                if (m != null)
                                {
                                    if (!string.IsNullOrEmpty(m.Name)) name = m.Name;
                                    author = m.Author ?? "";
                                    desc = m.Description ?? "";
                                    perms = m.Permissions ?? new List<string>();
                                    isSystem = m.System;
                                    trustedFromManifest = m.TrustedSource;
                                    kind = m.Kind ?? "";
                                }
                            }
                            // ★ 跳过 ShoreHue 内置副本（system 标记）：只作文件夹展示，不当作可安装/可删除的小组件
                            //   （内置功能由代码类提供，文件夹副本是给用户看的镜像）
                            if (isSystem) continue;
                            // ★ 信任判定：manifest 显式标记优先；内置副本/无标记的本地文件默认信任
                            //   （本地编程不检测；内置模板构建时已验证，且面板功能合理使用 Process.Start/FileInfo 等
                            //   被黑名单覆盖的 API——无条件沙箱会把它们全拦掉，见 2026-08 误杀回归）
                            bool trusted = trustedFromManifest ?? true;
                            list.Add(new WidgetPlugin
                            {
                                Id = id, Name = name, Author = author, Description = desc,
                                Permissions = perms, Group = group, Source = source,
                                Xaml = xaml, XamlCs = xamlCs,
                                TrustedSource = trusted, Kind = kind
                            });
                        }
                        catch { }
                    }
                }
            }
            catch { }
            _cache = list.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            ShoreHue.Core.Infrastructure.Logging.LogManager.Debug($"[插件] Installed {_cache.Count} 个: " + string.Join(",", _cache.Select(p => p.Id + "(" + (p.Kind ?? "?") + ")")));
        }

        public static WidgetPlugin? GetById(string id) => Installed.FirstOrDefault(p => p.Id == id);

        // ============================================================
        //  自定义状态栏显示项（seabed/状态栏/，IStatusProvider）
        // ============================================================

        private static Dictionary<string, IStatusProvider>? _statusProviders;
        private static readonly Dictionary<string, IStatusProvider> EmptyStatusProviders = new();

        /// <summary>已编译的自定义状态栏显示项：key = "status_&lt;id&gt;"，value = IStatusProvider 实例。
        /// 复用 Installed（ReloadCore 已扫描全部分组并解析 manifest），只过滤「状态栏」分组。</summary>
        public static IReadOnlyDictionary<string, IStatusProvider> StatusProviders
        {
            get
            {
                if (_statusProviders == null) ReloadStatusProviders();
                return _statusProviders ?? EmptyStatusProviders;
            }
        }

        /// <summary>重新扫描「状态栏」分组并编译全部 IStatusProvider（失败项跳过并记日志）。</summary>
        public static void ReloadStatusProviders()
        {
            var map = new Dictionary<string, IStatusProvider>();
            try
            {
                foreach (var plugin in Installed)
                {
                    if (plugin.Group != "状态栏") continue;
                    // manifest kind 过滤：显式非 StatusProvider 的类型跳过（缺省按分组放行）
                    if (!string.IsNullOrEmpty(plugin.Kind) && plugin.Kind != "StatusProvider") continue;
                    // ★ 沙箱只对市场来源（TrustedSource=false）执行（与小组件一致）
                    if (!plugin.TrustedSource)
                    {
                        string sandboxErr = WidgetCompiler.SandboxErrors(plugin.Source);
                        if (sandboxErr.Length > 0)
                        {
                            ShoreHue.Core.Infrastructure.Logging.LogManager.Warning(
                                $"状态栏插件 [{plugin.Id}] 被沙箱拦截: {sandboxErr}");
                            continue;
                        }
                    }
                    // ★ id 前缀 status_ 隔离编译缓存，避免与小组件 Widget_ 同名程序集冲突
                    string cacheId = "status_" + plugin.Id;
                    var (provider, err) = WidgetCompiler.Compile<IStatusProvider>(cacheId, plugin.Source);
                    if (provider != null) map[cacheId] = provider;
                    else ShoreHue.Core.Infrastructure.Logging.LogManager.Warning(
                        $"状态栏插件 [{plugin.Id}] 编译失败: {err}");
                }
            }
            catch { }
            _statusProviders = map;
            ShoreHue.Core.Infrastructure.Logging.LogManager.Debug($"[插件] 状态栏编译成功 {map.Count} 个: " + string.Join(",", map.Keys));
        }

        // ============================================================
        //  自定义动画（seabed/动画/，IAnimation）
        // ============================================================

        private static Dictionary<string, IAnimation>? _animations;
        private static readonly Dictionary<string, IAnimation> EmptyAnimations = new();

        /// <summary>已编译的自定义动画：key = 动画 Id（实例 Id，缺省用插件 id），value = IAnimation 实例。
        /// 同时注册进 AnimationRegistry（ShapeAnimator 运行时查表用）。</summary>
        public static IReadOnlyDictionary<string, IAnimation> Animations
        {
            get
            {
                if (_animations == null) ReloadAnimations();
                return _animations ?? EmptyAnimations;
            }
        }

        /// <summary>重新扫描「动画」分组并编译全部 IAnimation；注册表同步重建（清空后重新注册）。</summary>
        public static void ReloadAnimations()
        {
            var map = new Dictionary<string, IAnimation>();
            try
            {
                foreach (var plugin in Installed)
                {
                    if (plugin.Group != "动画") continue;
                    if (!string.IsNullOrEmpty(plugin.Kind) && plugin.Kind != "Animation") continue;
                    if (!plugin.TrustedSource)
                    {
                        string sandboxErr = WidgetCompiler.SandboxErrors(plugin.Source);
                        if (sandboxErr.Length > 0)
                        {
                            ShoreHue.Core.Infrastructure.Logging.LogManager.Warning(
                                $"动画插件 [{plugin.Id}] 被沙箱拦截: {sandboxErr}");
                            continue;
                        }
                    }
                    string cacheId = "anim_" + plugin.Id;
                    var (anim, err) = WidgetCompiler.Compile<IAnimation>(cacheId, plugin.Source);
                    if (anim == null)
                    {
                        ShoreHue.Core.Infrastructure.Logging.LogManager.Warning(
                            $"动画插件 [{plugin.Id}] 编译失败: {err}");
                        continue;
                    }
                    // ★ 注册表/设置存储都用动画实例的 Id（缺省回退文件夹 id）——GetResolvedShowAnimationType
                    //   返回的就是这个 Id，ShapeAnimator 据此查表；Name 用于设置页 ComboBox 展示。
                    string key = string.IsNullOrEmpty(anim.Id) ? plugin.Id : anim.Id;
                    if (!map.ContainsKey(key)) map[key] = anim;
                }
            }
            catch { }
            _animations = map;
            ShoreHue.Core.Infrastructure.Logging.LogManager.Debug($"[插件] 动画编译成功 {map.Count} 个: " + string.Join(",", map.Keys));
            // ★ 注册表与缓存同源：ShapeAnimator 只依赖 AnimationRegistry（动画命名空间），
            //   不反向依赖 UI 层，避免分层耦合。
            ShoreHue.Animation.AnimationRegistry.ReplaceAll(map);
        }

        /// <summary>保存（新建或覆盖）。返回错误信息，成功为空串。</summary>
        public static string Save(WidgetPlugin plugin)
        {
            if (string.IsNullOrWhiteSpace(plugin.Id) || !IsValidId(plugin.Id))
                return "Id 无效：仅允许英文/数字/下划线/连字符（2-32 字符）";
            if (string.IsNullOrWhiteSpace(plugin.Source))
                return "源码为空";
            try
            {
                // ★ 应用自身写盘：暂停 watcher，避免写文件触发事件链（watcher 只响应**用户**的增删）
                WithWatcherSuspended(() =>
                {
                    // ★ 写入分组子目录（默认「小组件」；与文件夹扫描一致）
                    string group = string.IsNullOrEmpty(plugin.Group) ? "小组件" : plugin.Group;
                    string groupPath = Path.Combine(RootDir, group);
                    Directory.CreateDirectory(groupPath);
                    string dir = Path.Combine(groupPath, plugin.Id);
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "main.cs"), plugin.Source);
                    var m = new WidgetManifest
                    {
                        Name = plugin.Name, Author = plugin.Author,
                        Description = plugin.Description, Permissions = plugin.Permissions,
                        TrustedSource = plugin.TrustedSource
                    };
                    File.WriteAllText(Path.Combine(dir, "manifest.json"),
                        JsonSerializer.Serialize(m, new JsonSerializerOptions { WriteIndented = true }));
                });
                Reload();
                Changed?.Invoke();
                return "";
            }
            catch (Exception ex)
            {
                return "保存失败：" + ex.Message;
            }
        }

        public static bool Delete(string id)
        {
            try
            {
                // ★ 应用自身删除：暂停 watcher，避免删除目录触发事件链（watcher 只响应**用户**的增删）
                bool deleted = false;
                WithWatcherSuspended(() =>
                {
                    // ★ 支持分组路径：从所有分组子目录中查找
                    if (!Directory.Exists(RootDir)) return;
                    foreach (var groupDir in Directory.GetDirectories(RootDir))
                    {
                        string dir = Path.Combine(groupDir, id);
                        if (Directory.Exists(dir))
                        {
                            Directory.Delete(dir, true);
                            deleted = true;
                            return;
                        }
                    }
                });
                if (deleted)
                {
                    // ★ 清编译缓存，释放 widget 实例与程序集引用
                    WidgetCompiler.Evict(id);
                    Reload();
                    Changed?.Invoke();
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>创建分组骨架（不存在时）：根目录 + 各分组子文件夹，方便用户直接放文件。</summary>
        private static void EnsureSkeleton()
        {
            MigrateLegacyWidgetsDir();
            if (!Directory.Exists(RootDir)) Directory.CreateDirectory(RootDir);
            string[] groups = { "小组件", "面板功能", "面板设计", "动画", "外观", "交互", "状态栏" };
            foreach (var g in groups)
            {
                string d = Path.Combine(RootDir, g);
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            }
        }

        /// <summary>把分组目录下的 .cs 单文件归一化为 &lt;name&gt;/main.cs 目录（id = 文件名）。</summary>
        private static void NormalizeSingleCs(string csFile, string groupDir)
        {
            string fileName = Path.GetFileNameWithoutExtension(csFile);
            if (string.IsNullOrEmpty(fileName)) return;
            string id = SanitizeId(fileName);
            if (id.Length < 2) return;
            string targetDir = Path.Combine(groupDir, id);
            if (Directory.Exists(targetDir)) { File.Delete(csFile); return; }   // 已存在同名目录 → 清理重复文件
            Directory.CreateDirectory(targetDir);
            File.Move(csFile, Path.Combine(targetDir, "main.cs"));
        }

        /// <summary>把 .dbp 包解包为 &lt;id&gt;/ 目录（manifest + main.cs + config），然后删除 .dbp。</summary>
        private static void NormalizeDbp(string dbpFile, string groupDir)
        {
            string fileName = Path.GetFileNameWithoutExtension(dbpFile);
            string id = SanitizeId(fileName);
            if (id.Length < 2) return;
            string targetDir = Path.Combine(groupDir, id);
            if (Directory.Exists(targetDir)) { File.Delete(dbpFile); return; }   // 已解包过 → 清理
            Directory.CreateDirectory(targetDir);
            using (var zip = System.IO.Compression.ZipFile.OpenRead(dbpFile))
            {
                foreach (var entry in zip.Entries)
                {
                    string name = Path.GetFileName(entry.FullName);
                    if (string.IsNullOrEmpty(name)) continue;
                    string dest = Path.Combine(targetDir, name);
                    using var src = entry.Open();
                    using var dst = File.Create(dest);
                    src.CopyTo(dst);
                }
            }
            File.Delete(dbpFile);
        }

        /// <summary>文件名 → 合法 id（英文/数字/下划线/连字符）。</summary>
        private static string SanitizeId(string name)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (name ?? ""))
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
                if (sb.Length >= 32) break;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 把海床树的节点落盘到文件夹（用户保存/创建时调用）：
        /// 路径 = seabed/&lt;树路径链&gt;/&lt;节点名&gt;/（与海床树一级/二级/三级结构一致），内含 manifest.json + 内容文件。
        /// manifest.json 是树↔文件夹的桥梁：文件夹里的文件被 ShoreHue 扫描时靠它还原节点。
        /// </summary>
        public static void SaveNodeToFolder(ShoreHue.Core.Models.CustomPanelDefinition cp)
        {
            try
            {
                // ★ 应用自身写盘：暂停 watcher（应用保存已显式 Reload+Changed，watcher 只响应**用户**的增删）
                WithWatcherSuspended(() => SaveNodeToFolderCore(cp));
            }
            catch { }
        }

        private static void SaveNodeToFolderCore(ShoreHue.Core.Models.CustomPanelDefinition cp)
        {
            try
            {
                EnsureSkeleton();
                // ★ 树路径 = 文件夹路径：按 ParentKey 找到父节点在树里的完整路径链（一级/二级/三级）
                string nodeDir;
                var pathChain = ShoreHue.UI.Seabed.ConfigTreeBuilder.FindPathNames(cp.ParentKey ?? "");
                var parts = new System.Collections.Generic.List<string>();
                if (pathChain.Count > 0)
                {
                    // 树路径链直接作为文件夹层级（如 面板设计/面板尺寸/节点名）
                    foreach (var seg in pathChain) parts.Add(SanitizeId(seg));
                }
                else
                {
                    // 兜底：按一级分类映射（节点无内置父链时）
                    parts.Add(MapCategoryToFolder(cp.Category));
                }
                string safeName = SanitizeId(cp.Name);
                if (safeName.Length < 2) return;
                parts.Add(safeName);
                string current = RootDir;
                foreach (var seg in parts)
                {
                    current = Path.Combine(current, seg);
                    Directory.CreateDirectory(current);
                }
                nodeDir = current;

                // manifest.json：完整记录节点元信息（树↔文件夹还原依据）
                var manifest = new Dictionary<string, object?>
                {
                    ["id"] = cp.Id,
                    ["name"] = cp.Name,
                    ["category"] = cp.Category,
                    ["kind"] = cp.Kind ?? "",
                    ["baseType"] = cp.BaseType ?? "",
                    ["parentKey"] = cp.ParentKey ?? "",
                    ["sourceKey"] = cp.SourceKey ?? "",
                    ["createdAt"] = cp.CreatedAt ?? "",
                    ["permissions"] = WidgetPermissions.Detect(cp.Source ?? ""),
                    ["trustedSource"] = cp.TrustedSource
                };
                File.WriteAllText(Path.Combine(nodeDir, "manifest.json"),
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

                // 内容文件：小组件/面板 → main.cs（+ 完全编程 .xaml/.xaml.cs）；配置 → config.json
                if (!string.IsNullOrEmpty(cp.Source))
                    File.WriteAllText(Path.Combine(nodeDir, "main.cs"), cp.Source);
                if (!string.IsNullOrEmpty(cp.Xaml))
                    File.WriteAllText(Path.Combine(nodeDir, safeName + ".xaml"), cp.Xaml);
                if (!string.IsNullOrEmpty(cp.XamlCs))
                    File.WriteAllText(Path.Combine(nodeDir, safeName + ".xaml.cs"), cp.XamlCs);
                if (!string.IsNullOrEmpty(cp.ConfigJson) && cp.ConfigJson != "{}")
                    File.WriteAllText(Path.Combine(nodeDir, "config.json"), cp.ConfigJson);
            }
            catch { }
        }

        /// <summary>
        /// 向已保存节点的海床文件夹写入附加文件（多形态：.xaml / .xaml.cs 等）。
        /// 目录按 分类/节点名 定位；找不到节点目录时静默跳过（仅附加文件，不影响主功能）。
        /// </summary>
        public static void WriteExtraFiles(ShoreHue.Core.Models.CustomPanelDefinition cp,
            System.Collections.Generic.List<ShoreHue.UI.Seabed.GitHubMarketService.PackageFile> files)
        {
            try
            {
                if (files == null || files.Count == 0) return;
                string group = MapCategoryToFolder(cp.Category);
                string safeName = SanitizeId(cp.Name);
                string dir = Path.Combine(RootDir, group, safeName);
                if (!Directory.Exists(dir)) return;
                foreach (var f in files)
                {
                    if (string.IsNullOrWhiteSpace(f.Name) || string.IsNullOrWhiteSpace(f.Content)) continue;
                    string fname = Path.GetFileName(f.Name);
                    if (string.IsNullOrEmpty(fname)) continue;
                    WithWatcherSuspended(() => File.WriteAllText(Path.Combine(dir, fname), f.Content));
                }
            }
            catch { }
        }

        /// <summary>
        /// 读取节点海床文件夹里的 XAML 形态文件（<名字>.xaml + <名字>.xaml.cs）。
        /// 按 分类/节点名 定位；找不到返回空串。用于选中节点时补充加载完全编程代码。
        /// </summary>
        public static (string Xaml, string XamlCs) LoadNodeXaml(ShoreHue.Core.Models.CustomPanelDefinition cp)
        {
            try
            {
                string group = MapCategoryToFolder(cp.Category);
                string safeName = SanitizeId(cp.Name);
                string dir = Path.Combine(RootDir, group, safeName);
                if (!Directory.Exists(dir)) return ("", "");
                string x = "", xc = "";
                string xf = Path.Combine(dir, safeName + ".xaml");
                string xcf = Path.Combine(dir, safeName + ".xaml.cs");
                if (File.Exists(xf)) x = File.ReadAllText(xf);
                if (File.Exists(xcf)) xc = File.ReadAllText(xcf);
                return (x, xc);
            }
            catch { return ("", ""); }
        }

        /// <summary>按 manifest.json 的 id 查找节点文件夹（跨分组）。找不到返回 null。</summary>
        public static string? FindNodeDirById(string customId)
        {
            try
            {
                if (string.IsNullOrEmpty(customId) || !Directory.Exists(RootDir)) return null;
                foreach (var groupDir in Directory.GetDirectories(RootDir))
                {
                    foreach (var dir in Directory.GetDirectories(groupDir))
                    {
                        string mf = Path.Combine(dir, "manifest.json");
                        if (!File.Exists(mf)) continue;
                        try
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllText(mf));
                            if (doc.RootElement.TryGetProperty("id", out var idEl) &&
                                idEl.ValueKind == JsonValueKind.String &&
                                idEl.GetString() == customId)
                                return dir;
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>删除海床节点的文件夹（按 manifest.json 里的 id 匹配，跨分组查找）。</summary>
        public static void DeleteNodeFolder(string customId)
        {
            try
            {
                if (!Directory.Exists(RootDir)) return;
                foreach (var groupDir in Directory.GetDirectories(RootDir))
                {
                    foreach (var dir in Directory.GetDirectories(groupDir))
                    {
                        string mf = Path.Combine(dir, "manifest.json");
                        if (!File.Exists(mf)) continue;
                        try
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllText(mf));
                            if (doc.RootElement.TryGetProperty("id", out var idEl) &&
                                idEl.ValueKind == JsonValueKind.String &&
                                idEl.GetString() == customId)
                            {
                                Directory.Delete(dir, true);
                                return;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>一级分类 → 分组文件夹名（树分类名可能含特殊字符，映射为安全目录名）。</summary>
        public static string MapCategoryToFolder(string category)
        {
            switch (category)
            {
                case "小组件": return "小组件";
                case "面板功能": return "面板功能";
                case "面板设计": return "面板设计";
                case "动画": return "动画";
                case "外观": return "外观";
                case "交互": return "交互";
                case "状态栏": return "状态栏";
                default: return "其他";
            }
        }

        /// <summary>在系统文件管理器中打开指定节点的文件夹（按 manifest.id 匹配，跨分组查找；找不到回退根目录）。</summary>
        public static void OpenNodeFolder(string customId, string name, string category)
        {
            var log = ShoreHue.Core.Infrastructure.Logging.LogManager.Debug;
            log($"[OpenFolder] 调用 customId={customId} name={name} category={category} RootDir={RootDir}");
            try
            {
                if (!Directory.Exists(RootDir)) { log("[OpenFolder] RootDir 不存在"); return; }
                // ① 按 manifest.id 精确定位
                if (!string.IsNullOrEmpty(customId))
                {
                    foreach (var groupDir in Directory.GetDirectories(RootDir))
                    {
                        foreach (var dir in Directory.GetDirectories(groupDir))
                        {
                            string mf = Path.Combine(dir, "manifest.json");
                            if (!File.Exists(mf)) continue;
                            try
                            {
                                using var doc = JsonDocument.Parse(File.ReadAllText(mf));
                                if (doc.RootElement.TryGetProperty("id", out var idEl) &&
                                    idEl.ValueKind == JsonValueKind.String &&
                                    idEl.GetString() == customId)
                                {
                                    log($"[OpenFolder] ① manifest.id 命中 dir={dir}");
                                    OpenFolderInExplorer(dir);
                                    return;
                                }
                            }
                            catch { }
                        }
                    }
                }
                // ② 回退：按 分组/节点名 定位
                string group = MapCategoryToFolder(category);
                string safeName = SanitizeId(name);
                string path = Path.Combine(RootDir, group, safeName);
                log($"[OpenFolder] ①未命中，尝试② path={path} exists={Directory.Exists(path)}");
                if (Directory.Exists(path))
                {
                    OpenFolderInExplorer(path);
                    return;
                }
                // ③ 兜底：先尝试分组目录（内置节点无独立文件夹 → 打开所属分组，用户能看到该分组所有文件）
                string groupPath = Path.Combine(RootDir, group);
                log($"[OpenFolder] ②未命中，尝试③ groupPath={groupPath} exists={Directory.Exists(groupPath)}");
                if (Directory.Exists(groupPath))
                {
                    OpenFolderInExplorer(groupPath);
                    return;
                }
                // ④ 最终兜底：打开根目录
                log("[OpenFolder] ③未命中，兜底打开根目录");
                OpenFolder();
            }
            catch (Exception ex) { log("[OpenFolder] 异常: " + ex); }
        }

        /// <summary>在系统文件管理器中打开小组件根目录（用户可直接增删/拖文件）。</summary>
        public static void OpenFolder()
        {
            try
            {
                EnsureSkeleton();
                OpenFolderInExplorer(RootDir);
            }
            catch { }
        }

        /// <summary>用 explorer.exe 显式打开文件夹（比 UseShellExecute 直接给目录更可靠，点击必生效）。</summary>
        private static void OpenFolderInExplorer(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + path + "\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private class WidgetManifest
        {
            public string? Name { get; set; }
            public string? Author { get; set; }
            public string? Description { get; set; }
            public List<string>? Permissions { get; set; }
            /// <summary>ShoreHue 内置副本标记（只展示，不可当作用户小组件安装/删除）。</summary>
            public bool System { get; set; }
            /// <summary>信任来源标记：false = 市场来源，加载前过沙箱；缺省/true = 本地代码直接加载。</summary>
            public bool? TrustedSource { get; set; }
            /// <summary>类型（Widget/Panel/Config/Category）。</summary>
            public string? Kind { get; set; }
        }
    }
}
