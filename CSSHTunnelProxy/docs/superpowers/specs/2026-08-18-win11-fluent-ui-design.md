# Win11 Fluent 风格界面重构设计

日期：2026-08-18
状态：已对齐，待实现

## 背景与目标

当前界面采用 VS 暗色调色板 + 基础控件样式，控件直角、无圆角、无 hover/pressed 动效，布局粗糙。目标是重构为 Win11 Fluent 风格：圆角控件、悬停/按压动效、系统强调色、Segoe UI Variable 字体、卡片式布局。

## 关键约束与决策

经 brainstorming 对齐：

1. **实现方式**：纯手写 ControlTemplate，不引入 WPF-UI 等外部库。体积小、风格可控，Mica 透明背景用 P/Invoke（见下）。
2. **目标平台**：以 Win10 为主。**不用 Mica**（Mica 是 Win11 专有 API，Win10 无），用纯色背景模拟 Win11 质感。跨版本一致、最稳。
3. **主题**：暗色 + 浅色双主题。补建 `Light.xaml`（当前缺失，会回退 Dark）。
4. **强调色**：跟随 Windows 系统强调色，从注册表 `HKCU\Software\Microsoft\Windows\DWM\AccentColor` 读取。读不到或默认值时回退 `#0078D4`。
5. **布局**：MainWindow + ConfigDialog 布局与样式一起重构，非仅换色。

## 设计

### 1. 主题资源体系

**调色板**：替换现有 VS 暗色为 Win11 Fluent 色板。

| 键 | 暗色值 | 浅色值 | 用途 |
|----|--------|--------|------|
| `WindowBackgroundColor` / `WindowBackgroundBrush` | `#1F1F1F` | `#FAFAFA` | 窗口背景 |
| `ControlBackgroundBrush` | `#2B2B2B` | `#FFFFFF` | 控件表面 |
| `CardBackgroundBrush` | `#292929` | `#FFFFFF` | 卡片背景 |
| `SidebarBackgroundBrush` | `#1F1F1F` | `#F3F3F3` | 导航栏 |
| `BorderColor` / `BorderBrush` | `#3F3F3F` | `#E5E5E5` | 边框 |
| `ForegroundColor` / `ForegroundBrush` | `#FFFFFF` | `#1A1A1A` | 主文字 |
| `MutedForegroundColor` / `MutedForegroundBrush` | `#9A9A9A` | `#6B6B6B` | 次要文字 |
| `AccentColor` / `AccentBrush` | 动态（系统强调色） | 动态 | 强调色 |
| `AccentForegroundColor` / `AccentForegroundBrush` | `#FFFFFF` | `#FFFFFF` | 强调色上的文字 |
| `SuccessBrush` / `DangerBrush` / `WarningBrush` | 保留语义色 | 保留 | 状态指示 |

**强调色动态化**：新建 `AccentColorProvider`（C#），启动时读注册表系统强调色，注入到 `Application.Resources` 的 `AccentColor`/`AccentBrush` 等键。控件用 `DynamicResource` 引用，主题切换或强调色变化时自动生效。

**主题切换**：`App.ApplyTheme` 已有回退逻辑（主题资源不存在则回退 Dark），保留。补建 `Light.xaml` 使 Light 真正可用。两个主题文件结构对称、键名一致，确保切换无遗漏。

**字体**：`Segoe UI Variable`（Win11 字体），Win10 上自动回退 `Segoe UI`，无影响。

### 2. 控件样式库（Resources/Controls.xaml）

集中放所有控件 `ControlTemplate`，两个主题共用。模板内用 `{DynamicResource ...}` 引用主题颜色键——主题切换时控件颜色自动更新，无需重载模板。

**要重写的控件**：

| 控件 | Win11 风格要点 |
|------|---------------|
| `Button` | 圆角 4px，三变体：Accent（强调色填充）、Standard（背景+边框）、Subtle（透明，hover 才显背景）。hover 提亮、pressed 变暗，Storyboard 颜色过渡 |
| `TextBox` / `PasswordBox` | 圆角 4px，聚焦时下边框变强调色（Win11 输入框特征），未聚焦细边框 |
| `ComboBox` | 圆角，下拉 Popup + 圆角列表，选中项强调色 |
| `CheckBox` / `RadioButton` | 选中时强调色填充，勾/圆点白色，过渡动画 |
| `ListBox` / `ListBoxItem` | 选中项强调色半透明胶囊背景 + 左侧强调色竖条（Win11 导航项样式），hover 浅色覆盖 |
| `MenuItem` / `ContextMenu` | 圆角，hover 背景，分隔线细色 |
| `ScrollBar` | 细滑块，hover 才展宽（Win11 滚动条特征） |
| `Separator` | 细线 |
| `TextBlock` / `Window` | 字体、前景色继承 |

**动效原则**：所有 hover/pressed 用 ~150ms 颜色过渡，不用复杂动画，保证流畅不卡。圆角统一 4px（小控件）/ 8px（卡片/窗口）。

**Button 变体用法**：通过 `Style` 静态键区分（`AccentButtonStyle`、`StandardButtonStyle`、`SubtleButtonStyle`），默认 Button 用 Standard。

### 3. MainWindow 布局重构

```
┌──────────┬───────────────────────────────────┐
│ 导航栏   │ 标题栏区（应用名 + 状态指示）       │
│ (60px)   ├───────────────────────────────────┤
│          │                                     │
│ ▸ 隧道   │   主内容区                          │
│ ▸ 日志   │   （隧道页/日志页/设置页切换）      │
│ ▸ 设置   │                                     │
│          │                                     │
│ ─────    │                                     │
│ (底部)   │                                     │
│ 状态摘要 │                                     │
└──────────┴───────────────────────────────────┘
```

**导航栏**：图标 + 文字，选中项用强调色半透明胶囊背景 + 左侧强调色竖条。当前无图标资源，先用 Unicode 符号（▸/📊/📋/⚙）占位，后续可换真实图标。

**隧道页**（主内容区）重构为卡片式：
- 顶部工具栏：新建/连接/断开/重连按钮（用新 Button 变体）
- 隧道列表：每个隧道一张**卡片**（圆角 8px、表面色背景、细边框），内含状态圆点 + 名称 + 服务器信息 + 端口 + 流量摘要，整张卡片可选中（选中时强调色边框）
- 详情面板：选中隧道的卡片下方展开，分组成卡片（状态/流量/运行时长/监听端口）

**日志页/设置页**：保留 `ContentControl` + DataTemplate 切换机制，内容用新控件样式重绘。设置页表单改成卡片分组。

**状态摘要**：导航栏底部显示已连接隧道数。托盘逻辑保留。

### 4. ConfigDialog 布局重构

当前 `ScrollViewer` + `StackPanel` 堆叠表单，无分组。重构为 Win11 设置页风格的**分组卡片**。

**结构**：四个卡片分组——基本信息、SSH 服务器、本地代理、高级。每张卡片圆角 8px、表面色背景、细边框、标题行加粗。新建 `SettingsCard` 样式（Border 模板）复用。

**窗口圆角**：Win10 上用 `AllowsTransparency=True` + 圆角窗口模板实现 Win11 圆角窗口特征。有轻微性能损耗（软件渲染），对工具型应用可接受。标题栏区域用拖拽区。

**按钮**：保存用 Accent 变体，取消用 Standard 变体。

### 5. 文件结构

```
src/SSHTunnelProxy.App/
├── Resources/
│   ├── Themes/
│   │   ├── Dark.xaml          (重写：Win11 暗色色板)
│   │   └── Light.xaml         (新建：Win11 浅色色板)
│   └── Controls.xaml          (新建：所有控件 ControlTemplate)
├── Framework/
│   └── AccentColorProvider.cs (新建：读注册表系统强调色)
├── Views/
│   ├── MainWindow.xaml        (重构：导航视图 + 卡片布局)
│   ├── MainWindow.xaml.cs     (小改：窗口圆角/拖拽)
│   ├── ConfigDialog.xaml      (重构：分组卡片)
│   └── ConfigDialog.xaml.cs   (小改：窗口圆角)
├── App.xaml                   (改：加载 Controls.xaml + 注入强调色)
└── App.xaml.cs                (小改：启动时调 AccentColorProvider)
```

**AccentColorProvider 实现要点**：
- 读 `HKCU\Software\Microsoft\Windows\DWM\AccentColor`（ARGB int），解析成 `Color`
- 读不到 / 值为 `0xFFFFFFFF`（默认）时回退 `#0078D4`
- 暴露 `Color AccentColor` 和 `Brush AccentBrush`
- 启动时注入到 `Application.Resources`（顶层，不在 MergedDictionaries 内）。资源查找顺序是先查 Application.Resources 顶层再查 MergedDictionaries，因此顶层注入的强调色会覆盖主题字典里的同名键，主题切换不影响强调色。控件用 `DynamicResource` 引用 `AccentBrush`，注入后自动生效。

**App.xaml 资源加载顺序**：
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Resources/Themes/Dark.xaml" />
            <ResourceDictionary Source="Resources/Controls.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```
主题字典先加载（提供颜色键），Controls.xaml 后加载（模板引用这些键）。`ApplyTheme` 只切换 Themes 字典，Controls.xaml 不动。

## 测试策略

- **单元测试**：`AccentColorProvider` 的注册表 ARGB 解析 + 回退逻辑（mock 注册表读取）。这是唯一能写单元测试的部分。
- **手动验证**：
  - 暗色/浅色切换无遗漏
  - 各控件 hover/pressed 动效
  - 隧道卡片选中态
  - ConfigDialog 各分组卡片
  - 系统强调色变化后控件跟随（改系统设置后重启）
- **回归**：确保之前的启动死锁修复不回退（`ApplyTheme` 回退逻辑保留），`ConfigureAwait(false)` 不受影响。

## 非目标

- 不引入 WPF-UI 或其他外部 UI 库。
- 不实现 Mica/Acrylic 透明背景（Win10 为主）。
- 不替换真实图标资源（先用 Unicode 符号占位）。
- 不改 ViewModel 逻辑（纯 UI 层重构，绑定路径保持兼容）。
