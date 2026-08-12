#include "CppConverter.h"
#include <cstdio>
#include <cstdlib>

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
