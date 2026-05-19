using MathEval;
using MathEval.Context;
using System.Diagnostics;

// 示例 1: 简单表达式计算
Console.WriteLine("=== 示例 1: 简单表达式 ===");
var result1 = Expression.Eval("2 + 3 * 4");
Console.WriteLine($"2 + 3 * 4 = {result1} (类型: {result1.GetType()})");

// 示例 2: 使用变量和函数
Console.WriteLine("\n=== 示例 2: 变量和函数 ===");
var context = new ExpressionContext();
context.Set("x", 5L);
context.Set("name", "World");
var absResult = Expression.Eval("x > 0 ? x : -x", context);
var greeting = Expression.Eval("$'Hello, {name}!'", context);
Console.WriteLine($"x = 5, abs(x) = {absResult}");
Console.WriteLine($"greeting = {greeting}");

// 示例 3: 使用 Builder API
Console.WriteLine("\n=== 示例 3: Builder API ===");
var calculator = Expression.Builder
    .With("radius", 3.0)
    .Build("PI * radius ^ 2");
var area = calculator.Eval<double>();
Console.WriteLine($"半径 3.0 的圆面积 = {area}");

// 示例 4: 内置函数
Console.WriteLine("\n=== 示例 4: 内置数学函数 ===");
var sqrtResult = Expression.Eval("sqrt(16)");
var sinResult = Expression.Eval("sin(PI / 2)");
var maxResult = Expression.Eval("max(10, 20)");
Console.WriteLine($"sqrt(16) = {sqrtResult}");
Console.WriteLine($"sin(PI/2) = {sinResult}");
Console.WriteLine($"max(10, 20) = {maxResult}");

// 示例 5: 位运算
Console.WriteLine("\n=== 示例 5: 位运算 ===");
var bitwiseAnd = Expression.Eval("0xFF & 0x0F");
var bitwiseOr = Expression.Eval("5 | 3");
var leftShift = Expression.Eval("1 << 4");
Console.WriteLine($"0xFF & 0x0F = {bitwiseAnd}");
Console.WriteLine($"5 | 3 = {bitwiseOr}");
Console.WriteLine($"1 << 4 = {leftShift}");

// 示例 6: 字符串插值和格式化
Console.WriteLine("\n=== 示例 6: 字符串插值和格式化 ===");
var formatted = Expression.Eval("$'Pi: {3.14159:F2}, Int: {42:D5}'");
Console.WriteLine($"格式化结果: {formatted}");

// 示例 7: 自定义函数
Console.WriteLine("\n=== 示例 7: 自定义函数 ===");
var customContext = new ExpressionContext();
customContext.SetFunction("doubleIt", (Func<double, double>)(x => x * 2));
var doubleResult = Expression.Eval("doubleIt(7.5)", customContext);
Console.WriteLine($"doubleIt(7.5) = {doubleResult}");

// 示例 8: 性能对比 - 优化前后的性能对比
Console.WriteLine("\n=== 示例 8: 性能对比 ===");
const string testExpr = "2 + 3 * 4 + 5 * (6 + 7 * 8 - 9 * 10)";
const int iterations = 100000;

// 无优化
var stopwatch = Stopwatch.StartNew();
for (int i = 0; i < iterations; i++) {
    Expression.Eval(testExpr);
}
stopwatch.Stop();
Console.WriteLine($"无优化 - {iterations}次耗时: {stopwatch.ElapsedMilliseconds}ms");

// 优化版
stopwatch.Restart();
for (int i = 0; i < iterations; i++) {
    Expression.OptimizedEval(testExpr);
}
stopwatch.Stop();
Console.WriteLine($"优化版 - {iterations}次耗时: {stopwatch.ElapsedMilliseconds}ms");

// 示例 9: 常量折叠优化
Console.WriteLine("\n=== 示例 9: 常量折叠 ===");
// 常量表达式中的所有常量会在解析阶段就被计算
var optimizedExpr = "(2 + 3 * 4 + 5 * 6 + 7 * 8 - 9 * 10)";
var unoptimizedResult = Expression.Eval(optimizedExpr, options: ExpressionOptions.None);
var optimizedResult2 = Expression.Eval(optimizedExpr, options: ExpressionOptions.ConstantFolding);
Console.WriteLine($"常量折叠前后结果一致: {unoptimizedResult.Equals(optimizedResult2)}");

Console.WriteLine("\n=== 所有示例完成! ===");
