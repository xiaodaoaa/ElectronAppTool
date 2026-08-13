#include "HexArrayConverter.h"
#include <cctype>
#include <cstdio>

// 返回字符的十六进制值（0-15），非十六进制字符返回 -1。
static int hexVal(unsigned char c) {
    if (c >= '0' && c <= '9') return c - '0';
    if (c >= 'a' && c <= 'f') return c - 'a' + 10;
    if (c >= 'A' && c <= 'F') return c - 'A' + 10;
    return -1;
}

std::string hexToCArray(const std::string& input) {
    // 覆盖输入：自动跳过空白字符（兼容无分隔 / 带空格两种写法）
    std::string hexDigits;
    hexDigits.reserve(input.size());
    for (size_t i = 0; i < input.size(); ++i) {
        unsigned char c = static_cast<unsigned char>(input[i]);
        if (std::isspace(c)) continue;
        if (hexVal(c) < 0) {
            // 遇到非法十六进制字符，整个输入不做转换，原样返回
            return input;
        }
        hexDigits += static_cast<char>(c);
    }
    // 无十六进制内容，或位数不为偶数（无法凑成完整字节），原样返回
    if (hexDigits.empty() || (hexDigits.size() % 2) != 0) {
        return input;
    }

    std::string out;
    out.reserve(hexDigits.size() / 2 * 5 + 1); // 每字节 "0xNN, " 约 5 字符 + 花括号
    out += "{";
    for (size_t i = 0; i < hexDigits.size(); i += 2) {
        int hi = hexVal(static_cast<unsigned char>(hexDigits[i]));
        int lo = hexVal(static_cast<unsigned char>(hexDigits[i + 1]));
        char buf[8];
        std::snprintf(buf, sizeof(buf), "0x%02X", hi * 16 + lo);
        out += buf;
        if (i + 2 < hexDigits.size()) out += ", ";
    }
    out += "}";
    return out;
}
