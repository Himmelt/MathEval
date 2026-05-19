<div align="center">
  <img src="logo.svg" alt="MathEval Logo" width="150" height="150">
  <h1>MathEval</h1>
</div>

MathEval 是一个面向 .NET 10+ 的轻量级表达式计算库。

## 特性

- 支持算术、比较、逻辑、位运算和字符串操作
- 14 级运算符优先级与正确的结合性
- 内置数学函数（abs、sqrt、sin、cos、tan、ln、log、exp 等）
- 支持带格式说明符的字符串插值（`$"Value: {expr:F2}"`）
- 支持继承的上下文系统，可注册符号和函数
- 支持强类型和弱类型函数注册
- 表达式缓存以提升性能
- 完善的错误处理，包含中文错误信息
- 支持 NaN/INF 特殊值

## 快速入门

```csharp
using MathEval;

// 简单计算
var result = Expression.Eval("2 + 3 * 4");  // 14 (long)

// 使用上下文
var context = new ExpressionContext();
context.Set("x", 5L);
context.Set("name", "World");
var abs = Expression.Eval("x > 0 ? x : -x", context);  // 5
var greeting = Expression.Eval("$'Hello, {name}!'", context);  // "Hello, World!"

// 流式 API
var calc = Expression.Builder
    .With("radius", 3.0)
    .Build("PI * radius ^ 2");
var area = calc.Eval<double>();  // 28.274...
```

## 安装

```bash
dotnet add package MathEval
```

## 许可证

MIT
