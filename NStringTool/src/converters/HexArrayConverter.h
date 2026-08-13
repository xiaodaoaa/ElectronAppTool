#pragma once
#include <string>

// 把十六进制字符串转为 C/C++ 字节数组初始化列表。
// 自动跳过空白字符，因此兼容无分隔（"112233"）与带空格（"11 22 33"）两种输入。
// 输出形如：{0x11, 0x22, 0x33, 0x44, 0x55, 0xFF, 0xEE}
// 输入非法（含非十六进制字符、或十六进制位数不为偶数）时返回原样 input，不转换。
std::string hexToCArray(const std::string& input);
