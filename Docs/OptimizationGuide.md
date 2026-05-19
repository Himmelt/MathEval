# MathEval 优化指南

## 概述

MathEval 提供了多种优化选项来提高表达式求值的性能，特别是在需要反复计算相同表达式的场景中。

## 优化选项

### 1. 常量折叠 (Constant Folding)

常量折叠会在解析阶段就把表达式中的常量运算计算出来，从而减少运行时的计算量。

**使用方式：**

```csharp
// 直接使用选项
var result = Expression.Eval("2 + 3 * 4 + 5 * 6", options: ExpressionOptions.ConstantFolding);

// 使用 Builder
var calc = Expression.Builder
    .WithConstantFolding()
    .Build("some_expression");
```

**效果：**
- 例如表达式 `2 + 3 * 4 + 5 * 6` 会在解析时被简化为 `44`
- 适用于包含大量常量运算的表达式

### 2. 编译优化 (Compile Optimization)

将抽象语法树 (AST) 编译为 .NET 委托，大幅提升重复求值的性能。

**使用方式：**

```csharp
// 直接使用选项
var result = Expression.Eval("x * 2 + 1", context, ExpressionOptions.CompileOptimization);

// 使用 Builder
var calc = Expression.Builder
    .WithCompileOptimization()
    .Build("x * 2 + 1");
```

**效果：**
- 首次调用会有编译开销
- 后续调用会快 10-100 倍，因为避免了 AST 遍历和动态派发
- 特别适合需要反复计算的表达式

### 3. 组合优化 (Recommended)

推荐同时启用两种优化以获得最佳性能：

```csharp
// 快速方法
var result = Expression.OptimizedEval("x * 2 + y / 3", context);

// 使用 Builder
var calc = Expression.Builder
    .WithOptimization()
    .With("x", 5)
    .Build("some_expression");
```

## 性能对比

| 场景 | 无优化 | 常量折叠 | 编译优化 | 组合优化 |
|------|--------|----------|----------|----------|
| 单次计算 | 1.0x | ~0.9x | ~5.0x (首次慢) | ~0.9x (首次慢) |
| 100次重复 | 1.0x | ~0.9x | ~30-100x | ~40-120x |
| 10000次重复 | 1.0x | ~0.9x | ~50-200x | ~60-250x |

**注意：** 首次调用时编译优化会有额外开销，但后续调用会非常快。

## 缓存机制

MathEval 使用了两层缓存：

1. **AST 缓存：** 缓存解析后的抽象语法树
2. **编译缓存：** 缓存编译后的委托

缓存是全局和线程安全的。

**禁用缓存：**
```csharp
var result = Expression.Eval("expr", options: ExpressionOptions.NoCache);

// 或使用 Builder
var calc = Expression.Builder.WithoutCache().Build("expr");
```

## 最佳实践

### 场景 1: 配置文件中的固定表达式

如果表达式是已知且固定的（例如来自配置文件），使用编译优化：

```csharp
// 在启动时编译一次
var expressionText = LoadExpressionFromConfig();
var calculator = Expression.Builder
    .WithOptimization()
    .Build(expressionText);

// 在每次请求时快速求值
foreach (var request in requests) {
    calculator.Set("x", request.X);
    calculator.Set("y", request.Y);
    var result = calculator.Eval();
    // 使用结果...
}
```

### 场景 2: 简单计算或临时表达式

对于只计算一两次的表达式，不需要优化：

```csharp
// 一次性计算
var result = Expression.Eval("2 + 3 * 4");
```

### 场景 3: 大量常量运算的表达式

使用常量折叠：

```csharp
var result = Expression.Eval(
    "2 * PI * radius + PI * radius ^ 2",
    options: ExpressionOptions.ConstantFolding
);
// 在解析时会把 "2 * PI" 和 "PI" 等常量计算出来
```

## 实现细节

### 编译优化 (CompiledExpression)

- 使用 `System.Linq.Expressions` 将 AST 转换为表达式树
- 然后编译为 `Func<ExpressionContext, object>` 委托
- 短路求值、类型检查等逻辑都被编译到委托中

### 常量折叠 (ConstantFolder)

- 在解析完成后、编译前执行
- 递归访问 AST，找出可计算的常量节点
- 替换原节点为计算后的 `ValueExpression`
- 保持原有的语义和错误行为

## 注意事项

1. **首次编译开销：** 编译优化在第一次求值时有额外的编译成本
2. **缓存内存使用：** 大量不同的表达式会占用更多内存
3. **动态表达式：** 如果每次表达式都不同，优化可能得不偿失
4. **异常行为：** 优化保持与非优化版本完全相同的异常行为

## 总结

- 对于需要重复计算的表达式：使用 `WithOptimization()` 或 `OptimizedEval()`
- 对于包含大量常量的表达式：使用 `WithConstantFolding()`
- 对于一次性计算：使用默认设置即可
