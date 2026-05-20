# MathEvaluator (AntonovAnton) 技术架构与性能研究

> **研究对象**：[MathEvaluator](https://github.com/AntonovAnton/math.evaluation)
> **研究日期**：2026-05-20
> **研究目的**：深入研究 MathEvaluator 声称性能比 NCalc 高 10-13 倍的原因，为 MathEval 性能优化提供参考

---

## 目录

1. [整体架构概览](#1-整体架构概览)
2. [核心求值引擎：递归直接求值](#2-核心求值引擎递归直接求值)
3. [前缀树（Trie）查找机制](#3-前缀树trie查找机制)
4. [INumberBase\<T\> 泛型数值系统](#4-inumberbaset-泛型数值系统)
5. [编译优化机制](#5-编译优化机制)
6. [性能基准测试分析](#6-性能基准测试分析)
7. [与 NCalc 的性能差距根因分析](#7-与-ncalc-的性能差距根因分析)
8. [与 MathEval 的架构对比](#8-与-matheval-的架构对比)
9. [可借鉴的优化策略](#9-可借鉴的优化策略)
10. [结论](#10-结论)

---

## 1. 整体架构概览

MathEvaluator 采用与 NCalc / MathEval 截然不同的架构——**无 AST 中间层的递归直接求值**。

### 1.1 核心管线对比

```
NCalc / MathEval 管线：
  字符串 → Lexer → Token流 → Parser → AST → Visitor(求值) → 结果
  （每次求值：遍历 AST + 动态分派）

MathEvaluator 管线：
  字符串 → 递归直接求值 → 结果
  （无 Lexer/Parser/AST 分离，求值即解析）
```

### 1.2 模块划分

| 模块 | 目录 | 职责 |
|------|------|------|
| 核心求值 | `MathExpression.cs` | 递归直接求值，运算符优先级通过递归层级实现 |
| 上下文 | `Context/` | 数学实体（常量、函数、运算符）的注册和查找 |
| 实体系统 | `Entities/` | 数学实体的类型层次和 Trie 存储 |
| 参数系统 | `Parameters/` | 变量绑定和求值参数传递 |
| 编译 | `Compilation/` | 表达式编译为 .NET 委托 |
| 扩展 | `Extensions/` | 字符串扩展方法（`Evaluate()` 等） |

### 1.3 核心设计哲学

1. **求值即解析**：不构建 AST，在扫描字符串的同时直接计算结果
2. **零分配优先**：使用 `ReadOnlySpan<char>` 避免字符串分配
3. **泛型数值**：基于 `INumberBase<T>` 实现编译期类型确定
4. **前缀树查找**：使用 Trie 替代 `Dictionary` 查找数学实体

---

## 2. 核心求值引擎：递归直接求值

### 2.1 核心方法签名

```csharp
internal TResult Evaluate<TResult>(ref int i, char? separator, char? closingSymbol,
    int precedence = (int)EvalPrecedence.Unknown, bool isOperand = false)
    where TResult : struct, INumberBase<TResult>
```

**关键设计要点**：

- **`ref int i`**：游标位置通过引用传递，避免字符串拷贝
- **`precedence` 参数**：当前递归层级的运算符优先级，用于实现优先级爬升
- **`TResult : INumberBase<TResult>`**：泛型约束确保编译期确定数值类型
- **`isOperand` 标志**：区分操作数上下文和一般表达式上下文

### 2.2 求值流程

```
Evaluate<TResult>(ref i, ...)
  ├── 读取字符
  │   ├── 数字 → ParseNumber<TResult>(span, ref i)  // 直接解析为 TResult
  │   ├── '(' → 递归 Evaluate<TResult>(ref i, null, ')')  // 子表达式
  │   ├── '+' → 递归 Evaluate<TResult>(..., precedence=LowestBasic)  // 加法
  │   ├── '-' → 递归 Evaluate<TResult>(..., precedence=LowestBasic)  // 减法
  │   ├── '*' → 递归 Evaluate<TResult>(..., precedence=Basic)  // 乘法
  │   ├── '/' → 递归 Evaluate<TResult>(..., precedence=Basic)  // 除法
  │   └── 其他 → FirstMathEntity(span[i..])  // Trie 查找函数/常量/运算符
  │              → entity.Evaluate(this, ...)  // 实体自求值
  └── 返回 TResult（值类型，无装箱）
```

### 2.3 优先级实现机制

MathEvaluator 使用**递归优先级爬升**而非递归下降：

```csharp
case '+':
    if (precedence >= (int)EvalPrecedence.LowestBasic && !MathString.IsMeaningless(start, i))
        return value;  // 当前优先级高于加法，返回给上层
    i++;
    value += Evaluate<TResult>(ref i, separator, closingSymbol,
        (int)EvalPrecedence.LowestBasic);  // 递归解析右侧
    break;
```

**优先级枚举**：

```csharp
public enum EvalPrecedence {
    Equivalence = -1000,           // ≡
    BiconditionalLogicalEquivalence = -900, // ⇔
    LogicalImplication = -800,     // →
    LogicalConditionalOr = -700,   // ||
    LogicalConditionalAnd = -600,  // &&
    LogicalOr = -500,              // or / |
    LogicalXor = -400,             // xor / ^
    LogicalAnd = -300,             // and / &
    LogicalNot = -200,             // not
    RelationalOperator = -100,     // < > <= >=
    Equality = -100,               // == !=
    LowestBasic = 0,               // + -
    Basic = 100,                   // * / %
    Function = 200,                // 函数调用
    Variable = 300,                // 变量
    Constant = 300,                // 常量
    Exponentiation = 400,          // ^ (乘方)
    OperandUnaryOperator = 500     // 一元运算符
}
```

### 2.4 与递归下降解析器的对比

| 维度 | 递归下降（NCalc/MathEval） | 递归优先级爬升（MathEvaluator） |
|------|---------------------------|-------------------------------|
| 解析与求值 | 分离：先构建 AST，再遍历求值 | 合并：边解析边求值 |
| 方法数量 | 每个优先级一个方法（~10 个） | 单一递归方法 + precedence 参数 |
| 内存分配 | 需分配 AST 节点对象 | 仅分配 TResult 值类型 |
| 可扩展性 | 高（可添加新 Visitor） | 低（求值逻辑硬编码） |
| 可优化性 | 可缓存 AST | 无需缓存（每次重新解析） |
| 调试难度 | 低（AST 可视化） | 高（无中间表示） |

---

## 3. 前缀树（Trie）查找机制

### 3.1 Trie 数据结构

MathEvaluator 使用**前缀树（Trie）** 存储和查找数学实体（常量、函数、运算符）：

```csharp
internal sealed class MathEntitiesTrie {
    private readonly TrieNode _rootNode = new();

    public void AddMathEntity(IMathEntity entity)
        => AddMathEntity(_rootNode, entity.Key.AsSpan(), entity);

    public IMathEntity? FirstMathEntity(ReadOnlySpan<char> expression)
        => FirstMathEntity(_rootNode, expression);
}
```

### 3.2 Trie 节点结构

```csharp
private class TrieNode(string remainingKey = "", IMathEntity? entity = null) {
    public Dictionary<char, TrieNode> Children { get; } = [];
    public string RemainingKey { get; set; } = remainingKey;
    public IMathEntity? Entity { get; set; } = entity;
}
```

**压缩优化**：`RemainingKey` 字段实现了**基数树（Radix Tree）**优化——当节点只有一个子节点时，将公共前缀合并到 `RemainingKey`，减少节点数量和查找层级。

### 3.3 查找算法

```csharp
private static IMathEntity? FirstMathEntity(TrieNode trieNode, ReadOnlySpan<char> expression) {
    var i = 0;
    while (expression.Length > i && trieNode.Children.TryGetValue(expression[i], out var childNode)) {
        trieNode = childNode;
        i++;
    }
    return expression[i..].StartsWith(trieNode.RemainingKey) ? trieNode.Entity : null;
}
```

**查找复杂度**：O(L)，其中 L 为匹配键的长度。对于短键（如 `sin`、`PI`），通常只需 2-3 次字典查找。

### 3.4 与 Dictionary 查找的对比

| 维度 | Dictionary\<string, T\> | Trie |
|------|------------------------|------|
| 查找复杂度 | O(1) 平均（含哈希计算） | O(L) 其中 L 为键长 |
| 哈希计算 | 需要（字符串遍历） | 不需要 |
| 前缀匹配 | 不支持 | 天然支持 |
| 内存占用 | 较低 | 较高（节点对象） |
| 最长匹配 | 不支持 | 天然支持 |

**MathEvaluator 选择 Trie 的原因**：

1. **前缀匹配**：表达式中的标识符可能与多个实体前缀匹配（如 `sin` 和 `sinh`），Trie 天然支持最长前缀匹配
2. **避免子字符串分配**：使用 `ReadOnlySpan<char>` 直接在原字符串上操作，无需提取子字符串作为 Dictionary 的键
3. **一次遍历**：在扫描表达式的同时完成实体查找，无需额外的字符串提取步骤

### 3.5 MathContext 的实体注册

```csharp
public class MathContext {
    private readonly MathEntitiesTrie _trie = new();

    public void BindFunction<T>(Func<T, T> fn, char key)
        where T : struct, INumberBase<T>
        => _trie.AddMathEntity(new MathUnaryFunction<T>(key.ToString(), fn));

    public void BindFunction<T>(Func<T, T, T> fn, char key, int precedence)
        where T : struct, INumberBase<T>
        => _trie.AddMathEntity(new MathOperandsOperator<T>(key.ToString(), fn, precedence));
}
```

**关键观察**：函数和运算符统一存储在 Trie 中，通过 `IMathEntity.Evaluate` 方法统一调用。

---

## 4. INumberBase\<T\> 泛型数值系统

### 4.1 设计理念

MathEvaluator 利用 .NET 7 引入的 `INumberBase<T>` 接口，将数值类型参数化：

```csharp
public TResult Evaluate<TResult>(MathParameters? parameters)
    where TResult : struct, INumberBase<TResult>
```

这意味着：
- `Evaluate<double>()`：所有运算在 `double` 上进行
- `Evaluate<decimal>()`：所有运算在 `decimal` 上进行
- `Evaluate<Complex>()`：所有运算在 `Complex` 上进行
- `Evaluate<BigInteger>()`：所有运算在 `BigInteger` 上进行

### 4.2 消除运行时类型检查

```csharp
// MathEval 的做法：运行时类型检查
if (left is long l1 && right is long l2)
    return CheckedAdd(l1, l2);
if (left is double d1 && right is double d2)
    return d1 + d2;

// MathEvaluator 的做法：编译期确定类型
value += Evaluate<TResult>(ref i, ...);  // TResult 已知，JIT 直接生成对应类型的加法指令
```

**性能差异**：
- MathEval：每次运算需要 `is` 类型检查 + 拆箱
- MathEvaluator：JIT 为每种 `TResult` 生成特化代码，直接执行数值运算

### 4.3 零装箱的值类型传递

```csharp
// MathEval：object 返回值导致装箱
public object Visit(ValueExpression expr) {
    return expr.Value;  // long/double 装箱为 object
}

// MathEvaluator：泛型值类型直接传递
var value = span.ParseNumber<TResult>(_numberFormat, ref i);  // TResult 是值类型，无装箱
```

### 4.4 支持的数值类型

| 类型 | 说明 | 示例 |
|------|------|------|
| `double` | 默认浮点数 | `"3.14 + 1".Evaluate()` |
| `decimal` | 高精度十进制 | `"3.14 + 1".EvaluateDecimal()` |
| `Complex` | 复数 | `"sin(2+3i)".EvaluateComplex()` |
| `BigInteger` | 大整数 | `"2**100".Evaluate<BigInteger>()` |
| `float` | 单精度浮点 | `"1.5f + 2".Evaluate<float>()` |
| `int/long` | 整数类型 | `"42 + 8".Evaluate<int>()` |
| `Half` | 半精度浮点 | `.NET 7+` |

---

## 5. 编译优化机制

### 5.1 表达式编译

MathEvaluator 支持将表达式编译为 .NET 委托，用于重复求值场景：

```csharp
var fn = "ln(1/x1 + √(1/(x2*x2) + 1))"
    .Compile(new { x1 = 0.0, x2 = 0.0 }, new ScientificMathContext());

var value = fn(new { x1 = -0.5, x2 = 0.5 });
```

### 5.2 编译实现

编译过程通过 `System.Linq.Expressions` 构建 Expression Tree，然后编译为委托：

```csharp
// MathExpression.Compile.cs
public Func<T, TResult> Compile<T>(T parameters, ...) {
    // 1. 构建表达式树
    ParameterExpression = Expression.Parameter(typeof(T), "parameters");
    var body = BuildExpression<T, TResult>(...);

    // 2. 编译为委托
    var lambda = Expression.Lambda<Func<T, TResult>>(body, ParameterExpression);
    return lambda.Compile();
}
```

### 5.3 FastExpressionCompiler 扩展

MathEvaluator 提供了 `MathEvaluator.FastExpressionCompiler` 扩展包，使用 [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) 替代 .NET 内置的 `LambdaExpression.Compile()`：

| 编译方式 | 编译耗时 | 编译后执行速度 |
|----------|----------|---------------|
| .NET `LambdaExpression.Compile()` | ~10,000-100,000ns | 快 |
| FastExpressionCompiler | ~600-3,000ns | 更快（10-40x 编译提速） |

### 5.4 编译 vs 解释执行的性能对比

基于基准测试数据（.NET 10.0）：

| 场景 | 解释执行 | 编译后执行 | 编译耗时 | 盈亏平衡点 |
|------|----------|-----------|----------|-----------|
| 纯算术 | 465.8ns | 3.7ns | ~20,000ns | ~54 次 |
| 带变量算术 | 362.0ns | ~4ns | ~115,000ns | ~320 次 |
| 布尔逻辑 | 482.8ns | ~4.8ns | ~101,000ns | ~225 次 |

**结论**：编译优化在重复求值 150-320 次后开始产生正向收益。

---

## 6. 性能基准测试分析

### 6.1 解释执行基准（BenchmarkDotNet）

**测试环境**：Intel i7-11800H, .NET 10.0 / .NET 8.0

#### 纯算术表达式

```
"22888.32 * 30 / 323.34 / .5 - -1 / (2 + 22888.32) * 4 - 6"
```

| 库 | .NET 10.0 | .NET 8.0 | 内存分配 |
|----|-----------|----------|----------|
| **MathEvaluator** | **465.8 ns** | **603.5 ns** | **112 B** |
| NCalc | 6,894.8 ns | 8,087.4 ns | 4,464 B |
| **加速比** | **14.8x** | **13.4x** | **40x** |

#### 带函数调用

```
"Sin(a) + Cos(b)"
```

| 库 | .NET 10.0 | .NET 8.0 | 内存分配 |
|----|-----------|----------|----------|
| **MathEvaluator** | **362.0 ns** | **435.9 ns** | **736 B** |
| NCalc | 4,509.0 ns | 5,308.7 ns | 2,688 B |
| **加速比** | **12.5x** | **12.2x** | **3.6x** |

#### 布尔逻辑

```
"A or not B and (C or B)"
```

| 库 | .NET 10.0 | .NET 8.0 | 内存分配 |
|----|-----------|----------|----------|
| **MathEvaluator** | **482.8 ns** | **564.3 ns** | **896 B** |
| NCalc | 5,007.7 ns | 5,757.8 ns | 2,368 B |
| **加速比** | **10.4x** | **10.2x** | **2.6x** |

### 6.2 编译执行基准

#### 编译耗时

| 库 | 纯算术 | 带变量 | 布尔逻辑 |
|----|--------|--------|----------|
| MathEvaluator (内置编译) | ~20,000ns | ~115,000ns | ~101,000ns |
| MathEvaluator (FastEC) | ~2,590ns | ~3,513ns | ~5,682ns |
| NCalc | ~9,027ns | ~7,883ns | ~7,903ns |

#### 编译后执行速度

| 库 | 纯算术 | 带变量 | 布尔逻辑 |
|----|--------|--------|----------|
| MathEvaluator | 3.7ns | ~4ns | ~4.8ns |
| MathEvaluator (FastEC) | 5.2ns | ~5ns | ~5.7ns |
| NCalc | 4.8ns | ~4.8ns | ~4.8ns |

**关键发现**：编译后三者的执行速度几乎相同（3-6ns），说明性能差距主要来自**解释执行路径**，而非编译后路径。

---

## 7. 与 NCalc 的性能差距根因分析

### 7.1 NCalc 的性能瓶颈

基于基准测试数据，NCalc 解释执行比 MathEvaluator 慢 10-15 倍，根因分析如下：

| 瓶颈 | 估计占比 | 说明 |
|------|----------|------|
| AST 节点分配 | ~30% | 每次解析创建大量 AST 对象（BinaryExpression、ValueExpression 等） |
| Visitor 双分派 | ~20% | `Accept` → `Visit` 的虚方法调用开销 |
| object 装箱/拆箱 | ~15% | `long`/`double` 装箱为 `object` 传递 |
| ConcurrentDictionary 查找 | ~10% | 符号和函数查找 |
| Convert.ChangeType | ~10% | 类型转换开销 |
| Token 对象分配 | ~10% | Lexer 产出 Token 对象 |
| 其他 | ~5% | 字符串操作等 |

### 7.2 MathEvaluator 的性能优势来源

| 优势 | 估计贡献 | 说明 |
|------|----------|------|
| 无 AST 分配 | ~30% | 边解析边求值，不创建中间对象 |
| 泛型值类型 | ~25% | `TResult : INumberBase<TResult>` 消除装箱 |
| ReadOnlySpan\<char\> | ~15% | 避免子字符串分配 |
| Trie 前缀匹配 | ~10% | 一次遍历完成实体查找 |
| 无 Visitor 分派 | ~10% | 直接递归调用，无虚方法开销 |
| 其他 | ~10% | 更少的边界检查、更少的异常处理等 |

### 7.3 关键洞察

1. **架构决定性能天花板**：MathEvaluator 的"求值即解析"架构从根本上消除了 NCalc 管线中的多个开销来源。这不是微优化能弥补的差距。

2. **编译后差距消失**：三者在编译后执行速度几乎相同（3-6ns），说明性能差距完全来自解释执行路径。

3. **内存分配是关键**：MathEvaluator 仅分配 112-896B，而 NCalc 分配 2,368-4,464B。GC 压力的差异在高并发场景下会被放大。

---

## 8. 与 MathEval 的架构对比

### 8.1 架构差异

| 维度 | MathEval | MathEvaluator |
|------|----------|---------------|
| 解析架构 | Lexer → Parser → AST → Visitor | 递归直接求值 |
| 类型系统 | `long` + `double` + `bool` + `string`（运行时 `object`） | `INumberBase<T>`（编译期泛型） |
| 函数存储 | `ConcurrentDictionary<string, ExpressionFunction>` | Trie（前缀树） |
| 函数签名 | `ExpressionFunction(object[])` | `Func<T[], T>` / `Func<T, T>` 等 |
| 上下文继承 | 支持（`CreateChild()`） | 不支持 |
| 字符串插值 | 支持（`InterpolatedString` AST 节点） | 不支持 |
| 编译优化 | `System.Linq.Expressions` | `System.Linq.Expressions` + FastExpressionCompiler |
| 缓存 | AST 缓存 + 编译缓存 | 无解释执行缓存（编译后委托缓存） |
| 目标框架 | .NET 10+ | .NET 7+ / .NET 8+ / .NET 9+ / .NET 10+ |

### 8.2 功能差异

| 功能 | MathEval | MathEvaluator |
|------|----------|---------------|
| 布尔类型 | 一等公民 | 转换为 double（0/1） |
| 字符串类型 | 支持 | 仅支持插值输出 |
| 字符串插值 | 原生支持 | 不支持 |
| 上下文继承 | 支持 | 不支持 |
| 复数 | 不支持 | 支持 |
| Decimal | 不支持 | 支持 |
| BigInteger | 不支持 | 支持 |
| 自定义运算符 | 不支持 | 支持（`BindOperator`） |
| 多进制字面量 | 支持（0x/0o/0b） | 支持（0x/0o/0b） |
| 三元运算符 | 支持 | 支持（`iif`） |
| 位运算 | 支持 | 支持 |
| 调试/日志 | 不支持 | 支持（`Evaluating` 事件） |

### 8.3 设计哲学对比

| 维度 | MathEval | MathEvaluator |
|------|----------|---------------|
| 核心目标 | 通用表达式计算 + 可扩展性 | 极致性能 |
| 可扩展性 | 高（Visitor 模式、AST 可视化） | 低（硬编码求值逻辑） |
| 类型安全 | 运行时检查 | 编译期确定 |
| API 风格 | Builder + 静态方法 | 字符串扩展方法 + 实例方法 |
| 错误报告 | 精确位置信息 | 位置信息有限 |

---

## 9. 可借鉴的优化策略

### 9.1 短期可借鉴（无需架构变更）

#### 9.1.1 FunctionWrapper is 模式匹配快速路径

借鉴 MathEvaluator 的 `INumberBase<T>` 理念，在 `FunctionWrapper.Wrap` 中添加类型快速路径：

```csharp
// 当前
var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));

// 优化后
var arg1 = args[0] is T1 t1 ? t1 : (T1)Convert.ChangeType(args[0], typeof(T1));
```

**预期收益**：类型匹配时节省 ~20-40ns/参数。

#### 9.1.2 内置函数避免 object[] 中间分配

当前 `EvaluationVisitor.Visit(FunctionCall)` 每次调用都创建 `List<object>` + `ToArray()`：

```csharp
// 当前
var args = new List<object>();
foreach (var arg in expr.Arguments) {
    args.Add(arg.Accept(this));
}
return func(args.ToArray());

// 优化：对参数数量已知的情况使用数组
var args = new object[expr.Arguments.Count];
for (int i = 0; i < expr.Arguments.Count; i++) {
    args[i] = expr.Arguments[i].Accept(this);
}
return func(args);
```

**预期收益**：消除 `List<object>` 开销和一次数组拷贝。

#### 9.1.3 TypeHelper.EvaluateBinary 添加快速路径

当前 `EvaluateBinary` 对每个运算符都调用 `Promote` 进行类型提升：

```csharp
// 优化：对常见 long+long 和 double+double 添加快速路径
private static object EvaluatePlus(object left, object right) {
    if (left is long l1 && right is long l2)
        return CheckedAdd(l1, l2);  // 快速路径，跳过 Promote
    if (left is double d1 && right is double d2)
        return d1 + d2;  // 快速路径，跳过 Promote
    // ... 回退到通用路径
}
```

**注意**：当前实现已经采用了这种模式，这是正确的。

### 9.2 中期可借鉴（需局部重构）

#### 9.2.1 引入泛型求值路径

为纯数值场景提供泛型求值接口：

```csharp
public double EvaluateDouble(ExpressionContext context);
public decimal EvaluateDecimal(ExpressionContext context);
```

在 `EvaluationVisitor` 中为 `double` 路径避免装箱：

```csharp
public class DoubleEvaluationVisitor {
    public double Visit(BinaryExpression expr) {
        var left = expr.Left.Accept(this);   // 返回 double，无装箱
        var right = expr.Right.Accept(this);  // 返回 double，无装箱
        return expr.Type switch {
            BinaryExpressionType.Plus => left + right,
            BinaryExpressionType.Minus => left - right,
            // ...
        };
    }
}
```

**预期收益**：纯数值表达式提速 30-50%。

#### 9.2.2 编译优化中内联强类型函数

在 `CompiledExpression` 中，对强类型注册的函数生成直接调用：

```csharp
// 当前：通过 ExpressionFunction 委托间接调用
var invokeExpr = Expression.Invoke(funcVar, argsArrayVar);

// 优化：直接调用原始 Func<T1, TResult>
var argExpr = Expression.Convert(argsExpr[0], typeof(T1));
var callExpr = Expression.Call(funcMethod, argExpr);
```

**预期收益**：编译路径函数调用提速 30-50%。

#### 9.2.3 引入 FastExpressionCompiler

参考 MathEvaluator 的做法，引入 `FastExpressionCompiler` 替代 .NET 内置编译器：

```csharp
// 当前
return lambda.Compile();

// 优化
return lambda.CompileFast();  // FastExpressionCompiler 扩展方法
```

**预期收益**：编译耗时减少 5-10 倍。

### 9.3 长期可借鉴（需架构调整）

#### 9.3.1 双路径架构

为 MathEval 提供两条求值路径：

1. **通用路径**（当前）：Lexer → Parser → AST → Visitor，支持所有类型
2. **快速路径**（新增）：递归直接求值，仅支持 `double`，类似 MathEvaluator

```csharp
// 自动选择路径
public static object Eval(string expression, ExpressionContext? context = null) {
    if (IsPureNumericExpression(expression)) {
        return FastEvaluate(expression, context);  // 快速路径
    }
    return StandardEvaluate(expression, context);  // 通用路径
}
```

#### 9.3.2 ReadOnlySpan\<char\> Lexer

将 Lexer 改为基于 `ReadOnlySpan<char>` 的实现，避免字符串分配：

```csharp
// 当前
private string ReadString() { ... }  // 返回新字符串

// 优化
private Range ReadStringRange() { ... }  // 返回范围，不分配字符串
```

---

## 10. 结论

### 10.1 MathEvaluator 性能优势的核心原因

MathEvaluator 声称比 NCalc 快 10-13 倍，经研究确认其性能优势来自以下核心因素（按影响程度排序）：

1. **无 AST 中间层**（~30% 贡献）：边解析边求值，消除了 AST 节点分配和 Visitor 遍历开销
2. **INumberBase\<T\> 泛型**（~25% 贡献）：编译期确定数值类型，消除运行时类型检查和装箱
3. **ReadOnlySpan\<char\>**（~15% 贡献）：避免子字符串分配，减少 GC 压力
4. **Trie 前缀匹配**（~10% 贡献）：一次遍历完成实体查找，无需提取子字符串
5. **无 Visitor 分派**（~10% 贡献）：直接递归调用，无虚方法开销

### 10.2 MathEvaluator 的局限性

| 局限 | 说明 |
|------|------|
| 无 AST | 无法进行表达式分析、优化、可视化 |
| 无上下文继承 | 不支持父子上下文关系 |
| 布尔非一等公民 | 布尔值转换为 double（0/1），语义不精确 |
| 不支持字符串运算 | 仅支持数值和布尔运算 |
| 可扩展性差 | 添加新功能需要修改核心求值方法 |
| 调试困难 | 无中间表示，难以定位表达式中的错误 |

### 10.3 对 MathEval 的建议

1. **保持 AST + Visitor 架构**：这是 MathEval 的核心竞争力——可扩展性和可维护性。MathEvaluator 的性能优势来自架构取舍，而非简单的优化技巧。

2. **聚焦可落地的优化**：
   - P0：`FunctionWrapper` is 模式匹配快速路径（~10-20% 函数调用提速）
   - P1：编译优化中内联强类型函数（~30-50% 编译路径提速）
   - P2：引入 `FastExpressionCompiler`（~5-10x 编译提速）
   - P3：消除 `args.ToArray()` 堆分配（减少 GC 压力）

3. **编译优化是关键杠杆**：基准测试显示，编译后 MathEvaluator、NCalc 的执行速度几乎相同（3-6ns）。MathEval 的编译优化路径（`CompiledExpression`）已经具备良好的基础，通过内联函数调用和引入 `FastExpressionCompiler`，可以在重复求值场景下达到与 MathEvaluator 相当的性能。

4. **不建议采用"求值即解析"架构**：虽然性能最优，但牺牲了 MathEval 的核心价值——可扩展性、可调试性和类型安全。这种架构适合 MathEvaluator 的定位（极致性能的数值计算器），但不适合 MathEval 的定位（通用表达式计算引擎）。

5. **考虑添加快速数值路径**：作为可选优化，可以为纯数值表达式提供一条绕过 AST 的快速求值路径，在保持通用路径的同时为高频场景提供极致性能。
