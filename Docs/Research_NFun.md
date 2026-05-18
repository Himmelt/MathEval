# NFun 技术架构研究

> **研究对象**：[NFun](https://github.com/tmteam/NFun)
> **研究日期**：2026-05-18
> **研究目的**：为 MathEval 技术方案提供参考

---

## 目录

1. [整体架构概览](#1-整体架构概览)
2. [词法分析器 (Lexer)](#2-词法分析器-lexer)
3. [语法分析器 (Parser)](#3-语法分析器-parser)
4. [类型推断系统](#4-类型推断系统)
5. [字符串插值实现](#5-字符串插值实现)
6. [上下文继承机制](#6-上下文继承机制)
7. [函数注册系统](#7-函数注册系统)
8. [NFun 与 MathEval PRD 的关键差异对照](#8-nfun-与-matheval-prd-的关键差异对照)
9. [编译管线全景](#9-编译管线全景)

---

## 1. 整体架构概览

NFun 是一个用 C# 实现的动态表达式语言引擎，其核心编译管线为：

```
源代码字符串 → Tokenizer(词法分析) → Parser(语法分析) → TicSetupVisitor(类型图构建) → Tic Solver(类型推断求解) → ExpressionBuilder(解释器构建) → FunnyRuntime(运行时)
```

核心模块划分：

| 模块 | 目录 | 职责 |
|------|------|------|
| 词法分析 | `Tokenization/` | 将源代码字符串转换为 Token 流 |
| 语法分析 | `SyntaxParsing/` | 将 Token 流转换为语法树(AST) |
| 类型推断 | `Tic/` + `TypeInferenceAdapter/` | 基于约束图的全局类型推断 |
| 解释执行 | `Interpretation/` | 将 AST 转换为可执行的表达式节点 |
| 运行时 | `Runtime/` | 变量管理、方程执行 |
| 函数系统 | `Functions/` + `Interpretation/Functions/` | 内置函数与用户自定义函数 |
| API 层 | `Api/` | Fluent API 入口 |

---

## 2. 词法分析器 (Lexer)

### 2.1 整体设计

NFun 的词法分析器采用**手写式逐字符扫描**方式，不使用生成器工具。核心类 `Tokenizer` 是有状态的（因为需要处理字符串插值的嵌套花括号），通过 `TryReadNext` 方法逐个产出 Token。

```csharp
// 入口：将字符串转为 Token 流
public static IEnumerable<Tok> ToTokens(string input) {
    var reader = new Tokenizer();
    for (var i = 0;;) {
        var res = reader.TryReadNext(input, i);
        yield return res;
        if (res.Is(TokType.Eof)) yield break;
        i = res.Finish;
    }
}
```

### 2.2 Token 模型

`Tok` 定义了不可变的 Token 结构，包含 `Type`(枚举)、`Value`(字符串值) 和 `Interval`(位置区间)。

`TokType` 定义了约 70 种 Token 类型，涵盖：

- **字面量**：`IntNumber`, `RealNumber`, `HexOrBinaryNumber`, `IpAddress`, `Text`
- **运算符**：`Plus`, `Minus`, `Mult`, `Div`, `Pow`, `DivInt`, `Rema` 等
- **比较运算**：`Equal`, `NotEqual`, `Less`, `More` 等
- **位运算**：`BitAnd`, `BitOr`, `BitXor`, `BitShiftLeft`, `BitShiftRight`, `BitInverse`
- **关键字**：`If`, `Else`, `Then`, `True`, `False`, `And`, `Or`, `Xor`, `Not`, `Rule`, `In`, `Step`, `Default`
- **类型关键字**：`Int16Type`, `Int32Type`, `RealType`, `BoolType`, `TextType` 等
- **字符串插值**：`TextOpenInterpolation`, `TextMidInterpolation`, `TextCloseInterpolation`
- **保留关键字**：`Reserved`（如 `fun`, `for`, `while`, `null` 等）

### 2.3 扫描策略

`TryReadNext` 方法的扫描优先级为：

1. **跳过空白和注释**：`#` 开头到行尾为注释，空格和制表符被跳过
2. **换行符**：`\r`, `\n`, `;` 均识别为 `NewLine`
3. **数字**：调用 `ReadNumberOrIp`，支持十进制、十六进制(`0x`)、二进制(`0b`)、IP地址(3个点)
4. **标识符/关键字**：调用 `ReadIdOrKeyword`，通过静态字典 `Keywords` 查表区分
5. **特殊符号**：调用 `TryReadUncommonSpecialSymbols`，支持双字符运算符如 `==`, `!=`, `<=`, `>=`, `//`, `**`, `..`, `<<`, `>>`
6. **引号字符串**：调用 `ReadText`，支持字符串插值

### 2.4 关键特性

| 特性 | 说明 |
|------|------|
| 隐式乘法 | `10x` 被识别为 `10 * x`（在 Parser 层处理） |
| IP 地址字面量 | `192.168.0.1` 被识别为 `IpAddress` Token |
| 数字分隔符 | 支持 `_` 分隔的数字如 `1_000_000` |
| 保留关键字检测 | 使用 `fun` 等保留字会抛出 `TokenIsReserved` 错误，并建议替代词（如 `fun` -> `rule`） |

### 2.5 Token 流封装

`TokFlow` 将 `Tok[]` 数组封装为带游标的流，提供 `MoveNext()`, `SkipNewLines()`, `IsCurrent()`, `MoveIf()` 等便捷方法，是 Parser 的输入接口。

---

## 3. 语法分析器 (Parser)

### 3.1 整体设计

NFun 的语法分析采用**递归下降 + Pratt 优先级爬升**的混合策略：

- **顶层结构**：`Parser` 类负责识别方程定义、变量声明、用户函数定义
- **表达式解析**：`SyntaxNodeReader` 静态类负责表达式内部的优先级爬升解析

### 3.2 顶层解析 (Parser)

`Parser.ParseTree` 方法循环读取顶层语句，根据 Token 序列区分三种顶层结构：

```csharp
// 1. 带类型的输入变量声明：  i:int
if (e is TypedVarDefSyntaxNode typed) {
    if (flow.IsCurrent(TokType.Def))
        ReadEquation(typed, typed.Id);    // y:int = expr
    else
        ReadInputVariableSpecification(typed);  // i:int (仅声明)
}
// 2. 命名方程：  y = expr
else if (flow.IsCurrent(TokType.Def) || flow.IsCurrent(TokType.Colon)) {
    if (e is NamedIdSyntaxNode variable)
        ReadEquation(variable, variable.Id);
    else if (e is FunCallSyntaxNode fun && !fun.IsOperator)
        ReadUserFunction(fun);  // 3. 用户函数定义：  f(a,b):int = expr
}
// 4. 匿名方程：  expr (结果赋给 "out")
else
    ReadAnonymousEquation(e);
```

### 3.3 表达式解析 (SyntaxNodeReader) - Pratt 解析器

这是 NFun Parser 最精巧的部分。采用 **Pratt 优先级爬升算法**，定义了 14 级运算符优先级：

```csharp
var priorities = new List<TokType[]>(14) {
    new[] { TokType.ArrOBr, TokType.Dot, TokType.ParenthOBr },  // 0: 后缀
    new[] { TokType.Pow },                                        // 1: 幂
    new[] { TokType.Mult, TokType.Div, TokType.DivInt, TokType.Rema }, // 2: 乘除
    new[] { TokType.Plus, TokType.Minus },                        // 3: 加减
    new[] { TokType.BitShiftLeft, TokType.BitShiftRight },        // 4: 移位
    new[] { TokType.BitAnd },                                     // 5: 位与
    new[] { TokType.BitXor },                                     // 6: 位异或
    new[] { TokType.BitOr },                                      // 7: 位或
    new[] { TokType.More, TokType.Less, TokType.MoreOrEqual, TokType.LessOrEqual }, // 8: 比较
    new [] { TokType.In, TokType.Equal, TokType.NotEqual },       // 9: 相等/包含
    Array.Empty<TokType>(),                                       // 10: 空位(unary not)
    new[] { TokType.And },                                        // 11: 逻辑与
    new[] { TokType.Xor },                                        // 12: 逻辑异或
    new[] { TokType.Or }                                          // 13: 逻辑或
};
```

核心递归方法 `ReadNodeOrNull(flow, priority)` 的逻辑：

1. 如果 `priority < MinPriority`，读取原子节点（常量、变量、一元运算、括号表达式等）
2. 否则，先递归读取 `priority - 1` 级的左操作数
3. 循环检查当前 Token 是否为当前优先级的运算符
4. 如果是，读取右操作数并构建二元运算节点
5. 如果运算符优先级高于当前级别，返回已构建的左子树

**特殊处理**：

- **隐式乘法**：`10x` 或 `10(x)` 通过 `IsHiddenMultiplication` 检测
- **比较链**：`a < b < c` 通过 `ReadComparisonChain` 解析为 `ComparisonChainSyntaxNode`
- **管道调用**：`x.f(y)` 通过 `.` 运算符解析为 `PipedFunCall`
- **数组切片**：`arr[1:3]` 和 `arr[1:5:2]` 通过 `ReadArraySlice` 解析

### 3.4 语法树节点

所有 AST 节点实现 `ISyntaxNode` 接口：

```csharp
public interface ISyntaxNode {
    FunnyType OutputType { get; set; }
    int OrderNumber { get; set; }
    int ParenthesesCount { get; set; }  // 包裹的括号层数
    Interval Interval { get; set; }
    T Accept<T>(ISyntaxNodeVisitor<T> visitor);
    IEnumerable<ISyntaxNode> Children { get; }
}
```

节点类型包括：`EquationSyntaxNode`, `FunCallSyntaxNode`, `ConstantSyntaxNode`, `GenericIntSyntaxNode`, `IfThenElseSyntaxNode`, `AnonymFunctionSyntaxNode`, `SuperAnonymFunctionSyntaxNode`, `ArraySyntaxNode`, `StructInitSyntaxNode`, `StructFieldAccessSyntaxNode`, `ComparisonChainSyntaxNode` 等。

### 3.5 Visitor 模式

语法树使用 Visitor 模式进行遍历，`ISyntaxNodeVisitor<T>` 定义了 21 个 Visit 方法，每个对应一种语法节点类型。支持两种遍历方式：

- **EnterVisitorBase**：进入节点时访问（前序遍历）
- **ExitVisitorBase**：离开节点时访问（后序遍历）

---

## 4. 类型推断系统

### 4.1 概述

这是 NFun 最核心也最复杂的模块，实现了一套基于**约束图 (Constraint Graph)** 的全局类型推断算法，命名为 **Tic** (Type Inference by Constraints)。

### 4.2 核心概念

#### TicNode - 约束图节点

`TicNode` 是类型推断的基本单元，有三种类型：

```csharp
public enum TicNodeType {
    Named = 2,       // 命名变量（输入/输出变量）
    SyntaxNode = 4,  // 语法节点
    TypeVariable = 8 // 泛型类型变量
}
```

每个 TicNode 包含：

- `State`：当前类型状态（`ITicNodeState`）
- `Ancestors`：祖先节点列表（表示 "此节点的类型必须是祖先节点类型的子类型"）
- `IsMemberOfAnything`：标记是否为复合类型的成员

#### 类型状态层次

```
ITicNodeState
├── ITypeState (已求解的具体类型)
│   ├── StatePrimitive (原始类型: Real, I32, Bool, Char, Ip, Any 等 18 种)
│   └── ICompositeState (复合类型)
│       ├── StateArray (数组类型，含元素 TicNode)
│       ├── StateFun (函数类型，含参数 TicNode[] 和返回值 TicNode)
│       └── StateStruct (结构体类型，含字段 Dictionary<string, TicNode>)
├── ConstrainsState (未求解的约束状态)
└── StateRefTo (引用状态，指向另一个 TicNode)
```

#### ConstrainsState - 约束状态

`ConstrainsState` 是类型推断的核心数据结构，表示一个尚未完全确定的类型：

```csharp
public class ConstrainsState : ITicNodeState {
    public StatePrimitive Ancestor { get; }      // 上界（必须是此类型的子类型）
    public ITypeState Descendant { get; }         // 下界（必须是此类型的父类型）
    public StatePrimitive Preferred { get; set; } // 首选类型
    public bool IsComparable { get; }             // 是否可比较
}
```

约束的表示法为 `[Descendant..Ancestor]`，例如 `[U8..Real]` 表示 "最小可取 U8，最大可取 Real"。

### 4.3 类型推断流程

`GraphBuilder.Solve` 方法执行四阶段求解：

```csharp
public ITicResults Solve(bool ignorePrefered = false) {
    // 阶段 0: 拓扑排序
    var sorted = Toposort();

    // 阶段 1: 向上拉约束 (PullConstraints)
    SolvingFunctions.PullConstraints(sorted);

    // 阶段 2: 向下推约束 (PushConstraints)
    SolvingFunctions.PushConstraints(sorted);

    // 阶段 3: 销毁/合并 (Destruction)
    bool allTypesAreSolved = SolvingFunctions.Destruction(sorted);

    // 阶段 4: 最终化 (Finalize) - 处理未求解的泛型
    if (!allTypesAreSolved)
        return SolvingFunctions.Finalize(...);
}
```

#### 阶段 0：拓扑排序

对约束图进行拓扑排序，同时检测并合并引用环和祖先环。如果发现环，则将环中所有节点合并为一个节点（通过 `SolvingFunctions.MergeGroup`）。

#### 阶段 1：PullConstraints（向上拉约束）

从叶节点向根节点传播类型信息。对于每个节点，检查其所有祖先的类型约束，将约束信息 "拉" 回来。

关键操作：

- 如果祖先是 `StatePrimitive`，检查后代是否可以隐式转换
- 如果祖先是 `ConstrainsState`，将后代信息合并到祖先约束中
- 如果祖先是 `StateArray`/`StateFun`/`StateStruct`，将后代转换为对应的复合类型

#### 阶段 2：PushConstraints（向下推约束）

从根节点向叶节点传播类型信息。逆序遍历拓扑排序结果，将已确定的类型信息 "推" 到后代节点。

#### 阶段 3：Destruction（销毁/合并）

处理剩余的约束对（祖先-后代关系），尝试将约束状态具体化：

- `ConstrainsState + ConstrainsState` -> 合并为一个，另一个变为 `StateRefTo`
- `ConstrainsState + StatePrimitive` -> 如果约束允许，具体化为原始类型
- 复合类型递归处理其成员

#### 阶段 4：Finalize（最终化）

处理无法完全求解的泛型类型：

- **协变求解** (`SolveCovariant`)：输出类型取最宽的祖先类型
- **逆变求解** (`SolveContravariant`)：输入类型取最窄的后代类型
- 利用 `Preferred` 首选类型提示来选择具体类型

### 4.4 原始类型体系

`StatePrimitive` 定义了 18 种原始类型，并通过两个 18x18 矩阵预计算了类型间的隐式转换关系：

- **LcaMap**（Last Common Ancestor）：两个类型的最近公共祖先
- **FcdMap**（First Common Descendant）：两个类型的最远公共后代

类型层次（从宽到窄）：

```
Any > Char, Bool, Ip, Real
Real > I96 > I64 > I48 > I32 > I24 > I16
Real > I96 > I64 > U64 > U48 > U32 > U24 > U16 > U12 > U8
```

### 4.5 类型推断适配层

`TypeInferenceAdapter/` 是语法树和 Tic 系统之间的桥梁：

- **TicSetupVisitor**：遍历语法树，为每个节点在 `GraphBuilder` 中创建对应的 TicNode 和约束关系
- **ApplyTiResultEnterVisitor / ApplyTiResultsExitVisitor**：将 Tic 求解结果回写到语法树节点的 `OutputType` 属性

### 4.6 IStateFunction - 双分派模式

Tic 的三个求解阶段都实现了 `IStateFunction` 接口，这是一个**二维双分派**模式，对 (祖先状态类型, 后代状态类型) 的所有组合定义了不同的行为：

```
               | Primitive | Constrains | Composite(Array/Fun/Struct)
Primitive      |   11种    |   11种     |   11种
Constrains     |   11种    |   11种     |   11种
Composite      |   11种    |   11种     |   11种 (细分 Array/Fun/Struct)
```

---

## 5. 字符串插值实现

NFun 支持两种字符串插值机制，分别在**词法层**和**模板层**实现。

### 5.1 词法层插值（表达式内插值）

**语法**：`"Hello {name}, you are {age} years old"`

#### 词法分析阶段

Tokenizer 使用 `_isInInterpolation` 标志和 `_interpolationLayers` 栈来跟踪插值嵌套：

```csharp
private bool _isInInterpolation = false;
private readonly Stack<InterpolationLayer> _interpolationLayers = new();
```

`InterpolationLayer` 记录了：

- `OpenQuoteSymbol`：开引号字符
- `FigureBracketsDiff`：花括号嵌套差（用于判断 `}` 是关闭插值还是嵌套花括号）

当遇到 `"...{` 时，产出 `TextOpenInterpolation` Token；遇到 `}...{` 时，产出 `TextMidInterpolation`；遇到 `}..."` 时，产出 `TextCloseInterpolation`。

#### 语法分析阶段

`SyntaxNodeReader` 的 `ReadInterpolationText` 方法将插值字符串转换为函数调用链：

```csharp
// "Hello {name}!" 转换为：
// concat3Texts("Hello ", toText(name), "!")
// 或更长的插值转换为：
// concatArrayOfTexts(["...", toText(x), "...", toText(y), ...])
```

优化策略：

| 片段数 | 转换结果 |
|--------|---------|
| 1 个 | `toText(expr)` |
| 2 个 | `concat2Texts(text1, text2)` |
| 3 个 | `concat3Texts(text1, text2, text3)` |
| 4+ 个 | `concatArrayOfTexts([text1, text2, ...])` |

### 5.2 模板层插值（StringTemplate）

**语法**：纯文本模板，`{expression}` 嵌入表达式

#### 构建过程

1. `SeparateStringTemplate` 将模板分割为交替的文本段和脚本段
2. 为每个脚本段生成匿名方程 `___intepol___0 = script0; ___intepol___1 = script1; ...`
3. 将合并后的脚本交给 `RuntimeBuilder.Build` 构建运行时
4. 创建 `StringTemplateCalculator`，持有文本段列表和输出变量引用

#### 计算过程

```csharp
public string Calculate() {
    _runtime.Run();
    var sb = new StringBuilder(_texts[0]);
    for (int i = 0; i < _outputVariables.Count; i++) {
        sb.Append(TypeHelper.GetFunText(_outputVariables[i].FunnyValue));
        sb.Append(_texts[i + 1]);
    }
    return sb.ToString();
}
```

### 5.3 QuotationReader - 引号读取器

`QuotationReader` 是底层引号读取工具，负责：

- 读取直到遇到闭合引号或 `{`
- 处理转义序列：`\\`, `\n`, `\r`, `\'`, `\"`, `\t`, `\{`, `\}`
- 支持四种引号字符：`'`, `"`, `'`, `"`（包括中文引号）

---

## 6. 上下文继承机制

### 6.1 变量作用域 - VariableScopeAliasTable

`VariableScopeAliasTable` 实现了基于栈的变量作用域管理：

```csharp
public class VariableScopeAliasTable {
    private readonly List<Dictionary<string, string>> _variableAliasesStack;

    public void EnterScope(int nodeNumber, IList<string> scopeVariables = null);
    public void ExitScope();
    public string GetVariableAlias(string origin);
    public void AddVariableAlias(string originName, string alias);
}
```

**核心机制**：

- 使用 `List<Dictionary<string, string>>` 作为作用域栈
- 每进入一个匿名函数/超级匿名函数，调用 `EnterScope` 压入新作用域
- 变量查找从栈顶向下搜索，实现**词法作用域**（闭包可以捕获外层变量）
- 变量别名格式为 `{nodeNumber}::{variableName}`，确保不同作用域的同名变量在 Tic 图中有唯一标识

**使用场景**（在 `TicSetupVisitor` 中）：

```csharp
// 匿名函数进入新作用域
public bool Visit(AnonymFunctionSyntaxNode node) {
    _aliasScope.EnterScope(node.OrderNumber);
    // 为每个参数创建别名
    _aliasScope.AddVariableAlias(originName, anonymName);
    VisitChildren(node);
    // 创建 Lambda 类型约束
    _ticTypeGraph.CreateLambda(..., aliasArgNames);
    _aliasScope.ExitScope();
    return true;
}
```

### 6.2 方言设置 - DialectSettings

`IDialect` 定义了语言行为配置：

```csharp
internal sealed class DialectSettings : IFunctionSelectorContext {
    public IfExpressionSetup IfExpressionSetup { get; }      // if 语法风格
    public IntegerPreferredType IntegerPreferredType { get; } // 整数首选类型
    public FunnyConverter Converter { get; }                  // Real 类型映射(double/decimal)
    public bool AllowIntegerOverflow { get; }                 // 是否允许整数溢出
    public AllowUserFunctions AllowUserFunctions { get; }     // 用户函数策略
}
```

**方言配置项**：

| 配置项 | 可选值 |
|--------|--------|
| `IfExpressionSetup` | `Deny`(禁用), `IfIfElse`(if if else), `IfElseIf`(if else if) |
| `IntegerPreferredType` | `Real`, `I32`, `I64` |
| `RealClrType` | `IsDouble`, `IsDecimal` |
| `IntegerOverflow` | `Checked`, `Unchecked` |
| `AllowUserFunctions` | `AllowAll`, `DenyRecursive`, `DenyUserFunctions` |

### 6.3 函数作用域 - ScopeFunctionDictionary

`IFunctionDictionary` 实现了函数字典的作用域继承：

```csharp
internal sealed class ScopeFunctionDictionary : IFunctionDictionary {
    private readonly IFunctionDictionary _origin;  // 父作用域
    private readonly Dictionary<string, IFunctionSignature> _functions; // 当前作用域

    public IFunctionSignature GetOrNull(string name, int argCount) {
        // 先查当前作用域
        _functions.TryGetValue(overloadName, out var signature);
        if (signature == null)
            return _origin.GetOrNull(name, argCount);  // 再查父作用域
        return signature;
    }
}
```

用户自定义函数通过 `ScopeFunctionDictionary` 添加到当前作用域，不会污染全局函数字典。

---

## 7. 函数注册系统

### 7.1 函数分类体系

```
IFunctionSignature (函数签名接口)
├── IConcreteFunction (具体函数)
│   ├── FunctionWithSingleArg / FunctionWithTwoArgs / FunctionWithManyArguments
│   ├── ConcreteHiOrderFunction
│   └── ConcreteUserFunction / ConcreteRecursiveUserFunction
├── IGenericFunction (泛型函数)
│   ├── GenericFunctionBase
│   │   ├── PureGenericFunctionBase (单泛型约束 T,T->T)
│   │   └── GenericFunctionWithSingleArgument / GenericFunctionWithTwoArguments
│   └── GenericUserFunction
└── ConcreteUserFunctionPrototype (递归函数的原型)
```

### 7.2 内置函数注册

`BaseFunctions` 在静态构造函数中注册所有内置函数，分为四类：

**泛型函数**（约 40 个）：

| 分类 | 函数 |
|------|------|
| 算术运算 | `Add`, `Substract`, `Multiply`, `DivideReal`, `DivideInt`, `Remainder`, `Negate`, `Abs` |
| 比较运算 | `Equal`, `NotEqual`, `More`, `Less`, `MoreOrEqual`, `LessOrEqual`, `Min`, `Max` |
| 位运算 | `BitOr`, `BitAnd`, `BitXor`, `BitInverse`, `BitShiftLeft`, `BitShiftRight` |
| 数组操作 | `Get`, `Slice`, `Find`, `Filter`, `Map`, `Reduce`, `Fold`, `Sort`, `Reverse`, `Chunk`, `Flat`, `Take`, `Skip`, `Repeat`, `Concat`, `Append`, `Set` |
| 聚合函数 | `Sum`, `Count`, `First`, `Last`, `MinElement`, `MaxElement`, `Median`, `Average` |
| 集合运算 | `Unique`, `Unite`, `Intersect`, `SubstractArrays`, `IsIn` |
| 范围 | `Range`, `RangeStep` |
| 类型转换 | `Convert` |

**具体函数**（约 20 个）：

| 分类 | 函数 |
|------|------|
| 逻辑 | `Not`, `And`, `Or`, `Xor` |
| 文本 | `ToText`, `Trim`, `TrimStart`, `TrimEnd`, `ToUpper`, `ToLower`, `Split`, `Join` |
| 插值专用 | `ConcatArrayOfTexts`, `Concat2Texts`, `Concat3Texts` |

**Double 专用函数**：`Pow`, `Sqrt`, `Sin`, `Cos`, `Tan`, `Atan`, `Atan2`, `Asin`, `Acos`, `Exp`, `Log`, `LogE`, `Log10`, `Round`, `Average`

**Decimal 专用函数**：与 Double 版本一一对应

### 7.3 函数字典

`ImmutableFunctionDictionary` 是不可变函数字典，使用 `"name argCount"` 作为键来支持重载：

```csharp
private static string GetOverloadName(string name, int argCount) => name + " " + argCount;
```

`CloneWith` 方法可以添加自定义函数，返回新的不可变字典。

### 7.4 用户自定义函数注册

用户函数通过 `FunnyCalculatorBuilder.WithFunction` 或 `HardcoreBuilder.WithFunction` 注册：

```csharp
// Fluent API 方式
Funny.WithFunction<double, double>("square", x => x * x)
     .BuildForCalcConstant();

// Hardcore API 方式
Funny.Hardcore
     .WithFunction<double, double>("square", x => x * x)
     .Build("square(3.0)");
```

内部通过 `LambdaWrapperFactory.Create` 将 CLR `Func<>` 委托包装为 `IConcreteFunction`：

```csharp
public FunnyCalculatorBuilder WithFunction<Tin, TOut>(string name, Func<Tin, TOut> function) {
    _customFunctionFactories.Add(d => LambdaWrapperFactory.Create(name, function, d.Converter));
    return this;
}
```

### 7.5 用户定义函数（脚本内）

脚本内的用户函数（如 `f(x) = x * 2`）通过 `RuntimeBuilder.BuildFunctionAndPutItToDictionary` 构建：

1. 创建独立的 `GraphBuilder` 进行类型推断
2. 如果无泛型 -> 创建 `ConcreteUserFunction`（或 `ConcreteRecursiveUserFunction`）
3. 如果有泛型 -> 创建 `GenericUserFunction`（延迟实例化具体版本）
4. 先注册 `ConcreteUserFunctionPrototype`（用于递归调用），再替换为实际实现

函数求解顺序通过 `CycleTopologySorting.Sort` 拓扑排序确定，确保被依赖的函数先编译。如果检测到循环依赖，抛出 `ComplexRecursion` 错误。

---

## 8. NFun 与 MathEval PRD 的关键差异对照

| 维度 | NFun 现状 | MathEval PRD 要求 | 差异分析 |
|------|-----------|------------------|----------|
| 类型系统 | 18 种原始类型 + Tic 全局推断 | 3 种值类型 + long/double 双类型 | MathEval 大幅简化，无需全局推断 |
| 解析器 | Pratt 优先级爬升 | 递归下降 | 递归下降更直观 |
| 字符串插值 | 转换为 concat 函数链 | 原生 InterpolatedString 节点 | MathEval 不污染函数命名空间 |
| 上下文继承 | 栈式作用域 + 别名表 | 父子链式继承 | MathEval 简化为显式 CreateChild() |
| 函数重载 | 支持（name + argCount 键） | 不支持（后注册覆盖） | MathEval 简化 |
| 用户自定义函数 | 脚本内定义 + 递归 | 仅宿主注册 | MathEval 简化 |
| 泛型函数 | 支持 | 不支持 | MathEval 无需 |
| 隐式乘法 | 支持（`10x`） | 不支持 | 避免歧义 |
| 比较链 | 支持（`a < b < c`） | 不支持 | PRD 未要求 |
| 数组/结构体 | 支持 | 不支持 | PRD 仅聚焦三种类型 |
| `^` 语义 | 乘方（`**` 也支持） | 乘方 | 一致 |
| `xor` 语义 | 逻辑异或 | 按位异或 | MathEval 语义不同 |

**核心结论**：NFun 的架构过于复杂（全局类型推断、18 种原始类型、泛型函数等），MathEval 只需借鉴其关键特性的实现思路（字符串插值状态机、上下文链式继承、强类型函数包装），而非整体架构。

---

## 9. 编译管线全景

```
1. Tokenizer.ToFlow(script)
   字符串 → TokFlow (Token 流)

2. Parser.Parse(flow)
   Token 流 → SyntaxTree (AST)

3. SetNodeNumberVisitor
   AST 节点编号

4. FindFunctionSolvingOrderOrThrow
   用户函数拓扑排序

5. 对每个用户函数:
   TicSetupVisitor → GraphBuilder → Solve → ApplyTiResults
   (类型推断 + 结果回写)

6. SolveBodyOrThrow
   主体类型推断:
   - TicSetupVisitor: AST → Tic 约束图
   - GraphBuilder.Solve: 四阶段求解
   - ApplyTiResultEnterVisitor/ExitVisitor: 结果回写

7. ExpressionBuilderVisitor.BuildExpression
   类型化 AST → IExpressionNode (可执行表达式树)

8. FunnyRuntime
   方程 + 变量 + 用户函数 → 可执行运行时
```

NFun 的架构设计有几个显著特点：

1. **全局类型推断**：不是逐表达式局部推断，而是构建全局约束图一次性求解，支持更复杂的类型推导
2. **泛型函数支持**：通过 `GenericUserFunction` 延迟实例化，在调用点根据实际参数类型生成具体实现
3. **不可变函数字典 + 作用域继承**：保证线程安全的同时支持函数重载和作用域隔离
4. **Pratt 解析器**：清晰优雅的优先级处理，支持隐式乘法、比较链、管道调用等语法糖
