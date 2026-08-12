#include "test_macros.h"
#include <cstdio>
#include <string>

int g_testPass = 0;
int g_testFail = 0;

// 由 test_converters.cpp 提供
void runConverterTests();

int main() {
    runConverterTests();
    std::printf("\n==== 测试结果 ====\n通过: %d\n失败: %d\n", g_testPass, g_testFail);
    return g_testFail == 0 ? 0 : 1;
}
