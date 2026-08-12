#pragma once

#include <string>

enum class Format {
    Cpp,
    Json,
    Html,
    Url
};

enum class Direction {
    Escape,
    Unescape
};

// 统一转换入口。各 Converter 在各自 .cpp 中实现，
// 由 convert() 按 format/direction 分发。
std::string convert(Format format, Direction direction, const std::string& input);
