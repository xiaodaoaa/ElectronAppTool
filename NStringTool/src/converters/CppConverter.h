#pragma once
#include <string>

// C/C++ 字符串转义：把控制字符和特殊字符转为 \n \t \" \\ \xNN 等
std::string cppEscape(const std::string& input);

// C/C++ 字符串去转义：把 \n \t \" \\ \xNN \uNNNN 还原为真实字符
// 非法转义序列保留原样（如 \q 保留为 \q）
std::string cppUnescape(const std::string& input);
