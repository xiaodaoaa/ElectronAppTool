#pragma once
#include <string>

// URL 编码（RFC 3986）：非保留字符 A-Za-z0-9-_.~ 外全部 %XX，UTF-8 多字节按字节编码
std::string urlEscape(const std::string& input);

// URL 解码：%XX 还原为字节，连续字节组合为 UTF-8 字符串。非法 % 保留原样。
std::string urlUnescape(const std::string& input);
