# NStringTool

Notepad++ 字符串转义/去转义插件。支持 C/C++、JSON、HTML/XML、URL 四种格式。

## 功能

在"插件 → NStringTool"菜单下提供 8 个操作：

- C/C++ 字符串 - 转义 / 去转义（\n \t \" \\ \xNN \uNNNN）
- JSON 字符串 - 转义 / 去转义（\" \\ \n \t \uXXXX，RFC 8259）
- HTML/XML 实体 - 转义 / 去转义（&lt; &gt; &amp; &#60; &#x3C;）
- URL 编码 - 转义 / 去转义（%20 %2F，RFC 3986）

有选中文本时转换选区，无选区时转换整个文档。每次转换可一次 Ctrl+Z 撤销。

## 构建

需要 Visual Studio（2015 及以上均可）+ CMake。

**推荐（VS 2022）：**

```bash
git clone <repo>
cd NStringTool
cmake -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release
```

**VS 2015（本机实测可用）：**

```bash
cmake -B build64 -G "Visual Studio 14 2015 Win64"
cmake --build build64 --config Release
```

生成 `build64/bin/Release/NStringTool.dll`（x64，PE32+）。注意 VS 2015 默认生成器是 Win32，必须用 `Win64` 变体或 `-A x64` 指定 64 位平台，否则生成的 Win32 DLL 无法加载到 64 位 Notepad++。

## 安装

把 `NStringTool.dll` 复制到 Notepad++ 的插件目录：

```
<Notepad++安装目录>\plugins\NStringTool\NStringTool.dll
```

注意 DLL 必须放在以插件名命名的子文件夹内（Notepad++ 7.6+ 要求）。重启 Notepad++。

## 测试

转换核心层有单元测试：

```bash
cmake --build build64 --target ConverterTests --config Debug
./build64/Debug/ConverterTests.exe
```

## 技术栈

C++17、CMake、Notepad++ Plugin SDK、Scintilla API、Win32。
