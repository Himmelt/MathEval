# NCalc 技术架构研究

> **研究对象**：[NCalc](https://github.com/ncalc/ncalc)
> **研究日期**：2026-05-18
> **研究目的**：为 MathEval 技术方案提供参考

---

## 目录

1. [整体架构概览](#1-整体架构概览)
2. [词法分析器 (Lexer)](#2-词法分析器-lexer)
3. [语法分析器 (Parser)](#3-语法分析器-parser)
4. [AST 节点类型设计](#4-ast-节点类型设计)
5. [Visitor 模式实现](#5-visitor-模式实现)
6. [表达式缓存机制](#6-表达式缓存机制)
7. [Expression 主入口 API 设计](#7-expression-主入口-api-设计)
8. [上下文系统设计](#8-上下文系统设计)
9. [NCalc 与 MathEval PRD 的关键差异对照](#9-ncalc-与-matheval-prd-的关键差异对照)
10. [架构全景图](#10-架构全景图)

---

## 1. 整体架构概览

NCalc 是一个 .NET 表达式计算库，核心编译管线为：

```
表达式字符串 → Lexer(词法分析) → Parser(语法分析) → AST → EvaluationVisitor(求值) → 结果
```

核心模块划分：

| 模块 | 职责 |
|------|------|
| Lexer | 将表达式字符串转换为 Token 流 |
| Parser | 将 Token 流转换为 AST |
| AST | 抽象语法树节点体系 |
| Visitor | 遍历 AST 执行操作（求值、序列化等） |
| Context | 符号和函数的存储 |
| Expression | 主入口 API |

---

## 2. 词法分析器 (Lexer)

### 2.1 整体设计

NCalc 的词法分析器采用**手写式逐字符扫描器**（Hand-written Scanner），而非生成器工具（如 ANTLR、Lex）。核心类为 `Lexer`。

### 2.2 核心实现机制

```
表达式字符串 → Lexer → Token 流
```

**关键设计要点**：

- **逐字符扫描**：`Lexer` 维护一个指向表达式字符串的游标（`_text` + `_position`），通过 `ReadChar()` 逐字符前进，`PeekChar()` 向前看一个字符
- **Token 类型枚举**：定义 `TokenType` 枚举，包含：
  - `Integer`、`Float`（数值）
  - `String`（字符串）
  - `Identifier`（标识符）
  - `Plus`、`Minus`、`Asterisk`、`Slash` 等运算符
  - `LeftParenthesis`、`RightParenthesis`、`Comma` 等分隔符
  - `EOF`（结束标记）
- **关键字识别**：标识符扫描完成后，通过查表法判断是否为关键字（`true`、`false`、`and`、`or`、`not` 等），若是则返回对应的关键字 Token 类型
- **数值字面量解析**：支持十进制整数和浮点数，通过判断是否包含小数点或指数部分来区分 `Integer` 和 `Float` Token
- **字符串字面量解析**：支持单引号和双引号两种定界符，内部处理转义字符
- **空白跳过**：`SkipWhiteSpace()` 方法跳过空格、制表符、换行符等

### 2.3 Token 数据结构

```csharp
public class Token
{
    public TokenType Type { get; }
    public string Text { get; }      // 原始文本
    public int Position { get; }     // 在表达式中的位置（用于错误报告）
}
```

### 2.4 扫描流程伪代码

```
while (position < text.Length):
    skip whitespace
    ch = current char
    if ch is digit:        → scanNumber()
    if ch is letter/_:     → scanIdentifier() → 可能转为关键字
    if ch is quote:        → scanString()
    if ch is operator:     → scanOperator() (可能需要向前看一个字符，如 <=, >=, !=, ==)
    if ch is delimiter:    → 返回对应 Token
```

### 2.5 设计特点与局限

| 特点 | 说明 |
|------|------|
| 无外部依赖 | 手写 Lexer 不依赖生成器，项目轻量 |
| 错误位置精确 | Token 携带位置信息，便于报错 |
| 不支持多进制字面量 | 原版 NCalc 不支持 `0x`、`0b`、`0o` 前缀 |
| 不支持字符串插值 | 原版无 `$"..."` 语法 |
| 不支持 `//` 整除运算符 | `/` 后紧跟 `/` 会被识别为两个除法运算符 |

---

## 3. 语法分析器 (Parser)

### 3.1 整体设计

NCalc 采用**递归下降解析器**（Recursive Descent Parser）。这是最经典的手写解析器模式，每个非终结符对应一个方法。

### 3.2 核心实现机制

```
Token 流 → Parser → AST (LogicalExpression 根节点)
```

**关键设计要点**：

- **运算符优先级通过方法调用层级实现**：优先级越低的运算符，对应的方法越靠近顶层（调用链的入口），优先级越高的运算符越靠近底层（调用链的末端）
- **左结合性通过循环实现**：`while (当前 Token 匹配运算符) { 消费 Token; 解析右侧; 构建节点 }`
- **右结合性通过递归实现**：`解析左侧; if (匹配) { 消费 Token; 递归调用自身解析右侧; 构建节点 }`

### 3.3 方法调用层级（优先级从低到高）

```
ParseExpression()                    // 入口
  └─ ParseConditional()              // 三元运算符 ?:
       └─ ParseLogicalOr()           // or / ||
            └─ ParseLogicalAnd()     // and / &&
                 └─ ParseEquality()  // == / !=
                      └─ ParseRelational()  // > / < / >= / <=
                           └─ ParseBitwiseOr()  // |
                                └─ ParseBitwiseXor()  // ^
                                     └─ ParseBitwiseAnd()  // &
                                          └─ ParseShift()  // << / >>
                                               └─ ParseAdditive()  // + / -
                                                    └─ ParseMultiplicative()  // * / / %
                                                         └─ ParsePower()  // ^ (乘方，右结合)
                                                              └─ ParseUnary()  // 一元 + / - / not / !
                                                                   └─ ParsePrimary()  // 字面量 / 标识符 / 函数调用 / 括号
```

### 3.4 关键方法实现示例

**二元运算（左结合）**：

```csharp
private LogicalExpression ParseAdditive()
{
    var left = ParseMultiplicative();
    while (_lexer.Token.Type == TokenType.Plus ||
           _lexer.Token.Type == TokenType.Minus)
    {
        var op = _lexer.Token.Type;
        _lexer.ReadNextToken();
        var right = ParseMultiplicative();
        left = new BinaryExpression(op, left, right);
    }
    return left;
}
```

**乘方运算（右结合）**：

```csharp
private LogicalExpression ParsePower()
{
    var left = ParseUnary();
    if (_lexer.Token.Type == TokenType.Caret)
    {
        _lexer.ReadNextToken();
        var right = ParsePower();  // 递归调用自身，实现右结合
        return new BinaryExpression(BinaryExpressionType.Power, left, right);
    }
    return left;
}
```

**三元运算符（右结合）**：

```csharp
private LogicalExpression ParseConditional()
{
    var condition = ParseLogicalOr();
    if (_lexer.Token.Type == TokenType.QuestionMark)
    {
        _lexer.ReadNextToken();
        var trueExpr = ParseExpression();  // 注意：用 ParseExpression 而非 ParseConditional
        Expect(TokenType.Colon);
        var falseExpr = ParseExpression();  // 右结合的关键
        return new ConditionalExpression(condition, trueExpr, falseExpr);
    }
    return condition;
}
```

### 3.5 函数调用解析

函数调用在 `ParsePrimary()` 中处理：

```csharp
if (token.Type == TokenType.Identifier && PeekToken().Type == TokenType.LeftParenthesis)
{
    var functionName = token.Text;
    ReadNextToken(); // 消费 '('
    var args = new List<LogicalExpression>();
    while (current != RightParenthesis)
    {
        args.Add(ParseExpression());
        if (current == Comma) ReadNextToken();
    }
    ReadNextToken(); // 消费 ')'
    return new FunctionCall(functionName, args);
}
```

### 3.6 设计特点与局限

| 特点 | 说明 |
|------|------|
| 纯手写，无依赖 | 不依赖 ANTLR 等工具，代码可读性好 |
| 优先级清晰 | 方法层级直观反映优先级 |
| 错误恢复有限 | 遇到语法错误直接抛 `ParseException`，不做错误恢复 |
| `^` 语义冲突 | NCalc 中 `^` 表示按位异或（非乘方），乘方需用 `Pow()` 函数 |

---

## 4. AST 节点类型设计

### 4.1 类型层次结构

NCalc 的 AST 节点以 `LogicalExpression` 为基类，构成如下层次：

```
LogicalExpression (抽象基类)
├── ValueExpression         — 常量值
├── IdentifierExpression    — 变量/参数引用
├── BinaryExpression        — 二元运算
├── UnaryExpression         — 一元运算
├── FunctionCall            — 函数调用
└── ConditionalExpression   — 三元条件表达式
```

### 4.2 各节点类型详解

**`LogicalExpression`（基类）**：

```csharp
public abstract class LogicalExpression
{
    // 所有节点共享的基类，通常为空或仅含通用属性
}
```

**`ValueExpression`（常量值节点）**：

```csharp
public class ValueExpression : LogicalExpression
{
    public object Value { get; }        // 实际值：bool / long / double / string / null
    public ValueType Type { get; }      // 值类型标识
}
```

- 承载所有字面量：`true`、`false`、`42`、`3.14`、`"hello"`
- `ValueType` 枚举区分：`Boolean`、`Integer`、`Float`、`String`、`DateTime`

**`IdentifierExpression`（标识符节点）**：

```csharp
public class IdentifierExpression : LogicalExpression
{
    public string Name { get; }         // 标识符名称
}
```

- 用于引用变量/参数，如 `x`、`PI`、`name`
- 求值时从上下文中查找对应值

**`BinaryExpression`（二元运算节点）**：

```csharp
public class BinaryExpression : LogicalExpression
{
    public BinaryExpressionType Type { get; }   // 运算类型
    public LogicalExpression LeftExpression { get; }
    public LogicalExpression RightExpression { get; }
}
```

- `BinaryExpressionType` 枚举包含：`Plus`、`Minus`、`Times`、`Div`、`Modulo`、`Exponent`（乘方）、`And`、`Or`、`BitwiseOr`、`BitwiseAnd`、`BitwiseXor`、`LeftShift`、`RightShift`、`Equal`、`NotEqual`、`Greater`、`GreaterOrEqual`、`Less`、`LessOrEqual`

**`UnaryExpression`（一元运算节点）**：

```csharp
public class UnaryExpression : LogicalExpression
{
    public UnaryExpressionType Type { get; }    // 运算类型
    public LogicalExpression Expression { get; }
}
```

- `UnaryExpressionType` 枚举包含：`Not`（逻辑非）、`Negate`（取负）、`BitwiseNot`（按位取反）、`Positive`（正号）

**`FunctionCall`（函数调用节点）**：

```csharp
public class FunctionCall : LogicalExpression
{
    public string Identifier { get; }           // 函数名
    public List<LogicalExpression> Parameters { get; }  // 参数列表
}
```

**`ConditionalExpression`（三元条件节点）**：

```csharp
public class ConditionalExpression : LogicalExpression
{
    public LogicalExpression Condition { get; }
    public LogicalExpression TrueExpression { get; }
    public LogicalExpression FalseExpression { get; }
}
```

### 4.3 设计特点

| 特点 | 说明 |
|------|------|
| 节点类型精简 | 仅 6 种节点类型，覆盖所有表达式语法 |
| 统一基类 | 所有节点继承 `LogicalExpression`，便于 Visitor 模式 |
| 运算类型外置 | 二元/一元运算的具体类型通过枚举区分，而非子类化 |
| 无插值字符串节点 | 原版 NCalc 不支持字符串插值 |
| 无类型注解 | AST 节点不携带类型信息，类型在求值时动态推断 |

---

## 5. Visitor 模式实现

### 5.1 核心接口设计

NCalc 采用经典的 Visitor 模式，分为两个层次：

**`IExpressionVisitor`（无返回值 Visitor）**：

```csharp
public interface IExpressionVisitor
{
    void Visit(LogicalExpression expression);     // 分发入口
    void Visit(ValueExpression expression);
    void Visit(IdentifierExpression expression);
    void Visit(BinaryExpression expression);
    void Visit(UnaryExpression expression);
    void Visit(FunctionCall expression);
    void Visit(ConditionalExpression expression);
}
```

**泛型 Visitor 接口**（部分版本）：

```csharp
public interface IExpressionVisitor<out T>
{
    T Visit(ValueExpression expression);
    T Visit(IdentifierExpression expression);
    T Visit(BinaryExpression expression);
    T Visit(UnaryExpression expression);
    T Visit(FunctionCall expression);
    T Visit(ConditionalExpression expression);
}
```

### 5.2 双分派机制 (Double Dispatch)

NCalc 使用**双分派**实现 Visitor 模式：

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

### 5.3 内置 Visitor 实现

**`EvaluationVisitor`（求值 Visitor）**——最核心的 Visitor：

```csharp
public class EvaluationVisitor : IExpressionVisitor
{
    private readonly ExpressionContext _context;
    public object Result { get; private set; }

    public void Visit(BinaryExpression expression)
    {
        // 先求值左右子树
        expression.LeftExpression.Accept(this);
        var left = Result;
        expression.RightExpression.Accept(this);
        var right = Result;

        // 短路求值处理
        if (expression.Type == BinaryExpressionType.And && IsFalse(left))
        {
            Result = false; return;
        }
        if (expression.Type == BinaryExpressionType.Or && IsTrue(left))
        {
            Result = true; return;
        }

        // 根据运算类型执行计算
        Result = EvaluateBinary(expression.Type, left, right);
    }

    public void Visit(IdentifierExpression expression)
    {
        // 从上下文中查找符号值
        if (_context.Parameters.TryGetValue(expression.Name, out var value))
        {
            Result = value is Func<object> lazy ? lazy() : value;
        }
        else
        {
            throw new IdentifierNotFoundException(expression.Name);
        }
    }

    public void Visit(FunctionCall expression)
    {
        // 先求值所有参数
        var args = new List<object>();
        foreach (var param in expression.Parameters)
        {
            param.Accept(this);
            args.Add(Result);
        }
        // 从上下文中查找函数并调用
        if (_context.Functions.TryGetValue(expression.Identifier, out var func))
        {
            Result = func(args.ToArray());
        }
        else
        {
            throw new FunctionNotFoundException(expression.Identifier);
        }
    }
}
```

### 5.4 Visitor 模式的扩展性

| 扩展场景 | 实现方式 |
|----------|----------|
| 表达式求值 | `EvaluationVisitor` |
| 表达式序列化 | 实现 `IExpressionVisitor`，输出字符串 |
| AST 优化/变换 | 实现 `IExpressionVisitor`，返回变换后的 AST |
| 类型检查 | 实现 `IExpressionVisitor`，推断表达式类型 |
| 代码生成 | 实现 `IExpressionVisitor`，生成 IL 或 Expression Tree |

### 5.5 设计特点

| 特点 | 说明 |
|------|------|
| 求值与 AST 解耦 | 求值逻辑完全在 Visitor 中，AST 节点不含求值逻辑 |
| 可扩展 | 新增操作只需新增 Visitor，无需修改 AST 节点 |
| 短路求值自然 | 在 `EvaluationVisitor` 的 `Visit(BinaryExpression)` 中可自然实现短路 |
| 无默认实现 | 接口无默认实现，每个 Visitor 必须实现所有 Visit 方法 |

---

## 6. 表达式缓存机制

### 6.1 缓存架构

```
表达式字符串 → 查缓存 → 命中 → 返回 AST
                       → 未命中 → Lexer → Parser → AST → 存入缓存 → 返回 AST
```

### 6.2 缓存实现

NCalc 使用**静态 `ConcurrentDictionary`** 作为缓存容器：

```csharp
private static readonly ConcurrentDictionary<string, LogicalExpression> _cache = new();

private LogicalExpression Parse(string expressionString, ExpressionOptions options)
{
    if (!options.HasFlag(ExpressionOptions.NoCache))
    {
        if (_cache.TryGetValue(expressionString, out var cached))
            return cached;
    }

    var lexer = new Lexer(expressionString);
    var parser = new Parser(lexer);
    var ast = parser.Parse();

    if (!options.HasFlag(ExpressionOptions.NoCache))
    {
        _cache.TryAdd(expressionString, ast);
    }

    return ast;
}
```

### 6.3 缓存键设计

- **缓存键**：表达式字符串本身（`string`）
- **缓存值**：`LogicalExpression`（AST 根节点）
- **隐含约束**：相同字符串总是产生相同的 AST（因为 NCalc 的文法无歧义）

### 6.4 缓存与求值的关系

```
Expression 对象
├── AST（来自缓存或新解析）—— 不可变，可安全共享
└── Context（每次求值独立）—— 可变，不缓存
```

- **AST 是不可变的**：缓存后多个 `Expression` 实例可共享同一 AST
- **Context 是可变的**：每次 `Eval()` 时传入的上下文不同，不影响缓存
- **线程安全**：`ConcurrentDictionary` 保证并发读写安全

### 6.5 ExpressionOptions 控制

```csharp
[Flags]
public enum ExpressionOptions
{
    None = 0,
    NoCache = 1,              // 禁用缓存
    IgnoreCase = 2,           // 标识符不区分大小写
    AllowNullParameter = 4,   // 允许 null 参数
}
```

### 6.6 设计特点与局限

| 特点 | 说明 |
|------|------|
| 线程安全 | `ConcurrentDictionary` 保证并发安全 |
| 全局缓存 | 静态字典，所有 `Expression` 实例共享 |
| 无缓存失效 | 缓存一旦写入不会主动移除（除进程退出） |
| 无缓存大小限制 | 长期运行可能导致内存增长 |
| 字符串作为键 | 简单但可能占用较多内存（大量不同表达式时） |

---

## 7. Expression 主入口 API 设计

### 7.1 核心类设计

`Expression` 类是 NCalc 的主入口，采用**构建-求值分离**模式：

```csharp
public class Expression
{
    private readonly string _expressionText;
    private readonly ExpressionOptions _options;
    private LogicalExpression _logicalExpression;  // AST（可能来自缓存）
    private ExpressionContext _context;             // 上下文

    // 构造函数
    public Expression(string expressionText, ExpressionOptions options = ExpressionOptions.None);
    public Expression(string expressionText, ExpressionContext context, ExpressionOptions options = ExpressionOptions.None);

    // 求值方法
    public object Evaluate();

    // 参数/函数注册（便捷方法，代理到 Context）
    public ExpressionParameters Parameters { get; }   // 符号字典
    public ExpressionFunctions Functions { get; }      // 函数字典

    // 上下文
    public ExpressionContext Context { get; set; }
}
```

### 7.2 典型使用模式

**模式一：简单求值**

```csharp
var result = new Expression("2 + 3 * 4").Evaluate();  // 14
```

**模式二：带参数求值**

```csharp
var expr = new Expression("x * 2 + 1");
expr.Parameters["x"] = 10;
var result = expr.Evaluate();  // 21
```

**模式三：带函数求值**

```csharp
var expr = new Expression("sqrt(16) + max(3, 5)");
expr.Functions["sqrt"] = args => Math.Sqrt(Convert.ToDouble(args[0]));
expr.Functions["max"] = args => Math.Max(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]));
var result = expr.Evaluate();  // 7
```

**模式四：复用 AST**

```csharp
var expr = new Expression("x * y");
expr.Parameters["x"] = 3;
expr.Parameters["y"] = 4;
var r1 = expr.Evaluate();  // 12

expr.Parameters["x"] = 5;
expr.Parameters["y"] = 6;
var r2 = expr.Evaluate();  // 30
// AST 只解析一次，多次求值复用
```

### 7.3 Evaluate() 内部流程

```
Evaluate()
  ├── 1. 确保 AST 已解析（_logicalExpression != null?）
  │      ├── 已解析 → 跳过
  │      └── 未解析 → Parse(_expressionText) → _logicalExpression
  │                    └── 查缓存 / Lexer + Parser
  ├── 2. 创建 EvaluationVisitor（传入 Context）
  ├── 3. _logicalExpression.Accept(visitor)
  └── 4. 返回 visitor.Result
```

### 7.4 设计特点与局限

| 特点 | 说明 |
|------|------|
| API 简洁 | 构造 + 求值两步完成 |
| 无泛型返回 | `Evaluate()` 返回 `object`，无 `Evaluate<T>()` |
| 无 Builder 模式 | 无 Fluent API，参数通过字典设置 |
| 无静态快捷方法 | 无 `Expression.Eval("...")` 静态方法 |
| 参数字典式 | `Parameters` 是字典，非 `Set("name", value)` 方法 |
| 上下文可选 | 可不传 Context，使用默认上下文 |

---

## 8. 上下文系统设计

### 8.1 核心类设计

NCalc 的上下文系统由 `ExpressionContext` 类承载：

```csharp
public class ExpressionContext
{
    // 符号（变量/常量）存储
    public Dictionary<string, object> Parameters { get; }

    // 函数存储
    public Dictionary<string, ExpressionFunction> Functions { get; }

    // 选项
    public ExpressionOptions Options { get; set; }
}
```

其中 `ExpressionFunction` 是函数委托类型：

```csharp
public delegate object ExpressionFunction(object[] args);
```

### 8.2 符号存储机制

```csharp
// 符号存储在 Parameters 字典中
context.Parameters["PI"] = 3.14159;           // 直接值
context.Parameters["x"] = 42;                  // 直接值
context.Parameters["now"] = new Func<object>(() => DateTime.Now);  // 延迟值
```

**延迟值的处理**：在 `EvaluationVisitor.Visit(IdentifierExpression)` 中：

```csharp
public void Visit(IdentifierExpression expression)
{
    if (_context.Parameters.TryGetValue(expression.Name, out var value))
    {
        // 如果值是 Func<object>，则调用委托获取实际值
        Result = value is Func<object> func ? func() : value;
    }
}
```

### 8.3 函数存储机制

```csharp
// 函数存储在 Functions 字典中
context.Functions["sqrt"] = args => Math.Sqrt(Convert.ToDouble(args[0]));
context.Functions["max"] = args => Math.Max(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]));
```

- 所有函数统一为 `Func<object[], object>` 签名
- 参数类型转换由函数实现者手动处理
- 无强类型函数注册（无 `Func<T1, TResult>` 支持）

### 8.4 内置函数与常量

**内置函数**（在求值时特殊处理）：

| 函数 | 说明 |
|------|------|
| `Abs` | 绝对值 |
| `Acos` | 反余弦 |
| `Asin` | 反正弦 |
| `Atan` | 反正切 |
| `Ceiling` | 向上取整 |
| `Cos` | 余弦 |
| `Exp` | e 的幂 |
| `Floor` | 向下取整 |
| `IEEERemainder` | IEEE 取余 |
| `Log` | 自然对数 |
| `Log10` | 以 10 为底对数 |
| `Max` | 最大值 |
| `Min` | 最小值 |
| `Pow` | 幂运算 |
| `Round` | 四舍五入 |
| `Sign` | 符号 |
| `Sin` | 正弦 |
| `Sqrt` | 平方根 |
| `Tan` | 正切 |
| `Truncate` | 截断 |

**内置常量**：

| 常量 | 值 |
|------|-----|
| `PI` | `Math.PI` |
| `E` | `Math.E` |

### 8.5 上下文继承

**原版 NCalc 不支持上下文继承**。这是 NCalc 的一个重要局限。每次创建 `Expression` 实例时，需要手动传入完整的上下文。

### 8.6 Expression 与 Context 的关系

```
Expression
├── _expressionText: string     // 表达式文本
├── _logicalExpression: AST     // 解析结果（可能来自缓存）
├── Parameters: Dictionary      // 符号（代理到 Context）
├── Functions: Dictionary       // 函数（代理到 Context）
└── Context: ExpressionContext   // 上下文对象
```

`Expression` 类的 `Parameters` 和 `Functions` 属性直接代理到内部 `Context` 对象，提供便捷访问。

### 8.7 设计特点与局限

| 特点 | 说明 |
|------|------|
| 字典式存储 | 符号和函数均使用 `Dictionary`，简单直接 |
| 无上下文继承 | 不支持父子上下文关系 |
| 弱类型函数 | 函数参数为 `object[]`，无类型安全 |
| 无延迟值一等支持 | 延迟值通过 `Func<object>` 约定，非显式 API |
| 线程安全不足 | 使用 `Dictionary` 而非 `ConcurrentDictionary` |
| 符号与函数命名空间分离 | 同名可同时存在为符号和函数 |

---

## 9. NCalc 与 MathEval PRD 的关键差异对照

| 维度 | NCalc 现状 | MathEval PRD 要求 | 差异分析 |
|------|-----------|------------------|----------|
| Lexer | 手写扫描器，不支持多进制字面量 | 需支持 `0x`/`0o`/`0b` 前缀 | 需扩展 Lexer |
| Lexer | 不支持字符串插值 | 需支持 `$"..."` 语法 | 需大幅扩展 Lexer |
| Parser | 递归下降，`^` 为按位异或 | `^` 为乘方，`xor` 为异或 | 需调整优先级和语义 |
| AST | 6 种节点，无插值字符串 | 需增加 `InterpolatedString` 节点 | 需新增节点类型 |
| Visitor | 双分派，`EvaluationVisitor` 求值 | 同样采用 Visitor 模式 | 架构一致 |
| 缓存 | `ConcurrentDictionary`，全局静态 | 同样采用 `ConcurrentDictionary` | 架构一致 |
| API | `new Expression().Evaluate()`，字典式参数 | `Expression.Eval()` 静态方法 + Builder 模式 + `ICalculator` 接口 | 需重新设计 API 层 |
| 上下文 | `Dictionary` 存储，无继承，弱类型函数 | `ConcurrentDictionary`，支持继承，强类型函数注册 | 需重新设计上下文系统 |
| 类型系统 | `object` 统一表示，无 `long`/`double` 区分 | `long + double` 双类型策略 | 需重新设计类型推断 |
| 线程安全 | `Dictionary` 非线程安全 | 要求线程安全 | 需使用并发容器 |

**核心结论**：NCalc 的整体架构（Lexer → Parser → AST → Visitor → Context）是成熟且可借鉴的，MathEval 可以在此基础上进行关键增强。

---

## 10. 架构全景图

```
┌─────────────────────────────────────────────────────────┐
│                    Expression (主入口)                    │
│  ┌──────────┐  ┌──────────────────┐  ┌───────────────┐ │
│  │ 构造参数  │  │ Evaluate() 求值  │  │ 缓存控制      │ │
│  │ string   │  │                  │  │ ExpressionOpt. │ │
│  │ options  │  │                  │  │ NoCache       │ │
│  └──────────┘  └────────┬─────────┘  └───────────────┘ │
└─────────────────────────┼───────────────────────────────┘
                          │
              ┌───────────┼───────────┐
              ▼           ▼           ▼
        ┌──────────┐ ┌────────┐ ┌──────────────┐
        │  Lexer   │ │ Parser │ │ AST Cache    │
        │ 逐字符扫描│ │递归下降│ │ ConcurrentDict│
        │ → Tokens │ │ → AST  │ │ string→AST   │
        └──────────┘ └───┬────┘ └──────────────┘
                          │
                          ▼
        ┌─────────────────────────────────┐
        │           AST 节点层次           │
        │  LogicalExpression (基类)        │
        │  ├── ValueExpression             │
        │  ├── IdentifierExpression        │
        │  ├── BinaryExpression            │
        │  ├── UnaryExpression             │
        │  ├── FunctionCall                │
        │  └── ConditionalExpression       │
        └───────────────┬─────────────────┘
                        │
                        ▼ Accept(visitor)
        ┌─────────────────────────────────┐
        │        Visitor 模式              │
        │  IExpressionVisitor              │
        │  ├── EvaluationVisitor (求值)    │
        │  └── (可扩展其他 Visitor)         │
        └───────────────┬─────────────────┘
                        │
                        ▼
        ┌─────────────────────────────────┐
        │        ExpressionContext         │
        │  Parameters: Dict<string,object>│
        │  Functions: Dict<string,Func>   │
        │  Options: ExpressionOptions      │
        │  (无上下文继承)                   │
        └─────────────────────────────────┘
```
