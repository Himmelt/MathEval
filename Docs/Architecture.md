# MathEval 项目架构规划

> **版本**：v1.0
> **最后更新**：2026-05-18
> **状态**：已审核

---

## 目录

1. [项目概述](#1-项目概述)
2. [技术栈](#2-技术栈)
3. [项目结构](#3-项目结构)
4. [核心模块设计](#4-核心模块设计)
5. [接口设计](#5-接口设计)
6. [实现计划](#6-实现计划)
7. [测试策略](#7-测试策略)

---

## 1. 项目概述

MathEval 是一个面向 .NET 10+ 的轻量级表达式计算引擎，融合了 NCalc 的简洁 API 和 NFun 的强大特性，支持布尔、数值、字符串三种类型，完整的运算符体系，类似 C# 的字符串插值，上下文继承，强类型函数注册，以及多线程安全。

### 1.1 核心特性

- **类型系统**：bool/number/string 三种类型，number 内部采用 long + double 双策略
- **运算符**：算术、关系、逻辑、位运算、三元条件，共 14 级优先级
- **字符串插值**：支持 `$"Hello {name}"` 语法，支持格式说明符
- **上下文系统**：支持符号注册（直接值/延迟值）、函数注册、上下文继承
- **函数注册**：弱类型委托和强类型 Func<> 两种方式
- **AST + Visitor**：可扩展的架构设计
- **表达式缓存**：避免重复解析
- **多线程安全**：使用 ConcurrentDictionary

---

## 2. 技术栈

| 技术 | 版本 | 用途 |
|------|------|------|
| .NET | 10+ | 运行时 |
| C# | 13+ | 开发语言 |
| xUnit | 最新 | 单元测试框架 |
| GitHub Actions | - | CI/CD |

---

## 3. 命名空间结构

```
MathEval
├── MathEval.Exceptions          # 异常类
├── MathEval.Lexer              # 词法分析
├── MathEval.Parser             # 语法分析
├── MathEval.AST                # 抽象语法树
├── MathEval.Visitors           # Visitor 模式
├── MathEval.Context            # 上下文系统
├── MathEval.Functions          # 内置函数
├── MathEval.TypeSystem         # 类型系统
├── MathEval                    # 公共 API
└── MathEval.Internal           # 内部工具
```

---

## 4. 项目结构

```
MathEval/
├── src/
│   └── MathEval/
│       ├── MathEval.csproj              # 主项目文件
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
│       ├── Lexer/                       # 词法分析模块
│       │   ├── TokenType.cs             # Token 类型枚举
│       │   ├── Token.cs                 # Token 数据结构
│       │   └── Lexer.cs                 # 词法分析器
│       ├── Parser/                      # 语法分析模块
│       │   ├── BinaryExpressionType.cs  # 二元运算符类型
│       │   ├── UnaryExpressionType.cs   # 一元运算符类型
│       │   └── Parser.cs                # 语法分析器
│       ├── AST/                         # 抽象语法树
│       │   ├── LogicalExpression.cs     # AST 基类
│       │   ├── ValueExpression.cs       # 常量值节点
│       │   ├── Identifier.cs            # 标识符节点
│       │   ├── BinaryExpression.cs      # 二元运算节点
│       │   ├── UnaryExpression.cs       # 一元运算节点
│       │   ├── FunctionCall.cs          # 函数调用节点
│       │   ├── InterpolatedString.cs    # 插值字符串节点
│       │   ├── ConditionalExpression.cs # 三元条件节点
│       │   ├── InterpolationSegment.cs  # 插值段基类
│       │   ├── TextSegment.cs           # 文本段
│       │   └── ExpressionSegment.cs     # 表达式段
│       ├── Visitors/                    # Visitor 模式
│       │   ├── IExpressionVisitor.cs    # 无返回值 Visitor 接口
│       │   ├── IExpressionVisitor{T}.cs # 泛型 Visitor 接口
│       │   └── EvaluationVisitor.cs     # 求值 Visitor
│       ├── Context/                     # 上下文系统
│       │   ├── ExpressionContext.cs     # 上下文类
│       │   └── ExpressionFunction.cs    # 函数委托类型
│       ├── Functions/                   # 内置函数
│       │   └── BuiltInFunctions.cs      # 内置函数注册
│       ├── TypeSystem/                  # 类型系统
│       │   └── TypeHelper.cs            # 类型辅助类
│       ├── API/                         # 公共 API
│       │   ├── Expression.cs            # 主入口类
│       │   ├── ExpressionBuilder.cs     # Builder 类
│       │   ├── ICalculator.cs           # 计算器接口
│       │   ├── Calculator.cs            # 计算器实现
│       │   └── ExpressionOptions.cs     # 选项枚举
│       └── Internal/                    # 内部工具
│           ├── ExpressionCache.cs       # 表达式缓存
│           └── FunctionWrapper.cs       # 函数包装器
├── tests/
│   └── MathEval.Tests/
│       ├── MathEval.Tests.csproj
│       ├── LexerTests.cs
│       ├── ParserTests.cs
│       ├── EvaluationTests.cs
│       ├── ContextTests.cs
│       ├── FunctionTests.cs
│       ├── StringInterpolationTests.cs
│       ├── CacheTests.cs
│       └── ThreadSafetyTests.cs
├── Docs/
│   ├── PRD.md
│   ├── TechDesign.md
│   └── Architecture.md
├── .gitignore
├── LICENSE
├── README.md
└── MathEval.sln
```

---

## 5. 模块依赖关系

```
API 层
  │
  ├─→ Parser 层
  │      │
  │      └─→ Lexer 层
  │
  ├─→ AST 层
  │
  ├─→ Visitor 层
  │      │
  │      ├─→ TypeSystem 层
  │      └─→ Context 层
  │              │
  │              └─→ Functions 层
  │
  └─→ Internal 层
         ├─→ ExpressionCache (无依赖)
         └─→ FunctionWrapper (→ Context)
```

### 依赖规则
- 上层可以依赖下层，下层不能依赖上层
- 同层模块之间可以相互依赖，但要避免循环依赖
- Exceptions 层被所有层依赖
- AST 层是核心数据结构，被 Parser 和 Visitor 层依赖

---

## 6. 核心模块设计

### 6.1 异常模块 (Exceptions)

异常类层次结构：

```
MathEvalException (基类)
├── ParseException
├── TypeMismatchException
└── EvaluateException
    ├── FunctionNotFoundException
    ├── FunctionTypeMismatchException
    ├── SymbolNotFoundException
    ├── DivisionByZeroException
    └── OverflowException
```

### 6.2 词法分析模块 (Lexer)

**职责**：将表达式字符串转换为 Token 序列

**主要组件**：
- `TokenType`：Token 类型枚举
- `Token`：Token 数据结构（类型、文本、位置）
- `Lexer`：词法分析器

**Lexer 状态机**：
- 正常状态：扫描数字、标识符、运算符、分隔符
- 字符串状态：扫描字符串字面量，处理转义字符
- 插值状态：扫描插值字符串，处理嵌套花括号

### 6.3 语法分析模块 (Parser)

**职责**：将 Token 序列转换为 AST

**主要组件**：
- `BinaryExpressionType`：二元运算符类型枚举
- `UnaryExpressionType`：一元运算符类型枚举
- `Parser`：递归下降解析器

**Parser 方法结构**（14 级优先级）：
```
ParseExpression()
  └─ ParseConditional()
       └─ ParseLogicalOr()
            └─ ParseLogicalAnd()
                 └─ ParseEquality()
                      └─ ParseRelational()
                           └─ ParseBitwiseOr()
                                └─ ParseBitwiseXor()
                                     └─ ParseBitwiseAnd()
                                          └─ ParseShift()
                                               └─ ParseAdditive()
                                                    └─ ParseMultiplicative()
                                                         └─ ParsePower()
                                                              └─ ParseUnary()
                                                                   └─ ParsePrimary()
```

### 6.4 AST 模块

**职责**：表示表达式的抽象语法树

**节点类型**：
- `LogicalExpression`：基类
- `ValueExpression`：常量值
- `Identifier`：标识符
- `BinaryExpression`：二元运算
- `UnaryExpression`：一元运算
- `FunctionCall`：函数调用
- `InterpolatedString`：插值字符串
- `ConditionalExpression`：三元条件

### 6.5 Visitor 模块

**职责**：遍历 AST 并执行操作

**主要组件**：
- `IExpressionVisitor`：无返回值 Visitor 接口
- `IExpressionVisitor<T>`：泛型 Visitor 接口
- `EvaluationVisitor`：求值 Visitor

### 6.6 上下文模块 (Context)

**职责**：管理符号、函数和上下文继承

**主要组件**：
- `ExpressionContext`：上下文类
  - `_symbols`：ConcurrentDictionary<string, SymbolEntry>
  - `_functions`：ConcurrentDictionary<string, ExpressionFunction>
  - `_parent`：父上下文引用
- `ExpressionFunction`：函数委托类型

### 6.7 类型系统模块 (TypeSystem)

**职责**：提供类型推断、转换和运算辅助

**主要组件**：
- `TypeHelper`：静态辅助类
  - `BoolToNumber()`：布尔转数值
  - `Promote()`：类型提升
  - `CheckedAdd/Subtract/Multiply()`：溢出检查的算术运算
  - `EvaluateBinary()`：二元运算求值
  - `EvaluateUnary()`：一元运算求值
  - `Format()`：格式化
  - `ToString()`：转字符串

### 6.8 API 模块

**职责**：提供简洁的公共 API

**主要组件**：
- `Expression`：静态入口类
  - `Eval()`：快捷求值
  - `Builder`：Builder 入口
- `ExpressionBuilder`：Fluent Builder
- `ICalculator`：计算器接口
- `Calculator`：计算器实现
- `ExpressionOptions`：选项枚举

### 6.9 内部模块 (Internal)

**职责**：提供内部工具

**主要组件**：
- `ExpressionCache`：表达式缓存
- `FunctionWrapper`：强类型函数包装器

---

## 7. 接口设计

### 7.1 公共 API

#### Expression 类

```csharp
public static class Expression
{
    public static object Eval(string expression, ExpressionContext? context = null, ExpressionOptions options = ExpressionOptions.None);
    public static T Eval<T>(string expression, ExpressionContext? context = null, ExpressionOptions options = ExpressionOptions.None);
    public static ExpressionBuilder Builder { get; }
}
```

#### ExpressionBuilder 类

```csharp
public class ExpressionBuilder
{
    public ExpressionBuilder With(string name, object value);
    public ExpressionBuilder With(string name, Func<object> value);
    public ExpressionBuilder WithFunction(string name, ExpressionFunction func);
    public ExpressionBuilder WithFunction<T1, TResult>(string name, Func<T1, TResult> func);
    public ExpressionBuilder WithFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> func);
    // ... 3-8 参数的 WithFunction 重载
    public ExpressionBuilder WithOptions(ExpressionOptions options);
    public ICalculator Build(string expression);
}
```

#### ICalculator 接口

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

#### ExpressionContext 类

```csharp
public class ExpressionContext
{
    public ExpressionContext();
    public void Set(string name, object value);
    public void Set(string name, Func<object> value);
    public void SetFunction(string name, ExpressionFunction func);
    public void SetFunction<T1, TResult>(string name, Func<T1, TResult> func);
    // ... 3-8 参数的 SetFunction 重载
    public ExpressionContext CreateChild();
    public void Remove(string name);
}
```

#### ExpressionOptions 枚举

```csharp
[Flags]
public enum ExpressionOptions
{
    None = 0,
    NoCache = 1
}
```

#### ExpressionFunction 委托

```csharp
public delegate object ExpressionFunction(object[] args);
```

### 7.2 内部接口

#### IExpressionVisitor 接口

```csharp
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
```

#### IExpressionVisitor<T> 接口

```csharp
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

---

## 8. 实现计划

### 8.1 阶段一：基础架构 (Day 1)

1. 创建解决方案和项目结构
2. 实现异常类
3. 实现 TokenType 和 Token
4. 实现 AST 基类和节点类
5. 实现 Visitor 接口
6. 实现 ExpressionOptions
7. 实现 ExpressionFunction 委托

### 8.2 阶段二：词法分析 (Day 2)

1. 实现 Lexer 基础结构
2. 实现数字扫描（十进制、十六进制、八进制、二进制）
3. 实现标识符和关键字扫描
4. 实现字符串字面量扫描（支持转义字符）
5. 实现字符串插值扫描
6. 实现运算符和分隔符扫描
7. 编写 Lexer 单元测试

### 8.3 阶段三：语法分析 (Day 3)

1. 实现 Parser 基础结构
2. 实现 14 级优先级的解析方法
3. 实现函数调用解析
4. 实现插值字符串解析
5. 实现深度限制和长度限制
6. 编写 Parser 单元测试

### 8.4 阶段四：类型系统 (Day 4)

1. 实现 TypeHelper 基础方法
2. 实现类型提升逻辑
3. 实现算术运算（包括溢出检查）
4. 实现位运算
5. 实现比较运算
6. 实现逻辑运算
7. 实现字符串拼接
8. 实现格式化
9. 编写 TypeHelper 单元测试

### 8.5 阶段五：求值和上下文 (Day 5)

1. 实现 EvaluationVisitor
2. 实现 ExpressionContext（符号和函数存储）
3. 实现上下文继承
4. 实现延迟值符号
5. 实现 BuiltInFunctions
6. 编写 Context 和 Evaluation 单元测试

### 8.6 阶段六：API 和缓存 (Day 6)

1. 实现 FunctionWrapper
2. 实现 ExpressionCache
3. 实现 Calculator
4. 实现 ExpressionBuilder
5. 实现 Expression 静态类
6. 编写 API 单元测试
7. 编写缓存测试

### 8.7 阶段七：高级特性 (Day 7)

1. 实现强类型函数注册（1-8 参数）
2. 实现字符串插值格式说明符
3. 编写 StringInterpolationTests
4. 编写 FunctionTests

### 8.8 阶段八：测试和优化 (Day 8)

1. 编写线程安全测试
2. 性能测试和优化
3. 集成测试
4. 文档完善

---

## 9. 测试策略

### 9.1 单元测试覆盖

| 模块 | 测试文件 | 覆盖内容 |
|------|----------|----------|
| Lexer | LexerTests.cs | 各种 Token 类型扫描、多进制数字、字符串、插值、关键字 |
| Parser | ParserTests.cs | 14 级优先级、各种表达式结构、错误处理 |
| Evaluation | EvaluationTests.cs | 所有运算符、类型推断、短路求值、边界条件 |
| Context | ContextTests.cs | 符号注册、延迟值、函数注册、上下文继承、删除 |
| Functions | FunctionTests.cs | 内置函数、强类型注册、参数验证 |
| StringInterpolation | StringInterpolationTests.cs | 插值语法、格式说明符、转义 |
| Cache | CacheTests.cs | 缓存命中、NoCache 选项、缓存清除 |
| ThreadSafety | ThreadSafetyTests.cs | 并发访问、上下文继承并发 |

### 9.2 测试用例分类

1. **功能测试**：验证每个功能是否按 PRD 要求工作
2. **边界测试**：测试边界条件（最大值、最小值、空值、除零等）
3. **错误测试**：验证异常是否正确抛出
4. **并发测试**：验证多线程安全
5. **性能测试**：验证表达式缓存效果和整体性能

### 9.3 测试数据

- 来自 PRD 的所有示例表达式
- 来自 NCalc 和 NFun 的测试用例
- 自定义的边界和错误测试用例
