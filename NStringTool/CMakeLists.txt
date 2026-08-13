cmake_minimum_required(VERSION 3.15)
project(NStringTool LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 注：以下两个目标块整体注释。原因：Task 1 阶段 src/ 与 tests/ 下尚无任何 .cpp，
# CMake 不接受零源文件的 SHARED 库 / 可执行目标（配置阶段报 "No SOURCES given"）。
# 后续任务创建对应源文件后，取消注释整个目标块（含 add_*、target_*、set_target_*）。
# 取消注释时按需启用各源文件行即可。

# ── 插件 DLL 目标 ──────────────────────────────────────────────
# Task 8：取消注释 src/PluginEntry.cpp，至此所有源文件启用，DLL 可完整链接。
add_library(NStringTool SHARED
    src/PluginEntry.cpp
    src/EditorOps.cpp
    src/converters/Converters.cpp
    src/converters/CppConverter.cpp
    src/converters/JsonConverter.cpp
    src/converters/HtmlConverter.cpp
    src/converters/UrlConverter.cpp
    src/converters/HexArrayConverter.cpp
)
target_include_directories(NStringTool PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_CURRENT_SOURCE_DIR}/sdk
)
target_link_libraries(NStringTool PRIVATE user32)
# 源文件含中文注释，需 /utf-8 让 MSVC 正确解析 UTF-8 源（避免 C4819 警告与
# GBK 误读导致的反斜杠行接续语法错误）。VS 2015 支持 /utf-8。
target_compile_options(NStringTool PRIVATE /utf-8)
set_target_properties(NStringTool PROPERTIES
    OUTPUT_NAME "NStringTool"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

# ── 测试程序目标（仅转换核心层）──────────────────────────────
# 已取消注释：test_main.cpp / test_converters.cpp 已在本任务创建。
# 各 Converter 源文件行仍注释，待 Task 3-6 创建对应 .cpp 后逐个启用。
add_executable(ConverterTests
    tests/test_main.cpp
    tests/test_converters.cpp
    src/converters/CppConverter.cpp
    src/converters/Converters.cpp
    src/converters/JsonConverter.cpp
    src/converters/HtmlConverter.cpp
    src/converters/UrlConverter.cpp
    src/converters/HexArrayConverter.cpp
)
target_include_directories(ConverterTests PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)
# 源文件含中文注释，需 /utf-8 让 MSVC 正确解析 UTF-8 源（避免 C4819 警告与
# GBK 误读导致的反斜杠行接续语法错误）。VS 2015 支持 /utf-8。
target_compile_options(ConverterTests PRIVATE /utf-8)
