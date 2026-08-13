#pragma once
#include "Converters.h"
#include <windows.h>
#include <string>
#include <functional>

// 转换函数类型：输入待转换文本，返回转换结果
// 用 std::function 而非裸函数指针，以支持捕获参数的 lambda 与普通函数
using ConvertFn = std::function<std::string(const std::string&)>;

// 保存 Notepad++ 与 Scintilla 句柄（由 PluginEntry::setInfo 调用）
void setNppHandles(HWND nppHandle, HWND scintillaMain, HWND scintillaSecond);

// 对当前编辑器执行转换：
// 有主选区时转换选区，无选区时转换整个文档。
// 整个替换作为一个撤销单元（Ctrl+Z 一次还原）。
void applyConversion(Format format, Direction direction);

// 用自定义转换函数执行上述转换流程（复用相同 Scintilla 读写/撤销逻辑）
void applyConversionWith(ConvertFn fn);
