#include "JsonConverter.h"
#include <cstdio>
#include <cstdlib>

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
