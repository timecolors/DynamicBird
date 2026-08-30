# 🕊️ 其他鸟笼 · 在线市场（DynamicBird 内置）

市场包放在本目录，随主仓库一起提交，**CI 会自动编译验证**所有包的源码（不能编译的包会挂掉 PR）。

## 目录结构

```
market/
  index.json                  ← 聚合索引（列表展示用，新增包后同步登记）
  packages/<包ID>/
    manifest.json             ← 元信息（安装必需字段）
    main.cs                   ← 源码（C#，实现 IWidget / 配置代码）
```

## 添加一个包

1. 新建 `packages/<包ID>/` 目录（ID 用小写连字符，如 `my-timer`）
2. 写 `main.cs`（小组件/面板实现 IWidget；配置项写 config 代码）
3. 写 `manifest.json`：
   ```json
   {
     "id": "my-timer",
     "name": "我的计时器",
     "kind": "Widget",            // Widget | Panel | Config
     "version": "1.0.0",
     "author": "你的名字",
     "description": "一句话说明",
     "baseType": "Widget",
     "parentKey": "panel-widgets",
     "sourceKey": "widget-timer"
   }
   ```
4. 在 `index.json` 的 `packages` 数组登记（id/name/kind/version/author/description/permissions）
5. 提交 PR（或直接 push）——CI 会编译验证 main.cs

## 安全

- 安装时客户端**重新检测权限**（不信 manifest 声明），风险权限弹窗确认
- 编译走**沙箱**（TrustedSource=false）：拦截 Process/反射/DllImport/注册表/窗口钩子/截屏/文件写/剪贴板等
- 上传前请自觉使用「导出」功能标注权限
