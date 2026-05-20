# FastEval 无上下文快速求值器 — 技术设计文档

> **版本**：v1.0
> **创建日期**：2026-05-20
> **状态**：待实现
> **关联 TODO**：实现无上下文快速求值器（FastEval 方案 A）

---

## 目录

1. [设计目标与动机](#1-设计目标与动机)
2. [功能边界](#2-功能边界)
3. [公共 API 设计](#3-公共-api-设计)
4. [内部架构](#4-内部架构)
5. [FastScanner 字符扫描器](#5-fastscanner-字符扫描器)
6. [FastEvaluator 递归求值器](#6-fastevaluator-递归求值器)
7. [内置函数表设计](#7-内置函数表设计)
8. [变量绑定机制](#8-变量绑定机制)
9. [错误处理策略](#9-错误处理策略)
10. [性能预期与对比](#10-性能预期与对比)
11. [项目结构](#11-项目结构)
12. [与现有管线的共存策略](#12-与现有管线的共存策略)
13. [测试策略](#13-测试策略)
14. [实现步骤](#14-实现步骤)

---

## 1. 设计目标与动机

### 1.1 动机

当前 MathEval 的求值管线为 `Lexer → Parser → AST → Visitor`，每次求值（即使缓存命中后）仍需：

- 遍历 AST 节点（~15 个 Accept/Visit 虚方法分派）
- `object` 装箱/拆箱（`long`/`double`/`bool` → `object`）
- `TypeHelper.Promote` 运行时类型检查
- `List<object> + ToArray()` 函数参数分配
- `ConcurrentDictionary` 查找符号和函数

这些架构性固定开销合计约 **3,000-5,000ns + 3-5KB 堆分配**。

而 MathEvaluator（AntonovAnton）采用"求值即解析"架构，做到 **465ns + 112B**，快 **7-12 倍**。

### 1.2 设计目标

在不破坏现有 AST + Visitor 架构的前提下，为**纯数值单次求值场景**提供一条独立的快速路径：

| 目标 | 指标 |
|------|------|
| 求值速度 | 600-1,500ns（vs 当前 3,000-5,000ns） |
| 堆分配 | 50-200 bytes（vs 当前 3-5KB） |
| 代码独立性 | 完全独立模块，零耦合现有代码 |
| API 一致性 | 与 `Expression.Eval()` 风格统一 |
| 功能子集 | 纯数值 + 布尔 + 简单变量 + 内置函数 |

### 1.3 设计原则

1. **零 AST**：不构建任何中间表示，边扫描边求值
2. **零装箱**：泛型 `T` 返回值，全程值类型传递
3. **零缓存**：单次使用，无需缓存层
4. **零线程安全开销**：使用普通 `Dictionary`，不使用 `ConcurrentDictionary`
5. **语法子集**：仅支持现有语法的纯数值子集

---

## 2. 功能边界

### 2.1 支持的功能

| 功能 | 示例 | 说明 |
|------|------|------|
| 整数算术 | `2 + 3 * 4` | `long` 类型，checked 溢出 |
| 浮点算术 | `3.14 * 2` | `double` 类型 |
| 幂运算 | `2 ^ 10` | `Math.Pow` |
| 整除 | `7 // 2` | `long` 结果 |
| 取模 | `7 % 3` | `long`/`double` |
| 位运算 | `0xFF & 0x0F \| 0x01` | `~ & \| ^ << >>` |
| 比较 | `x > 0` | 返回 `bool` |
| 逻辑 | `a and b or not c` | 短路求值，`and/or/not/xor` + `&&/\|\|/!` |
| 三元条件 | `x > 0 ? 1 : -1` | 短路类型推断 |
| 内置函数 | `sin(x)`, `max(a, b)` | 硬编码函数表 |
| 简单变量 | `x * 2 + y` | 通过参数字典传入 |
| 多进制字面量 | `0xFF`, `0o17`, `0b1010` | 十六/八/二进制 |
| NaN / INF | `NaN`, `INF` | `double.NaN` / `double.PositiveInfinity` |
| 括号分组 | `(a + b) * c` | 嵌套括号 |
| 一元运算 | `-x`, `+x`, `not x`, `~x` | 取负/正号/逻辑非/按位取反 |

### 2.2 不支持的功能

| 功能 | 原因 | 替代方案 |
|------|------|----------|
| 字符串类型 | 需要字符串类型系统 | 使用 `Expression.Eval()` |
| 字符串拼接 `"a" + "b"` | 需要字符串类型系统 | 使用 `Expression.Eval()` |
| 字符串插值 `$"x={x}"` | 需要 InterpolatedString AST | 使用 `Expression.Eval()` |
| 上下文继承 | 无 Context 概念 | 使用 `Expression.Eval()` |
| 延迟值 `Func<object>` | 无 Context 概念 | 使用 `Expression.Eval()` |
| 弱类型自定义函数 | 需要 `ExpressionFunction(object[])` | 使用 `Expression.Eval()` |
| 常量折叠 | 单次求值无编译阶段 | 使用 `Expression.OptimizedEval()` |
| 编译优化 | 单次求值无编译阶段 | 使用 `Expression.OptimizedEval()` |
| AST 可访问 | 无 AST | 使用 `Expression.Eval()` |
| 精确错误行列号 | 无 Token 位置链 | 使用 `Expression.Eval()` |

### 2.3 内置常量

| 常量 | 值 | 类型 |
|------|-----|------|
| `PI` | `3.14159265358979` | `double` |
| `E` | `2.71828182845905` | `double` |
| `true` | `true` | `bool` |
| `false` | `false` | `bool` |
| `NaN` | `double.NaN` | `double` |
| `INF` | `double.PositiveInfinity` | `double` |

---

## 3. 公共 API 设计

### 3.1 FastEval 静态入口

```csharp
namespace MathEval;

public static class FastEval
{
    public static double EvalDouble(string expression,
        IReadOnlyDictionary<string, double>? variables = null);

    public static long EvalLong(string expression,
        IReadOnlyDictionary<string, long>? variables = null);

    public static bool EvalBool(string expression,
        IReadOnlyDictionary<string, object>? variables = null);

    public static T Eval<T>(string expression,
        IReadOnlyDictionary<string, object>? variables = null)
        where T : struct;
}
```

### 3.2 用法示例

```csharp
// 纯常量表达式
var result = FastEval.EvalDouble("3.14 * 2 + 1");
// result = 7.28

// 带变量
var vars = new Dictionary<string, double> { ["x"] = 10.0, ["y"] = 20.0 };
var result = FastEval.EvalDouble("x * 2 + y", vars);
// result = 40.0

// 整数运算
var result = FastEval.EvalLong("0xFF & 0x0F | 0x01");
// result = 15

// 布尔运算
var vars = new Dictionary<string, object> { ["a"] = true, ["b"] = false };
var result = FastEval.EvalBool("a and not b", vars);
// result = true

// 三元条件
var vars = new Dictionary<string, double> { ["x"] = -5.0 };
var result = FastEval.EvalDouble("x > 0 ? x : -x", vars);
// result = 5.0

// 内置函数
var result = FastEval.EvalDouble("sin(PI / 6) + cos(PI / 3)");
// result ≈ 1.0
```

### 3.3 与现有 API 的选择指南

```
用户场景决策树：
│
├─ 表达式中包含字符串/插值？
│   └─ 是 → Expression.Eval()
│
├─ 需要上下文继承/延迟值/自定义函数？
│   └─ 是 → Expression.Eval()
│
├─ 同一表达式需要重复求值 150+ 次？
│   └─ 是 → Expression.OptimizedEval()（编译优化）
│
└─ 纯数值表达式，一次性求值？
    └─ 是 → FastEval.EvalDouble() / EvalLong() / EvalBool()
```

---

## 4. 内部架构

### 4.1 整体管线

```
FastEval.EvalDouble("sin(x) + cos(y)", vars)
  │
  └─ new FastEvaluator<double>(expression, vars)
       │
       ├─ [1] FastScanner 初始化
       │     └─ _text = expression, _position = 0
       │
       └─ [2] EvalExpression()  ← 递归优先级爬升
             │
             ├─ 扫描字符，识别运算符/数字/标识符
             ├─ 数字 → 直接解析为 double（零装箱）
             ├─ 标识符 → 变量查找（Dictionary.TryGetValue）
             ├─ 函数 → 内置函数表查找 + 参数栈分配求值
             ├─ 运算符 → 递归求值右侧 + 直接运算
             └─ 返回 double（值类型，零装箱）
```

### 4.2 核心类关系

```
FastEval (静态入口)
  │
  └─ FastEvaluator<T> where T : struct
       │
       ├─ FastScanner (内嵌结构体，基于 ReadOnlySpan<char>)
       │     ├─ SkipWhitespace()
       │     ├─ ReadDouble() / ReadLong()
       │     ├─ ReadIdentifierSpan() → ReadOnlySpan<char>
       │     ├─ MatchOperator()
       │     └─ Peek() / Advance()
       │
       ├─ EvalExpression() → T
       │     ├─ EvalConditional()
       │     ├─ EvalLogicalOr()
       │     ├─ EvalLogicalAnd()
       │     ├─ EvalEquality()
       │     ├─ EvalRelational()
       │     ├─ EvalBitwiseOr()
       │     ├─ EvalBitwiseXor()
       │     ├─ EvalBitwiseAnd()
       │     ├─ EvalShift()
       │     ├─ EvalAdditive()
       │     ├─ EvalMultiplicative()
       │     ├─ EvalPower()
       │     ├─ EvalUnary()
       │     └─ EvalPrimary()
       │
       └─ BuiltInFastFunctions (静态类，硬编码函数表)
             └─ Dictionary<string, Delegate>
```

---

## 5. FastScanner 字符扫描器

### 5.1 设计要点

- 基于 `ReadOnlySpan<char>` 操作，**零字符串分配**
- 不产出 Token 对象，仅维护游标位置
- 数字直接解析为目标类型 `T`
- 标识符返回 `ReadOnlySpan<char>`，通过 `SequenceEqual` 比较

### 5.2 核心实现

```csharp
internal ref struct FastScanner
{
    private readonly string _text;
    private int _position;

    public FastScanner(string text)
    {
        _text = text;
        _position = 0;
    }

    public bool IsAtEnd => _position >= _text.Length;

    public char Peek()
    {
        return _position < _text.Length ? _text[_position] : '\0';
    }

    public char PeekNext()
    {
        return _position + 1 < _text.Length ? _text[_position + 1] : '\0';
    }

    public char Read()
    {
        return _text[_position++];
    }

    public void SkipWhitespace()
    {
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            _position++;
    }

    public ReadOnlySpan<char> Text => _text.AsSpan();

    public ReadOnlySpan<char> RemainingText => _text.AsSpan(_position);

    public int Position => _position;

    public double ReadDouble()
    {
        var start = _position;
        if (Peek() == '0' && (PeekNext() == 'x' || PeekNext() == 'X'))
        {
            _position += 2;
            return ReadHexAsDouble();
        }
        if (Peek() == '0' && (PeekNext() == 'o' || PeekNext() == 'O'))
        {
            _position += 2;
            return ReadOctalAsDouble();
        }
        if (Peek() == '0' && (PeekNext() == 'b' || PeekNext() == 'B'))
        {
            _position += 2;
            return ReadBinaryAsDouble();
        }
        return ReadDecimalAsDouble(start);
    }

    public long ReadLong()
    {
        if (Peek() == '0' && (PeekNext() == 'x' || PeekNext() == 'X'))
        {
            _position += 2;
            return ReadHexAsLong();
        }
        if (Peek() == '0' && (PeekNext() == 'o' || PeekNext() == 'O'))
        {
            _position += 2;
            return ReadOctalAsLong();
        }
        if (Peek() == '0' && (PeekNext() == 'b' || PeekNext() == 'B'))
        {
            _position += 2;
            return ReadBinaryAsLong();
        }
        return ReadDecimalAsLong();
    }

    public ReadOnlySpan<char> ReadIdentifierSpan()
    {
        var start = _position;
        while (_position < _text.Length && IsIdentifierPart(_text[_position]))
            _position++;
        return _text.AsSpan(start, _position - start);
    }

    private static bool IsIdentifierStart(char ch)
    {
        return char.IsLetter(ch) || ch == '_'
            || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherLetter;
    }

    private static bool IsIdentifierPart(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch == '_'
            || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherLetter
            || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherNumber;
    }

    private double ReadDecimalAsDouble(int start)
    {
        while (_position < _text.Length && char.IsDigit(_text[_position]))
            _position++;

        if (_position < _text.Length && _text[_position] == '.')
        {
            _position++;
            while (_position < _text.Length && char.IsDigit(_text[_position]))
                _position++;
        }

        if (_position < _text.Length && (_text[_position] == 'e' || _text[_position] == 'E'))
        {
            _position++;
            if (_position < _text.Length && (_text[_position] == '+' || _text[_position] == '-'))
                _position++;
            while (_position < _text.Length && char.IsDigit(_text[_position]))
                _position++;
        }

        return double.Parse(_text.AsSpan(start, _position - start));
    }

    private long ReadDecimalAsLong()
    {
        var start = _position;
        while (_position < _text.Length && char.IsDigit(_text[_position]))
            _position++;
        return long.Parse(_text.AsSpan(start, _position - start));
    }

    // ReadHexAsLong, ReadOctalAsLong, ReadBinaryAsLong 等实现...
    // ReadHexAsDouble 等通过 long 中转
}
```

### 5.3 关键设计决策

| 决策 | 选择 | 原因 |
|------|------|------|
| `ref struct` | ✅ 使用 | 避免 `ReadOnlySpan<char>` 跨堆分配 |
| 标识符比较 | `SequenceEqual` | 避免分配新字符串 |
| 数字解析 | `double.Parse(ReadOnlySpan)` | .NET 8+ 原生支持 Span 解析 |
| 多进制 | `long` 中转 → 转目标类型 | 复用 `Convert.ToInt64` 逻辑 |

---

## 6. FastEvaluator 递归求值器

### 6.1 核心类结构

```csharp
internal sealed class FastEvaluator<T> where T : struct
{
    private readonly FastScanner _scanner;
    private readonly IReadOnlyDictionary<string, T>? _variables;
    private readonly IReadOnlyDictionary<string, object>? _objectVariables;

    public FastEvaluator(string expression,
        IReadOnlyDictionary<string, T>? variables = null,
        IReadOnlyDictionary<string, object>? objectVariables = null)
    {
        _scanner = new FastScanner(expression);
        _variables = variables;
        _objectVariables = objectVariables;
    }

    public T Evaluate()
    {
        _scanner.SkipWhitespace();
        if (_scanner.IsAtEnd)
            throw new FastEvalException("表达式不能为空");

        var result = EvalExpression();

        _scanner.SkipWhitespace();
        if (!_scanner.IsAtEnd)
            throw new FastEvalException($"意外的字符 '{_scanner.Peek()}'，位置 {_scanner.Position}");

        return result;
    }
}
```

### 6.2 优先级爬升递归方法

与现有 Parser 的方法层级完全对应，但**直接计算而非构建 AST**：

```csharp
private T EvalExpression() => EvalConditional();

private T EvalConditional()
{
    var condition = EvalLogicalOr();
    _scanner.SkipWhitespace();

    if (_scanner.Peek() == '?')
    {
        _scanner.Read();
        var trueValue = EvalExpression();
        _scanner.SkipWhitespace();
        if (_scanner.Peek() != ':')
            throw new FastEvalException("三元运算符缺少 ':'");
        _scanner.Read();
        var falseValue = EvalExpression();

        if (typeof(T) == typeof(bool) || typeof(T) == typeof(long) || typeof(T) == typeof(double))
        {
            bool cond = ConvertToBool(condition);
            return cond ? trueValue : falseValue;
        }
        throw new FastEvalException("三元运算符条件必须为布尔类型");
    }
    return condition;
}

private T EvalLogicalOr()
{
    var left = EvalLogicalAnd();
    while (true)
    {
        _scanner.SkipWhitespace();
        if (MatchKeyword("or") || MatchOperator("||"))
        {
            if (ConvertToBool(left)) return BoolToT(true);
            var right = EvalLogicalAnd();
            left = BoolToT(ConvertToBool(right));
        }
        else break;
    }
    return left;
}

private T EvalLogicalAnd()
{
    var left = EvalEquality();
    while (true)
    {
        _scanner.SkipWhitespace();
        if (MatchKeyword("and") || MatchOperator("&&"))
        {
            if (!ConvertToBool(left)) return BoolToT(false);
            var right = EvalEquality();
            left = BoolToT(ConvertToBool(right));
        }
        else break;
    }
    return left;
}

private T EvalAdditive()
{
    var left = EvalMultiplicative();
    while (true)
    {
        _scanner.SkipWhitespace();
        if (_scanner.Peek() == '+')
        {
            _scanner.Read();
            var right = EvalMultiplicative();
            left = Add(left, right);
        }
        else if (_scanner.Peek() == '-')
        {
            _scanner.Read();
            var right = EvalMultiplicative();
            left = Subtract(left, right);
        }
        else break;
    }
    return left;
}

private T EvalMultiplicative()
{
    var left = EvalPower();
    while (true)
    {
        _scanner.SkipWhitespace();
        if (_scanner.Peek() == '*')
        {
            _scanner.Read();
            var right = EvalPower();
            left = Multiply(left, right);
        }
        else if (_scanner.Peek() == '/')
        {
            if (_scanner.PeekNext() == '/')
            {
                _scanner.Read(); _scanner.Read();
                var right = EvalPower();
                left = IntegerDivide(left, right);
            }
            else
            {
                _scanner.Read();
                var right = EvalPower();
                left = Divide(left, right);
            }
        }
        else if (_scanner.Peek() == '%')
        {
            _scanner.Read();
            var right = EvalPower();
            left = Modulo(left, right);
        }
        else break;
    }
    return left;
}

private T EvalPower()
{
    var left = EvalUnary();
    _scanner.SkipWhitespace();
    if (_scanner.Peek() == '^')
    {
        _scanner.Read();
        var right = EvalPower();
        return Power(left, right);
    }
    return left;
}

private T EvalUnary()
{
    _scanner.SkipWhitespace();
    if (_scanner.Peek() == '+')
    {
        _scanner.Read();
        return EvalUnary();
    }
    if (_scanner.Peek() == '-')
    {
        _scanner.Read();
        var operand = EvalUnary();
        return Negate(operand);
    }
    if (MatchKeyword("not") || _scanner.Peek() == '!')
    {
        if (_scanner.Peek() == '!') _scanner.Read();
        var operand = EvalUnary();
        return Not(operand);
    }
    if (_scanner.Peek() == '~')
    {
        _scanner.Read();
        var operand = EvalUnary();
        return BitwiseNot(operand);
    }
    return EvalPrimary();
}

private T EvalPrimary()
{
    _scanner.SkipWhitespace();
    var ch = _scanner.Peek();

    if (char.IsDigit(ch) || ch == '.')
    {
        return ReadNumber();
    }

    if (ch == '(')
    {
        _scanner.Read();
        var result = EvalExpression();
        _scanner.SkipWhitespace();
        if (_scanner.Peek() != ')')
            throw new FastEvalException("未闭合的括号");
        _scanner.Read();
        return result;
    }

    if (IsIdentifierStart(ch))
    {
        return EvalIdentifierOrFunction();
    }

    throw new FastEvalException($"意外的字符 '{ch}'，位置 {_scanner.Position}");
}
```

### 6.3 标识符与函数求值

```csharp
private T EvalIdentifierOrFunction()
{
    var identifierSpan = _scanner.ReadIdentifierSpan();
    _scanner.SkipWhitespace();

    if (_scanner.Peek() == '(')
    {
        return EvalFunctionCall(identifierSpan);
    }

    if (identifierSpan.SequenceEqual("true"))
        return BoolToT(true);
    if (identifierSpan.SequenceEqual("false"))
        return BoolToT(false);
    if (identifierSpan.SequenceEqual("NaN"))
        return DoubleToT(double.NaN);
    if (identifierSpan.SequenceEqual("INF"))
        return DoubleToT(double.PositiveInfinity);
    if (identifierSpan.SequenceEqual("PI"))
        return DoubleToT(3.14159265358979);
    if (identifierSpan.SequenceEqual("E"))
        return DoubleToT(2.71828182845905);

    return LookupVariable(identifierSpan);
}

private T EvalFunctionCall(ReadOnlySpan<char> name)
{
    _scanner.Read();

    var args = new List<T>();
    _scanner.SkipWhitespace();
    if (_scanner.Peek() != ')')
    {
        args.Add(EvalExpression());
        while (true)
        {
            _scanner.SkipWhitespace();
            if (_scanner.Peek() != ',') break;
            _scanner.Read();
            args.Add(EvalExpression());
        }
    }

    _scanner.SkipWhitespace();
    if (_scanner.Peek() != ')')
        throw new FastEvalException("函数调用未闭合");
    _scanner.Read();

    return CallBuiltInFunction(name, args);
}
```

### 6.4 类型特化运算方法

为 `double`、`long`、`bool` 提供特化的运算实现，避免运行时类型检查：

```csharp
private static T Add(T left, T right)
{
    if (typeof(T) == typeof(double))
        return (T)(object)((double)(object)left + (double)(object)right);
    if (typeof(T) == typeof(long))
        return (T)(object)checked((long)(object)left + (long)(object)right);
    throw new FastEvalException("加法运算需要数值类型");
}

private static T Subtract(T left, T right)
{
    if (typeof(T) == typeof(double))
        return (T)(object)((double)(object)left - (double)(object)right);
    if (typeof(T) == typeof(long))
        return (T)(object)checked((long)(object)left - (long)(object)right);
    throw new FastEvalException("减法运算需要数值类型");
}

private static T Multiply(T left, T right)
{
    if (typeof(T) == typeof(double))
        return (T)(object)((double)(object)left * (double)(object)right);
    if (typeof(T) == typeof(long))
        return (T)(object)checked((long)(object)left * (long)(object)right);
    throw new FastEvalException("乘法运算需要数值类型");
}

private static T Divide(T left, T right)
{
    if (typeof(T) == typeof(double))
    {
        var d = (double)(object)right;
        if (d == 0) throw new DivisionByZeroException();
        return (T)(object)((double)(object)left / d);
    }
    if (typeof(T) == typeof(long))
    {
        var l = (long)(object)right;
        if (l == 0) throw new DivisionByZeroException();
        return (T)(object)((double)(long)(object)left / l);
    }
    throw new FastEvalException("除法运算需要数值类型");
}

private static T Negate(T operand)
{
    if (typeof(T) == typeof(double))
        return (T)(object)(-(double)(object)operand);
    if (typeof(T) == typeof(long))
        return (T)(object)checked(-(long)(object)operand);
    throw new FastEvalException("取负运算需要数值类型");
}

private static bool ConvertToBool(T value)
{
    if (typeof(T) == typeof(bool))
        return (bool)(object)value;
    if (typeof(T) == typeof(long))
        return (long)(object)value != 0;
    if (typeof(T) == typeof(double))
        return (double)(object)value != 0;
    throw new FastEvalException("无法转换为布尔类型");
}

private static T BoolToT(bool value)
{
    if (typeof(T) == typeof(bool))
        return (T)(object)value;
    if (typeof(T) == typeof(long))
        return (T)(object)(value ? 1L : 0L);
    if (typeof(T) == typeof(double))
        return (T)(object)(value ? 1.0 : 0.0);
    throw new FastEvalException("无法从布尔类型转换");
}

private static T DoubleToT(double value)
{
    if (typeof(T) == typeof(double))
        return (T)(object)value;
    if (typeof(T) == typeof(long))
        return (T)(object)(long)value;
    throw new FastEvalException("无法从 double 类型转换");
}
```

**性能说明**：虽然 `if (typeof(T) == typeof(double))` 看起来像运行时检查，但 .NET JIT 的**泛型特化**会为每种 `T` 生成独立的机器码，这些 `if` 分支在 JIT 编译后会被完全消除（死代码消除），实际执行的是直接类型运算指令。

---

## 7. 内置函数表设计

### 7.1 函数表结构

```csharp
internal static class BuiltInFastFunctions
{
    private static readonly Dictionary<string, Func<double[], double>> _doubleFunctions;
    private static readonly Dictionary<string, Func<long[], long>> _longFunctions;

    static BuiltInFastFunctions()
    {
        _doubleFunctions = new Dictionary<string, Func<double[], double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["sin"] = args => Math.Sin(args[0]),
            ["cos"] = args => Math.Cos(args[0]),
            ["tan"] = args => Math.Tan(args[0]),
            ["asin"] = args => Math.Asin(args[0]),
            ["acos"] = args => Math.Acos(args[0]),
            ["atan"] = args => Math.Atan(args[0]),
            ["atan2"] = args => Math.Atan2(args[0], args[1]),
            ["sqrt"] = args => args[0] < 0
                ? throw new FastEvalException("不能对负数求平方根")
                : Math.Sqrt(args[0]),
            ["abs"] = args => Math.Abs(args[0]),
            ["exp"] = args => Math.Exp(args[0]),
            ["ln"] = args => args[0] <= 0
                ? throw new FastEvalException("不能对非正数求对数")
                : Math.Log(args[0]),
            ["log"] = args => args[0] <= 0
                ? throw new FastEvalException("不能对非正数求对数")
                : Math.Log(args[0]),
            ["log10"] = args => args[0] <= 0
                ? throw new FastEvalException("不能对非正数求对数")
                : Math.Log10(args[0]),
            ["log2"] = args => args[0] <= 0
                ? throw new FastEvalException("不能对非正数求对数")
                : Math.Log2(args[0]),
            ["ceil"] = args => Math.Ceiling(args[0]),
            ["floor"] = args => Math.Floor(args[0]),
            ["round"] = args => args.Length == 1
                ? Math.Round(args[0])
                : Math.Round(args[0], (int)args[1]),
            ["truncate"] = args => Math.Truncate(args[0]),
            ["sign"] = args => (double)Math.Sign(args[0]),
            ["max"] = args => Math.Max(args[0], args[1]),
            ["min"] = args => Math.Min(args[0], args[1]),
            ["pow"] = args => args[0] < 0 && args[1] != Math.Floor(args[1])
                ? throw new FastEvalException("不能对负数求非整数次幂")
                : Math.Pow(args[0], args[1]),
        };

        _longFunctions = new Dictionary<string, Func<long[], long>>(StringComparer.OrdinalIgnoreCase)
        {
            ["abs"] = args => Math.Abs(args[0]),
            ["sign"] = args => (long)Math.Sign(args[0]),
            ["max"] = args => Math.Max(args[0], args[1]),
            ["min"] = args => Math.Min(args[0], args[1]),
        };
    }

    public static bool TryGetDoubleFunction(string name, out Func<double[], double>? func)
        => _doubleFunctions.TryGetValue(name, out func);

    public static bool TryGetLongFunction(string name, out Func<long[], long>? func)
        => _longFunctions.TryGetValue(name, out func);
}
```

### 7.2 函数调用路径

```csharp
private T CallBuiltInFunction(ReadOnlySpan<char> name, List<T> args)
{
    var nameStr = name.ToString();

    if (typeof(T) == typeof(double) && BuiltInFastFunctions.TryGetDoubleFunction(nameStr, out var doubleFunc))
    {
        var doubleArgs = new double[args.Count];
        for (int i = 0; i < args.Count; i++)
            doubleArgs[i] = Convert.ToDouble(args[i]);
        return DoubleToT(doubleFunc(doubleArgs));
    }

    if (typeof(T) == typeof(long) && BuiltInFastFunctions.TryGetLongFunction(nameStr, out var longFunc))
    {
        var longArgs = new long[args.Count];
        for (int i = 0; i < args.Count; i++)
            longArgs[i] = Convert.ToInt64(args[i]);
        return (T)(object)longFunc(longArgs);
    }

    throw new FastEvalException($"未知函数 '{nameStr}'");
}
```

**优化空间**：后续可用 `Span<T>` 替代 `List<T>` + `double[]`/`long[]`，进一步减少堆分配。但 `List<T>` 已比当前 `List<object>` + `ToArray()` + `Convert.ChangeType` 链路快得多。

---

## 8. 变量绑定机制

### 8.1 变量查找策略

```csharp
private T LookupVariable(ReadOnlySpan<char> name)
{
    if (_variables != null)
    {
        foreach (var kv in _variables)
        {
            if (name.SequenceEqual(kv.Key))
                return kv.Value;
        }
    }

    if (_objectVariables != null)
    {
        foreach (var kv in _objectVariables)
        {
            if (name.SequenceEqual(kv.Key))
            {
                if (kv.Value is T typedValue)
                    return typedValue;
                return (T)Convert.ChangeType(kv.Value, typeof(T));
            }
        }
    }

    throw new FastEvalException($"未定义的变量 '{name.ToString()}'");
}
```

### 8.2 字典查找优化

当前使用 `foreach` 线性扫描 + `SequenceEqual` 比较。后续可优化为：

1. **预构建 `Dictionary<string, T>`**：在 `FastEvaluator` 构造时，将 `IReadOnlyDictionary` 转为 `Dictionary`，使用 `TryGetValue` O(1) 查找
2. **Span 字典查找**：.NET 9+ 的 `GetAlternateLookup<ReadOnlySpan<char>>` 允许直接用 Span 查字典，零字符串分配

```csharp
// .NET 9+ 优化（后续版本）
private T LookupVariable(ReadOnlySpan<char> name)
{
    if (_variables != null && _variables.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out var value))
        return value;
    throw new FastEvalException($"未定义的变量 '{name.ToString()}'");
}
```

---

## 9. 错误处理策略

### 9.1 FastEvalException

```csharp
public class FastEvalException : MathEvalException
{
    public int Position { get; }

    public FastEvalException(string message, int position = -1)
        : base(position >= 0 ? $"{message}，位置 {position}" : message)
    {
        Position = position;
    }
}
```

### 9.2 错误精度对比

| 错误类型 | Expression.Eval() | FastEval |
|----------|-------------------|----------|
| 语法错误 | 精确行列号 | 近似字符位置 |
| 类型错误 | 完整类型信息 | 简化类型信息 |
| 除零错误 | 相同 | 相同 |
| 溢出错误 | 相同 | 相同 |
| 函数未找到 | 相同 | 相同 |
| 变量未找到 | 相同 | 相同 |

### 9.3 表达式长度限制

与现有 Lexer 一致：**4096 字符**。超出时抛出 `FastEvalException`。

---

## 10. 性能预期与对比

### 10.1 基准表达式

```
"22888.32 * 30 / 323.34 / .5 - -1 / (2 + 22888.32) * 4 - 6"
```

### 10.2 预期性能

| 库 / 路径 | 预期耗时 | 堆分配 | 相对 MathEvaluator |
|-----------|----------|--------|-------------------|
| **MathEvaluator** | ~465ns | ~112B | 1.0x（基准） |
| **FastEval (方案 A)** | ~600-1,500ns | ~50-200B | 1.3-3.2x |
| **MathEval (当前)** | ~3,000-5,000ns | ~3-5KB | 6.5-10.8x |
| **NCalc** | ~5,000-7,000ns | ~2.5-4.5KB | 10.8-15.1x |

### 10.3 开销分解

| 开销 | MathEvaluator | FastEval | MathEval (当前) |
|------|--------------|----------|----------------|
| 字符串扫描 | ~100ns | ~100-200ns | ~2,000ns (Lexer) |
| 语法解析 | 0（即求值） | 0（即求值） | ~1,500ns (Parser) |
| AST 构建 | 0 | 0 | ~1,000ns |
| AST 遍历 | 0 | 0 | ~1,000ns |
| 值类型运算 | ~300ns | ~300-800ns | ~500ns (含装箱) |
| 函数调用 | ~50ns | ~50-200ns | ~200ns (含 object[]) |
| 符号查找 | ~20ns (Trie) | ~30-50ns (Dict) | ~30ns (ConcurrentDict) |
| **合计** | **~470ns** | **~480-1,250ns** | **~3,230ns** |

### 10.4 FastEval 仍慢于 MathEvaluator 的原因

| 因素 | 额外开销 | 说明 |
|------|----------|------|
| `typeof(T)` 运行时检查 | ~50-200ns | JIT 特化后大部分消除，但首次调用有开销 |
| `List<T>` 参数分配 | ~50-100ns | 函数调用时分配，可用 `Span<T>` 优化 |
| `Dictionary` 线性扫描变量 | ~50-100ns | 可用 .NET 9 `GetAlternateLookup` 优化 |
| `name.ToString()` 函数名分配 | ~30-50ns | 可用 Span 字典查找优化 |
| 无 Trie 前缀匹配 | ~20-50ns | 直接字符匹配 vs Trie 遍历 |

---

## 11. 项目结构

### 11.1 新增文件

```
MathEval/
├── FastEval/
│   ├── FastEval.cs                    # 公共静态入口
│   ├── FastEvaluator{T}.cs            # 泛型递归求值器
│   ├── FastScanner.cs                 # 字符扫描器（ref struct）
│   ├── BuiltInFastFunctions.cs        # 内置函数表
│   └── FastEvalException.cs           # 异常类型
```

### 11.2 不修改的现有文件

FastEval 模块**完全独立**，不修改任何现有文件。仅在 `Expression.cs` 中添加一行注释引导用户：

```csharp
public static class Expression
{
    // 现有方法不变

    // 新增：引导注释（可选）
    // 对于纯数值表达式的一次性求值，考虑使用 FastEval.EvalDouble() 以获得更高性能
}
```

---

## 12. 与现有管线的共存策略

### 12.1 语法同步规则

FastEval 支持的语法是现有语法的**严格子集**。当现有语法变更时，需遵循以下同步规则：

| 变更类型 | 同步策略 |
|----------|----------|
| 新增运算符 | 若属于数值/布尔/位运算子集，同步添加到 FastEval |
| 新增内置函数 | 同步添加到 `BuiltInFastFunctions` |
| 修改优先级 | 同步修改 `FastEvaluator` 的递归层级 |
| 新增类型（如 decimal） | 不影响 FastEval（FastEval 不支持） |
| 新增语法结构（如数组） | 不影响 FastEval（FastEval 不支持） |

### 12.2 语义一致性保证

FastEval 的运算语义必须与现有管线**完全一致**：

| 语义 | 现有管线 | FastEval | 一致性 |
|------|----------|----------|--------|
| 整数溢出 | `checked` | `checked` | ✅ |
| 除法类型 | `long / long → double` | `long / long → double` | ✅ |
| 整除 | `long // long → long` | `long // long → long` | ✅ |
| 幂运算 | `long ^ long → long/float` | `long ^ long → long/float` | ✅ |
| 逻辑短路 | `and`/`or` 短路 | `and`/`or` 短路 | ✅ |
| 三元短路 | 只求值选中分支 | 只求值选中分支 | ✅ |
| 布尔转数值 | `true → 1L` | `true → 1L` | ✅ |
| NaN 传播 | 算术运算传播 NaN | 算术运算传播 NaN | ✅ |

---

## 13. 测试策略

### 13.1 测试分类

| 测试类别 | 说明 | 数量预期 |
|----------|------|----------|
| **语义等价测试** | 同一表达式在 FastEval 和 `Expression.Eval()` 中结果一致 | ~200 |
| **边界值测试** | 溢出、除零、NaN、INF、极大/极小值 | ~50 |
| **变量绑定测试** | 各种变量组合 | ~30 |
| **函数测试** | 每个内置函数的正确性 | ~80 |
| **错误测试** | 语法错误、类型错误、未定义变量/函数 | ~40 |
| **性能基准测试** | BenchmarkDotNet 对比 | ~10 |

### 13.2 语义等价测试模板

```csharp
[Theory]
[InlineData("2 + 3", 5L)]
[InlineData("3.14 * 2", 6.28)]
[InlineData("0xFF & 0x0F", 15L)]
[InlineData("2 ^ 10", 1024L)]
[InlineData("true and false", false)]
[InlineData("sin(PI / 2)", 1.0)]
public void FastEval_ShouldMatch_ExpressionEval(string expression, object expected)
{
    var fastResult = FastEval.Eval<double>(expression);
    var normalResult = (double)Expression.Eval(expression);

    Assert.Equal(normalResult, fastResult, 0.0001);
    Assert.Equal(Convert.ToDouble(expected), fastResult, 0.0001);
}
```

### 13.3 性能基准测试模板

```csharp
[MemoryDiagnoser]
public class FastEvalBenchmarks
{
    private const string ArithmeticExpr = "22888.32 * 30 / 323.34 / .5 - -1 / (2 + 22888.32) * 4 - 6";
    private const string FunctionExpr = "Sin(a) + Cos(b)";
    private static readonly Dictionary<string, double> Vars = new() { ["a"] = 1.0, ["b"] = 2.0 };

    [Benchmark]
    public double FastEval_Arithmetic() => FastEval.EvalDouble(ArithmeticExpr);

    [Benchmark]
    public double MathEval_Arithmetic() => Expression.Eval<double>(ArithmeticExpr);

    [Benchmark]
    public double FastEval_Function() => FastEval.EvalDouble(FunctionExpr, Vars);

    [Benchmark]
    public double MathEval_Function() => Expression.Eval<double>(FunctionExpr, new ExpressionContext { ["a"] = 1.0, ["b"] = 2.0 });
}
```

---

## 14. 实现步骤

### Phase 1：核心框架（~400 行）

| 步骤 | 内容 | 产出 |
|------|------|------|
| 1.1 | 创建 `FastEval/` 目录和文件结构 | 空文件 |
| 1.2 | 实现 `FastScanner`（ref struct） | 数字解析 + 标识符读取 + 操作符匹配 |
| 1.3 | 实现 `FastEvalException` | 异常类 |
| 1.4 | 实现 `FastEvaluator<double>` 核心递归 | 加减乘除 + 括号 + 幂运算 |
| 1.5 | 实现 `FastEval.EvalDouble()` 静态入口 | 公共 API |

### Phase 2：完整运算符（~300 行）

| 步骤 | 内容 | 产出 |
|------|------|------|
| 2.1 | 添加位运算（`& \| ^ ~ << >>`） | 位运算方法 |
| 2.2 | 添加比较运算（`< > <= >= == !=`） | 比较方法 |
| 2.3 | 添加逻辑运算（`and or not xor && \|\| !`） | 短路逻辑方法 |
| 2.4 | 添加三元条件（`?:`） | 条件方法 |
| 2.5 | 添加整除和取模（`// %`） | 整除/取模方法 |

### Phase 3：函数与变量（~200 行）

| 步骤 | 内容 | 产出 |
|------|------|------|
| 3.1 | 实现 `BuiltInFastFunctions` 函数表 | 20+ 内置函数 |
| 3.2 | 实现函数调用求值 | `EvalFunctionCall` |
| 3.3 | 实现变量查找 | `LookupVariable` |
| 3.4 | 实现 `FastEvaluator<long>` 特化 | `EvalLong` 路径 |
| 3.5 | 实现 `FastEvaluator<bool>` 特化 | `EvalBool` 路径 |

### Phase 4：多进制与常量（~100 行）

| 步骤 | 内容 | 产出 |
|------|------|------|
| 4.1 | 添加 `0x/0o/0b` 多进制解析 | `ReadHex/ReadOctal/ReadBinary` |
| 4.2 | 添加 `PI/E/NaN/INF/true/false` 常量 | 标识符匹配 |
| 4.3 | 添加 `Eval<T>()` 泛型入口 | 统一入口 |

### Phase 5：测试与基准（~500 行测试）

| 步骤 | 内容 | 产出 |
|------|------|------|
| 5.1 | 语义等价测试 | ~200 测试用例 |
| 5.2 | 边界值和错误测试 | ~90 测试用例 |
| 5.3 | BenchmarkDotNet 基准测试 | 性能数据 |
| 5.4 | 验证性能目标 | 600-1,500ns 达标确认 |

### 总代码量预估

| 模块 | 行数 |
|------|------|
| FastScanner | ~200 |
| FastEvaluator{T} | ~500 |
| BuiltInFastFunctions | ~100 |
| FastEval (入口) | ~50 |
| FastEvalException | ~15 |
| **合计** | **~865 行** |
