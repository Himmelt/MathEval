# MathEval

A lightweight expression evaluation library for .NET 10+.

## Features

- Arithmetic, comparison, logical, bitwise, and string operations
- 14-level operator precedence with correct associativity
- Built-in math functions (abs, sqrt, sin, cos, tan, ln, log, exp, etc.)
- String interpolation with format specifiers (`$"Value: {expr:F2}"`)
- Context system with inheritance and symbol/function registration
- Strong-typed and weak-typed function registration
- Expression caching for performance
- Comprehensive error handling with Chinese error messages
- NaN/INF special value support

## Quick Start

```csharp
using MathEval;

// Simple evaluation
var result = Expression.Eval("2 + 3 * 4");  // 14 (long)

// With context
var context = new ExpressionContext();
context.Set("x", 5L);
context.Set("name", "World");
var abs = Expression.Eval("x > 0 ? x : -x", context);  // 5
var greeting = Expression.Eval("$'Hello, {name}!'", context);  // "Hello, World!"

// Fluent API
var calc = Expression.Builder
    .With("radius", 3.0)
    .Build("PI * radius ^ 2");
var area = calc.Eval<double>();  // 28.274...
```

## Installation

```bash
dotnet add package MathEval
```

## License

MIT
