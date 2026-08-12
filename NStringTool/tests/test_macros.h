#pragma once
#include <cstdio>
#include <string>

// 简单断言宏。失败时打印并计数，不中断。
// 供 test_main.cpp 与各 test_*.cpp 共享，避免宏重复定义。
extern int g_testPass;
extern int g_testFail;

#define ASSERT_EQ(actual, expected) do { \
    if ((actual) == (expected)) { ++g_testPass; } \
    else { ++g_testFail; std::printf("FAIL %s:%d: ASSERT_EQ\n  actual:   %s\n  expected: %s\n", \
        __FILE__, __LINE__, std::string(actual).c_str(), std::string(expected).c_str()); } \
} while(0)

#define ASSERT_TRUE(cond) do { \
    if ((cond)) { ++g_testPass; } \
    else { ++g_testFail; std::printf("FAIL %s:%d: ASSERT_TRUE(%s)\n", __FILE__, __LINE__, #cond); } \
} while(0)
