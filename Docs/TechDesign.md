# MathEval 技术实现方案

> **版本**：v1.1
> **最后更新**：2026-05-18
> **状态**：已审核

---

## 目录

1. [整体架构选型](#1-整体架构选型)
2. [从 NCalc 借鉴的内容](#2-从-ncalc-借鉴的内容)
3. [从 NFun 借鉴的内容](#3-从-nfun-借鉴的内容)
4. [各模块技术方案](#4-各模块技术方案)
5. [关键技术决策总结](#5-关键技术决策总结)
6. [编译管线全景](#6-编译管线全景)

---

## 1. 整体架构选型

**核心决策：采用 NCalc 的架构骨架 + NFun 的关键特性增强**

| 架构维度 | 选择方案 | 来源 | 理由 |
|----------|---------|------|------|
| 解析器类型 | **手写递归下降解析器** | NCalc | 简单可靠、无外部依赖、优先级通过方法层级自然表达 |
| 词法分析器 | **手写逐字符扫描器** | NCalc + NFun 增强 | 轻量，但需扩展多进制字面量和字符串插值 |
| 求值引擎 | **AST + Visitor 模式** | NCalc | 解耦求值与结构，可扩展性强 |
| 类型系统 | **bool/long/double/string**（对外表现为 bool/number/string） | PRD 自定 | NCalc 无区分，NFun 过于复杂（18 种原始类型），MathEval 只需 3 种值类型 |
| 上下文系统 | **ConcurrentDictionary + 链式继承** | NFun 启发 | NCalc 无继承，NFun 有作用域栈，MathEval 采用父子链式继承 |
| 函数注册 | **弱类型 + 强类型双通道** | NCalc 弱类型 + NFun 强类型 | 兼顾灵活性和类型安全 |
| 缓存机制 | **ConcurrentDictionary 全局缓存** | NCalc | 成熟方案，线程安全 |

---

## 2. 从 NCalc 借鉴的内容

### 2.1 核心借鉴

| 借鉴项 | NCalc 实现 | MathEval 适配 |
|--------|-----------|--------------|
| **递归下降解析器** | 方法层级对应优先级，左结合用循环，右结合用递归 | 直接采用此模式，调整优先级层级以匹配 PRD 的 14 级优先级 |
| **AST 节点体系** | `LogicalExpression` 基类 + 6 种节点 | 采用相同基类设计，扩展为 7 种节点（增加 `InterpolatedString`） |
| **双分派 Visitor** | `Accept` + `Visit` 双分派 | 直接采用，增加泛型 Visitor 接口 `IExpressionVisitor<T>` |
| **EvaluationVisitor** | 求值逻辑完全在 Visitor 中 | 采用相同模式，但需实现完整的 `long`/`double` 类型推断 |
| **表达式缓存** | 静态 `ConcurrentDictionary<string, AST>` | 直接采用，增加 `ExpressionOptions.NoCache` 控制 |

### 2.2 NCalc 的不足（MathEval 需改进）

| 不足 | MathEval 改进方案 |
|------|------------------|
| 不支持多进制字面量 | Lexer 增加 `0x`/`0o`/`0b` 前缀扫描 |
| 不支持字符串插值 | Lexer 增加 `$` 前缀扫描，Parser 增加 `InterpolatedString` 节点 |
| `^` 表示异或 | 改为 `^` 表示乘方，`xor` 表示异或 |
| 无上下文继承 | 实现 `CreateChild()` 链式继承 |
| 弱类型函数注册 | 增加强类型 `Func<T1, ..., TResult>` 注册 |
| `Dictionary` 非线程安全 | 改用 `ConcurrentDictionary` |
| 无泛型返回值 | 增加 `Eval<T>()` 方法 |
| 无 Builder 模式 | 增加 Fluent Builder API |

---

## 3. 从 NFun 借鉴的内容

### 3.1 核心借鉴

| 借鉴项 | NFun 实现 | MathEval 适配 |
|--------|-----------|--------------|
| **字符串插值词法分析** | `_isInInterpolation` 标志 + `_interpolationLayers` 栈跟踪嵌套花括号 | 借鉴此状态机模式，但简化为 `$` 前缀 + 花括号嵌套追踪 |
| **字符串插值 AST 构建** | 将插值转换为 `concat3Texts` 等函数调用链 | **不采用**此方案，改为原生 `InterpolatedString` 节点，在求值时直接拼接，避免函数名污染 |
| **上下文继承/作用域** | `VariableScopeAliasTable` 栈式作用域 + `ScopeFunctionDictionary` 链式查找 | 借鉴链式查找模式，但简化为 `CreateChild()` 显式创建子上下文，子上下文持有父上下文引用 |
| **强类型函数注册** | `LambdaWrapperFactory.Create` 将 `Func<T1, TOut>` 包装为 `IConcreteFunction` | 借鉴此包装模式，将 `Func<T1, ..., TResult>` 统一转换为内部 `ExpressionFunction` 委托 |
| **保留关键字检测** | 保留字列表 + 错误提示 | 借鉴此模式，`NaN`/`INF` 等关键字不可覆盖 |

### 3.2 NFun 的复杂特性（MathEval 不采用）

| 不采用的特性 | 原因 |
|-------------|------|
| 全局类型推断（Tic 系统） | MathEval 只有 3 种值类型，无需复杂的约束图求解 |
| 18 种原始类型 | MathEval 只需 `bool`/`number`/`string` |
| Pratt 优先级爬升 | 递归下降更直观，优先级通过方法层级自然表达 |
| 隐式乘法（`10x`） | PRD 未要求，增加歧义 |
| 比较链（`a < b < c`） | PRD 未要求 |
| 用户自定义函数（脚本内） | MathEval 只支持宿主注册函数 |
| 泛型函数 | MathEval 无泛型需求 |

---

## 4. 各模块技术方案

### 4.1 项目结构

```
MathEval/
├── src/
│   └── MathEval/
│       ├── MathEval.csproj              # .NET 10 类库
│       ├── Exceptions/                  # 异常层次结构
│       │   ├── MathEvalException.cs
│       │   ├── ParseException.cs
│       │   ├── EvaluateException.cs
│       │   ├── TypeMismatchException.cs
│       │   ├── FunctionNotFoundException.cs
│       │   ├── FunctionTypeMismatchException.cs
│       │   ├── SymbolNotFoundException.cs
│       │   ├── DivisionByZeroException.cs
│       │   └── OverflowException.cs
│       ├── Lexer/                       # 词法分析
│       │   ├── TokenType.cs             # Token 类型枚举
│       │   ├── Token.cs                 # Token 数据结构
│       │   └── Lexer.cs                 # 逐字符扫描器
│       ├── Parser/                      # 语法分析
│       │   └── Parser.cs                # 递归下降解析器
│       ├── AST/                         # 抽象语法树
│       │   ├── LogicalExpression.cs     # AST 基类
│       │   ├── ValueExpression.cs       # 常量值节点
│       │   ├── Identifier.cs            # 标识符节点
│       │   ├── BinaryExpression.cs      # 二元运算节点
│       │   ├── UnaryExpression.cs       # 一元运算节点
│       │   ├── FunctionCall.cs          # 函数调用节点
│       │   ├── InterpolatedString.cs    # 插值字符串节点
│       │   └── ConditionalExpression.cs # 三元条件节点
│       ├── Visitors/                    # Visitor 模式
│       │   ├── IExpressionVisitor.cs    # Visitor 接口
│       │   └── EvaluationVisitor.cs     # 求值 Visitor
│       ├── Context/                     # 上下文系统
│       │   ├── ExpressionContext.cs     # 上下文（符号+函数+继承）
│       │   └── ExpressionFunction.cs    # 函数委托类型
│       ├── Functions/                   # 内置函数
│       │   └── BuiltInFunctions.cs      # 内置数学函数注册
│       ├── API/                         # 公共 API
│       │   ├── Expression.cs            # 主入口（静态方法 + Builder）
│       │   ├── ICalculator.cs           # 计算器接口
│       │   ├── Calculator.cs            # 计算器实现
│       │   └── ExpressionOptions.cs     # 选项枚举
│       └── TypeSystem/                  # 类型推断
│           └── TypeHelper.cs            # 类型转换与推断工具
├── tests/
│   └── MathEval.Tests/
│       └── ...
└── Docs/
    └── ...
```

### 4.2 词法分析器（Lexer）技术方案

**核心设计**：手写逐字符扫描器，维护 `_text` + `_position` 游标。

#### 4.2.1 Token 类型

```csharp
public enum TokenType
{
    // 字面量
    Integer, Float, String, Boolean, NaN, INF,
    // 标识符
    Identifier,
    // 算术运算符
    Plus, Minus, Asterisk, Slash, DoubleSlash, Percent, Caret,
    // 位运算符
    Ampersand, Pipe, XorKeyword, Tilde, LeftShift, RightShift,
    // 逻辑运算符
    AndKeyword, OrKeyword, NotKeyword, DoubleAmpersand, DoublePipe, Exclamation,
    // 比较运算符
    Equal, NotEqual, Less, Greater, LessOrEqual, GreaterOrEqual,
    // 三元运算符
    QuestionMark, Colon,
    // 分隔符
    LeftParenthesis, RightParenthesis, Comma,
    // 字符串插值
    InterpolationStart,      // $ 符号
    InterpolationOpen,       // {
    InterpolationClose,      // }
    InterpolationEscape,     // {{ 或 }}
    InterpolationFormat,     // : 格式说明符
    // 结束
    EOF
}
```

#### 4.2.2 Token 数据结构

```csharp
public class Token
{
    public TokenType Type { get; }
    public string Text { get; }       // 原始文本
    public int Position { get; }      // 在表达式中的位置（用于错误报告）
    public int Line { get; }          // 行号
    public int Column { get; }        // 列号
}
```

#### 4.2.3 关键扫描逻辑

**1. 多进制数字扫描**（借鉴 NFun 的 `ReadNumberOrIp`，但简化）：

- `0x`/`0X` 前缀 → 十六进制，调用 `ReadHexNumber()` → 产出 `Integer` Token
- `0o`/`0O` 前缀 → 八进制，调用 `ReadOctalNumber()` → 产出 `Integer` Token
- `0b`/`0B` 前缀 → 二进制，调用 `ReadBinaryNumber()` → 产出 `Integer` Token
- 其他 → 十进制，调用 `ReadDecimalNumber()` → 区分 `Integer`（无小数点/指数）和 `Float`（有小数点/指数）

**2. 字符串插值扫描**（借鉴 NFun 的 `_interpolationLayers` 栈）：

- 遇到 `$` + 引号 → 进入插值模式
- 遇到 `{` → 嵌套层级 +1，产出 `InterpolationOpen`
- 遇到 `}` → 嵌套层级 -1，产出 `InterpolationClose`
- 遇到 `{{` / `}}` → 产出转义花括号
- 嵌套层级归零 + 遇到闭合引号 → 退出插值模式

**3. 关键字识别**：

标识符扫描完成后查表，关键字列表：`true`, `false`, `and`, `or`, `not`, `xor`, `NaN`, `INF`（不区分大小写匹配）

**4. 双字符运算符前瞻**：

`==`, `!=`, `<=`, `>=`, `//`, `<<`, `>>`, `&&`, `||`

**5. 表达式长度限制**：

Lexer 初始化时检查表达式长度，超过 4096 字符抛出 `ParseException`

#### 4.2.4 扫描流程伪代码

```
while (position < text.Length):
    skip whitespace
    ch = current char
    if ch is digit:        → scanNumber()
    if ch is letter/_/Unicode: → scanIdentifier() → 可能转为关键字
    if ch is quote:        → scanString()
    if ch is '$' and next is quote: → scanInterpolatedString()
    if ch is operator:     → scanOperator() (可能需要向前看一个字符)
    if ch is delimiter:    → 返回对应 Token
```

### 4.3 语法分析器（Parser）技术方案

**核心设计**：递归下降解析器，方法层级对应优先级。

#### 4.3.1 14 级优先级完整定义（从高到低）

| 优先级 | 运算符 | 含义 | 结合性 |
|--------|--------|------|--------|
| 1 | `()` | 分组 | - |
| 2 | `+`, `-`, `not`, `!`, `~` | 正号、取负、逻辑非、按位取反 | 右结合 |
| 3 | `^` | 乘方 | 右结合 |
| 4 | `*`, `/`, `//`, `%` | 乘法、除法（浮点）、整除、取模 | 左结合 |
| 5 | `+`, `-` | 加法/字符串拼接、减法 | 左结合 |
| 6 | `<<`, `>>` | 左移、右移 | 左结合 |
| 7 | `&` | 按位与 | 左结合 |
| 8 | `xor` | 按位异或 | 左结合 |
| 9 | `|` | 按位或 | 左结合 |
| 10 | `>`, `<`, `>=`, `<=` | 大于、小于、大于等于、小于等于 | 左结合 |
| 11 | `==`, `!=` | 等于、不等于 | 左结合 |
| 12 | `and`, `&&` | 逻辑与（短路） | 左结合 |
| 13 | `or`, `||` | 逻辑或（短路） | 左结合 |
| 14 | `?:` | 三元条件 | 右结合 |

#### 4.3.2 方法调用层级（14 级优先级，从低到高）

```
ParseExpression()                        // 入口
  └─ ParseConditional()                  // 14: ?: 三元（右结合）
       └─ ParseLogicalOr()               // 13: or / ||
            └─ ParseLogicalAnd()         // 12: and / &&
                 └─ ParseEquality()      // 11: == / !=
                      └─ ParseRelational()  // 10: > / < / >= / <=
                           └─ ParseBitwiseOr()  // 9: |
                                └─ ParseBitwiseXor()  // 8: xor
                                     └─ ParseBitwiseAnd()  // 7: &
                                          └─ ParseShift()  // 6: << / >>
                                               └─ ParseAdditive()  // 5: + / -
                                                    └─ ParseMultiplicative()  // 4: * / / // %
                                                         └─ ParsePower()  // 3: ^ （右结合）
                                                              └─ ParseUnary()  // 2: + / - / not / ! / ~
                                                                   └─ ParsePrimary()  // 1: 字面量 / 标识符 / 函数调用 / 括号 / 插值字符串
```

#### 4.3.3 关键实现要点

- **左结合**（优先级 4~12）：`while` 循环消费同优先级运算符
- **右结合**（优先级 3 乘方）：递归调用 `ParsePower()` 自身
- **右结合**（优先级 14 三元）：`trueExpr = ParseExpression()`，`falseExpr = ParseExpression()`
- **函数调用**：在 `ParsePrimary()` 中，标识符后跟 `(` 则解析为函数调用
- **插值字符串**：在 `ParsePrimary()` 中，遇到 `$` + 引号则解析为 `InterpolatedString` 节点
- **深度限制**：维护 `_depth` 计数器，超过 1024 抛出 `ParseException`
- **空表达式检查**：解析前检查是否为空或纯空白，抛出 `ParseException`

#### 4.3.4 二元运算解析示例（左结合）

```csharp
private LogicalExpression ParseAdditive()
{
    var left = ParseMultiplicative();
    while (CurrentToken.Type == TokenType.Plus ||
           CurrentToken.Type == TokenType.Minus)
    {
        var op = CurrentToken.Type;
        ReadNextToken();
        var right = ParseMultiplicative();
        left = new BinaryExpression(MapBinaryType(op), left, right);
    }
    return left;
}
```

#### 4.3.5 乘方解析示例（右结合）

```csharp
private LogicalExpression ParsePower()
{
    var left = ParseUnary();
    if (CurrentToken.Type == TokenType.Caret)
    {
        ReadNextToken();
        var right = ParsePower();  // 递归调用自身，实现右结合
        return new BinaryExpression(BinaryExpressionType.Power, left, right);
    }
    return left;
}
```

#### 4.3.6 三元运算符解析示例（右结合）

```csharp
private LogicalExpression ParseConditional()
{
    var condition = ParseLogicalOr();
    if (CurrentToken.Type == TokenType.QuestionMark)
    {
        ReadNextToken();
        var trueExpr = ParseExpression();
        Expect(TokenType.Colon);
        var falseExpr = ParseExpression();
        return new ConditionalExpression(condition, trueExpr, falseExpr);
    }
    return condition;
}
```

### 4.4 AST 节点设计

**借鉴 NCalc 的 `LogicalExpression` 基类 + 子类化模式**，增加 `InterpolatedString` 节点：

```csharp
// 基类 - 借鉴 NCalc
public abstract class LogicalExpression
{
    public abstract void Accept(IExpressionVisitor visitor);
    public abstract T Accept<T>(IExpressionVisitor<T> visitor);
}

// 常量值 - 借鉴 NCalc ValueExpression
// 注意：对外表现为 3 种类型（bool/number/string），内部 number 分为 long/double
public class ValueExpression : LogicalExpression
{
    public object Value { get; }           // bool / long / double / string
}

// 标识符 - 借鉴 NCalc IdentifierExpression
public class Identifier : LogicalExpression
{
    public string Name { get; }
}

// 二元运算 - 借鉴 NCalc BinaryExpression
public class BinaryExpression : LogicalExpression
{
    public BinaryExpressionType Type { get; }  // 枚举区分运算类型
    public LogicalExpression Left { get; }
    public LogicalExpression Right { get; }
}

// 一元运算 - 借鉴 NCalc UnaryExpression
public class UnaryExpression : LogicalExpression
{
    public UnaryExpressionType Type { get; }   // Positive / Negate / Not / BitwiseNot
    public LogicalExpression Operand { get; }
}

// 函数调用 - 借鉴 NCalc FunctionCall
public class FunctionCall : LogicalExpression
{
    public string Name { get; }
    public List<LogicalExpression> Arguments { get; }
}

// 插值字符串 - MathEval 新增（NCalc 无此节点）
// 不采用 NFun 的 concat 函数链方案，而是原生节点
public class InterpolatedString : LogicalExpression
{
    public List<InterpolationSegment> Segments { get; }  // 文本段 + 表达式段
}

public abstract class InterpolationSegment
{
}

public class TextSegment : InterpolationSegment     // 纯文本
{
    public string Text { get; }
}

public class ExpressionSegment : InterpolationSegment  // 表达式 + 可选格式
{
    public LogicalExpression Expression { get; }
    public string? FormatSpec { get; }    // 可选，如 "F2"
}

// 三元条件 - 借鉴 NCalc ConditionalExpression
public class ConditionalExpression : LogicalExpression
{
    public LogicalExpression Condition { get; }
    public LogicalExpression TrueExpression { get; }
    public LogicalExpression FalseExpression { get; }
}
```

**为什么不采用 NFun 的 concat 函数链方案？**

- NFun 将插值字符串转换为 `concat3Texts("Hello ", toText(name), "!")` 函数调用链
- 这会污染函数命名空间，且增加不必要的函数调用开销
- MathEval 采用原生 `InterpolatedString` 节点，求值时直接 `StringBuilder` 拼接，更简洁高效

### 4.5 Visitor 模式技术方案

**借鉴 NCalc 的双分派机制**，增加泛型版本：

```csharp
// 无返回值 Visitor
public interface IExpressionVisitor
{
    void Visit(ValueExpression expr);
    void Visit(Identifier expr);
    void Visit(BinaryExpression expr);
    void Visit(UnaryExpression expr);
    void Visit(FunctionCall expr);
    void Visit(InterpolatedString expr);
    void Visit(ConditionalExpression expr);
}

// 泛型 Visitor - PRD 要求
public interface IExpressionVisitor<out T>
{
    T Visit(ValueExpression expr);
    T Visit(Identifier expr);
    T Visit(BinaryExpression expr);
    T Visit(UnaryExpression expr);
    T Visit(FunctionCall expr);
    T Visit(InterpolatedString expr);
    T Visit(ConditionalExpression expr);
}
```

#### 4.5.1 双分派机制

**步骤一：AST 节点的 Accept 方法**

```csharp
// 在 LogicalExpression 基类中
public abstract void Accept(IExpressionVisitor visitor);

// 在各子类中
public override void Accept(IExpressionVisitor visitor)
{
    visitor.Visit(this);  // this 的运行时类型决定调用哪个 Visit 重载
}
```

**步骤二：Visitor 的 Visit(LogicalExpression) 分发方法**

```csharp
public void Visit(LogicalExpression expression)
{
    expression.Accept(this);  // 二次分派，调用具体子类的 Accept
}
```

#### 4.5.2 EvaluationVisitor 核心逻辑

```csharp
public class EvaluationVisitor : IExpressionVisitor<object>
{
    private readonly ExpressionContext _context;

    public EvaluationVisitor(ExpressionContext context)
    {
        _context = context;
    }

    // 二元运算 - 最复杂的部分
    public object Visit(BinaryExpression expr)
    {
        // 短路求值处理（and / or）
        if (expr.Type == BinaryExpressionType.And)
        {
            var leftResult = expr.Left.Accept(this);
            if (!TypeHelper.IsTruthy(leftResult))
                return false;
            var rightResult = expr.Right.Accept(this);
            TypeHelper.RequireBool(rightResult);
            return rightResult;
        }
        if (expr.Type == BinaryExpressionType.Or)
        {
            var leftResult = expr.Left.Accept(this);
            if (TypeHelper.IsTruthy(leftResult))
                return true;
            var rightResult = expr.Right.Accept(this);
            TypeHelper.RequireBool(rightResult);
            return rightResult;
        }

        // 非短路运算：先求值两侧
        var left = expr.Left.Accept(this);
        var right = expr.Right.Accept(this);

        // 根据 BinaryExpressionType 分发到具体计算
        return TypeHelper.EvaluateBinary(expr.Type, left, right);
    }

    // 标识符求值
    public object Visit(Identifier expr)
    {
        if (_context.TryGetSymbol(expr.Name, out var value))
        {
            return value;
        }
        throw new SymbolNotFoundException(expr.Name);
    }

    // 函数调用求值
    public object Visit(FunctionCall expr)
    {
        var args = new List<object>();
        foreach (var arg in expr.Arguments)
        {
            args.Add(arg.Accept(this));
        }
        if (_context.TryGetFunction(expr.Name, out var func))
        {
            return func(args.ToArray());
        }
        throw new FunctionNotFoundException(expr.Name);
    }

    // 插值字符串求值
    public object Visit(InterpolatedString expr)
    {
        var sb = new StringBuilder();
        foreach (var segment in expr.Segments)
        {
            if (segment is TextSegment textSeg)
            {
                sb.Append(textSeg.Text);
            }
            else if (segment is ExpressionSegment exprSeg)
            {
                var value = exprSeg.Expression.Accept(this);
                if (exprSeg.FormatSpec != null)
                    sb.Append(TypeHelper.Format(value, exprSeg.FormatSpec));
                else
                    sb.Append(TypeHelper.ToString(value));
            }
        }
        return sb.ToString();
    }

    // 三元条件求值 - 短路类型推断
    public object Visit(ConditionalExpression expr)
    {
        var condition = expr.Condition.Accept(this);
        TypeHelper.RequireBool(condition);
        if ((bool)condition)
            return expr.TrueExpression.Accept(this);  // 只返回选中分支的类型
        else
            return expr.FalseExpression.Accept(this); // 只返回选中分支的类型
    }

    public object Visit(ValueExpression expr) => expr.Value;

    public object Visit(UnaryExpression expr)
    {
        var operand = expr.Operand.Accept(this);
        return TypeHelper.EvaluateUnary(expr.Type, operand);
    }
}
```

### 4.6 类型推断系统技术方案

**核心原则**：不采用 NFun 的 Tic 约束图（过于复杂），而是在求值时动态推断。

**TypeHelper 工具类**：

```csharp
public static class TypeHelper
{
    // 布尔转数值（优先 long）
    public static object BoolToNumber(bool value, bool preferDouble = false)
    {
        long longValue = value ? 1L : 0L;
        return preferDouble ? (double)longValue : longValue;
    }

    // 数值类型提升
    public static (object, object) Promote(object left, object right)
    {
        // bool → long（如果另一操作数为 long）
        // bool → double（如果另一操作数为 double）
        // long + double → double + double
        var leftIsBool = left is bool;
        var rightIsBool = right is bool;
        var leftIsLong = left is long;
        var rightIsLong = right is long;
        var leftIsDouble = left is double;
        var rightIsDouble = right is double;

        // 处理布尔值
        if (leftIsBool)
            left = BoolToNumber((bool)left, rightIsDouble);
        if (rightIsBool)
            right = BoolToNumber((bool)right, leftIsDouble || left is double);

        // 提升 long 到 double
        if ((left is long && right is double) || (left is double && right is long))
        {
            left = left is long l ? (double)l : left;
            right = right is long r ? (double)r : right;
        }

        return (left, right);
    }

    // 溢出检查的算术运算
    public static long CheckedAdd(long a, long b) => checked(a + b);
    public static long CheckedSubtract(long a, long b) => checked(a - b);
    public static long CheckedMultiply(long a, long b) => checked(a * b);

    // 数值转字符串
    public static string ToString(object value)
    {
        return value switch
        {
            bool b => b.ToString(),
            long l => l.ToString(),
            double d => d.ToString("G"),
            string s => s,
            _ => value?.ToString() ?? ""
        };
    }

    // 格式化 - 仅支持 D/d/E/e/F/f/G/g/X/x
    public static string Format(object value, string formatSpec)
    {
        // 非数值类型使用格式说明符抛出 EvaluateException
        if (value is not long && value is not double)
            throw new EvaluateException($"Format specifier '{formatSpec}' can only be used with numeric types");

        // 检查格式说明符是否支持
        var firstChar = char.ToLowerInvariant(formatSpec[0]);
        var supportedFormats = new[] { 'd', 'e', 'f', 'g', 'x' };
        if (!supportedFormats.Contains(firstChar))
            throw new ParseException($"Unsupported format specifier: {formatSpec}");

        // 格式化
        return string.Format($"{{0:{formatSpec}}}", value);
    }

    // 检查是否为真值
    public static bool IsTruthy(object value)
    {
        return value is bool b && b;
    }

    // 要求为布尔类型
    public static void RequireBool(object value)
    {
        if (value is not bool)
            throw new TypeMismatchException("Expected boolean type", "bool", value?.GetType().Name ?? "null");
    }

    // 二元运算求值
    public static object EvaluateBinary(BinaryExpressionType type, object left, object right)
    {
        // + 运算符：字符串拼接优先（PRD 2.3.3 规则）
        if (type == BinaryExpressionType.Plus)
        {
            if (left is string || right is string)
            {
                return ToString(left) + ToString(right);
            }
        }

        // 提升类型
        var (promotedLeft, promotedRight) = Promote(left, right);

        // 根据运算符类型处理
        return type switch
        {
            BinaryExpressionType.Plus => EvaluatePlus(promotedLeft, promotedRight),
            BinaryExpressionType.Minus => EvaluateMinus(promotedLeft, promotedRight),
            BinaryExpressionType.Multiply => EvaluateMultiply(promotedLeft, promotedRight),
            BinaryExpressionType.Divide => EvaluateDivide(promotedLeft, promotedRight),
            BinaryExpressionType.IntegerDivide => EvaluateIntegerDivide(promotedLeft, promotedRight),
            BinaryExpressionType.Modulo => EvaluateModulo(promotedLeft, promotedRight),
            BinaryExpressionType.Power => EvaluatePower(promotedLeft, promotedRight),
            BinaryExpressionType.BitwiseAnd => EvaluateBitwiseAnd(promotedLeft, promotedRight),
            BinaryExpressionType.BitwiseOr => EvaluateBitwiseOr(promotedLeft, promotedRight),
            BinaryExpressionType.BitwiseXor => EvaluateBitwiseXor(promotedLeft, promotedRight),
            BinaryExpressionType.LeftShift => EvaluateLeftShift(promotedLeft, promotedRight),
            BinaryExpressionType.RightShift => EvaluateRightShift(promotedLeft, promotedRight),
            BinaryExpressionType.Equal => EvaluateEqual(left, right),
            BinaryExpressionType.NotEqual => EvaluateNotEqual(left, right),
            BinaryExpressionType.LessThan => EvaluateLessThan(promotedLeft, promotedRight),
            BinaryExpressionType.LessThanOrEqual => EvaluateLessThanOrEqual(promotedLeft, promotedRight),
            BinaryExpressionType.GreaterThan => EvaluateGreaterThan(promotedLeft, promotedRight),
            BinaryExpressionType.GreaterThanOrEqual => EvaluateGreaterThanOrEqual(promotedLeft, promotedRight),
            _ => throw new InvalidOperationException($"Unknown binary operator: {type}")
        };
    }

    // 一元运算求值
    public static object EvaluateUnary(UnaryExpressionType type, object operand)
    {
        return type switch
        {
            UnaryExpressionType.Positive => EvaluatePositive(operand),
            UnaryExpressionType.Negate => EvaluateNegate(operand),
            UnaryExpressionType.Not => EvaluateNot(operand),
            UnaryExpressionType.BitwiseNot => EvaluateBitwiseNot(operand),
            _ => throw new InvalidOperationException($"Unknown unary operator: {type}")
        };
    }

    // 具体运算实现...
    private static object EvaluatePlus(object left, object right)
    {
        if (left is long l1 && right is long l2)
            return CheckedAdd(l1, l2);
        if (left is double d1 && right is double d2)
            return d1 + d2;
        throw new TypeMismatchException("Addition requires numeric types", "number", GetTypeName(left, right));
    }

    private static object EvaluateEqual(object left, object right)
    {
        // 不同类型比较返回 false
        if (left.GetType() != right.GetType())
            return false;
        return Equals(left, right);
    }

    private static string GetTypeName(object a, object b)
    {
        if (a is bool || b is bool) return "bool";
        if (a is long || b is long) return "long";
        if (a is double || b is double) return "double";
        if (a is string || b is string) return "string";
        return "unknown";
    }
}
```

### 4.7 上下文系统技术方案

**借鉴 NFun 的链式查找模式**，但简化为显式父子关系：

```csharp
public class ExpressionContext
{
    private readonly ExpressionContext? _parent;  // 父上下文（null 表示根上下文）

    // 符号存储：名称 → 符号条目
    private readonly ConcurrentDictionary<string, SymbolEntry> _symbols;

    // 函数存储：名称 → 函数条目
    private readonly ConcurrentDictionary<string, ExpressionFunction> _functions;

    // 符号条目：统一直接值和延迟值
    private class SymbolEntry
    {
        public object? DirectValue { get; }
        public Func<object>? LazyValue { get; }
        public bool IsLazy => LazyValue != null;

        public object GetValue() => IsLazy ? LazyValue!() : DirectValue!;
    }

    // 构造函数 - 根上下文（注册内置常量和函数）
    public ExpressionContext() : this(null)
    {
        // 注册内置常量
        Set("PI", 3.14159265358979);
        Set("E", 2.71828182845905);

        // 注册内置数学函数
        BuiltInFunctions.Register(this);
    }

    // 构造函数 - 子上下文
    private ExpressionContext(ExpressionContext? parent)
    {
        _parent = parent;
        _symbols = new ConcurrentDictionary<string, SymbolEntry>(StringComparer.Ordinal);
        _functions = new ConcurrentDictionary<string, ExpressionFunction>(StringComparer.Ordinal);
    }

    // 保留关键字
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "and", "or", "not", "xor", "NaN", "INF"
    };

    // 注册直接值符号
    public void Set(string name, object value)
    {
        // 检查是否为保留关键字
        if (ReservedKeywords.Contains(name))
            throw new InvalidOperationException($"Cannot set reserved keyword: {name}");

        _symbols[name] = new SymbolEntry { DirectValue = value };
    }

    // 注册延迟值符号
    public void Set(string name, Func<object> value)
    {
        // 检查是否为保留关键字
        if (ReservedKeywords.Contains(name))
            throw new InvalidOperationException($"Cannot set reserved keyword: {name}");

        _symbols[name] = new SymbolEntry { LazyValue = value };
    }

    // 注册弱类型函数
    public void SetFunction(string name, ExpressionFunction func)
    {
        // 检查是否为保留关键字
        if (ReservedKeywords.Contains(name))
            throw new InvalidOperationException($"Cannot register function with reserved keyword: {name}");

        _functions[name] = func;
    }

    // 注册强类型函数 - 借鉴 NFun 的 LambdaWrapperFactory
    public void SetFunction<T1, TResult>(string name, Func<T1, TResult> func)
    {
        SetFunction(name, FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> func)
    {
        SetFunction(name, FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> func)
    {
        SetFunction(name, FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> func)
    {
        SetFunction(name, FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> func)
    {
        SetFunction(name, FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, TResult> func)
    {
        SetFunction(name, FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, T7, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> func)
    {
        SetFunction(name, FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> func)
    {
        SetFunction(name, FunctionWrapper.Wrap(func));
    }

    // 查找符号 - 链式查找（借鉴 NFun ScopeFunctionDictionary）
    public bool TryGetSymbol(string name, out object value)
    {
        if (_symbols.TryGetValue(name, out var entry))
        {
            value = entry.GetValue();
            return true;
        }
        if (_parent != null)
            return _parent.TryGetSymbol(name, out value);
        value = null!;
        return false;
    }

    // 查找函数 - 链式查找
    public bool TryGetFunction(string name, out ExpressionFunction func)
    {
        if (_functions.TryGetValue(name, out func))
            return true;
        if (_parent != null)
            return _parent.TryGetFunction(name, out func);
        func = null!;
        return false;
    }

    // 创建子上下文
    public ExpressionContext CreateChild()
    {
        return new ExpressionContext(this);
    }

    // 删除 - 仅删除当前上下文中的项
    public void Remove(string name)
    {
        _symbols.TryRemove(name, out _);
        _functions.TryRemove(name, out _);
        // 不影响父上下文，静默无错误
    }
}
```

#### 4.7.1 上下文继承规则实现

| 规则 | 实现方式 |
|------|---------|
| 子可见父 | `TryGetSymbol`/`TryGetFunction` 先查自身，再查 `_parent` |
| 子新增不可见父 | 子上下文的 `_symbols`/`_functions` 是独立的 `ConcurrentDictionary` |
| 子不可删除父项 | `Remove` 仅操作自身的字典，父字典不受影响 |
| 父修改影响子 | 子通过 `_parent` 引用实时查找，父新增/修改立即可见 |
| 子覆盖不影响父 | 子字典中的同名项遮蔽父字典项，但不修改父字典 |
| 多层继承 | `CreateChild()` 递归创建，`_parent` 链形成多层 |

### 4.8 Expression 主入口 API 技术方案

**借鉴 NCalc 的简洁 API + NFun 的 Fluent Builder**：

```csharp
// 表达式选项枚举
[Flags]
public enum ExpressionOptions
{
    None = 0,
    NoCache = 1
}

public static class Expression
{
    // 静态快捷方法
    public static object Eval(string expression, ExpressionContext? context = null, ExpressionOptions options = ExpressionOptions.None)
    {
        return Eval<object>(expression, context, options);
    }

    public static T Eval<T>(string expression, ExpressionContext? context = null, ExpressionOptions options = ExpressionOptions.None)
    {
        context ??= new ExpressionContext();
        var calculator = new Calculator(expression, context, options);
        return calculator.Eval<T>();
    }

    // Builder 入口
    public static ExpressionBuilder Builder => new();
}

public class ExpressionBuilder
{
    private readonly ExpressionContext _context = new();
    private ExpressionOptions _options = ExpressionOptions.None;

    public ExpressionBuilder With(string name, object value)
    {
        _context.Set(name, value);
        return this;
    }

    public ExpressionBuilder With(string name, Func<object> value)
    {
        _context.Set(name, value);
        return this;
    }

    public ExpressionBuilder WithFunction(string name, ExpressionFunction func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithFunction<T1, TResult>(string name, Func<T1, TResult> func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithOptions(ExpressionOptions options)
    {
        _options = options;
        return this;
    }

    // ... 更多 Func<> 重载

    public ICalculator Build(string expression)
    {
        return new Calculator(expression, _context, _options);
    }
}
```

#### 4.8.1 ICalculator 接口

```csharp
public interface ICalculator
{
    object Eval();
    T Eval<T>();
    void Set(string name, object value);
    void Set(string name, Func<object> value);
    void Remove(string name);
}
```

#### 4.8.2 Calculator 实现

```csharp
public class Calculator : ICalculator
{
    private readonly string _expressionText;
    private readonly ExpressionContext _context;
    private readonly ExpressionOptions _options;
    private LogicalExpression? _ast;

    public Calculator(string expression, ExpressionContext context, ExpressionOptions options = ExpressionOptions.None)
    {
        _expressionText = expression ?? throw new ArgumentNullException(nameof(expression));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options;
    }

    public object Eval()
    {
        EnsureParsed();
        var visitor = new EvaluationVisitor(_context);
        return _ast!.Accept(visitor);
    }

    public T Eval<T>()
    {
        var result = Eval();
        // 处理类型转换
        if (result is T typedResult)
            return typedResult;
        try
        {
            return (T)Convert.ChangeType(result, typeof(T));
        }
        catch (InvalidCastException)
        {
            throw new TypeMismatchException($"Cannot convert result to type {typeof(T).Name}", typeof(T).Name, result?.GetType().Name ?? "null");
        }
    }

    public void Set(string name, object value) => _context.Set(name, value);
    public void Set(string name, Func<object> value) => _context.Set(name, value);
    public void Remove(string name) => _context.Remove(name);

    private void EnsureParsed()
    {
        if (_ast != null) return;

        // 检查空表达式
        if (string.IsNullOrWhiteSpace(_expressionText))
            throw new ParseException("Expression cannot be empty or whitespace", 1, 1);

        // 查缓存（如果启用）
        if (!_options.HasFlag(ExpressionOptions.NoCache) && ExpressionCache.TryGet(_expressionText, out _ast))
            return;

        // 解析
        var lexer = new Lexer(_expressionText);
        var parser = new Parser(lexer);
        _ast = parser.Parse();

        // 缓存（如果启用）
        if (!_options.HasFlag(ExpressionOptions.NoCache))
            ExpressionCache.Set(_expressionText, _ast);
    }
}
```

### 4.9 强类型函数注册技术方案

**借鉴 NFun 的 `LambdaWrapperFactory` 模式**，将强类型 `Func<>` 包装为内部统一委托：

```csharp
// 内部统一函数委托类型
public delegate object ExpressionFunction(object[] args);

// 包装器：将 Func<T1, TResult> 转换为 ExpressionFunction
public static class FunctionWrapper
{
    public static ExpressionFunction Wrap<T1, TResult>(Func<T1, TResult> func)
    {
        return args =>
        {
            if (args.Length != 1)
                throw new FunctionTypeMismatchException($"Function expects 1 argument, got {args.Length}");
            try
            {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var result = func(arg1);
                return result!;
            }
            catch (InvalidCastException)
            {
                throw new FunctionTypeMismatchException($"Argument type mismatch for function");
            }
        };
    }

    public static ExpressionFunction Wrap<T1, T2, TResult>(Func<T1, T2, TResult> func)
    {
        return args =>
        {
            if (args.Length != 2)
                throw new FunctionTypeMismatchException($"Function expects 2 arguments, got {args.Length}");
            try
            {
                var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));
                var arg2 = (T2)Convert.ChangeType(args[1], typeof(T2));
                var result = func(arg1, arg2);
                return result!;
            }
            catch (InvalidCastException)
            {
                throw new FunctionTypeMismatchException($"Argument type mismatch for function");
            }
        };
    }

    // ... 支持 3~8 个参数
}
```

### 4.10 内置数学函数技术方案

内置函数在 `BuiltInFunctions.Register` 方法中注册：

```csharp
internal static class BuiltInFunctions
{
    public static void Register(ExpressionContext context)
    {
        // 绝对值 - 根据输入类型返回对应类型
        context.SetFunction("abs", (ExpressionFunction)(args =>
        {
            if (args[0] is long l)
                return Math.Abs(l);
            if (args[0] is double d)
                return Math.Abs(d);
            throw new FunctionTypeMismatchException("abs expects a numeric argument");
        }));

        context.SetFunction("sqrt", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value < 0)
                throw new EvaluateException("Square root of negative number is not allowed");
            return Math.Sqrt(value);
        }));

        context.SetFunction("sin", (ExpressionFunction)(args => Math.Sin(Convert.ToDouble(args[0]))));
        context.SetFunction("cos", (ExpressionFunction)(args => Math.Cos(Convert.ToDouble(args[0]))));
        context.SetFunction("tan", (ExpressionFunction)(args => Math.Tan(Convert.ToDouble(args[0]))));

        context.SetFunction("asin", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value < -1 || value > 1)
                throw new EvaluateException("asin expects argument in range [-1, 1]");
            return Math.Asin(value);
        }));

        context.SetFunction("acos", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value < -1 || value > 1)
                throw new EvaluateException("acos expects argument in range [-1, 1]");
            return Math.Acos(value);
        }));

        context.SetFunction("atan", (ExpressionFunction)(args => Math.Atan(Convert.ToDouble(args[0]))));
        context.SetFunction("atan2", (ExpressionFunction)(args => Math.Atan2(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]))));
        context.SetFunction("exp", (ExpressionFunction)(args => Math.Exp(Convert.ToDouble(args[0]))));

        context.SetFunction("ln", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("Logarithm of non-positive number is not allowed");
            return Math.Log(value);
        }));

        context.SetFunction("log", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("Logarithm of non-positive number is not allowed");
            return Math.Log(value);
        }));

        context.SetFunction("log10", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("Logarithm of non-positive number is not allowed");
            return Math.Log10(value);
        }));

        context.SetFunction("log2", (ExpressionFunction)(args =>
        {
            var value = Convert.ToDouble(args[0]);
            if (value <= 0)
                throw new EvaluateException("Logarithm of non-positive number is not allowed");
            return Math.Log2(value);
        }));

        context.SetFunction("ceil", (ExpressionFunction)(args => (long)Math.Ceiling(Convert.ToDouble(args[0]))));
        context.SetFunction("floor", (ExpressionFunction)(args => (long)Math.Floor(Convert.ToDouble(args[0]))));

        // round 支持单参数和双参数
        context.SetFunction("round", (ExpressionFunction)(args =>
        {
            if (args.Length == 1)
            {
                return (long)Math.Round(Convert.ToDouble(args[0]));
            }
            else if (args.Length == 2)
            {
                var value = Convert.ToDouble(args[0]);
                var digits = Convert.ToInt32(args[1]);
                if (digits < 0)
                    throw new EvaluateException("round decimal digits must be non-negative");
                return Math.Round(value, digits);
            }
            throw new FunctionTypeMismatchException("round expects 1 or 2 arguments");
        }));

        context.SetFunction("truncate", (ExpressionFunction)(args => (long)Math.Truncate(Convert.ToDouble(args[0]))));
        context.SetFunction("sign", (ExpressionFunction)(args => (long)Math.Sign(Convert.ToDouble(args[0]))));

        // max - 根据输入类型返回对应类型
        context.SetFunction("max", (ExpressionFunction)(args =>
        {
            if (args[0] is long l1 && args[1] is long l2)
                return Math.Max(l1, l2);
            var d1 = Convert.ToDouble(args[0]);
            var d2 = Convert.ToDouble(args[1]);
            if (double.IsNaN(d1) || double.IsNaN(d2))
                throw new EvaluateException("max does not accept NaN arguments");
            return Math.Max(d1, d2);
        }));

        // min - 根据输入类型返回对应类型
        context.SetFunction("min", (ExpressionFunction)(args =>
        {
            if (args[0] is long l1 && args[1] is long l2)
                return Math.Min(l1, l2);
            var d1 = Convert.ToDouble(args[0]);
            var d2 = Convert.ToDouble(args[1]);
            if (double.IsNaN(d1) || double.IsNaN(d2))
                throw new EvaluateException("min does not accept NaN arguments");
            return Math.Min(d1, d2);
        }));

        context.SetFunction("pow", (ExpressionFunction)(args =>
        {
            var x = Convert.ToDouble(args[0]);
            var y = Convert.ToDouble(args[1]);
            if (x < 0 && y != Math.Floor(y))
                throw new EvaluateException("Cannot raise negative number to non-integer power");
            return Math.Pow(x, y);
        }));
    }
}
```

### 4.11 异常系统技术方案

```csharp
// 基类
public class MathEvalException : Exception
{
    public MathEvalException(string message) : base(message) { }
    public MathEvalException(string message, Exception innerException) : base(message, innerException) { }
}

// 解析异常
public class ParseException : MathEvalException
{
    public int Line { get; }
    public int Column { get; }
    public ParseException(string message, int line, int column) : base(message)
    {
        Line = line;
        Column = column;
    }
}

// 求值异常基类
public class EvaluateException : MathEvalException
{
    public EvaluateException(string message) : base(message) { }
    public EvaluateException(string message, Exception innerException) : base(message, innerException) { }
}

// 类型不匹配
public class TypeMismatchException : MathEvalException
{
    public string ExpectedType { get; }
    public string ActualType { get; }
    public TypeMismatchException(string message, string expectedType, string actualType)
        : base(message)
    {
        ExpectedType = expectedType;
        ActualType = actualType;
    }
}

// 以下均继承 EvaluateException
public class FunctionNotFoundException : EvaluateException
{
    public string FunctionName { get; }
    public FunctionNotFoundException(string functionName) : base($"Function '{functionName}' not found")
    {
        FunctionName = functionName;
    }
}

public class FunctionTypeMismatchException : EvaluateException
{
    public FunctionTypeMismatchException(string message) : base(message) { }
}

public class SymbolNotFoundException : EvaluateException
{
    public string SymbolName { get; }
    public SymbolNotFoundException(string symbolName) : base($"Symbol '{symbolName}' not found")
    {
        SymbolName = symbolName;
    }
}

public class DivisionByZeroException : EvaluateException
{
    public DivisionByZeroException() : base("Division by zero") { }
}

public class OverflowException : EvaluateException
{
    public OverflowException(string message) : base(message) { }
    public OverflowException(string message, Exception innerException) : base(message, innerException) { }
}
```

### 4.12 表达式缓存技术方案

```csharp
internal static class ExpressionCache
{
    private static readonly ConcurrentDictionary<string, LogicalExpression> _cache = new();

    public static bool TryGet(string expression, [MaybeNullWhen(false)] out LogicalExpression ast)
    {
        return _cache.TryGetValue(expression, out ast);
    }

    public static void Set(string expression, LogicalExpression ast)
    {
        _cache.TryAdd(expression, ast);
    }

    public static void Clear()
    {
        _cache.Clear();
    }
}
```

---

## 5. 关键技术决策总结

| 决策 | 选择 | 不选 | 原因 |
|------|------|------|------|
| 解析器 | 手写递归下降 | ANTLR/Pratt | 无外部依赖，优先级直观 |
| 类型推断 | 求值时动态推断 | NFun Tic 约束图 | 3 种类型无需全局推断 |
| 插值字符串 | 原生 AST 节点 | NFun concat 函数链 | 不污染命名空间，更高效 |
| 上下文继承 | 父引用链式查找 | NFun 作用域栈 | 更简单，满足 PRD 需求 |
| 函数注册 | 包装为统一委托 | NFun 的 IConcreteFunction 体系 | MathEval 无重载，无需复杂体系 |
| 线程安全 | ConcurrentDictionary | lock + Dictionary | 细粒度锁，性能更好 |
| 溢出检查 | checked 算术上下文 | unchecked | PRD 要求 long 溢出抛异常 |
| 缓存 | 静态 ConcurrentDictionary | 无缓存/LRU | NCalc 验证过的成熟方案 |
| 三元类型推断 | 短路策略 | 类型统一 | PRD 明确要求短路类型推断 |

---

## 6. 编译管线全景

```
表达式字符串
    │
    ▼
┌─────────┐     ┌──────────────┐     ┌──────────────┐
│  Lexer  │────▶│    Parser    │────▶│  AST Cache   │
│ 逐字符扫描│     │  递归下降    │     │ ConcurrentDict│
│ → Tokens │     │  → AST      │     │ string→AST   │
│ 长度限制 │     │ 深度限制    │     │ NoCache选项  │
└─────────┘     └──────┬───────┘     └──────┬───────┘
                       │                     │
                       ▼                     ▼
              ┌─────────────────────────────────┐
              │          AST 节点层次             │
              │  LogicalExpression (基类)         │
              │  ├── ValueExpression              │
              │  ├── Identifier                   │
              │  ├── BinaryExpression             │
              │  ├── UnaryExpression              │
              │  ├── FunctionCall                 │
              │  ├── InterpolatedString           │
              │  └── ConditionalExpression        │
              └───────────────┬─────────────────┘
                              │
                              ▼ Accept(visitor)
              ┌─────────────────────────────────┐
              │       EvaluationVisitor          │
              │  ┌─────────────────────────────┐│
              │  │ TypeHelper (类型推断/转换)    ││
              │  │ checked 算术 (溢出检查)      ││
              │  │ 短路求值 (and/or/?:)         ││
              │  └─────────────────────────────┘│
              └───────────────┬─────────────────┘
                              │
                              ▼
              ┌─────────────────────────────────┐
              │       ExpressionContext          │
              │  ConcurrentDictionary (符号)     │
              │  ConcurrentDictionary (函数)     │
              │  _parent 引用 (链式继承)         │
              │  内置常量: PI, E                 │
              │  内置函数: abs, sqrt, sin...     │
              └─────────────────────────────────┘
```
