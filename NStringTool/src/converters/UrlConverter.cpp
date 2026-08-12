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
