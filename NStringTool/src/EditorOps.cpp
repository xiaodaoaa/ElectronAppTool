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
    applyConversionWith([&](const std::string& input) {
        return convert(format, direction, input);
    });
}

void applyConversionWith(ConvertFn fn) {
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
        std::string output = fn(input);

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
