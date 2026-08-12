#pragma once
#include <string>

// JSON 字符串转义（RFC 8259）：\" \\ \b \f \n \r \t \uXXXX
std::string jsonEscape(const std::string& input);

// JSON 字符串去转义：还原上述序列。非法序列保留原样。
std::string jsonUnescape(const std::string& input);
