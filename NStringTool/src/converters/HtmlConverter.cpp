#include "HtmlConverter.h"
#include <cstdio>
#include <cstring>
#include <cstdlib>

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
