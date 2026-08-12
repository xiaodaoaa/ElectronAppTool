#include "Converters.h"
#include "CppConverter.h"
#include "JsonConverter.h"
#include "HtmlConverter.h"
#include "UrlConverter.h"

std::string convert(Format format, Direction direction, const std::string& input) {
    switch (format) {
        case Format::Cpp:
            return direction == Direction::Escape ? cppEscape(input) : cppUnescape(input);
        case Format::Json:
            return direction == Direction::Escape ? jsonEscape(input) : jsonUnescape(input);
        case Format::Html:
            return direction == Direction::Escape ? htmlEscape(input) : htmlUnescape(input);
        case Format::Url:
            return direction == Direction::Escape ? urlEscape(input) : urlUnescape(input);
    }
    return input;
}
