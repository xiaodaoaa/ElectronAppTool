#pragma once
#include <string>

// HTML/XML 实体转义：& < > " ' 转为命名实体
std::string htmlEscape(const std::string& input);

// HTML/XML 实体去转义：还原命名实体和数字实体（&#60; &#x3C;）。未知实体保留原样。
std::string htmlUnescape(const std::string& input);
