# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Notepad++ 字符串转义/去转义插件，C++17 + CMake。独立于仓库内其他子项目（均为 Electron / .NET），无 npm / dotnet 依赖 —— 构建仅依赖 Visual Studio + CMake，且都必须以 **x64** 平台构建（Win32 DLL 无法加载到 64 位 Notepad++）。

## 构建与测试

```bash
# 推荐（VS 2022）
cmake -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release            # → build/bin/Release/NStringTool.dll

# VS 2015（本机实测）：默认生成器是 Win32，必须用 Win64 变体
cmake -B build64 -G "Visual Studio 14 2015 Win64"
cmake --build build64 --config Release          # → build64/bin/Release/NStringTool.dll

# 单元测试（仅转换核心层）
cmake --build build64 --target ConverterTests --config Debug
./build64/Debug/ConverterTests.exe              # 退出码 0=全通过，1=有失败
```

- CMake 目标：`NStringTool`（插件 SHARED DLL）与 `ConverterTests`（测试可执行文件，只覆盖 `src/converters/`，不覆盖 `PluginEntry`/`EditorOps`）。
- 两个目标均已开启 `/utf-8`（MSVC 编译选项）—— 源文件含中文注释，缺少该选项会触发 C4819 警告、甚至因 GBK 误读反斜杠导致行接续语法错误。新增源文件进入目标时需保持这一惯例。
- 提交的 DLL 产物 `build64/bin/Release/NStringTool.dll` 被 `.gitignore` 的 `*.dll` 规则排除但已 `git add -f` 强制入库。注意 `.gitignore` 忽略 `build/`、`build*/`、`bin/`、`*.exp`、`*.lib`、`*.pdb`、`CMakeCache.txt`、`CMakeFiles/`。

## 安装与验证

把 `NStringTool.dll` 复制到 `<Notepad++安装目录>\plugins\NStringTool\NStringTool.dll` —— **必须放在以插件名命名的子文件夹内**（Notepad++ 7.6+ 要求），然后重启 Notepad++，在“插件 → NStringTool”菜单下应有 9 个操作（8 个转义/去转义 + 1 个十六进制转 C 数组）。

## 架构

核心原则：**转换逻辑与编辑器交互彻底分离**。转换核心层 (`src/converters/`) 是纯函数、不依赖 Notepad++/Scintilla/Windows，因此可被 `ConverterTests` 独立单元测试；编辑器交互层 (`PluginEntry` + `EditorOps`) 负责 Scintilla 读写与菜单回调。

### 数据流

```
菜单点击 → PluginEntry 菜单回调（9 个独立 onXxx 函数）
        → EditorOps::applyConversion(format, direction)   [Scintilla 交互，转发到 applyConversionWith]
        → convert(format, direction, input) | 自定义 fn(input)   [转换核心]
        → EditorOps 用单次撤销单元替换选区/文档并重新选中
```

`EditorOps` 提供统一入口 `applyConversionWith(ConvertFn)`（`ConvertFn` 是 `std::function<std::string(const std::string&)>`，支持捕获参数的 lambda 与普通函数）承载全部 Scintilla 读写/撤销逻辑；`applyConversion(format, direction)` 只是把它转发给 `convert()` 的包装。单向操作（如 hex→数组）直接调用 `applyConversionWith(hexToCArray)`，不经过 `Format/Direction` 分发。

### 各文件职责

- **`src/PluginEntry.cpp`** — DLL 入口与菜单注册。导出 `setInfo`/`getName`/`getFuncsArray`/`beNotified`/`messageProc`/`isUnicode` 六个标准插件函数；`setInfo` 保存 `NppData` 并把三个窗口句柄转交给 `EditorOps`。8 个菜单项各自绑定一个独立回调函数（`onCppEscape`…`onUrlUnescape`），每个回调只调用 `applyConversion` 一次。`beNotified`/`messageProc` 是空实现，本插件不处理通知也不拦截消息。
- **`src/EditorOps.cpp/.h`** — Scintilla 交互层。持有 `g_nppHandle`/`g_scintillaMain`/`g_scintillaSecond`；通过 `NPPM_GETCURRENTSCINTILLA` 获取当前活跃视图（含分屏第二视图）。`applyConversion`: 有选区转换选区、无选区转换整个文档，用 `SCI_GETTEXTRANGEFULL` 读文本 → `convert()` → `SCI_BEGINUNDOACTION`/`SCI_REPLACETARGET`/`SCI_ENDUNDOACTION` 包裹成一次可 Ctrl+Z 撤销的操作，最后重选新文本。异常时弹 MessageBox 警告。
- **`src/Converters.h`** — 统一入口声明；`enum Format { Cpp, Json, Html, Url }`、`enum Direction { Escape, Unescape }`。
- **`src/converters/Converters.cpp`** — `convert()` 按 format/direction 分发到各转换器的 `xxxEscape`/`xxxUnescape`。
- **`src/converters/` 四个转换器**（每对 `.cpp/.h`）—— 纯函数 `std::string` → `std::string`，逐字节处理：

  | 转换器 | 转义产物 | 去转义注意点 |
  |--------|----------|--------------|
  | `CppConverter` | `\n \t \r \" \\`，其他控制字符 `\xNN` | 支持 `\xNN`、`\uNNNN`（含代理对合并为 UTF-8）；非法序列保留原样 |
  | `JsonConverter` | `\" \\ \b \f \n \r \t`，其他 `<0x20` 用 `\uXXXX`（RFC 8259） | `\uXXXX` 含代理对；非法转义保留原样 |
  | `HtmlConverter` | `&amp; &lt; &gt; &quot; &#39;` | 数字实体 `&#NN;`/`&#xHH;` + 命名实体表；未知实体保留 `&` 原样 |
  | `UrlConverter` | 非保留字符外全部 `%XX`（RFC 3986） | `%XX` 十六进制解出原始字节；非法 `%` 保留原样 |
  | `HexArrayConverter`（单向，无去转义） | `hexToCArray(input)` → `{0x11, 0x22, ...}` | — |

  四个转义都遵循“无法识别/非法的转义序列原样保留”这一约定（不做破坏性报错）。`HexArrayConverter` 是唯一的**单向**转换器：`hexToCArray` 自动跳过空白字符，兼容无分隔（`"1122"`）与带空格（`"11 22"`）两种输入；输出为 C/C++ 数组初始化列表 `{...}`（不含 `unsigned char hexArray[] =` 前缀，也无分号）；输入含非十六进制字符或十六进制位数非偶数时整体返回原样 input，不转换。它不纳入 `Format/Direction` 分发，由 `PluginEntry` 通过 `applyConversionWith(hexToCArray)` 直接调用。

- **`sdk/`** — Notepad++ Plugin SDK 头文件（`PluginInterface.h`、`Notepad_plus_msgs.h`、`Scintilla.h`、`Sci_Position.h`），只读、勿改。
- **`tests/`** — `test_main.cpp`（`main`，维护 `g_testPass`/`g_testFail` 计数器）+ `test_converters.cpp`（单个 `runConverterTests()` 用 `ASSERT_EQ`/`ASSERT_TRUE` 宏跑全部断言）+ `test_macros.h`（宏定义）。无第三方测试框架。
- **`.superpowers/` 与 `docs/`** — 开发过程文档（superpowers 计划/规格），非运行时代码，无需改动。

## 约定与注意事项

- **编码**：源文件为 UTF-8，含中文注释，靠 `/utf-8` 编译选项保证 MSVC 正确解析。保持源码与注释中文即可。
- **单次撤销**：`EditorOps` 用 `SCI_BEGINUNDOACTION`/`SCI_ENDUNDOACTION` 包裹整个替换，保证一次 Ctrl+Z 还原 —— 这是刻意设计的交互。新增转换路径时不得破坏这一点。
- **行为**：空文档/空选区时不执行任何操作。
- 新增成对（转义/去转义）转换器时：在 `Converters.h` 加 `Format` 枚举值、新增一对 `converters/XxxConverter.cpp/.h`、在 `Converters.cpp` 的 `convert()` 加分支、把 `.cpp` 加入 `CMakeLists.txt` 的 `NStringTool` 与 `ConverterTests` 两个目标、并在 `tests/test_converters.cpp` 补断言。新增单向转换器（如 hex→数组）时不改 `Format/Direction`，直接在 `EditorOps.h` 用 `applyConversionWith(ConvertFn)` 挂载，在 `PluginEntry` 加菜单项与回调。
- 新增菜单项：改 `PluginEntry.cpp` 的 `MenuItemIndex` 枚举、`kMenuNames` 数组、新增独立回调函数、把回调加入 `getFuncsArray` 的 `handlers` 数组 —— 四处必须同步（`MENU_COUNT` 由枚举自动推导）。
