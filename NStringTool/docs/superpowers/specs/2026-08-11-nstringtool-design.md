# NStringTool — Notepad++ 字符串转义/去转义插件设计文档

- 日期：2026-08-11
- 状态：已批准
- 目标平台：Notepad++ 8.x，仅 x64
- 工作目录：`D:\Workspace\ElectronApp\NStringTool`
- Notepad++ 源码参考路径：`D:\Workspace\OpenSouces\notepad-plus-plus`

## 1. 概述

NStringTool 是一个 Notepad++ 插件，用于对编辑器中的文本进行字符串转义与去转义。支持四种格式：C/C++ 字符串、JSON 字符串、HTML/XML 实体、URL 编码。用户通过"插件"菜单触发操作，插件对当前选中文本（无选中则对整个文档）执行转换并替换原文。

### 成功标准

- 4 种格式 × 2 方向 = 8 个菜单项全部可用
- 选中文本转换正确；无选区时对整个文档转换正确
- 中文（UTF-8 多字节）、emoji（代理对）不乱码
- 撤销（Ctrl+Z）可一次还原整个转换操作
- 非法转义序列不崩溃、不丢数据（保留原样）

## 2. 整体架构

单一 Windows DLL（`NStringTool.dll`），导出 Notepad++ 插件 SDK 规定的 6 个标准入口函数。内部分三层：

```
NStringTool.dll
├── 插件入口层（PluginEntry）      ← 与 Notepad++ 对接：setInfo/getName/getFuncsArray/beNotified/messageProc/isUnicode
├── 编辑器操作层（EditorOps）      ← 封装 Scintilla 文本读写：获取选区、替换选区、撤销分组
└── 转换核心层（Converters）      ← 4 种格式各一对 escape/unescape 纯函数，无 UI 依赖
```

三层依赖方向：入口层 → 编辑器操作层 → 转换核心层。转换核心层不依赖上层，可独立单元测试。

## 3. 菜单结构

在"插件"菜单下注册以插件名 `NStringTool` 命名的子菜单，下挂 8 个菜单项，按格式分组，组间用分隔符分隔：

```
插件 → NStringTool
        ├── C/C++ 字符串
        │     ├── 转义
        │     └── 去转义
        ├── JSON 字符串
        │     ├── 转义
        │     └── 去转义
        ├── HTML/XML 实体
        │     ├── 转义
        │     └── 去转义
        └── URL 编码
              ├── 转义
              └── 去转义
```

Notepad++ 7.6+ 会自动把连续的 `FuncItem` 归到以插件名命名的子菜单下。分隔符通过 `FuncItem._itemName` 设为 `"---"` 实现。

## 4. 转换核心层（Converters）

### 4.1 统一接口

所有转换函数签名统一为：

```cpp
// 输入：原始 UTF-8 字节流，输出：转换后的 UTF-8 字节流
std::string escape(const std::string& input);
std::string unescape(const std::string& input);
```

内部统一用 `std::string`（UTF-8 字节流）传递，因为 Scintilla 内部就是 UTF-8。不在转换层做 `wchar_t` 转换，避免编码损失和性能开销。

### 4.2 各格式规则

| 格式 | escape（转义） | unescape（去转义） |
|------|---------------|-------------------|
| **C/C++** | `"` → `\"`、`\` → `\\`、换行 → `\n`、Tab → `\t`、CR → `\r`、其他控制字符 → `\xNN` | `\n`→换行、`\t`→Tab、`\"`→`"`、`\\`→`\`、`\xNN`→字节、`\uNNNN`→UTF-8、非法转义符保留原样 |
| **JSON** | 符合 RFC 8259：`"`→`\"`、`\`→`\\`、控制字符→`\n`/`\t`/`\uXXXX` | RFC 8259 反向解析，非法序列保留原样（不抛异常） |
| **HTML/XML** | `&`→`&amp;`、`<`→`&lt;`、`>`→`&gt;`、`"`→`&quot;`、`'`→`&#39;` | 解析所有命名实体（`&lt;` 等）+ 数字实体（`&#60;` `&#x3C;`），未知实体保留原样 |
| **URL** | 非保留字符（`A-Za-z0-9-_.~`）外全部 `%XX`，UTF-8 多字节按字节逐个编码 | `%XX`→字节，连续字节再组合成 UTF-8 字符串 |

### 4.3 边界处理

- 所有 `unescape` 遇到非法序列时**保留原样**（不抛异常），保证操作可逆、不丢数据。
- C/C++ 和 JSON 的 `unescape` 对 `\uXXXX` 代理对（surrogate pair，如 `\uD83D\uDE00`）正确合并为单个 UTF-8 字符。
- 空字符串输入返回空字符串。

## 5. 编辑器操作层（EditorOps）

### 5.1 核心流程

每次菜单项点击执行：

1. 获取当前活跃的 Scintilla 视图句柄（主/副视图，通过 `NPPM_GETCURRENTSCINTILLA`）
2. 读取选区：若主选区长度 > 0，取主选区文本；否则取整个文档
3. 把选区文本（UTF-8）交给对应 Converter 转换
4. 用 `SCI_BEGINUNDOACTION` / `SCI_ENDUNDOACTION` 包裹替换，使整个替换作为一个撤销单元
5. 用 `SCI_SETTARGETRANGE` + `SCI_REPLACETARGET` 替换选区文本
6. 替换后用 `SCI_SETSEL` 重新选中新的文本范围

### 5.2 关键 Scintilla 消息

| 消息 | 用途 |
|------|------|
| `SCI_GETSELECTIONSTART` / `SCI_GETSELECTIONEND` | 获取主选区范围 |
| `SCI_GETLENGTH` | 文档总长度（无选区时用） |
| `SCI_GETTEXTRANGE` | 读取选区文本 |
| `SCI_SETTARGETRANGE` / `SCI_REPLACETARGET` | 替换文本 |
| `SCI_BEGINUNDOACTION` / `SCI_ENDUNDOACTION` | 撤销分组 |
| `SCI_SETSEL` | 替换后重选 |

### 5.3 设计决策

- 用 `SCI_REPLACETARGET` 而非 `SCI_REPLACESEL`：前者配合 target range 能精确控制替换范围，对长文本和多字节字符更稳定，且能正确处理"无选区时替换整个文档"的场景。

### 5.4 错误处理

- 任何 Scintilla 调用失败时静默返回（不崩溃）
- 转换层抛异常时用 try/catch 包裹，失败时弹 MessageBox 提示错误

## 6. 插件入口层（PluginEntry）

实现 SDK 规定的 6 个导出函数：

| 函数 | 职责 |
|------|------|
| `getName()` | 返回 `L"NStringTool"` |
| `setInfo(NppData)` | 保存 Notepad++ + Scintilla 窗口句柄到全局变量 |
| `getFuncsArray(int*)` | 返回 8 个 `FuncItem` 数组，每项绑定菜单回调 + 格式/方向标识 |
| `beNotified(SCNotification*)` | 监听 `NPPN_TBMODIFICATION` 等通知 |
| `messageProc(...)` | 空实现（本插件不需要拦截 Windows 消息） |
| `isUnicode()` | 返回 `TRUE` |

### 6.1 菜单回调分发

8 个菜单项共用一个分发函数 `dispatchCommand(cmdId)`，根据 `cmdID` 映射到具体的 `{格式, 方向}` 组合，调用 `EditorOps::applyConversion(format, direction)`。避免 8 个独立函数的重复代码。

## 7. 项目结构与构建系统

### 7.1 目录结构

```
NStringTool/
├── CMakeLists.txt
├── src/
│   ├── PluginEntry.cpp          ← 6 个导出函数 + 菜单分发
│   ├── EditorOps.h/.cpp        ← Scintilla 读写封装
│   ├── Converters.h            ← 统一接口声明
│   └── converters/
│       ├── CppConverter.h/.cpp
│       ├── JsonConverter.h/.cpp
│       ├── HtmlConverter.h/.cpp
│       └── UrlConverter.h/.cpp
├── sdk/                        ← 从 notepad-plus-plus 源码复制过来的头文件
│   ├── PluginInterface.h
│   ├── Notepad_plus_msgs.h
│   └── Scintilla.h
├── tests/                      ← 转换核心层单元测试（控制台程序）
└── README.md
```

### 7.2 CMake 要点

- 目标：`NStringTool`（SHARED 库，输出 `.dll`）
- C++ 标准：C++17
- 包含路径：`src/` + `sdk/`
- 链接：仅 `user32.lib`（MessageBox 用），无其他依赖
- 输出目录：`bin/x64/`
- 构建配置：仅 x64

### 7.3 SDK 头文件来源

从 `D:\Workspace\OpenSouces\notepad-plus-plus` 复制以下文件到项目 `sdk/` 目录，使插件可独立编译，不依赖完整 Notepad++ 源码树：

- `PowerEditor/src/MISC/PluginsManager/PluginInterface.h`
- `PowerEditor/src/MISC/PluginsManager/Notepad_plus_msgs.h`
- `scintilla/include/Scintilla.h`

## 8. 测试策略

### 8.1 转换核心层（单元测试）

可独立单元测试。为每种格式写测试程序（控制台小程序），覆盖：

- 正向：`"a\nb"` → `"a\\nb"`（C++ escape）
- 反向：`"a\\nb"` → `"a\nb"`（C++ unescape）
- 往返一致性：`unescape(escape(x)) == x`（对常见输入成立）
- 边界：空字符串、纯 ASCII、中文（UTF-8 多字节）、emoji（代理对）、非法转义序列

用独立 `tests/` 子目录 + 简单断言宏，不引入 GoogleTest 等重框架。

### 8.2 编辑器操作层 + 入口层（集成测试）

手动集成测试。编译 DLL 后放入 Notepad++ 的 `plugins` 目录，人工验证：

- 选中文本转换、无选区时全文档转换
- 撤销（Ctrl+Z）能一次还原
- 4 种格式 × 2 方向 = 8 个菜单项全部可用
- 中文/emoji 不乱码

## 9. 错误处理原则

- 转换层：非法输入保留原样，绝不抛异常导致崩溃
- 编辑器层：Scintilla 调用失败静默返回
- 入口层：try/catch 包裹所有菜单回调，异常时 MessageBox 提示
- 内存：所有 `new` 配对 `delete`，优先用 `std::string`/`std::vector` 避免裸指针

## 10. 交付物

- `NStringTool.dll`（x64）
- 源码（含 CMakeLists.txt）
- README.md（构建说明 + 安装说明）
- 测试程序源码

### 安装方式

用户手动把 DLL 放到 Notepad++ 的 `plugins\NStringTool\` 目录（7.6+ 插件目录结构要求 DLL 放在以插件名命名的子文件夹内）。
