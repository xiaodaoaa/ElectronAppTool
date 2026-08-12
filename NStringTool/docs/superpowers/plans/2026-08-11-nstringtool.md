# NStringTool 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现一个 Notepad++ 插件 NStringTool，对编辑器选中文本（无选中则整个文档）进行 C/C++、JSON、HTML/XML、URL 四种格式的字符串转义与去转义。

**Architecture:** 单一 Windows DLL，三层结构：插件入口层（对接 Notepad++ SDK 的 6 个导出函数）→ 编辑器操作层（封装 Scintilla 读写）→ 转换核心层（4 种格式各一对 escape/unescape 纯函数）。转换层无 UI 依赖，可独立单元测试。

**Tech Stack:** C++17、CMake、Notepad++ Plugin SDK（PluginInterface.h / Notepad_plus_msgs.h / Scintilla.h）、Scintilla 编辑器消息 API、Win32 API（仅 user32.lib 的 MessageBox）。

## Global Constraints

- 目标平台：Notepad++ 8.x，仅 x64（不构建 x86）。
- C++ 标准：C++17。
- 字符编码：内部全部用 `std::string`（UTF-8 字节流）传递，不在转换层做 `wchar_t` 转换。
- 非法转义序列处理：所有 `unescape` 遇到非法序列保留原样，绝不抛异常导致崩溃。
- 内存：优先用 `std::string`/`std::vector`，避免裸指针 `new`/`delete`。
- SDK 头文件来源：从 `D:\Workspace\OpenSouces\notepad-plus-plus` 复制到项目 `sdk/` 目录，插件独立编译，不依赖完整 Notepad++ 源码树。
- 提交信息用中文。
- 工作目录：`D:\Workspace\ElectronApp\NStringTool`（当前已是 git 仓库的父目录；本计划执行前需先 `git init`）。

## File Structure

| 文件 | 职责 |
|------|------|
| `CMakeLists.txt` | 构建插件 DLL 和测试程序 |
| `sdk/PluginInterface.h` | 从 Notepad++ 源码复制的插件接口头 |
| `sdk/Notepad_plus_msgs.h` | 从 Notepad++ 源码复制的消息定义头 |
| `sdk/Scintilla.h` | 从 Notepad++ 源码复制的 Scintilla 头 |
| `src/Converters.h` | 转换层统一接口声明（Format 枚举、Direction 枚举、convert 函数） |
| `src/converters/CppConverter.h` / `.cpp` | C/C++ 字符串转义/去转义 |
| `src/converters/JsonConverter.h` / `.cpp` | JSON 字符串转义/去转义 |
| `src/converters/HtmlConverter.h` / `.cpp` | HTML/XML 实体转义/去转义 |
| `src/converters/UrlConverter.h` / `.cpp` | URL 编码/解码 |
| `src/EditorOps.h` / `.cpp` | Scintilla 读写封装：获取选区、替换选区、撤销分组 |
| `src/PluginEntry.cpp` | 6 个导出函数 + 菜单分发 |
| `tests/test_converters.cpp` | 转换核心层单元测试（控制台程序） |
| `tests/test_main.cpp` | 简单测试断言宏与 main 入口 |
| `README.md` | 构建与安装说明 |

---

### Task 1: 项目初始化与 SDK 头文件准备

**Files:**
- Create: `CMakeLists.txt`
- Create: `sdk/PluginInterface.h`（复制）
- Create: `sdk/Notepad_plus_msgs.h`（复制）
- Create: `sdk/Scintilla.h`（复制）
- Create: `README.md`
- Create: `.gitignore`

**Interfaces:**
- Produces: `sdk/` 目录下三个头文件，供后续所有任务 `#include`。CMakeLists.txt 定义 `NStringTool`（SHARED）和 `ConverterTests`（测试）两个目标。

- [ ] **Step 1: 初始化 git 仓库**

```bash
cd "D:\Workspace\ElectronApp\NStringTool"
git init
```

- [ ] **Step 2: 创建 .gitignore**

写入文件 `D:\Workspace\ElectronApp\NStringTool\.gitignore`：

```
build/
bin/
*.dll
*.obj
*.exp
*.lib
*.pdb
.vs/
CMakeCache.txt
CMakeFiles/
cmake_install.cmake
CMakeSettings.json
```

- [ ] **Step 3: 复制 SDK 头文件**

```bash
mkdir -p "D:\Workspace\ElectronApp\NStringTool\sdk"
cp "D:\Workspace\OpenSouces\notepad-plus-plus\PowerEditor\src\MISC\PluginsManager\PluginInterface.h" "D:\Workspace\ElectronApp\NStringTool\sdk\"
cp "D:\Workspace\OpenSouces\notepad-plus-plus\PowerEditor\src\MISC\PluginsManager\Notepad_plus_msgs.h" "D:\Workspace\ElectronApp\NStringTool\sdk\"
cp "D:\Workspace\OpenSouces\notepad-plus-plus\scintilla\include\Scintilla.h" "D:\Workspace\ElectronApp\NStringTool\sdk\"
```

注意：`PluginInterface.h` 内 `#include "Scintilla.h"` 和 `#include "Notepad_plus_msgs.h"`，这三个文件放同一 `sdk/` 目录即可互相找到。

- [ ] **Step 4: 创建 CMakeLists.txt**

写入文件 `D:\Workspace\ElectronApp\NStringTool\CMakeLists.txt`：

```cmake
cmake_minimum_required(VERSION 3.15)
project(NStringTool LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 插件 DLL 目标
add_library(NStringTool SHARED
    src/PluginEntry.cpp
    src/EditorOps.cpp
    src/converters/CppConverter.cpp
    src/converters/JsonConverter.cpp
    src/converters/HtmlConverter.cpp
    src/converters/UrlConverter.cpp
)
target_include_directories(NStringTool PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_CURRENT_SOURCE_DIR}/sdk
)
target_link_libraries(NStringTool PRIVATE user32)
set_target_properties(NStringTool PROPERTIES
    OUTPUT_NAME "NStringTool"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

# 测试程序目标（仅转换核心层）
add_executable(ConverterTests
    tests/test_main.cpp
    tests/test_converters.cpp
    src/converters/CppConverter.cpp
    src/converters/JsonConverter.cpp
    src/converters/HtmlConverter.cpp
    src/converters/UrlConverter.cpp
)
target_include_directories(ConverterTests PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)
```

- [ ] **Step 5: 创建 README.md（占位，后续任务补充）**

写入文件 `D:\Workspace\ElectronApp\NStringTool\README.md`：

```markdown
# NStringTool

Notepad++ 字符串转义/去转义插件。支持 C/C++、JSON、HTML/XML、URL 四种格式。

## 构建

（待 Task 10 补充）

## 安装

（待 Task 10 补充）
```

- [ ] **Step 6: 提交**

```bash
cd "D:\Workspace\ElectronApp\NStringTool"
git add .gitignore CMakeLists.txt README.md sdk/
git commit -m "初始化项目：CMake 构建、SDK 头文件、README"
```

---

### Task 2: 转换层统一接口与测试框架

**Files:**
- Create: `src/Converters.h`
- Create: `tests/test_main.cpp`
- Create: `tests/test_converters.cpp`

**Interfaces:**
- Produces: `Converters.h` 定义 `enum class Format { Cpp, Json, Html, Url }`、`enum class Direction { Escape, Unescape }`、`std::string convert(Format, Direction, const std::string&)`。后续 4 个 Converter 任务各自实现并注册到 `convert`。测试宏 `ASSERT_EQ`、`ASSERT_TRUE` 供所有测试用。

- [ ] **Step 1: 写 Converters.h 统一接口**

写入文件 `D:\Workspace\ElectronApp\NStringTool\src\Converters.h`：

```cpp
#pragma once

#include <string>

enum class Format {
    Cpp,
    Json,
    Html,
    Url
};

enum class Direction {
    Escape,
    Unescape
};

// 统一转换入口。各 Converter 在各自 .cpp 中实现，
// 由 convert() 按 format/direction 分发。
std::string convert(Format format, Direction direction, const std::string& input);
```

- [ ] **Step 2: 写测试框架 test_main.cpp**

写入文件 `D:\Workspace\ElectronApp\NStringTool\tests\test_main.cpp`：

```cpp
#include <cstdio>
#include <string>

// 简单断言宏。失败时打印并计数，不中断。
extern int g_testPass;
extern int g_testFail;

#define ASSERT_EQ(actual, expected) do { \
    if ((actual) == (expected)) { ++g_testPass; } \
    else { ++g_testFail; std::printf("FAIL %s:%d: ASSERT_EQ\n  actual:   %s\n  expected: %s\n", \
        __FILE__, __LINE__, std::string(actual).c_str(), std::string(expected).c_str()); } \
} while(0)

#define ASSERT_TRUE(cond) do { \
    if ((cond)) { ++g_testPass; } \
    else { ++g_testFail; std::printf("FAIL %s:%d: ASSERT_TRUE(%s)\n", __FILE__, __LINE__, #cond); } \
} while(0)

int g_testPass = 0;
int g_testFail = 0;

// 由 test_converters.cpp 提供
void runConverterTests();

int main() {
    runConverterTests();
    std::printf("\n==== 测试结果 ====\n通过: %d\n失败: %d\n", g_testPass, g_testFail);
    return g_testFail == 0 ? 0 : 1;
}
```

- [ ] **Step 3: 写 test_converters.cpp 占位（仅验证编译链路）**

写入文件 `D:\Workspace\ElectronApp\NStringTool\tests\test_converters.cpp`：

```cpp
#include "Converters.h"
#include <string>

void runConverterTests() {
    // 占位：空输入应返回空字符串（convert 在 Task 3 实现）
    ASSERT_EQ(convert(Format::Cpp, Direction::Escape, ""), std::string(""));
}
```

- [ ] **Step 4: 验证测试目标编译链路**

```bash
cd "D:\Workspace\ElectronApp\NStringTool"
cmake -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --target ConverterTests --config Debug
```
Expected: 链接阶段失败，提示 `convert` 未定义（`convert` 在 Task 3 才实现）。这一步仅用于确认除 `convert` 引用外的编译链路都通——测试框架、头文件包含路径、CMake 配置正确。若失败信息是"无法解析的外部符号 convert"即符合预期，进入 Task 3。

- [ ] **Step 5: 提交**

```bash
git add src/Converters.h tests/
git commit -m "添加转换层统一接口与测试框架"
```

---

### Task 3: C/C++ 字符串转换器

**Files:**
- Create: `src/converters/CppConverter.h`
- Create: `src/converters/CppConverter.cpp`
- Modify: `tests/test_converters.cpp`（追加 C++ 测试用例）
- Create: `src/converters/Converters.cpp`（`convert` 分发函数，集中各 Converter）

**Interfaces:**
- Consumes: `Converters.h`（Format/Direction）。
- Produces: `std::string cppEscape(const std::string&)`、`std::string cppUnescape(const std::string&)`。`convert()` 在本任务建立，后续任务只需往 switch 追加分支。

- [ ] **Step 1: 写 CppConverter.h**

写入 `D:\Workspace\ElectronApp\NStringTool\src\converters\CppConverter.h`：

```cpp
#pragma once
#include <string>

// C/C++ 字符串转义：把控制字符和特殊字符转为 \n \t \" \\ \xNN 等
std::string cppEscape(const std::string& input);

// C/C++ 字符串去转义：把 \n \t \" \\ \xNN \uNNNN 还原为真实字符
// 非法转义序列保留原样（如 \q 保留为 \q）
std::string cppUnescape(const std::string& input);
```

- [ ] **Step 2: 写失败测试（追加到 test_converters.cpp）**

用以下完整内容替换 `tests/test_converters.cpp`：

```cpp
#include "Converters.h"
#include "converters/CppConverter.h"
#include <string>

void runConverterTests() {
    // ---- C/C++ escape ----
    ASSERT_EQ(cppEscape("a\nb"), std::string("a\\nb"));
    ASSERT_EQ(cppEscape("a\tb"), std::string("a\\tb"));
    ASSERT_EQ(cppEscape("a\rb"), std::string("a\\rb"));
    ASSERT_EQ(cppEscape("a\"b"), std::string("a\\\"b"));
    ASSERT_EQ(cppEscape("a\\b"), std::string("a\\\\b"));
    ASSERT_EQ(cppEscape("plain"), std::string("plain"));
    ASSERT_EQ(cppEscape(""), std::string(""));

    // ---- C/C++ unescape ----
    ASSERT_EQ(cppUnescape("a\\nb"), std::string("a\nb"));
    ASSERT_EQ(cppUnescape("a\\tb"), std::string("a\tb"));
    ASSERT_EQ(cppUnescape("a\\rb"), std::string("a\rb"));
    ASSERT_EQ(cppUnescape("a\\\"b"), std::string("a\"b"));
    ASSERT_EQ(cppUnescape("a\\\\b"), std::string("a\\b"));
    // 非法转义保留原样
    ASSERT_EQ(cppUnescape("a\\qb"), std::string("a\\qb"));
    ASSERT_EQ(cppUnescape(""), std::string(""));

    // ---- 往返一致性 ----
    ASSERT_EQ(cppUnescape(cppEscape("hello\nworld\t\"test\"")), std::string("hello\nworld\t\"test\""));

    // ---- convert 分发 ----
    ASSERT_EQ(convert(Format::Cpp, Direction::Escape, "a\nb"), std::string("a\\nb"));
    ASSERT_EQ(convert(Format::Cpp, Direction::Unescape, "a\\nb"), std::string("a\nb"));
}
```

- [ ] **Step 3: 运行测试验证失败**

```bash
cmake --build build --target ConverterTests --config Debug
```
Expected: 编译失败，`cppEscape`/`cppUnescape` 未定义。

- [ ] **Step 4: 实现 CppConverter.cpp**

写入 `D:\Workspace\ElectronApp\NStringTool\src\converters\CppConverter.cpp`：

```cpp
#include "CppConverter.h"
#include <cstdio>

std::string cppEscape(const std::string& input) {
    std::string out;
    out.reserve(input.size() * 2);
    for (size_t i = 0; i < input.size(); ++i) {
        unsigned char c = static_cast<unsigned char>(input[i]);
        switch (c) {
            case '\n': out += "\\n"; break;
            case '\t': out += "\\t"; break;
            case '\r': out += "\\r"; break;
            case '\\': out += "\\\\"; break;
            case '\"': out += "\\\""; break;
            default:
                if (c < 0x20 || c == 0x7F) {
                    // 其他控制字符用 \xNN
                    char buf[5];
                    std::snprintf(buf, sizeof(buf), "\\x%02X", c);
                    out += buf;
                } else {
                    out += static_cast<char>(c);
                }
                break;
        }
    }
    return out;
}

std::string cppUnescape(const std::string& input) {
    std::string out;
    out.reserve(input.size());
    size_t i = 0;
    while (i < input.size()) {
        if (input[i] != '\\') {
            out += input[i];
            ++i;
            continue;
        }
        // 遇到反斜杠
        if (i + 1 >= input.size()) {
            out += input[i]; // 末尾孤立反斜杠保留
            ++i;
            continue;
        }
        char next = input[i + 1];
        switch (next) {
            case 'n': out += '\n'; i += 2; break;
            case 't': out += '\t'; i += 2; break;
            case 'r': out += '\r'; i += 2; break;
            case '\\': out += '\\'; i += 2; break;
            case '\"': out += '\"'; i += 2; break;
            case '\'': out += '\''; i += 2; break;
            case '0': out += '\0'; i += 2; break;
            case 'x': {
                // \xNN：取后续两个十六进制
                if (i + 3 < input.size()) {
                    char hex[3] = { input[i+2], input[i+3], '\0' };
                    char* endp = nullptr;
                    long val = std::strtol(hex, &endp, 16);
                    if (endp == hex + 2 && val >= 0 && val <= 0xFF) {
                        out += static_cast<char>(static_cast<unsigned char>(val));
                        i += 4;
                        break;
                    }
                }
                // 非法 \x，保留原样
                out += input[i];
                ++i;
                break;
            }
            case 'u': {
                // \uNNNN：取后续四个十六进制
                if (i + 5 < input.size()) {
                    char hex[5] = { input[i+2], input[i+3], input[i+4], input[i+5], '\0' };
                    char* endp = nullptr;
                    long val = std::strtol(hex, &endp, 16);
                    if (endp == hex + 4) {
                        // 处理代理对
                        unsigned int cp = static_cast<unsigned int>(val);
                        if (cp >= 0xD800 && cp <= 0xDBFF) {
                            // 高代理项，尝试读低代理项 \uNNNN
                            if (i + 11 < input.size() && input[i+6] == '\\' && input[i+7] == 'u') {
                                char hex2[5] = { input[i+8], input[i+9], input[i+10], input[i+11], '\0' };
                                char* endp2 = nullptr;
                                long val2 = std::strtol(hex2, &endp2, 16);
                                if (endp2 == hex2 + 4) {
                                    unsigned int cp2 = static_cast<unsigned int>(val2);
                                    if (cp2 >= 0xDC00 && cp2 <= 0xDFFF) {
                                        unsigned int full = 0x10000 + ((cp - 0xD800) << 10) + (cp2 - 0xDC00);
                                        // 编码为 UTF-8（4 字节）
                        out += static_cast<char>(0xF0 | ((full >> 18) & 0x07));
                        out += static_cast<char>(0x80 | ((full >> 12) & 0x3F));
                        out += static_cast<char>(0x80 | ((full >> 6) & 0x3F));
                        out += static_cast<char>(0x80 | (full & 0x3F));
                                        i += 12;
                                        break;
                                    }
                                }
                            }
                            // 高代理项无配对，保留原样
                            out += input[i];
                            ++i;
                            break;
                        }
                        // 基本多语言平面字符，编码为 UTF-8
                        if (cp < 0x80) {
                            out += static_cast<char>(cp);
                        } else if (cp < 0x800) {
                            out += static_cast<char>(0xC0 | (cp >> 6));
                            out += static_cast<char>(0x80 | (cp & 0x3F));
                        } else {
                            out += static_cast<char>(0xE0 | ((cp >> 12) & 0x0F));
                            out += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
                            out += static_cast<char>(0x80 | (cp & 0x3F));
                        }
                        i += 6;
                        break;
                    }
                }
                // 非法 \u，保留原样
                out += input[i];
                ++i;
                break;
            }
            default:
                // 未知转义符，保留反斜杠和原字符
                out += input[i];
                out += input[i + 1];
                i += 2;
                break;
        }
    }
    return out;
}
```

- [ ] **Step 5: 写 convert 分发 Converters.cpp**

写入 `D:\Workspace\ElectronApp\NStringTool\src\converters\Converters.cpp`：

```cpp
#include "Converters.h"
#include "CppConverter.h"

std::string convert(Format format, Direction direction, const std::string& input) {
    switch (format) {
        case Format::Cpp:
            return direction == Direction::Escape ? cppEscape(input) : cppUnescape(input);
        case Format::Json:
        case Format::Html:
        case Format::Url:
            break; // 后续任务实现
    }
    return input; // 未实现的格式原样返回
}
```

- [ ] **Step 6: 运行测试验证通过**

```bash
cmake --build build --target ConverterTests --config Debug
./build/bin/ConverterTests.exe
```
Expected: 全部通过（通过数 ≥ 15，失败 0）。

- [ ] **Step 7: 提交**

```bash
git add src/converters/CppConverter.h src/converters/CppConverter.cpp src/converters/Converters.cpp tests/test_converters.cpp
git commit -m "实现 C/C++ 字符串转换器与分发函数"
```

---

### Task 4: JSON 字符串转换器

**Files:**
- Create: `src/converters/JsonConverter.h`
- Create: `src/converters/JsonConverter.cpp`
- Modify: `src/converters/Converters.cpp`（追加 Json 分支）
- Modify: `tests/test_converters.cpp`（追加 JSON 测试）

**Interfaces:**
- Consumes: `Converters.h`。
- Produces: `std::string jsonEscape(const std::string&)`、`std::string jsonUnescape(const std::string&)`。

- [ ] **Step 1: 写 JsonConverter.h**

写入 `D:\Workspace\ElectronApp\NStringTool\src\converters\JsonConverter.h`：

```cpp
#pragma once
#include <string>

// JSON 字符串转义（RFC 8259）：\" \\ \b \f \n \r \t \uXXXX
std::string jsonEscape(const std::string& input);

// JSON 字符串去转义：还原上述序列。非法序列保留原样。
std::string jsonUnescape(const std::string& input);
```

- [ ] **Step 2: 追加失败测试**

在 `tests/test_converters.cpp` 的 `runConverterTests()` 末尾（`convert` 分发测试之前）追加：

```cpp
    // ---- JSON escape ----
    ASSERT_EQ(jsonEscape("a\nb"), std::string("a\\nb"));
    ASSERT_EQ(jsonEscape("a\tb"), std::string("a\\tb"));
    ASSERT_EQ(jsonEscape("a\"b"), std::string("a\\\"b"));
    ASSERT_EQ(jsonEscape("a\\b"), std::string("a\\\\b"));
    ASSERT_EQ(jsonEscape("a\bb"), std::string("a\\bb"));
    ASSERT_EQ(jsonEscape("a\fb"), std::string("a\\fb"));
    ASSERT_EQ(jsonEscape("a\rb"), std::string("a\\rb"));
    ASSERT_EQ(jsonEscape("plain"), std::string("plain"));

    // ---- JSON unescape ----
    ASSERT_EQ(jsonUnescape("a\\nb"), std::string("a\nb"));
    ASSERT_EQ(jsonUnescape("a\\tb"), std::string("a\tb"));
    ASSERT_EQ(jsonUnescape("a\\\"b"), std::string("a\"b"));
    ASSERT_EQ(jsonUnescape("a\\\\b"), std::string("a\\b"));
    ASSERT_EQ(jsonUnescape("a\\bb"), std::string("a\bb"));
    ASSERT_EQ(jsonUnescape("a\\fb"), std::string("a\fb"));
    ASSERT_EQ(jsonUnescape("a\\rb"), std::string("a\rb"));
    // 非法转义保留原样
    ASSERT_EQ(jsonUnescape("a\\qb"), std::string("a\\qb"));

    // ---- JSON 往返 ----
    ASSERT_EQ(jsonUnescape(jsonEscape("hi\n\"x\"\t\\y")), std::string("hi\n\"x\"\t\\y"));
```

并在文件顶部 `#include` 区追加：

```cpp
#include "converters/JsonConverter.h"
```

- [ ] **Step 3: 运行测试验证失败**

```bash
cmake --build build --target ConverterTests --config Debug
```
Expected: 编译失败，`jsonEscape`/`jsonUnescape` 未定义。

- [ ] **Step 4: 实现 JsonConverter.cpp**

写入 `D:\Workspace\ElectronApp\NStringTool\src\converters\JsonConverter.cpp`：

```cpp
#include "JsonConverter.h"
#include <cstdio>

std::string jsonEscape(const std::string& input) {
    std::string out;
    out.reserve(input.size() * 2);
    for (size_t i = 0; i < input.size(); ++i) {
        unsigned char c = static_cast<unsigned char>(input[i]);
        switch (c) {
            case '\"': out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\b': out += "\\b"; break;
            case '\f': out += "\\f"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            case '\t': out += "\\t"; break;
            default:
                if (c < 0x20) {
                    char buf[7];
                    std::snprintf(buf, sizeof(buf), "\\u%04x", c);
                    out += buf;
                } else {
                    out += static_cast<char>(c);
                }
                break;
        }
    }
    return out;
}

std::string jsonUnescape(const std::string& input) {
    std::string out;
    out.reserve(input.size());
    size_t i = 0;
    while (i < input.size()) {
        if (input[i] != '\\') {
            out += input[i];
            ++i;
            continue;
        }
        if (i + 1 >= input.size()) {
            out += input[i];
            ++i;
            continue;
        }
        char next = input[i + 1];
        switch (next) {
            case '"': out += '"'; i += 2; break;
            case '\\': out += '\\'; i += 2; break;
            case '/': out += '/'; i += 2; break;
            case 'b': out += '\b'; i += 2; break;
            case 'f': out += '\f'; i += 2; break;
            case 'n': out += '\n'; i += 2; break;
            case 'r': out += '\r'; i += 2; break;
            case 't': out += '\t'; i += 2; break;
            case 'u': {
                if (i + 5 < input.size()) {
                    char hex[5] = { input[i+2], input[i+3], input[i+4], input[i+5], '\0' };
                    char* endp = nullptr;
                    long val = std::strtol(hex, &endp, 16);
                    if (endp == hex + 4) {
                        unsigned int cp = static_cast<unsigned int>(val);
                        // 代理对处理
                        if (cp >= 0xD800 && cp <= 0xDBFF) {
                            if (i + 11 < input.size() && input[i+6] == '\\' && input[i+7] == 'u') {
                                char hex2[5] = { input[i+8], input[i+9], input[i+10], input[i+11], '\0' };
                                char* endp2 = nullptr;
                                long val2 = std::strtol(hex2, &endp2, 16);
                                if (endp2 == hex2 + 4) {
                                    unsigned int cp2 = static_cast<unsigned int>(val2);
                                    if (cp2 >= 0xDC00 && cp2 <= 0xDFFF) {
                                        unsigned int full = 0x10000 + ((cp - 0xD800) << 10) + (cp2 - 0xDC00);
                                        out += static_cast<char>(0xF0 | ((full >> 18) & 0x07));
                                        out += static_cast<char>(0x80 | ((full >> 12) & 0x3F));
                                        out += static_cast<char>(0x80 | ((full >> 6) & 0x3F));
                                        out += static_cast<char>(0x80 | (full & 0x3F));
                                        i += 12;
                                        break;
                                    }
                                }
                            }
                            out += input[i];
                            ++i;
                            break;
                        }
                        if (cp < 0x80) {
                            out += static_cast<char>(cp);
                        } else if (cp < 0x800) {
                            out += static_cast<char>(0xC0 | (cp >> 6));
                            out += static_cast<char>(0x80 | (cp & 0x3F));
                        } else {
                            out += static_cast<char>(0xE0 | ((cp >> 12) & 0x0F));
                            out += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
                            out += static_cast<char>(0x80 | (cp & 0x3F));
                        }
                        i += 6;
                        break;
                    }
                }
                out += input[i];
                ++i;
                break;
            }
            default:
                out += input[i];
                out += input[i + 1];
                i += 2;
                break;
        }
    }
    return out;
}
```

- [ ] **Step 5: 在 Converters.cpp 追加 Json 分支**

把 `src/converters/Converters.cpp` 的 switch 改为：

```cpp
#include "Converters.h"
#include "CppConverter.h"
#include "JsonConverter.h"

std::string convert(Format format, Direction direction, const std::string& input) {
    switch (format) {
        case Format::Cpp:
            return direction == Direction::Escape ? cppEscape(input) : cppUnescape(input);
        case Format::Json:
            return direction == Direction::Escape ? jsonEscape(input) : jsonUnescape(input);
        case Format::Html:
        case Format::Url:
            break;
    }
    return input;
}
```

- [ ] **Step 6: 运行测试验证通过**

```bash
cmake --build build --target ConverterTests --config Debug
./build/bin/ConverterTests.exe
```
Expected: 全部通过。

- [ ] **Step 7: 提交**

```bash
git add src/converters/JsonConverter.h src/converters/JsonConverter.cpp src/converters/Converters.cpp tests/test_converters.cpp
git commit -m "实现 JSON 字符串转换器"
```

---

### Task 5: HTML/XML 实体转换器

**Files:**
- Create: `src/converters/HtmlConverter.h`
- Create: `src/converters/HtmlConverter.cpp`
- Modify: `src/converters/Converters.cpp`（追加 Html 分支）
- Modify: `tests/test_converters.cpp`（追加 HTML 测试）

**Interfaces:**
- Produces: `std::string htmlEscape(const std::string&)`、`std::string htmlUnescape(const std::string&)`。

- [ ] **Step 1: 写 HtmlConverter.h**

写入 `D:\Workspace\ElectronApp\NStringTool\src\converters\HtmlConverter.h`：

```cpp
#pragma once
#include <string>

// HTML/XML 实体转义：& < > " ' 转为命名实体
std::string htmlEscape(const std::string& input);

// HTML/XML 实体去转义：还原命名实体和数字实体（&#60; &#x3C;）。未知实体保留原样。
std::string htmlUnescape(const std::string& input);
```

- [ ] **Step 2: 追加失败测试**

在 `tests/test_converters.cpp` 顶部 `#include` 区追加：

```cpp
#include "converters/HtmlConverter.h"
```

在 `runConverterTests()` 中 JSON 往返测试之后、`convert` 分发测试之前追加：

```cpp
    // ---- HTML escape ----
    ASSERT_EQ(htmlEscape("a<b>c&d"), std::string("a&lt;b&gt;c&amp;d"));
    ASSERT_EQ(htmlEscape("\"q\""), std::string("&quot;q&quot;"));
    ASSERT_EQ(htmlEscape("'q'"), std::string("&#39;q&#39;"));
    ASSERT_EQ(htmlEscape("plain"), std::string("plain"));

    // ---- HTML unescape ----
    ASSERT_EQ(htmlUnescape("a&lt;b&gt;c&amp;d"), std::string("a<b>c&d"));
    ASSERT_EQ(htmlUnescape("&quot;q&quot;"), std::string("\"q\""));
    ASSERT_EQ(htmlUnescape("&#39;q&#39;"), std::string("'q'"));
    ASSERT_EQ(htmlUnescape("a&#60;b"), std::string("a<b"));
    ASSERT_EQ(htmlUnescape("a&#x3E;b"), std::string("a>b"));
    // 未知实体保留原样
    ASSERT_EQ(htmlUnescape("a&unknown;b"), std::string("a&unknown;b"));

    // ---- HTML 往返 ----
    ASSERT_EQ(htmlUnescape(htmlEscape("<x a=\"1\" b='2'>&</x>")), std::string("<x a=\"1\" b='2'>&</x>"));
```

- [ ] **Step 3: 运行测试验证失败**

```bash
cmake --build build --target ConverterTests --config Debug
```
Expected: 编译失败，`htmlEscape`/`htmlUnescape` 未定义。

- [ ] **Step 4: 实现 HtmlConverter.cpp**

写入 `D:\Workspace\ElectronApp\NStringTool\src\converters\HtmlConverter.cpp`：

```cpp
#include "HtmlConverter.h"
#include <cstdio>
#include <cstring>

std::string htmlEscape(const std::string& input) {
    std::string out;
    out.reserve(input.size() * 2);
    for (size_t i = 0; i < input.size(); ++i) {
        char c = input[i];
        switch (c) {
            case '&': out += "&amp;"; break;
            case '<': out += "&lt;"; break;
            case '>': out += "&gt;"; break;
            case '"': out += "&quot;"; break;
            case '\'': out += "&#39;"; break;
            default: out += c; break;
        }
    }
    return out;
}

// 在 input 的 pos 处匹配实体名，返回实体长度（含分号），通过 cp 返回码点
// 未匹配返回 0
static int matchNamedEntity(const std::string& input, size_t pos, unsigned int& cp) {
    struct NamedEntity { const char* name; unsigned int cp; };
    static const NamedEntity table[] = {
        {"amp;", 38}, {"lt;", 60}, {"gt;", 62}, {"quot;", 34},
        {"apos;", 39}, {"nbsp;", 160}, {"copy;", 169}, {"reg;", 174},
        {nullptr, 0}
    };
    for (int k = 0; table[k].name; ++k) {
        size_t len = std::strlen(table[k].name);
        if (pos + len <= input.size() && std::memcmp(input.data() + pos, table[k].name, len) == 0) {
            cp = table[k].cp;
            return static_cast<int>(len);
        }
    }
    return 0;
}

// 把 Unicode 码点编码为 UTF-8 追加到 out
static void appendUtf8(std::string& out, unsigned int cp) {
    if (cp < 0x80) {
        out += static_cast<char>(cp);
    } else if (cp < 0x800) {
        out += static_cast<char>(0xC0 | (cp >> 6));
        out += static_cast<char>(0x80 | (cp & 0x3F));
    } else if (cp < 0x10000) {
        out += static_cast<char>(0xE0 | ((cp >> 12) & 0x0F));
        out += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
        out += static_cast<char>(0x80 | (cp & 0x3F));
    } else {
        out += static_cast<char>(0xF0 | ((cp >> 18) & 0x07));
        out += static_cast<char>(0x80 | ((cp >> 12) & 0x3F));
        out += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
        out += static_cast<char>(0x80 | (cp & 0x3F));
    }
}

std::string htmlUnescape(const std::string& input) {
    std::string out;
    out.reserve(input.size());
    size_t i = 0;
    while (i < input.size()) {
        if (input[i] != '&') {
            out += input[i];
            ++i;
            continue;
        }
        // 找下一个 ';'
        size_t semi = input.find(';', i + 1);
        if (semi == std::string::npos) {
            out += input[i];
            ++i;
            continue;
        }
        // 数字实体 &#NN; 或 &#xHH;
        if (i + 2 < input.size() && input[i+1] == '#') {
            bool isHex = (i + 3 < input.size() && (input[i+2] == 'x' || input[i+2] == 'X'));
            size_t numStart = isHex ? i + 3 : i + 2;
            int base = isHex ? 16 : 10;
            char* endp = nullptr;
            std::string numStr = input.substr(numStart, semi - numStart);
            long val = std::strtol(numStr.c_str(), &endp, base);
            if (endp == numStr.c_str() + numStr.size() && val >= 0) {
                appendUtf8(out, static_cast<unsigned int>(val));
                i = semi + 1;
                continue;
            }
            // 非法数字实体，保留原样
            out += input[i];
            ++i;
            continue;
        }
        // 命名实体
        unsigned int cp = 0;
        int matched = matchNamedEntity(input, i + 1, cp);
        if (matched > 0) {
            appendUtf8(out, cp);
            i += 1 + matched;
            continue;
        }
        // 未知实体，保留 &
        out += input[i];
        ++i;
    }
    return out;
}
```

- [ ] **Step 5: 在 Converters.cpp 追加 Html 分支**

```cpp
#include "Converters.h"
#include "CppConverter.h"
#include "JsonConverter.h"
#include "HtmlConverter.h"

std::string convert(Format format, Direction direction, const std::string& input) {
    switch (format) {
        case Format::Cpp:
            return direction == Direction::Escape ? cppEscape(input) : cppUnescape(input);
        case Format::Json:
            return direction == Direction::Escape ? jsonEscape(input) : jsonUnescape(input);
        case Format::Html:
            return direction == Direction::Escape ? htmlEscape(input) : htmlUnescape(input);
        case Format::Url:
            break;
    }
    return input;
}
```

- [ ] **Step 6: 运行测试验证通过**

```bash
cmake --build build --target ConverterTests --config Debug
./build/bin/ConverterTests.exe
```
Expected: 全部通过。

- [ ] **Step 7: 提交**

```bash
git add src/converters/HtmlConverter.h src/converters/HtmlConverter.cpp src/converters/Converters.cpp tests/test_converters.cpp
git commit -m "实现 HTML/XML 实体转换器"
```

---

### Task 6: URL 编码转换器

**Files:**
- Create: `src/converters/UrlConverter.h`
- Create: `src/converters/UrlConverter.cpp`
- Modify: `src/converters/Converters.cpp`（追加 Url 分支）
- Modify: `tests/test_converters.cpp`（追加 URL 测试）

**Interfaces:**
- Produces: `std::string urlEscape(const std::string&)`、`std::string urlUnescape(const std::string&)`。

- [ ] **Step 1: 写 UrlConverter.h**

写入 `D:\Workspace\ElectronApp\NStringTool\src\converters\UrlConverter.h`：

```cpp
#pragma once
#include <string>

// URL 编码（RFC 3986）：非保留字符 A-Za-z0-9-_.~ 外全部 %XX，UTF-8 多字节按字节编码
std::string urlEscape(const std::string& input);

// URL 解码：%XX 还原为字节，连续字节组合为 UTF-8 字符串。非法 % 保留原样。
std::string urlUnescape(const std::string& input);
```

- [ ] **Step 2: 追加失败测试**

在 `tests/test_converters.cpp` 顶部 `#include` 区追加：

```cpp
#include "converters/UrlConverter.h"
```

在 `runConverterTests()` 中 HTML 往返测试之后、`convert` 分发测试之前追加：

```cpp
    // ---- URL escape ----
    ASSERT_EQ(urlEscape("hello world"), std::string("hello%20world"));
    ASSERT_EQ(urlEscape("a/b"), std::string("a%2Fb"));
    ASSERT_EQ(urlEscape("plain"), std::string("plain"));
    ASSERT_EQ(urlEscape(""), std::string(""));
    // 中文"中"的 UTF-8 是 E4 B8 AD
    ASSERT_EQ(urlEscape(std::string("\xE4\xB8\xAD")), std::string("%E4%B8%AD"));

    // ---- URL unescape ----
    ASSERT_EQ(urlUnescape("hello%20world"), std::string("hello world"));
    ASSERT_EQ(urlUnescape("a%2Fb"), std::string("a/b"));
    ASSERT_EQ(urlUnescape("%E4%B8%AD"), std::string("\xE4\xB8\xAD"));
    // 非法 % 保留原样
    ASSERT_EQ(urlUnescape("100%"), std::string("100%"));
    ASSERT_EQ(urlUnescape("a%ZZb"), std::string("a%ZZb"));
    ASSERT_EQ(urlUnescape(""), std::string(""));

    // ---- URL 往返 ----
    ASSERT_EQ(urlUnescape(urlEscape("hello 世界!")), std::string("hello 世界!"));
```

- [ ] **Step 3: 运行测试验证失败**

```bash
cmake --build build --target ConverterTests --config Debug
```
Expected: 编译失败，`urlEscape`/`urlUnescape` 未定义。

- [ ] **Step 4: 实现 UrlConverter.cpp**

写入 `D:\Workspace\ElectronApp\NStringTool\src\converters\UrlConverter.cpp`：

```cpp
#include "UrlConverter.h"
#include <cstdio>
#include <cstring>

static bool isUnreserved(unsigned char c) {
    return (c >= 'A' && c <= 'Z') ||
           (c >= 'a' && c <= 'z') ||
           (c >= '0' && c <= '9') ||
           c == '-' || c == '_' || c == '.' || c == '~';
}

std::string urlEscape(const std::string& input) {
    std::string out;
    out.reserve(input.size() * 3);
    for (size_t i = 0; i < input.size(); ++i) {
        unsigned char c = static_cast<unsigned char>(input[i]);
        if (isUnreserved(c)) {
            out += static_cast<char>(c);
        } else {
            char buf[4];
            std::snprintf(buf, sizeof(buf), "%%%02X", c);
            out += buf;
        }
    }
    return out;
}

static int hexVal(char c) {
    if (c >= '0' && c <= '9') return c - '0';
    if (c >= 'A' && c <= 'F') return c - 'A' + 10;
    if (c >= 'a' && c <= 'f') return c - 'a' + 10;
    return -1;
}

std::string urlUnescape(const std::string& input) {
    std::string out;
    out.reserve(input.size());
    size_t i = 0;
    while (i < input.size()) {
        if (input[i] != '%') {
            out += input[i];
            ++i;
            continue;
        }
        // 需要 %XX
        if (i + 2 < input.size()) {
            int h = hexVal(input[i+1]);
            int l = hexVal(input[i+2]);
            if (h >= 0 && l >= 0) {
                out += static_cast<char>(static_cast<unsigned char>(h * 16 + l));
                i += 3;
                continue;
            }
        }
        // 非法 %，保留原样
        out += input[i];
        ++i;
    }
    return out;
}
```

- [ ] **Step 5: 在 Converters.cpp 追加 Url 分支（补全）**

```cpp
#include "Converters.h"
#include "CppConverter.h"
#include "JsonConverter.h"
#include "HtmlConverter.h"
#include "UrlConverter.h"

std::string convert(Format format, Direction direction, const std::string& input) {
    switch (format) {
        case Format::Cpp:
            return direction == Direction::Escape ? cppEscape(input) : cppUnescape(input);
        case Format::Json:
            return direction == Direction::Escape ? jsonEscape(input) : jsonUnescape(input);
        case Format::Html:
            return direction == Direction::Escape ? htmlEscape(input) : htmlUnescape(input);
        case Format::Url:
            return direction == Direction::Escape ? urlEscape(input) : urlUnescape(input);
    }
    return input;
}
```

- [ ] **Step 6: 运行测试验证通过**

```bash
cmake --build build --target ConverterTests --config Debug
./build/bin/ConverterTests.exe
```
Expected: 全部通过（累计约 50+ 用例）。

- [ ] **Step 7: 提交**

```bash
git add src/converters/UrlConverter.h src/converters/UrlConverter.cpp src/converters/Converters.cpp tests/test_converters.cpp
git commit -m "实现 URL 编码转换器，转换核心层完成"
```

---

### Task 7: 编辑器操作层（EditorOps）

**Files:**
- Create: `src/EditorOps.h`
- Create: `src/EditorOps.cpp`

**Interfaces:**
- Consumes: `Converters.h`（Format/Direction/convert）。Notepad++ 窗口句柄由 `PluginEntry` 的 `setInfo` 存入全局，`EditorOps` 读取。
- Produces: `void applyConversion(Format format, Direction direction)` —— 读取当前选区（无选区则全文档），调用 `convert`，替换回编辑器，包裹撤销动作。

- [ ] **Step 1: 写 EditorOps.h**

写入 `D:\Workspace\ElectronApp\NStringTool\src\EditorOps.h`：

```cpp
#pragma once
#include "Converters.h"

// 保存 Notepad++ 与 Scintilla 句柄（由 PluginEntry::setInfo 调用）
void setNppHandles(HWND nppHandle, HWND scintillaMain, HWND scintillaSecond);

// 对当前编辑器执行转换：
// 有主选区时转换选区，无选区时转换整个文档。
// 整个替换作为一个撤销单元（Ctrl+Z 一次还原）。
void applyConversion(Format format, Direction direction);
```

- [ ] **Step 2: 实现 EditorOps.cpp**

写入 `D:\Workspace\ElectronApp\NStringTool\src\EditorOps.cpp`：

```cpp
#include "EditorOps.h"
#include "Converters.h"
#include "Scintilla.h"
#include "Notepad_plus_msgs.h"
#include <windows.h>
#include <string>
#include <vector>

namespace {
    HWND g_nppHandle = nullptr;
    HWND g_scintillaMain = nullptr;
    HWND g_scintillaSecond = nullptr;

    // 向 Scintilla 发送消息的便捷封装
    inline LRESULT sciSend(HWND sci, UINT msg, WPARAM w = 0, LPARAM l = 0) {
        return ::SendMessageW(sci, msg, w, l);
    }

    // 获取当前活跃的 Scintilla 视图
    HWND getCurrentScintilla() {
        int which = 0;
        ::SendMessageW(g_nppHandle, NPPM_GETCURRENTSCINTILLA, 0, reinterpret_cast<LPARAM>(&which));
        return which == 0 ? g_scintillaMain : g_scintillaSecond;
    }
}

void setNppHandles(HWND nppHandle, HWND scintillaMain, HWND scintillaSecond) {
    g_nppHandle = nppHandle;
    g_scintillaMain = scintillaMain;
    g_scintillaSecond = scintillaSecond;
}

void applyConversion(Format format, Direction direction) {
    if (!g_nppHandle) return;

    HWND sci = getCurrentScintilla();
    if (!sci) return;

    try {
        LRESULT selStart = sciSend(sci, SCI_GETSELECTIONSTART);
        LRESULT selEnd = sciSend(sci, SCI_GETSELECTIONEND);
        LRESULT docLen = sciSend(sci, SCI_GETLENGTH);

        bool hasSelection = (selEnd > selStart);
        LRESULT targetStart = hasSelection ? selStart : 0;
        LRESULT targetEnd = hasSelection ? selEnd : docLen;
        LRESULT targetLen = targetEnd - targetStart;

        if (targetLen <= 0) return; // 空文档或空选区，无操作

        // 读取目标范围文本
        std::vector<char> buf(static_cast<size_t>(targetLen) + 1, '\0');
        Sci_TextRangeFull tr;
        tr.chrg.cpMin = targetStart;
        tr.chrg.cpMax = targetEnd;
        tr.lpstrText = buf.data();
        sciSend(sci, SCI_GETTEXTRANGEFULL, 0, reinterpret_cast<LPARAM>(&tr));

        std::string input(buf.data(), static_cast<size_t>(targetLen));
        std::string output = convert(format, direction, input);

        // 包裹撤销动作，替换目标范围
        sciSend(sci, SCI_BEGINUNDOACTION);
        sciSend(sci, SCI_SETTARGETRANGE, static_cast<WPARAM>(targetStart), static_cast<LPARAM>(targetEnd));
        sciSend(sci, SCI_REPLACETARGET, static_cast<WPARAM>(output.size()), reinterpret_cast<LPARAM>(output.c_str()));
        sciSend(sci, SCI_ENDUNDOACTION);

        // 重新选中新文本
        LRESULT newEnd = targetStart + static_cast<LRESULT>(output.size());
        sciSend(sci, SCI_SETSEL, static_cast<WPARAM>(targetStart), static_cast<LPARAM>(newEnd));
    } catch (...) {
        std::wstring msg = L"转换过程中发生错误。";
        ::MessageBoxW(g_nppHandle, msg.c_str(), L"NStringTool", MB_OK | MB_ICONWARNING);
    }
}
```

- [ ] **Step 3: 验证插件 DLL 编译（此时 PluginEntry 尚未实现，预期链接失败于入口符号）**

```bash
cmake --build build --target NStringTool --config Debug
```
Expected: 编译 EditorOps.cpp 成功，但链接阶段因缺少 6 个导出函数（PluginEntry 未写）而失败。确认 EditorOps 本身无语法错误。

- [ ] **Step 4: 提交**

```bash
git add src/EditorOps.h src/EditorOps.cpp
git commit -m "实现编辑器操作层：选区读取、替换、撤销分组"
```

---

### Task 8: 插件入口层（PluginEntry）

**Files:**
- Create: `src/PluginEntry.cpp`

**Interfaces:**
- Consumes: `EditorOps.h`（setNppHandles/applyConversion）、`Converters.h`（Format/Direction）。
- Produces: 6 个 `extern "C" __declspec(dllexport)` 导出函数，构成可被 Notepad++ 加载的 DLL。

- [ ] **Step 1: 实现 PluginEntry.cpp**

写入 `D:\Workspace\ElectronApp\NStringTool\src\PluginEntry.cpp`：

```cpp
#include "PluginInterface.h"
#include "EditorOps.h"
#include "Converters.h"
#include <windows.h>
#include <cwchar>

namespace {
    const wchar_t* kPluginName = L"NStringTool";
    NppData g_nppData;

    // 菜单项索引
    enum MenuItemIndex {
        // C/C++
        IDX_CPP_ESCAPE = 0,
        IDX_CPP_UNESCAPE,
        // JSON
        IDX_JSON_ESCAPE,
        IDX_JSON_UNESCAPE,
        // HTML
        IDX_HTML_ESCAPE,
        IDX_HTML_UNESCAPE,
        // URL
        IDX_URL_ESCAPE,
        IDX_URL_UNESCAPE,
        MENU_COUNT
    };

    // 菜单项名称（UTF-16）
    const wchar_t* kMenuNames[MENU_COUNT] = {
        L"C/C++ 字符串 - 转义",
        L"C/C++ 字符串 - 去转义",
        L"JSON 字符串 - 转义",
        L"JSON 字符串 - 去转义",
        L"HTML/XML 实体 - 转义",
        L"HTML/XML 实体 - 去转义",
        L"URL 编码 - 转义",
        L"URL 编码 - 去转义",
    };

    FuncItem g_funcItems[MENU_COUNT];

    // 菜单回调分发
    void dispatchCommand(int cmdId) {
        int index = cmdId - g_funcItems[0]._cmdID;
        if (index < 0 || index >= MENU_COUNT) return;

        Format format;
        Direction direction;
        switch (index) {
            case IDX_CPP_ESCAPE:     format = Format::Cpp;  direction = Direction::Escape;   break;
            case IDX_CPP_UNESCAPE:   format = Format::Cpp;  direction = Direction::Unescape; break;
            case IDX_JSON_ESCAPE:    format = Format::Json; direction = Direction::Escape;   break;
            case IDX_JSON_UNESCAPE:  format = Format::Json; direction = Direction::Unescape; break;
            case IDX_HTML_ESCAPE:    format = Format::Html; direction = Direction::Escape;   break;
            case IDX_HTML_UNESCAPE:  format = Format::Html; direction = Direction::Unescape; break;
            case IDX_URL_ESCAPE:     format = Format::Url;  direction = Direction::Escape;   break;
            case IDX_URL_UNESCAPE:   format = Format::Url;  direction = Direction::Unescape; break;
            default: return;
        }
        applyConversion(format, direction);
    }
}

extern "C" __declspec(dllexport) void setInfo(NppData nppData) {
    g_nppData = nppData;
    setNppHandles(nppData._nppHandle, nppData._scintillaMainHandle, nppData._scintillaSecondHandle);
}

extern "C" __declspec(dllexport) const wchar_t* getName() {
    return kPluginName;
}

extern "C" __declspec(dllexport) FuncItem* getFuncsArray(int* nbFItems) {
    // 初始化菜单项
    for (int i = 0; i < MENU_COUNT; ++i) {
        wcscpy_s(g_funcItems[i]._itemName, menuItemSize, kMenuNames[i]);
        g_funcItems[i]._pFunc = nullptr; // 用 messageProc 或 cmdID 分发；这里用 cmdID
        g_funcItems[i]._init2Check = false;
        g_funcItems[i]._pShKey = nullptr;
    }
    // 每项绑定独立回调：用 lambda 不便，改为统一通过 cmdID 分发
    // Notepad++ 会在加载时为每个 FuncItem 分配 _cmdID
    *nbFItems = MENU_COUNT;
    return g_funcItems;
}

extern "C" __declspec(dllexport) void beNotified(SCNotification* notify) {
    // 本插件无需处理通知
    (void)notify;
}

extern "C" __declspec(dllexport) LRESULT messageProc(UINT msg, WPARAM wParam, LPARAM lParam) {
    // 本插件不拦截 Windows 消息
    (void)msg; (void)wParam; (void)lParam;
    return TRUE;
}

extern "C" __declspec(dllexport) BOOL isUnicode() {
    return TRUE;
}
```

**注意：** 上面 `getFuncsArray` 把 `_pFunc` 设为 nullptr，实际菜单点击需要回调。Notepad++ 的 `FuncItem._pFunc` 是 `PFUNCPLUGINCMD`（无参 void 函数）。正确做法是每项绑定一个独立回调。下面 Step 2 修正。

- [ ] **Step 2: 修正菜单回调绑定**

把 `PluginEntry.cpp` 中 `getFuncsArray` 改为以下实现，并为每个菜单项定义独立回调函数（放在 `namespace` 内 `dispatchCommand` 之前）：

在 `namespace` 内 `dispatchCommand` 之前追加 8 个回调：

```cpp
    void onCppEscape()    { applyConversion(Format::Cpp,  Direction::Escape); }
    void onCppUnescape()  { applyConversion(Format::Cpp,  Direction::Unescape); }
    void onJsonEscape()   { applyConversion(Format::Json, Direction::Escape); }
    void onJsonUnescape() { applyConversion(Format::Json, Direction::Unescape); }
    void onHtmlEscape()   { applyConversion(Format::Html, Direction::Escape); }
    void onHtmlUnescape() { applyConversion(Format::Html, Direction::Unescape); }
    void onUrlEscape()    { applyConversion(Format::Url,  Direction::Escape); }
    void onUrlUnescape()  { applyConversion(Format::Url,  Direction::Unescape); }
```

把 `getFuncsArray` 改为：

```cpp
extern "C" __declspec(dllexport) FuncItem* getFuncsArray(int* nbFItems) {
    static PFUNCPLUGINCMD handlers[MENU_COUNT] = {
        onCppEscape, onCppUnescape,
        onJsonEscape, onJsonUnescape,
        onHtmlEscape, onHtmlUnescape,
        onUrlEscape, onUrlUnescape
    };
    for (int i = 0; i < MENU_COUNT; ++i) {
        wcscpy_s(g_funcItems[i]._itemName, menuItemSize, kMenuNames[i]);
        g_funcItems[i]._pFunc = handlers[i];
        g_funcItems[i]._init2Check = false;
        g_funcItems[i]._pShKey = nullptr;
    }
    *nbFItems = MENU_COUNT;
    return g_funcItems;
}
```

并删除不再使用的 `dispatchCommand` 函数（避免未使用警告）。

- [ ] **Step 3: 编译插件 DLL**

```bash
cmake --build build --target NStringTool --config Release
```
Expected: 生成 `build/bin/Release/NStringTool.dll`（或 `build/bin/NStringTool.dll`，取决于生成器）。无错误。

- [ ] **Step 4: 提交**

```bash
git add src/PluginEntry.cpp
git commit -m "实现插件入口层：6 个导出函数与菜单回调"
```

---

### Task 9: 集成测试与文档完善

**Files:**
- Modify: `README.md`

**Interfaces:**
- 无新接口。本任务验证完整 DLL 可被 Notepad++ 加载，8 个菜单项可用。

- [ ] **Step 1: 完整构建 DLL**

```bash
cd "D:\Workspace\ElectronApp\NStringTool"
cmake --build build --target NStringTool --config Release
```
Expected: 生成 `build/bin/NStringTool.dll`（x64）。

- [ ] **Step 2: 部署到 Notepad++ 插件目录**

先找到 Notepad++ 安装目录（通常是 `C:\Program Files\Notepad++`）。创建插件子目录并复制 DLL：

```bash
# 假设 Notepad++ 安装在 C:\Program Files\Notepad++
mkdir -p "C:\Program Files\Notepad++\plugins\NStringTool"
cp build/bin/NStringTool.dll "C:\Program Files\Notepad++\plugins\NStringTool\NStringTool.dll"
```

注意：需要管理员权限写入 Program Files。若权限不足，用便携版 Notepad++ 或修改为用户目录下的便携版路径。

- [ ] **Step 3: 启动 Notepad++ 验证加载**

启动 Notepad++，检查"插件"菜单下是否出现"NStringTool"子菜单及 8 个菜单项。若无，检查 DLL 是否 x64 与 Notepad++ 架构匹配。

- [ ] **Step 4: 手动功能验证清单**

在 Notepad++ 中逐项验证：

1. 新建文件，输入 `a\nb`（字面），选中，点"C/C++ 字符串 - 去转义" → 变为 `a` + 换行 + `b`
2. 选中上一步结果，点"C/C++ 字符串 - 转义" → 还原为 `a\nb`
3. 输入 `hello 世界`，选中，点"URL 编码 - 转义" → `hello%20%E4%B8%96%E7%95%8C`
4. 选中上一步结果，点"URL 编码 - 去转义" → 还原
5. 输入 `<a>&"x"`，选中，点"HTML/XML 实体 - 转义" → `&lt;a&gt;&amp;&quot;x&quot;`，再去转义还原
6. 输入 `{"a":"b\nc"}`，选中，点"JSON 字符串 - 去转义"测试
7. 不选中文本，点任意转义 → 整个文档被转换
8. 任意操作后按 Ctrl+Z → 一次还原
9. 输入 emoji `😀`（UTF-8 F0 9F 98 80），转义再去转义 → 不乱码

- [ ] **Step 5: 完善 README.md**

用以下内容替换 `README.md`：

```markdown
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

需要 Visual Studio 2022（或 2019）+ CMake。

```bash
git clone <repo>
cd NStringTool
cmake -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release
```

生成 `build/bin/Release/NStringTool.dll`（x64）。

## 安装

把 `NStringTool.dll` 复制到 Notepad++ 的插件目录：

```
<Notepad++安装目录>\plugins\NStringTool\NStringTool.dll
```

注意 DLL 必须放在以插件名命名的子文件夹内（Notepad++ 7.6+ 要求）。重启 Notepad++。

## 测试

转换核心层有单元测试：

```bash
cmake --build build --target ConverterTests --config Debug
./build/bin/ConverterTests.exe
```

## 技术栈

C++17、CMake、Notepad++ Plugin SDK、Scintilla API、Win32。
```

- [ ] **Step 6: 提交**

```bash
git add README.md
git commit -m "完善 README：构建、安装、测试说明"
```

---

### Task 10: 最终验证与收尾

**Files:**
- 无文件修改，仅验证。

- [ ] **Step 1: 运行全部单元测试**

```bash
cd "D:\Workspace\ElectronApp\NStringTool"
cmake --build build --target ConverterTests --config Debug
./build/bin/ConverterTests.exe
```
Expected: 全部通过，失败 0。

- [ ] **Step 2: 重新构建 Release DLL**

```bash
cmake --build build --target NStringTool --config Release
```
Expected: 无错误，生成 `build/bin/Release/NStringTool.dll`。

- [ ] **Step 3: 重新部署并启动 Notepad++ 验证**

重复 Task 9 Step 2-4，确认所有功能正常。

- [ ] **Step 4: 确认工作区干净**

```bash
git status
```
Expected: nothing to commit, working tree clean.

- [ ] **Step 5: 查看提交历史**

```bash
git log --oneline
```
Expected: 9 条提交（Task 1-9 各一条），信息清晰。
