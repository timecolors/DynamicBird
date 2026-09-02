// ==================== 海床 = 内置文件资源管理器（文件夹=真相） ====================
// 树 = seabed 目录的真实扫描结果；点文件读真文件、保存写回、删除按选中项（文件/目录）走回收站；
// 编译/AI 提示词/应用/变体等海床能力全部作用在「目录 + manifest」上，能力不减。
// 外部改动（磁盘内容变化）保存前提示覆盖；IO 失败（占用/只读/杀软锁）重试并给出错误，不静默吞。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShoreHue.UI.Settings.Pages
{
    public partial class SeabedPage
    {
        // ===== 资源管理器状态 =====
        private readonly System.Collections.Generic.HashSet<string> _expandedDirs = new(StringComparer.OrdinalIgnoreCase);
        private string? _fsPath;                 // 当前选中（文件或目录，绝对路径）
        private bool _fsIsDir;
        private string? _fsXamlPath;             // 完全编程：.xaml 文件路径（可空）
        private string? _fsXamlCsPath;           // 完全编程：.xaml.cs 文件路径（可空）
        private readonly Dictionary<string, string> _fsSnapshot = new(StringComparer.OrdinalIgnoreCase);   // 打开时磁盘内容快照（外部改动检测）

        private static string ExplorerRoot => ShoreHue.UI.Widgets.Dynamic.WidgetPluginStore.RootDir;

        private bool _treeRefreshQueued;   // 防抖：watcher 连续事件合并为一次重建

        /// <summary>widget 插件仓库变化（用户增删文件/目录）→ 树刷新（合并、回 UI 线程）。</summary>
        private void OnWidgetStoreChanged()
        {
            if (!IsLoaded) return;
            if (_treeRefreshQueued) return;
            _treeRefreshQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _treeRefreshQueued = false;
                try { LoadExplorerTree(); } catch { }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnSeabedPageUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ShoreHue.UI.Widgets.Dynamic.WidgetPluginStore.Changed -= OnWidgetStoreChanged;
        }

        /// <summary>海床树：加载（文件资源管理器视图，LoadTree 统一入口）。</summary>
        private void LoadTree() => LoadExplorerTree();

        /// <summary>树 = seabed 目录扫描（展开的目录显示子目录/文件），与资源管理器一致。</summary>
        private void LoadExplorerTree()
        {
            ResetArm();
            var nodes = new List<FlatNode>();
            try
            {
                if (Directory.Exists(ExplorerRoot))
                {
                    foreach (var g in Directory.GetDirectories(ExplorerRoot)
                                 .OrderBy(p => Path.GetFileName(p), StringComparer.CurrentCultureIgnoreCase))
                    {
                        nodes.Add(new FlatNode { Level = 0, DisplayOverride = Path.GetFileName(g), FsPath = g, FsIsDir = true });
                        if (_expandedDirs.Contains(g)) ScanExplorerDir(g, nodes, 1);
                    }
                }
            }
            catch { }
            _flatNodes = nodes;
            lstConfigTree.ItemsSource = _flatNodes;
        }

        /// <summary>展开目录时列出其子目录 + 文件（文件仅当该目录本身已展开才显示其自身文件）。</summary>
        private void ScanExplorerDir(string dir, List<FlatNode> nodes, int level)
        {
            if (level > 3) return;
            try
            {
                // 子目录行
                foreach (var sub in Directory.GetDirectories(dir)
                             .OrderBy(p => Path.GetFileName(p), StringComparer.CurrentCultureIgnoreCase))
                {
                    string kind = ReadManifestField(sub, "kind") ?? "";
                    bool sys = string.Equals(ReadManifestField(sub, "system"), "True", StringComparison.OrdinalIgnoreCase)
                               || ReadManifestField(sub, "system") == "true";
                    nodes.Add(new FlatNode
                    {
                        Level = level,
                        DisplayOverride = Path.GetFileName(sub),
                        FsPath = sub,
                        FsIsDir = true,
                        FsIsSystem = sys,
                        FsKind = kind,
                        FsIsConfigDir = kind == "Config",
                        FsManifestId = ReadManifestField(sub, "id"),
                        HasDelete = true
                    });
                    if (_expandedDirs.Contains(sub) && level < 3) ScanExplorerDir(sub, nodes, level + 1);
                }
                // 本目录文件行（manifest.json 也展示，只读元信息）
                foreach (var f in Directory.GetFiles(dir)
                             .OrderBy(p => Path.GetFileName(p), StringComparer.CurrentCultureIgnoreCase))
                {
                    bool isManifest = string.Equals(Path.GetFileName(f), "manifest.json", StringComparison.OrdinalIgnoreCase);
                    nodes.Add(new FlatNode
                    {
                        Level = level,
                        DisplayOverride = Path.GetFileName(f),
                        FsPath = f,
                        FsIsDir = false,
                        FsKind = FsKindOf(dir),
                        HasDelete = !isManifest
                    });
                }
            }
            catch { }
        }

        /// <summary>目录的 manifest kind（无 manifest 时按父目录推断：小组件→Widget、面板功能→Panel、动画→Animation、状态栏→StatusProvider）。</summary>
        private static string? FsKindOf(string dir)
        {
            string? k = ReadManifestField(dir, "kind");
            if (!string.IsNullOrEmpty(k)) return k;
            string group = Path.GetFileName(Path.GetDirectoryName(dir) ?? "") ?? "";
            return group switch
            {
                "小组件" => "Widget",
                "面板功能" => "Panel",
                "动画" => "Animation",
                "状态栏" => "StatusProvider",
                _ => null
            };
        }

        private static string? ReadManifestField(string dir, string name)
        {
            try
            {
                string mf = Path.Combine(dir, "manifest.json");
                if (!File.Exists(mf)) return null;
                using var doc = JsonDocument.Parse(File.ReadAllText(mf));
                if (doc.RootElement.TryGetProperty(name, out var v))
                    return v.ValueKind == JsonValueKind.String ? v.GetString()
                        : v.ValueKind == JsonValueKind.True ? "true"
                        : v.ValueKind == JsonValueKind.False ? "false" : v.GetRawText();
            }
            catch { }
            return null;
        }

        /// <summary>目录切换展开/收起；文件则读入编辑器。</summary>
        private void OnExplorerRow(FlatNode fn)
        {
            _fsPath = fn.FsPath;
            _fsIsDir = fn.FsIsDir;
            if (fn.FsIsDir)
            {
                // 目录：切换展开 → 重建树（资源管理器语义：单击展开/收起）
                if (!_expandedDirs.Remove(fn.FsPath)) _expandedDirs.Add(fn.FsPath);
                LoadExplorerTree();
                lstConfigTree.SelectedItem = null;
                // ★ 编辑区目录状态：提示该目录能力（保留已打开文件不打断编辑）
                txtNodeTitle.Text = Path.GetFileName(fn.FsPath);
                txtNodeHint.Text = fn.FsIsConfigDir
                    ? "配置目录：点开 config.json 编辑，或用「应用」写回设置"
                    : (Directory.GetFiles(fn.FsPath).Any(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                        ? "功能目录：点文件编辑；右键可新建文件夹/重命名/删除"
                        : "（空）右键新建文件夹，或放入代码文件即成为功能");
                txtJsonStatus.Text = "";
                return;
            }
            // 文件：读入编辑器
            OpenExplorerFile(fn.FsPath!);
            lstConfigTree.SelectedItem = null;
        }

        // ==================== VS Code 式文件操作（右键菜单） ====================

        private static FlatNode? CtxNode(object sender)
        {
            return (sender as System.Windows.FrameworkElement)?.DataContext as FlatNode;
        }

        /// <summary>右键所在目录：目录行=自身；文件行=所在目录。</summary>
        private static string? CtxDirOf(FlatNode fn) => fn.FsIsDir ? fn.FsPath : Path.GetDirectoryName(fn.FsPath);

        /// <summary>右键 → 新建文件夹（命名）。</summary>
        private void Fs_NewFolder_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var fn = CtxNode(sender);
            string? parent = fn != null ? CtxDirOf(fn) : null;
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent)) { txtJsonStatus.Text = "请右键一个目录以在其中新建文件夹"; return; }
            var dlg = new InputDialog("海床 · 新建文件夹", "在「" + Path.GetFileName(parent) + "」下新建文件夹，输入名称：", "新功能");
            dlg.Owner = System.Windows.Window.GetWindow(this);
            if (dlg.ShowDialog() != true) return;
            string name = dlg.ResultText.Trim();
            if (string.IsNullOrEmpty(name)) { txtJsonStatus.Text = "名称不能为空"; return; }
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { txtJsonStatus.Text = "名称含非法字符"; return; }
            string target = Path.Combine(parent, name);
            if (Directory.Exists(target) || File.Exists(target)) { txtJsonStatus.Text = "同名已存在：" + name; return; }
            try
            {
                Directory.CreateDirectory(target);
                _expandedDirs.Add(parent);   // 展开父目录让新文件夹可见
                LoadExplorerTree();
                txtJsonStatus.Text = "已新建文件夹：" + name + "（放入代码文件即成为功能）";
            }
            catch (Exception ex) { txtJsonStatus.Text = "新建失败：" + ex.Message; }
        }

        /// <summary>右键 → 重命名（文件/文件夹）。</summary>
        private void Fs_Rename_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var fn = CtxNode(sender);
            if (fn?.FsPath == null) return;
            string src = fn.FsPath;
            string oldName = Path.GetFileName(src);
            string parent = Path.GetDirectoryName(src)!;
            var dlg = new InputDialog("海床 · 重命名", "输入新名称：", oldName);
            dlg.Owner = System.Windows.Window.GetWindow(this);
            if (dlg.ShowDialog() != true) return;
            string name = dlg.ResultText.Trim();
            if (string.IsNullOrEmpty(name) || name == oldName) return;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { txtJsonStatus.Text = "名称含非法字符"; return; }
            string target = Path.Combine(parent, name);
            if (Directory.Exists(target) || File.Exists(target)) { txtJsonStatus.Text = "同名已存在：" + name; return; }
            try
            {
                if (fn.FsIsDir) Directory.Move(src, target); else File.Move(src, target);
                // 目录重命名 → 同步 manifest.name（id 保持稳定）；文件级重命名不动 manifest
                if (fn.FsIsDir)
                {
                    string mf = Path.Combine(target, "manifest.json");
                    if (File.Exists(mf))
                    {
                        try
                        {
                            string json = File.ReadAllText(mf);
                            var m = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
                            if (m != null) { m["name"] = name; File.WriteAllText(mf, System.Text.Json.JsonSerializer.Serialize(m, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); }
                        }
                        catch { }
                    }
                }
                if (_fsPath == src) { _fsPath = null; txtNodeTitle.Text = ""; }
                _expandedDirs.Add(parent);
                LoadExplorerTree();
                txtJsonStatus.Text = "已重命名：" + oldName + " → " + name;
            }
            catch (Exception ex) { txtJsonStatus.Text = "重命名失败：" + ex.Message; }
        }

        /// <summary>右键 → 删除（文件/目录，走回收站，与行末 ✕ 一致）。</summary>
        private void Fs_Delete_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var fn = CtxNode(sender);
            if (fn?.FsPath != null) TryArmOrDelete(fn, null);   // 两击确认（无弹窗）
        }

        /// <summary>右键 → 在资源管理器中打开（目录=自身；文件=所在目录）。</summary>
        private void Fs_OpenDir_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var fn = CtxNode(sender);
            string? target = fn != null ? CtxDirOf(fn) : null;
            if (string.IsNullOrEmpty(target) || !Directory.Exists(target)) { txtJsonStatus.Text = "目标目录不存在"; return; }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + target + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { txtJsonStatus.Text = "打开失败：" + ex.Message; }
        }

        /// <summary>按扩展名把文件读入对应编辑器（完全编程双框 / 简单单框 / config.json JSON）。</summary>
        private void OpenExplorerFile(string path)
        {
            try
            {
                string name = Path.GetFileName(path);
                _fsPath = path;
                _fsIsDir = false;
                txtNodeTitle.Text = name;
                _fsXamlPath = null;
                _fsXamlCsPath = null;
                _fsSnapshot.Clear();

                if (name.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase))
                {
                    // 完全编程：.xaml + 兄弟 .xaml.cs
                    SetProgMode(1);
                    string dir = Path.GetDirectoryName(path)!;
                    string xamlPath = path;
                    string csPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + ".xaml.cs");
                    string xaml = File.ReadAllText(xamlPath);
                    string cs = File.Exists(csPath) ? File.ReadAllText(csPath) : "";
                    txtXamlEditor.Text = xaml;
                    txtXamlCsEditor.Text = cs;
                    _fsXamlPath = xamlPath;
                    _fsXamlCsPath = File.Exists(csPath) ? csPath : null;
                    _fsSnapshot[xamlPath] = xaml;
                    if (_fsXamlCsPath != null) _fsSnapshot[_fsXamlCsPath] = cs;
                    txtNodeHint.Text = "完全编程（XAML + 代码后置），保存 = 写回目录中的真实文件";
                }
                else if (name.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase))
                {
                    SetProgMode(1);
                    string dir = Path.GetDirectoryName(path)!;
                    string csPath = path;
                    string xamlPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + ".xaml");
                    string cs = File.ReadAllText(csPath);
                    string xaml = File.Exists(xamlPath) ? File.ReadAllText(xamlPath) : "";
                    txtXamlEditor.Text = xaml;
                    txtXamlCsEditor.Text = cs;
                    _fsXamlPath = File.Exists(xamlPath) ? xamlPath : null;
                    _fsXamlCsPath = csPath;
                    _fsSnapshot[csPath] = cs;
                    if (_fsXamlPath != null) _fsSnapshot[_fsXamlPath] = xaml;
                    txtNodeHint.Text = "完全编程（代码后置 + XAML），保存 = 写回目录中的真实文件";
                }
                else
                {
                    // 简单模式：.cs / main.cs / config.json / manifest.json（manifest 只读展示）
                    SetProgMode(0);
                    string content = File.ReadAllText(path);
                    txtJsonEditor.Text = content;
                    _fsSnapshot[path] = content;
                    txtNodeHint.Text = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        ? (name == "manifest.json" ? "元信息（只读展示）" : "配置 JSON，保存 = 写回该文件")
                        : "C# 源码（简单编程），保存 = 写回该文件";
                }
                txtJsonStatus.Text = "";
                txtJsonEditor.Visibility = System.Windows.Visibility.Visible;
                xamlEditorPanel.Visibility = System.Windows.Visibility.Collapsed;
                UpdateEditorVisibility();   // 按模式恢复显示
            }
            catch (Exception ex)
            {
                txtJsonStatus.Text = "打开失败：" + ex.Message;
            }
        }

        /// <summary>切换编程模式（0=简单，1=完全），不触发任何保存（海床控件已排除自动保存）。</summary>
        private void SetProgMode(int idx)
        {
            if (cmbProgMode != null && cmbProgMode.SelectedIndex != idx) cmbProgMode.SelectedIndex = idx;
        }

        // ==================== 保存：写回真实文件 ====================

        /// <summary>「保存文件」：把当前编辑器内容写回选中文件（或 XAML 双文件）；外部改动先提示；IO 失败重试并报错。</summary>
        private void BtnSaveFile_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_fsPath == null || _fsIsDir) { txtJsonStatus.Text = "请先在左侧选中一个文件"; return; }
            try
            {
                var targets = new List<(string Path, string Content)>();
                if (_fsXamlPath != null || _fsXamlCsPath != null)
                {
                    if (_fsXamlPath != null) targets.Add((_fsXamlPath, txtXamlEditor.Text ?? ""));
                    if (_fsXamlCsPath != null) targets.Add((_fsXamlCsPath, txtXamlCsEditor.Text ?? ""));
                }
                else
                {
                    targets.Add((_fsPath, txtJsonEditor.Text ?? ""));
                }

                // ★ 外部改动检测：磁盘内容与打开快照不同、且不等于当前编辑内容 → 提示覆盖（VS Code 语义）
                foreach (var t in targets)
                {
                    if (_fsSnapshot.TryGetValue(t.Path, out var snap) && snap != t.Content)
                    {
                        string disk = File.ReadAllText(t.Path);
                        if (disk != snap && disk != t.Content)
                        {
                            var r = System.Windows.MessageBox.Show(
                                Path.GetFileName(t.Path) + " 已在磁盘上被修改，是否仍要覆盖？",
                                "海床", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                            if (r != System.Windows.MessageBoxResult.Yes) { txtJsonStatus.Text = "已取消保存（保留磁盘版本）"; return; }
                        }
                    }
                }

                foreach (var t in targets)
                {
                    WriteFileWithRetry(t.Path, t.Content);
                    _fsSnapshot[t.Path] = t.Content;
                }
                txtJsonStatus.Text = "已保存：" + string.Join("、", targets.Select(t => Path.GetFileName(t.Path)));
            }
            catch (Exception ex)
            {
                txtJsonStatus.Text = "保存失败：" + ex.Message;
                System.Windows.MessageBox.Show("保存失败：\n" + ex.Message + "\n\n若文件被占用（如已在其他程序打开）或为只读，请关闭占用后重试。",
                    "海床", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>写文件（应用自身写盘走 watcher 挂起防事件链；IO 占用重试 3 次）。</summary>
        private void WriteFileWithRetry(string path, string content)
        {
            ShoreHue.UI.Widgets.Dynamic.WidgetPluginStore.WithWatcherSuspended(() =>
            {
                Exception? last = null;
                for (int i = 0; i < 3; i++)
                {
                    try { File.WriteAllText(path, content, new System.Text.UTF8Encoding(false)); return; }
                    catch (IOException ex) { last = ex; System.Threading.Thread.Sleep(300); }
                    catch (UnauthorizedAccessException ex) { last = ex; System.Threading.Thread.Sleep(300); }
                }
                if (last != null) throw last;
            });
        }

        // ==================== 删除：两击确认（与全项目「再点一次删除」一致，无弹窗），走回收站 ====================

        /// <summary>两击删除入口：第一击进入 3 秒确认态（行末 ✕ 变「再点一次删除」，右键删除以状态提示）；
        /// 3 秒内再次点击同一项 → 真正删除（回收站，可恢复）。内置项在状态里提示。</summary>
        private void TryArmOrDelete(FlatNode fn, System.Windows.Controls.Button? btn)
        {
            if (fn.FsPath == null) return;
            if (string.Equals(fn.FsPath, ExplorerRoot, StringComparison.OrdinalIgnoreCase))
            {
                txtJsonStatus.Text = "不能删除海床根目录";
                return;
            }
            if (IsArmed(fn.FsPath))
            {
                ExplorerDeleteCore(fn);   // 第二击：执行
                return;
            }
            if (btn != null) ArmDelete(btn, fn.FsPath);   // 行末 ✕：按钮进入「再点一次删除」
            else ArmPathNoButton(fn.FsPath);              // 右键删除：状态栏进入确认态
            bool system = fn.FsIsSystem || IsUnderSystemDir(fn.FsPath);
            txtJsonStatus.Text = "已选择删除「" + Path.GetFileName(fn.FsPath) + "」，3 秒内再点一次确认（进入回收站）"
                + (system ? "；注意：这是 ShoreHue 内置项" : "");
        }

        /// <summary>无行末按钮的确认态（右键删除）：记录 + 3 秒自动还原。</summary>
        private void ArmPathNoButton(string id)
        {
            ResetArm();
            _armDeleteId = id;
            _armDeleteAt = DateTime.Now;
            var t = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3),
                IsEnabled = true
            };
            t.Tick += (_, _) => ResetArm();
            _armDeleteTimer = t;
        }

        /// <summary>执行删除：文件→删文件；目录→删整个目录。回收站优先，占用重试，失败仅状态提示（无弹窗）。</summary>
        private void ExplorerDeleteCore(FlatNode fn)
        {
            if (fn.FsPath == null) return;
            // 根目录保护
            if (string.Equals(fn.FsPath, ExplorerRoot, StringComparison.OrdinalIgnoreCase)) { txtJsonStatus.Text = "不能删除海床根目录"; return; }
            string disp = Path.GetFileName(fn.FsPath);

            try
            {
                DeleteToRecycle(fn.FsPath, fn.FsIsDir);
                // 同步派生索引：若该目录 manifest.id 是海床自定义项（custom_*），从 CustomPanels 移除
                string? mid = fn.FsIsDir ? ReadManifestField(fn.FsPath, "id") : null;
                if (!string.IsNullOrEmpty(mid) && mid.StartsWith("custom_", StringComparison.Ordinal))
                {
                    var list = _settings.CustomPanels;
                    list.RemoveAll(p => p.Id == mid);
                    _settings.CustomPanels = list;
                }
                _expandedDirs.Remove(fn.FsPath);
                if (_fsPath == fn.FsPath) { _fsPath = null; txtJsonEditor.Text = ""; txtXamlEditor.Text = ""; txtXamlCsEditor.Text = ""; txtNodeTitle.Text = ""; }
                LoadExplorerTree();
                txtJsonStatus.Text = "已删除（回收站）：" + disp;
            }
            catch (Exception ex)
            {
                txtJsonStatus.Text = "删除失败：" + ex.Message + "（若文件被其他程序占用，请关闭后重试）";
            }
        }

        private static bool IsUnderSystemDir(string path)
        {
            string dir = path;
            for (int i = 0; i < 4 && !string.IsNullOrEmpty(dir) && dir.Length > ExplorerRoot.Length; i++)
            {
                if (string.Equals(ReadManifestField(dir, "system"), "true", StringComparison.OrdinalIgnoreCase)) return true;
                dir = Path.GetDirectoryName(dir) ?? "";
            }
            return false;
        }

        /// <summary>删除到回收站（Microsoft.VisualBasic.FileIO，Windows 桌面运行时自带）；异常回退永久删除。</summary>
        private void DeleteToRecycle(string path, bool isDir)
        {
            try
            {
                if (isDir)
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                else
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            catch (Exception)
            {
                // 回退：永久删除（占用/权限错误仍会抛出，由调用方提示）
                if (isDir) Directory.Delete(path, true); else File.Delete(path);
            }
        }

        // ==================== 编译（作用于当前编辑内容） ====================

        private void CompileFsFile()
        {
            try
            {
                if (_fsPath == null) { txtJsonStatus.Text = "请先选中文件"; return; }
                string name = Path.GetFileName(_fsPath);
                string err;
                if (_fsXamlPath != null || _fsXamlCsPath != null)
                {
                    // 完全编程：XAML + 代码后置 联合编译（文件夹内新 id，防缓存冲突）
                    var (w, cerr) = ShoreHue.UI.Widgets.Dynamic.WidgetCompiler.CompileXaml(
                        "fs_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                        txtXamlEditor.Text ?? "", txtXamlCsEditor.Text ?? "");
                    err = w == null ? cerr : "";
                }
                else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    try { _ = JsonDocument.Parse(txtJsonEditor.Text ?? ""); err = ""; }
                    catch (Exception jex) { err = "JSON 语法错误：" + jex.Message; }
                }
                else
                {
                    err = ShoreHue.UI.Widgets.Dynamic.WidgetCompiler.Validate(
                        "fs_" + Guid.NewGuid().ToString("N").Substring(0, 8), txtJsonEditor.Text ?? "");
                }
                txtJsonStatus.Text = err.Length == 0 ? "编译通过（未保存，点「保存文件」写回）" : "编译失败：" + err;
            }
            catch (Exception ex) { txtJsonStatus.Text = "编译异常：" + ex.Message; }
        }

        // ==================== 复制 AI 提示词（按目录 manifest kind） ====================

        private void CopyFsAiPrompt()
        {
            try
            {
                if (_fsPath == null) { txtJsonStatus.Text = "请先选中文件或目录"; return; }
                string dir = _fsIsDir ? _fsPath : Path.GetDirectoryName(_fsPath)!;
                string name = Path.GetFileName(_fsIsDir ? dir : _fsPath);
                string kind = ReadManifestField(dir, "kind") ?? FsKindOf(dir) ?? "";
                string currentSrc = string.Join("\n/* --- */\n",
                    Directory.GetFiles(dir)
                        .Where(f => !f.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase)
                        .Select(File.ReadAllText));
                var node = new ShoreHue.Core.Models.ConfigNode
                {
                    Key = "fs_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    Name = name,
                    Category = Path.GetFileName(Path.GetDirectoryName(dir) ?? "") ?? "",
                    Kind = kind,
                    CustomId = "fs:" + name
                };
                var mode = cmbProgMode?.SelectedIndex == 1
                    ? ShoreHue.UI.Settings.Pages.PromptGenerator.ProgrammingMode.Xaml
                    : ShoreHue.UI.Settings.Pages.PromptGenerator.ProgrammingMode.Simple;
                string prompt = ShoreHue.UI.Settings.Pages.PromptGenerator.Generate(node, currentSrc, mode);
                System.Windows.Clipboard.SetText(prompt);
                txtJsonStatus.Text = "已复制 AI 提示词（" + (kind == "" ? name : kind) + "，产出文件放回 " + dir + " 即生效）";
            }
            catch (Exception ex) { txtJsonStatus.Text = "复制失败：" + ex.Message; }
        }

        // ==================== 应用：配置目录（config.json）→ 写回设置 + 冲突标记 ====================

        private void ApplyFsConfigDir(string dir)
        {
            try
            {
                string cfgPath = Path.Combine(dir, "config.json");
                if (!File.Exists(cfgPath)) { txtJsonStatus.Text = "该目录没有 config.json，无法应用"; return; }
                var data = ShoreHue.Core.Services.SettingsFileManager.Load();
                using var doc = JsonDocument.Parse(File.ReadAllText(cfgPath));
                var overrides = data.AppliedPresets ?? new Dictionary<string, string>();
                string presetName = Path.GetFileName(dir);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    SetPropertyFromJson(data, prop.Name, prop.Value);
                    foreach (var n in ShoreHue.UI.Seabed.ConfigTreeBuilder.FindNodeChain(prop.Name))
                        overrides[n.Key] = presetName;
                }
                data.AppliedPresets = overrides;
                _settings.Apply(data);
                LoadTree();
                txtJsonStatus.Text = "已应用配置目录「" + presetName + "」（冲突项已置灰，可在设置页两击解除）";
            }
            catch (Exception ex) { txtJsonStatus.Text = "应用失败：" + ex.Message; }
        }

        private static void SetPropertyFromJson(object data, string name, JsonElement v)
        {
            var p = data.GetType().GetProperty(name);
            if (p == null || !p.CanWrite) return;
            try
            {
                object? val;
                if (v.ValueKind == JsonValueKind.String) val = v.GetString();
                else if (v.ValueKind == JsonValueKind.Number)
                {
                    var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                    val = t == typeof(double) ? v.GetDouble()
                        : t == typeof(float) ? v.GetSingle()
                        : t == typeof(int) ? v.GetInt32()
                        : t == typeof(long) ? v.GetInt64()
                        : v.GetRawText();
                }
                else if (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) val = v.GetBoolean();
                else val = null;
                p.SetValue(data, val);
            }
            catch { }
        }

        // ==================== 变体：当前目录另存为副本 ====================

        private void CopyFsAsVariant(FlatNode fn)
        {
            if (fn?.FsPath == null) return;
            CopyFsAsVariantPath(fn.FsPath);
        }

        /// <summary>变体：把 path（目录或目录内文件）所在功能目录整体复制为新目录（名称N）。</summary>
        private void CopyFsAsVariantPath(string path)
        {
            try
            {
                string dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { txtJsonStatus.Text = "变体作用于功能目录（请选中目录或目录内文件）"; return; }
                if (string.Equals(dir, ExplorerRoot, StringComparison.OrdinalIgnoreCase)) { txtJsonStatus.Text = "不能把海床根目录另存为变体"; return; }
                string parent = Path.GetDirectoryName(dir)!;
                string name = Path.GetFileName(dir);
                string newName = NextSiblingName(name,
                    Directory.GetDirectories(parent).Select(p => Path.GetFileName(p)));
                string newDir = Path.Combine(parent, newName);
                var dlg = new InputDialog("海床 · 另存为变体", "复制目录为变体，输入新目录名：", newName);
                dlg.Owner = System.Windows.Window.GetWindow(this);
                if (dlg.ShowDialog() != true) { txtJsonStatus.Text = "已取消"; return; }
                string input = dlg.ResultText.Trim();
                if (string.IsNullOrEmpty(input)) { txtJsonStatus.Text = "名称不能为空"; return; }
                newDir = Path.Combine(parent, SanitizeFsName(input));
                if (Directory.Exists(newDir)) { txtJsonStatus.Text = "同名目录已存在"; return; }
                Directory.CreateDirectory(newDir);
                foreach (var f in Directory.GetFiles(dir)) File.Copy(f, Path.Combine(newDir, Path.GetFileName(f)), true);
                // 更新副本 manifest：新 id/name
                string nmf = Path.Combine(newDir, "manifest.json");
                if (File.Exists(nmf))
                {
                    string json = File.ReadAllText(nmf);
                    using var doc = JsonDocument.Parse(json);
                    var mf = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>();
                    mf["id"] = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    mf["name"] = input;
                    mf["system"] = false;
                    File.WriteAllText(nmf, JsonSerializer.Serialize(mf, new JsonSerializerOptions { WriteIndented = true }));
                }
                LoadExplorerTree();
                txtJsonStatus.Text = "已创建变体目录：" + Path.GetFileName(newDir) + "（watcher 将自动识别）";
            }
            catch (Exception ex) { txtJsonStatus.Text = "创建变体失败：" + ex.Message; }
        }

        private static string SanitizeFsName(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s ?? "")
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
            }
            return sb.Length >= 2 ? sb.ToString() : "variant";
        }
    }
}
