#pragma once
#include "Converters.h"
#include <windows.h>

// 保存 Notepad++ 与 Scintilla 句柄（由 PluginEntry::setInfo 调用）
void setNppHandles(HWND nppHandle, HWND scintillaMain, HWND scintillaSecond);

// 对当前编辑器执行转换：
// 有主选区时转换选区，无选区时转换整个文档。
// 整个替换作为一个撤销单元（Ctrl+Z 一次还原）。
void applyConversion(Format format, Direction direction);
