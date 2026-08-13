#include "PluginInterface.h"
#include "EditorOps.h"
#include "Converters.h"
#include "converters/HexArrayConverter.h"
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
        // 十六进制 → C 数组
        IDX_HEX_TO_ARRAY,
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
        L"十六进制 - 转C数组",
    };

    FuncItem g_funcItems[MENU_COUNT];

    // 菜单回调（Step 2 修正版：每项独立回调，绑定到 _pFunc）
    void onCppEscape()    { applyConversion(Format::Cpp,  Direction::Escape); }
    void onCppUnescape()  { applyConversion(Format::Cpp,  Direction::Unescape); }
    void onJsonEscape()   { applyConversion(Format::Json, Direction::Escape); }
    void onJsonUnescape() { applyConversion(Format::Json, Direction::Unescape); }
    void onHtmlEscape()   { applyConversion(Format::Html, Direction::Escape); }
    void onHtmlUnescape() { applyConversion(Format::Html, Direction::Unescape); }
    void onUrlEscape()    { applyConversion(Format::Url,  Direction::Escape); }
    void onUrlUnescape()  { applyConversion(Format::Url,  Direction::Unescape); }
    // 十六进制转 C 数组：自动跳过空白，兼容无分隔/带空格两种输入
    void onHexToArray()   { applyConversionWith(hexToCArray); }
}

extern "C" __declspec(dllexport) void setInfo(NppData nppData) {
    g_nppData = nppData;
    setNppHandles(nppData._nppHandle, nppData._scintillaMainHandle, nppData._scintillaSecondHandle);
}

extern "C" __declspec(dllexport) const wchar_t* getName() {
    return kPluginName;
}

extern "C" __declspec(dllexport) FuncItem* getFuncsArray(int* nbFItems) {
    static PFUNCPLUGINCMD handlers[MENU_COUNT] = {
        onCppEscape, onCppUnescape,
        onJsonEscape, onJsonUnescape,
        onHtmlEscape, onHtmlUnescape,
        onUrlEscape, onUrlUnescape,
        onHexToArray
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
