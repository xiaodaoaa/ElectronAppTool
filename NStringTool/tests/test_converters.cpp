#include "test_macros.h"
#include "Converters.h"
#include "converters/CppConverter.h"
#include "converters/JsonConverter.h"
#include "converters/HtmlConverter.h"
#include "converters/UrlConverter.h"
#include <string>

void runConverterTests() {
    // ---- C/C++ escape ----
    ASSERT_EQ(cppEscape("a\nb"), std::string("a\\nb"));
    ASSERT_EQ(cppEscape("a\tb"), std::string("a\\tb"));
    ASSERT_EQ(cppEscape("a\rb"), std::string("a\\rb"));
    ASSERT_EQ(cppEscape("a\"b"), std::string("a\\\"b"));
    ASSERT_EQ(cppEscape("a\\b"), std::string("a\\\\b"));
    ASSERT_EQ(cppEscape("plain"), std::string("plain"));
    ASSERT_EQ(cppEscape(""), std::string(""));

    // ---- C/C++ unescape ----
    ASSERT_EQ(cppUnescape("a\\nb"), std::string("a\nb"));
    ASSERT_EQ(cppUnescape("a\\tb"), std::string("a\tb"));
    ASSERT_EQ(cppUnescape("a\\rb"), std::string("a\rb"));
    ASSERT_EQ(cppUnescape("a\\\"b"), std::string("a\"b"));
    ASSERT_EQ(cppUnescape("a\\\\b"), std::string("a\\b"));
    // 非法转义保留原样
    ASSERT_EQ(cppUnescape("a\\qb"), std::string("a\\qb"));
    ASSERT_EQ(cppUnescape(""), std::string(""));

    // ---- 往返一致性 ----
    ASSERT_EQ(cppUnescape(cppEscape("hello\nworld\t\"test\"")), std::string("hello\nworld\t\"test\""));

    // ---- JSON escape ----
    ASSERT_EQ(jsonEscape("a\nb"), std::string("a\\nb"));
    ASSERT_EQ(jsonEscape("a\tb"), std::string("a\\tb"));
    ASSERT_EQ(jsonEscape("a\"b"), std::string("a\\\"b"));
    ASSERT_EQ(jsonEscape("a\\b"), std::string("a\\\\b"));
    ASSERT_EQ(jsonEscape("a\bb"), std::string("a\\bb"));
    ASSERT_EQ(jsonEscape("a\fb"), std::string("a\\fb"));
    ASSERT_EQ(jsonEscape("a\rb"), std::string("a\\rb"));
    ASSERT_EQ(jsonEscape("plain"), std::string("plain"));

    // ---- JSON unescape ----
    ASSERT_EQ(jsonUnescape("a\\nb"), std::string("a\nb"));
    ASSERT_EQ(jsonUnescape("a\\tb"), std::string("a\tb"));
    ASSERT_EQ(jsonUnescape("a\\\"b"), std::string("a\"b"));
    ASSERT_EQ(jsonUnescape("a\\\\b"), std::string("a\\b"));
    ASSERT_EQ(jsonUnescape("a\\bb"), std::string("a\bb"));
    ASSERT_EQ(jsonUnescape("a\\fb"), std::string("a\fb"));
    ASSERT_EQ(jsonUnescape("a\\rb"), std::string("a\rb"));
    // 非法转义保留原样
    ASSERT_EQ(jsonUnescape("a\\qb"), std::string("a\\qb"));

    // ---- JSON 往返 ----
    ASSERT_EQ(jsonUnescape(jsonEscape("hi\n\"x\"\t\\y")), std::string("hi\n\"x\"\t\\y"));

    // ---- HTML escape ----
    ASSERT_EQ(htmlEscape("a<b>c&d"), std::string("a&lt;b&gt;c&amp;d"));
    ASSERT_EQ(htmlEscape("\"q\""), std::string("&quot;q&quot;"));
    ASSERT_EQ(htmlEscape("'q'"), std::string("&#39;q&#39;"));
    ASSERT_EQ(htmlEscape("plain"), std::string("plain"));

    // ---- HTML unescape ----
    ASSERT_EQ(htmlUnescape("a&lt;b&gt;c&amp;d"), std::string("a<b>c&d"));
    ASSERT_EQ(htmlUnescape("&quot;q&quot;"), std::string("\"q\""));
    ASSERT_EQ(htmlUnescape("&#39;q&#39;"), std::string("'q'"));
    ASSERT_EQ(htmlUnescape("a&#60;b"), std::string("a<b"));
    ASSERT_EQ(htmlUnescape("a&#x3E;b"), std::string("a>b"));
    // 未知实体保留原样
    ASSERT_EQ(htmlUnescape("a&unknown;b"), std::string("a&unknown;b"));

    // ---- HTML 往返 ----
    ASSERT_EQ(htmlUnescape(htmlEscape("<x a=\"1\" b='2'>&</x>")), std::string("<x a=\"1\" b='2'>&</x>"));

    // ---- URL escape ----
    ASSERT_EQ(urlEscape("hello world"), std::string("hello%20world"));
    ASSERT_EQ(urlEscape("a/b"), std::string("a%2Fb"));
    ASSERT_EQ(urlEscape("plain"), std::string("plain"));
    ASSERT_EQ(urlEscape(""), std::string(""));
    // 中文"中"的 UTF-8 是 E4 B8 AD
    ASSERT_EQ(urlEscape(std::string("\xE4\xB8\xAD")), std::string("%E4%B8%AD"));

    // ---- URL unescape ----
    ASSERT_EQ(urlUnescape("hello%20world"), std::string("hello world"));
    ASSERT_EQ(urlUnescape("a%2Fb"), std::string("a/b"));
    ASSERT_EQ(urlUnescape("%E4%B8%AD"), std::string("\xE4\xB8\xAD"));
    // 非法 % 保留原样
    ASSERT_EQ(urlUnescape("100%"), std::string("100%"));
    ASSERT_EQ(urlUnescape("a%ZZb"), std::string("a%ZZb"));
    ASSERT_EQ(urlUnescape(""), std::string(""));

    // ---- URL 往返 ----
    ASSERT_EQ(urlUnescape(urlEscape("hello 世界!")), std::string("hello 世界!"));

    // ---- convert 分发 ----
    ASSERT_EQ(convert(Format::Cpp, Direction::Escape, "a\nb"), std::string("a\\nb"));
    ASSERT_EQ(convert(Format::Cpp, Direction::Unescape, "a\\nb"), std::string("a\nb"));
}
