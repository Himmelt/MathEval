# MathEval 代码审查报告（第四轮 · 架构与优化专项）

> **审查日期**：2026-07-12
> **审查范围**：MathEval 主项目 + MathEval.Fast 子项目 + 测试项目
> **基线**：工作区已有第三方报告（`MathEval_CodeReview_Report.md`）记录 14 个问题（均已修复），本报告仅包含**该报告中未涉及的新发现**
> **审查重点**：BUG 查找、架构合理性、代码可优化点

---

## 目录

1. [新发现的 BUG](#一新发现的-bug)
2. [架构不合理之处](#二架构不合理之处)
3. [可优化之处](#三可优化之处)
4. [汇总表](#四汇总表)
5. [优先级建议](#五优先级建议)

---

## 一、新发现的 BUG

### BUG-A【严重】：编译模式（CompileOptimization）丢失 And/Or 短路求值语义

**位置**：`MathEval/Optimization/CompiledExpression.cs` L148-L164 `CompileBinaryExpression`

**问题**：`EvaluationVisitor` 对 `And`/`Or` 做了显式短路求值（先算左操作数，再决定是否算右操作数），但 `CompiledExpression` 把所有二元运算统一编译为「先算左、再算右、最后调 `TypeHelper.EvaluateBinary`」。表达式树 `Block([leftVar, rightVar], assignLeft, assignRight, call)` 中 `assignLeft`/`assignRight` 是**顺序执行**的，左右两侧都被强制求值，短路语义完全丢失。

```csharp
// CompiledExpression.CompileBinaryExpression — 所有二元运算统一编译，无短路
var leftExpr = CompileNode(expr.Left, contextParam);
var rightExpr = CompileNode(expr.Right, contextParam);   // 右侧表达式被无条件编译

var leftVar = LinqExpression.Variable(typeof(object), "left");
var rightVar = LinqExpression.Variable(typeof(object), "right");

var assignLeft = LinqExpression.Assign(leftVar, leftExpr);
var assignRight = LinqExpression.Assign(rightVar, rightExpr);

var call = LinqExpression.Call(typeHelperMethod!, opType, leftVar, rightVar);

return LinqExpression.Block([leftVar, rightVar], assignLeft, assignRight, call);
```

**对比解释模式的短路实现**（`EvaluationVisitor.Visit(BinaryExpression)`）：

```csharp
if (expr.Type == BinaryExpressionType.And) {
    var leftResult = expr.Left.Accept(this);
    if (leftResult is double[]) { ... }
    var leftDouble = TypeHelper.ToDouble(leftResult);
    if (leftDouble == 0) return 0.0;  // 短路：右侧不求值
    var rightResult = expr.Right.Accept(this);
    ...
}
```

**复现（解释模式 vs 编译模式结果不一致）**：

| 表达式 | 解释模式（EvaluationVisitor） | 编译模式（CompiledExpression） |
|--------|------|------|
| `0 && undefinedVar` | 短路，返回 `0` | 不短路，抛 `SymbolNotFoundException` |
| `1 || undefinedVar` | 短路，返回 `1` | 不短路，抛 `SymbolNotFoundException` |
| `0 && (1/0 == 0)` | 短路，返回 `0` | 不短路，右侧求值（`1/0=Infinity`，最终返回 `0`，但右侧被不必要地求值） |

更危险的是右侧含副作用（如用户自定义函数抛异常、写外部状态）时，两种模式行为发散，违反语义等价性。

**证据**：测试中短路求值在 `OptimizedEval`/`CompileOptimization` 下的一致性**没有专门测试**。`BugVerificationTests.cs` 仅覆盖数组广播/越界/长度校验/缓存竞态，无编译模式短路用例。

**建议修复**：对 `BinaryExpressionType.And`/`Or` 特殊处理，编译为条件分支形式：

```csharp
// 伪代码：And 的编译
var leftResult = CompileNode(expr.Left, contextParam);
var leftDouble = Call(TypeHelper.ToDouble, leftResult);
var condition = NotEqual(leftDouble, Constant(0.0));
// 短路：left 为 0 时直接返回 0，否则求值右侧
var rightResult = CompileNode(expr.Right, contextParam);
var rightDouble = Call(TypeHelper.ToDouble, rightResult);
var rightBool = NotEqual(rightDouble, Constant(0.0));
var andResult = Condition(rightBool, Constant(1.0), Constant(0.0));
return Condition(condition, andResult, Constant(0.0));
```

---

### BUG-B【严重】：NaN 真值在主项目与 Fast 项目中相反

**位置**：
- 主项目：`MathEval/TypeSystem/TypeHelper.cs` L107 `Not`（`d == 0 ? 1.0 : 0.0`）
- 主项目：`MathEval/Visitors/EvaluationVisitor.cs` L126-128 条件表达式（`ToDouble(condition) != 0`）
- Fast 项目：`MathEval.Fast/BuiltIn/BuiltInOperators.cs` L17-19 `ConvertToBool`（`value != 0 && !double.IsNaN(value)`）

**问题**：两套实现对 NaN 的真值解释完全相反。主项目把 NaN 视为 truthy（非零即真），Fast 项目把 NaN 视为 falsy（显式排除 NaN）。

**根因分析**：

主项目中有三处决定 NaN 真值的地方，全部使用 `!= 0` 判断：

1. **`EvaluationVisitor.Visit(BinaryExpression)` And/Or 短路**：`ToDouble(leftResult) != 0` → NaN 非 0 → truthy
2. **`EvaluationVisitor.Visit(ConditionalExpression)`**：`ToDouble(condition) != 0` → NaN 非 0 → truthy
3. **`TypeHelper.EvaluateUnary(Not)`**：`d == 0 ? 1.0 : 0.0` → NaN 不等于 0 → truthy → `!NaN` = 0

Fast 项目的 `ConvertToBool`：

```csharp
private static bool ConvertToBool(double value) => value != 0 && !double.IsNaN(value);
// NaN: 0 != 0 为 false → 短路 → 返回 false → falsy → !NaN = 1
```

**行为对比**：

| 表达式 | 主项目（NaN = truthy） | Fast（NaN = falsy） |
|--------|------|------|
| `NaN ? 1 : 0` | `1` | `0` |
| `NaN && 1` | `1` | `0` |
| `NaN \|\| 0` | `1` | `0` |
| `!NaN` | `0` | `1` |

**证据**：`CrossValidationTests.cs` 对 NaN 只测了算术/比较运算（第 157-224 行），**没有任何 NaN 作为条件真值的交叉验证用例**，该分歧一直未被发现。

**建议修复**：统一规约。建议遵循 IEEE 754 / JavaScript 语义，NaN 视为 falsy（与 Fast 一致）。在主项目修改：

1. `TypeHelper.EvaluateUnary(Not)`：`d == 0 || double.IsNaN(d) ? 1.0 : 0.0`
2. `EvaluationVisitor.Visit(BinaryExpression)` And/Or 短路：改用 `ConvertToBool` 辅助方法（含 NaN 检查）
3. `EvaluationVisitor.Visit(ConditionalExpression)`：同上
4. `TypeHelper.EvaluateBinary(And/Or)` 标量分支：`(d1 != 0 && !double.IsNaN(d1) && d2 != 0 && !double.IsNaN(d2)) ? 1.0 : 0.0`

并补充 NaN 真值交叉验证测试。

---

### BUG-C【严重】：BUG-11 修复不完整 — Fast 函数名与关键字仍大小写不敏感

**位置**：
- `MathEval.Fast/BuiltIn/BuiltInFunctions.cs` L78-L91：函数表用 `StringComparer.OrdinalIgnoreCase`
- `MathEval.Fast/Core/FastScanner.cs` L112-L127：`TryMatchKeyword` 用 `char.ToLowerInvariant` 比较

**问题**：第三方报告 BUG-11 的修复决策为「主项目与 Fast 均遵循大小写敏感」，但**只改了主项目**的常量/关键字比较器（`OrdinalIgnoreCase` → `Ordinal`），Fast 项目的函数名表和关键字匹配仍然大小写不敏感，与决策矛盾。

**行为对比**：

| 表达式 | 主项目（Ordinal，敏感） | Fast（OrdinalIgnoreCase，不敏感） |
|--------|------|------|
| `SIN(0)` | `FunctionNotFoundException` | `0` |
| `MAX(3,5)` | `FunctionNotFoundException` | `5` |
| `a OR b` | `SymbolNotFoundException`（OR 被当作标识符） | 正常求值 |
| `a AND b` | 同上失败 | 正常求值 |
| `NOT a` / `3 XOR 2` | 同上失败 | 正常求值 |

**证据**：`FastEvalTests.cs` 第 715-736 行明确断言 Fast 的关键字大小写不敏感（`a OR b → true`），但**无主/Fast 交叉验证**。

**建议修复**：按 BUG-11 既定决策，将 Fast 项目的比较器改为 `Ordinal`：

1. `BuiltInFunctions` 的 `_functions`、`_nameToId` 改用 `StringComparer.Ordinal`
2. `FastScanner.TryMatchKeyword` / `EqualsLower` 改为精确比较
3. 补充主/Fast 关键字和函数名大小写的交叉验证测试

---

### BUG-D【中等】：IndexPushdownOptimizer 对用户自定义函数做不安全下推

**位置**：`MathEval/Optimization/IndexPushdownOptimizer.cs` L33-L38

**问题**：优化器默认开启（除非设 `DisableIndexPushdown`），对非 `max`/`min` 的**所有**函数调用执行 `f(a)[i] → f(a[i])` 下推。这隐含假设「所有非聚合函数都是 element-wise 的」，但用户可通过 `SetFunction` 注册任意语义的函数。

```csharp
// f(a)[i] → f(a[i])  (function calls - push index into non-literal args)
ArrayIndexExpression { Array: FunctionCall func, Index: var idx }
    when !_aggregateFunctions.Contains(func.Name)
    => Optimize(new FunctionCall(func.Name,
        [.. func.Arguments.Select(arg => arg is ValueExpression
            ? arg                              // scalar literal: don't push
            : new ArrayIndexExpression(arg, idx))])),
```

**问题示例**：

```csharp
ctx.SetFunction("sum", (ExpressionFunction)(args => args.Sum(a => Convert.ToDouble(a))));
// 用户写 sum(arr)[0]，期望：先对 arr 求和得标量，再索引（返回标量本身）
Expression.Eval("sum([1,2,3])[0]", ctx);  // 应返回 6
// 优化后变成 sum([1,2,3][0]) = sum(1) = 1 — 语义完全错误
```

类似受影响的函数：`avg`、`count`、`len`、`sort`、`dot`、`median` 等所有归约/非 element-wise 函数。

优化器无法区分「element-wise 函数」与「归约函数」，仅硬编码排除 `max`/`min`。

**证据**：测试中 IndexPushdownOptimizer 只覆盖了内置函数（`sin`/`max`），**无任何 `ctx.SetFunction` + IndexPushdown 组合测试**。

**建议修复**：

- 短期：仅对已知 element-wise 的内置函数执行下推，对用户函数默认不下推
- 长期：在 `SetFunction` API 增加 `FunctionFlags.ElementWise` 标记，优化器据此决定是否下推

---

### BUG-E【中等】：Parser 深度限制对嵌套函数调用无效，可被构造导致栈溢出

**位置**：`MathEval/Parser/Parser.cs` L227-L290 `ParsePrimary`

**问题**：`CheckDepth()` 在 `ParsePrimary` 入口 `_depth++`，但 `Identifier` 分支在调用 `ParseIdentifierOrFunction()` **之前**就 `_depth--`（第 261 行），导致函数调用的递归嵌套深度不累积。

```csharp
private LogicalExpression ParsePrimary() {
    CheckDepth();  // _depth++

    switch (CurrentToken.Type) {
        ...
        case Lexer.TokenType.Identifier:
            _depth--;              // 立即回退！
            expr = ParseIdentifierOrFunction();  // 函数调用内的 ParseExpression 不继承深度
            break;
        ...
    }
}
```

**对比**：`LeftParenthesis` 分支在 `Expect` 之后 `_depth--`，括号嵌套深度会累积 ✓

**影响**：

| 嵌套模式 | 每层字符数 | 4096 字符内可达层数 | 是否触发 MaxDepth | 风险 |
|----------|-----------|---------------------|-------------------|------|
| `((((x))))` | ~2 | ~2000 | 是（深度累积） | 低 |
| `f(f(f(...)))` | ~3 | ~1300 | **否**（深度回退） | **高** |
| `!!!!x` | ~1 | ~4096 | **否**（ParseUnary 无 CheckDepth） | **高** |

~1300 层递归下降足以在 .NET 中触发 `StackOverflowException`（不可捕获，进程崩溃）。

**建议修复**：

1. 统一深度跟踪：在 `ParsePrimary` 末尾统一 `_depth--`，而非各 case 内提前减
2. 在 `ParseUnary` 中也调用 `CheckDepth`（当前未调用）
3. 或引入独立于 `_depth` 的调用栈深度计数器

---

### BUG-F【中等】：OptimizedExpressionCache.SetCompiled 覆盖时丢失 AST

**位置**：`MathEval/Optimization/OptimizedExpressionCache.cs` L43-L45

**问题**：`SetCompiled` 创建全新的 `CacheEntry { Compiled = compiled }`（`Ast = null`），若同一键先前由 `Set` 存入了 AST，会被覆盖丢失。

```csharp
public static void SetCompiled(string expression, int options, CompiledExpression compiled) {
    _cache.Set((expression, options), new CacheEntry { Compiled = compiled });
    // ↑ 新 entry 的 Ast = null，覆盖了旧 entry 的 Ast
}
```

**触发路径**（`Calculator.EnsureParsed` → `EnsureCompiled`）：

1. `EnsureParsed`：从 `ExpressionCache` 获取 AST → `_ast = cachedAst`
2. `EnsureCompiled`：编译 AST → `OptimizedExpressionCache.SetCompiled(...)` 覆盖旧条目（丢 Ast）
3. 后续若有人调 `OptimizedExpressionCache.TryGetAst` → 命中但 `Ast = null` → 返回 false → 被迫重解析

**建议修复**：`SetCompiled` 应读取既有 entry 并只更新 `Compiled` 字段：

```csharp
public static void SetCompiled(string expression, int options, CompiledExpression compiled) {
    if (_cache.TryGet((expression, options), out var entry)) {
        entry.Compiled = compiled;  // 仅更新 Compiled，保留 Ast
    } else {
        _cache.Set((expression, options), new CacheEntry { Compiled = compiled });
    }
}
```

注意：当前 `CacheEntry` 是 class（引用语义），`LruCache.Set` 对已存在的键会替换 value，需调整 LruCache 或改用更新语义。

---

### BUG-G【轻微】：InstructionCache._cache 字段非 readonly，SetCapacity 非原子

**位置**：`MathEval.Fast/VM/InstructionCache.cs` L4-L21

**问题**：

1. `_cache` 非 `readonly`，`SetCapacity` 直接 `_cache = new LruCache<>(capacity)` 重新赋值
2. 并发下另一线程可能持有旧引用继续写入旧缓存（数据丢失）
3. `GetOrCompile` 是 check-then-set（非原子），并发首跑会重复编译

```csharp
private static LruCache<string, Instruction[]> _cache = new(256);

public static void SetCapacity(int capacity) {
    _cache = new LruCache<string, Instruction[]>(capacity);  // 旧缓存引用丢失
}
```

**建议修复**：`_cache` 改为 `readonly`，`SetCapacity` 清空后改容量（或接受重复编译，编译幂等）。主项目的 `ExpressionCache` 已用 `readonly` 是正确做法。

---

## 二、架构不合理之处

### ARCH-1：主项目存在两套并行且职责重叠的缓存

**位置**：`MathEval/Internal/ExpressionCache.cs` + `MathEval/Optimization/OptimizedExpressionCache.cs`

**问题**：

- `ExpressionCache`：存 AST，键 `(Expression, Options)`
- `OptimizedExpressionCache`：存 AST + Compiled，键 `(Expression, Options)`

两者职责高度重叠。`Calculator` 实际只读 `ExpressionCache` 的 AST 和 `OptimizedExpressionCache` 的 Compiled，**从不读写** `OptimizedExpressionCache` 的 Ast 字段。`OptimizedExpressionCache.TryGetAst`/`Set`/`GetOrAdd`/`GetOrAddCompiled` 在 Calculator 路径下基本是死代码。

这导致：
1. 双写不一致（BUG-F 的根因）
2. 同一表达式可能存两份 AST（内存浪费）
3. 缓存失效需同时清两处

**建议**：合并为单一缓存，条目结构 `{ Ast, Compiled }`，Calculator 统一从一个入口读写。

---

### ARCH-2：CompiledExpression 与 EvaluationVisitor 两套求值逻辑用「复制粘贴」维持一致

**位置**：
- `MathEval/Optimization/CompiledExpression.cs` L32-L81 `CallFunctionWithBroadcast`
- `MathEval/Visitors/EvaluationVisitor.cs` L70-L107 `Visit(FunctionCall)`

**问题**：`CallFunctionWithBroadcast` 几乎是 `Visit(FunctionCall)` 逐行复制：

| 功能 | EvaluationVisitor | CompiledExpression |
|------|-------------------|-------------------|
| 聚合函数展平 | ✓ `FlattenArgs` | ✓ `FlattenArgs`（复制） |
| 数组广播检测 | ✓ `args.FirstOrDefault(a => a is double[])` | ✓ 同 |
| 长度校验 | ✓ | ✓ |
| element-wise 循环 | ✓ | ✓ |
| 聚合函数集合 | `_aggregateFunctions` (`OrdinalIgnoreCase`) | `_aggregateFunctions` (`OrdinalIgnoreCase`) |

另外 `IndexPushdownOptimizer` 也有独立的 `_aggregateFunctions`（`Ordinal`），三处定义且比较器不一。

BUG-A 正是这类「复制后忘记同步短路逻辑」的产物。任一处修改都易遗漏其他处。

**建议**：

1. 提取共享的 `FunctionCallEvaluator` 静态方法，两边复用
2. 聚合函数表统一为单一数据源（建议放到 `Functions/` 模块）

---

### ARCH-3：MathEval 与 MathEval.Fast 两套独立实现无共享规约层

**位置**：整体架构层面

**问题**：两项目各自独立实现 Lexer/Parser/Evaluator，行为差异已暴露多处：

| 维度 | 主项目 | Fast |
|------|--------|------|
| NaN 真值 | truthy | falsy（BUG-B） |
| 关键字大小写 | 敏感 | 不敏感（BUG-C） |
| 类型系统 | object（double/string/bool/double[]） | double-only |
| 数组支持 | ✓ | ✗ |
| 字符串插值 | ✓ | ✗ |
| 上下文继承 | ✓ | ✗ |
| 强类型函数 | ✓ | ✗ |
| 用户自定义函数 | ✓ | ✗ |

`CrossValidationTests` 试图对齐但覆盖不足（BUG-B/C 的分歧均无交叉验证）。

**建议**：

1. 抽取共享规约层：「运算符优先级表」「内置函数定义表」「类型转换规约」「真值定义」为单一数据源
2. 两项目从同一规约生成实现，或明确 Fast 为「double-only 子集」并在文档与 API 命名上强提示（如 `FastScalarEval`）
3. 补充 NaN 真值、关键字/函数名大小写的交叉验证测试

---

### ARCH-4：标量索引回退语义是优化器的实现细节泄漏到用户语义

**位置**：`MathEval/Visitors/EvaluationVisitor.cs` L147-L155

**问题**：对 `scalarVar[0]`（x 为标量变量）静默返回 x 本身，是 IndexPushdownOptimizer 的「补丁」——优化器把 `(arr + x)[i]` 变成 `arr[i] + x[i]`，但 `x[i]` 应返回 `x`。这个优化器内部需求泄漏到了用户语义：用户写错索引（如 `x[999]`）得不到错误反馈。

```csharp
// 标量索引回退（为 IndexPushdown 服务）
if (array is double) {
    if (expr.Array is ValueExpression)
        throw new EvaluateException($"无法对标量 {array} 进行索引");  // BUG-12 修复：仅字面量抛异常
    return array;  // 变量标量仍静默返回
}
```

已有报告 BUG-12 修复了 `5[0]`（字面量标量），但 `x[0]`（变量标量）仍静默返回。区分「优化器产生的标量索引」与「用户错误」的方式（`expr.Array is ValueExpression`）过于脆弱——`5[0]` 被报错但 `(5)[0]` 不报错。

**建议**：在 AST 节点（如 `ArrayIndexExpression`）上加 `IsSynthetic` 标记，优化器生成的索引设为 true，仅 synthetic 索引对标量做回退，用户原始写的标量索引一律抛异常。

---

### ARCH-5：ExpressionContext 构造函数无条件注册全部内置函数

**位置**：`MathEval/Context/ExpressionContext.cs` L14-L18

**问题**：每次 `new ExpressionContext()` 都执行 `BuiltInFunctions.Register(this)`，向 `ConcurrentDictionary` 插入 ~30 个符号/函数。`CreateChild()` 不会重复注册（子上下文独立存储），但 `Expression.Eval` 静态入口每次调用都 `new ExpressionContext()`，对一次性求值造成不必要开销。

```csharp
public ExpressionContext() {
    _parent = null;
    _symbols = new ConcurrentDictionary<string, SymbolEntry>(StringComparer.Ordinal);
    _functions = new ConcurrentDictionary<string, ExpressionFunction>(StringComparer.Ordinal);
    BuiltInFunctions.Register(this);  // 每次 ~30 次 ConcurrentDictionary 插入
}
```

**建议**：内置函数表用静态 `FrozenDictionary` 共享，`ExpressionContext` 仅持有用户符号，查找时先查用户符号再查内置函数。或缓存一个默认上下文模板。

---

### ARCH-6：LogicalExpression AST 节点为可变 class，优化器跨节点共享同一实例

**位置**：`MathEval/Optimization/IndexPushdownOptimizer.cs` L23-L25

**问题**：优化器在 `(a op b)[i]` 下推时，把同一个 `idx` 表达式实例塞进左右两个新的 `ArrayIndexExpression`：

```csharp
ArrayIndexExpression { Array: BinaryExpression bin, Index: var idx }
    => new BinaryExpression(bin.Type,
        Optimize(bin.Left is ValueExpression ? bin.Left : new ArrayIndexExpression(bin.Left, idx)),
        //                                                                              ^^^ 同一实例
        Optimize(bin.Right is ValueExpression ? bin.Right : new ArrayIndexExpression(bin.Right, idx))),
        //                                                                              ^^^ 同一实例
```

当前节点属性是 `{ get; }` 只读、Visitor 不变性，所以暂时安全。但 AST 是 `class`（引用语义）而非 `record`，缺少不变性约束。任何后续「AST 变换/带状态 Visitor」改动都可能踩到「共享节点被双重遍历/修改」的坑。

**建议**：

1. AST 节点改 `record` 或加不可变约束
2. 或优化器对 `idx` 调 `Optimize(idx)` 并确保深拷（当前 `Optimize` 对叶节点返回自身，仍有共享风险）

---

## 三、可优化之处

### OPT-1：LruCache.GetOrAdd 在持锁状态下调用 factory

**位置**：`MathEval/Internal/LruCache.cs` L57-L81

**问题**：`GetOrAdd` 在 `lock` 内调用 `factory(key)`。当 `OptimizedExpressionCache.GetOrAddCompiled` 用它编译 AST 时（`Expression.Compile` 耗时 ms 级），会阻塞**所有**缓存读写。

```csharp
public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory) {
    lock (_lock) {
        ...
        var value = factory(key);  // 持锁编译，阻塞所有缓存操作
        ...
    }
}
```

**建议**：改为 double-checked locking：锁内只查/占位，锁外编译，再锁内回填；或用 `ConcurrentDictionary<,>` + `Lazy<T>`。

---

### OPT-2：Calculator.EnsureCompiled 无去重，并发首跑重复编译

**位置**：`MathEval/Calculator.cs` L110-L128

**问题**：check-then-set 非原子，并发首次求值同一表达式会各自 `new CompiledExpression` 并各调一次 `Expression.Compile`（昂贵）。

**建议**：用 `OptimizedExpressionCache.GetOrAddCompiled`（修好 OPT-1 后）或 `Lazy<CompiledExpression>` 去重。

---

### OPT-3：ConstantFolder 不折叠函数调用且在 IndexPushdown 之前运行

**位置**：
- `MathEval/Optimization/ConstantFolder.cs` L68-L80
- `MathEval/Calculator.cs` L96-L104

**问题**：

1. `FoldFunction` 是空操作（注释承认），`sin(0)`、`1+2*3` 等纯函数无法折叠
2. `ConstantFolding` 先于 `IndexPushdown` 执行，导致 `[1,2,3][0]` 经下推后变成 `1[0]` 等模式无法再折叠

```csharp
// Calculator.EnsureParsed
if (_options.HasFlag(ExpressionOptions.ConstantFolding)) {
    _ast = ConstantFolder.Fold(_ast);  // 先折叠（无法折叠 sin(0)）
}
if (!_options.HasFlag(ExpressionOptions.DisableIndexPushdown)) {
    _ast = IndexPushdownOptimizer.Optimize(_ast);  // 后下推（产生 1[0]，无法再折叠）
}
```

**建议**：

1. 折叠支持白名单纯函数（`sin/cos/sqrt/abs/...`），先递归折叠参数，若全为常量则调用函数求值
2. 把 `IndexPushdown` 放在 `ConstantFolding` 之前，或在 `IndexPushdown` 后再跑一轮折叠

---

### OPT-4：BytecodeVM 的 Call 指令每次 new double[argCount]

**位置**：`MathEval.Fast/VM/BytecodeVM.cs` L67-L76

**问题**：每次函数调用在堆上分配参数数组：

```csharp
case OpCode.Call: {
    var func = BuiltInFunctions.GetEvaluateById(instr.FunctionId);
    var argCount = instr.IntOperand;
    var args = new double[argCount];  // 每次堆分配
    for (int i = argCount - 1; i >= 0; i--) {
        args[i] = stack[--sp];
    }
    stack[sp++] = func(args);
    break;
}
```

**建议**：用 `ArrayPool<double>.Shared` 租借（栈已用 ArrayPool，参数数组同理），或对 `argCount <= 8` 用 `stackalloc`。

---

### OPT-5：FastEvaluator.CallBuiltInFunction 把 Span 转 double[]

**位置**：`MathEval.Fast/Core/FastEvaluator.cs` L417-L423

**问题**：`args.ToArray()` 每次函数调用都堆分配，与类注释宣称的「零字符串分配」「栈分配」相悖。

```csharp
private static double CallBuiltInFunction(ReadOnlySpan<char> name, ReadOnlySpan<double> args) {
    if (BuiltInFunctions.TryGetFunction(name, out var func)) {
        double[] arr = args.ToArray();  // 堆分配！
        return func(arr);
    }
    throw new FastEvalException($"未知函数 '{name}'", "");
}
```

**建议**：`BuiltInFunctions` 的 `Evaluate` 签名改为 `Func<ReadOnlySpan<double>, double>`，避免分配。或对 `argCount <= 8` 用 `stackalloc` + 复制到 `Span<double>`。

---

### OPT-6：CompiledExpression 大量使用反射 GetMethod

**位置**：`MathEval/Optimization/CompiledExpression.cs` 多处

**问题**：多处 `typeof(...).GetMethod(...)` 在编译期（首次求值）执行。虽是 one-off 成本，但可用 `nameof` + 表达式树 `((Func<...>)TypeHelper.X).Method` 模式（部分已用）统一，提升可维护性并避免重载歧义。

**建议**：统一用 `((Func<...>)MethodGroup).Method` 模式，消除所有 `GetMethod` 调用。

---

### OPT-7：Expression.Eval 静态入口每次新建 ExpressionContext 并注册内置函数

**位置**：`MathEval/Expression.cs` L19-L23

**问题**：每次静态调用都 `new ExpressionContext()`（触发 ARCH-5 的 ~30 次 ConcurrentDictionary 插入）。对纯数值表达式 `Expression.Eval("1+2")` 是纯开销。

```csharp
public static T Eval<T>(string expression, ExpressionContext? context = null, ExpressionOptions options = ExpressionOptions.None) {
    context ??= new ExpressionContext();  // 每次新建 + 注册 ~30 个符号/函数
    var calculator = new Calculator(expression, context, options);
    return calculator.Eval<T>();
}
```

**建议**：提供无内置函数的轻量上下文路径，或缓存一个默认上下文模板（需注意线程安全）。

---

### OPT-8：OptimizedExpressionCache 与 ExpressionCache 缓存键含 Options，但 CompileOptimization 不改 AST

**位置**：`MathEval/Calculator.cs` L89-L106

**问题**：同一表达式 `ConstantFolding` 与 `ConstantFolding|CompileOptimization` 生成**相同** AST，却因 Options 不同存两份。`CompileOptimization` 仅影响是否编译为委托，不影响 AST 本身。

**建议**：把 AST 缓存键拆为「AST 影响选项（NoCache|ConstantFolding|DisableIndexPushdown）」与「编译影响选项（CompileOptimization）」两部分，AST 共享。

---

## 四、汇总表

| 编号 | 类别 | 严重度 | 位置 | 简述 |
|------|------|--------|------|------|
| BUG-A | BUG | 🔴 严重 | Optimization/CompiledExpression.cs | 编译模式丢失 And/Or 短路，与解释模式语义发散 |
| BUG-B | BUG | 🔴 严重 | TypeSystem/TypeHelper.cs vs Fast/BuiltIn/BuiltInOperators.cs | NaN 真值在主/Fast 相反 |
| BUG-C | BUG | 🔴 严重 | Fast/BuiltIn/BuiltInFunctions.cs + Core/FastScanner.cs | BUG-11 修复不完整，Fast 函数名/关键字仍大小写不敏感 |
| BUG-D | BUG | 🟠 中等 | Optimization/IndexPushdownOptimizer.cs | 对用户自定义非 element-wise 函数做不安全下推 |
| BUG-E | BUG | 🟠 中等 | Parser/Parser.cs | 深度限制对嵌套函数调用无效，可致栈溢出 |
| BUG-F | BUG | 🟠 中等 | Optimization/OptimizedExpressionCache.cs | SetCompiled 覆盖时丢失 AST |
| BUG-G | BUG | ⚪ 轻微 | Fast/VM/InstructionCache.cs | 静态缓存字段非 readonly、SetCapacity 非原子 |
| ARCH-1 | 架构 | 🟠 中等 | Internal/ExpressionCache + Optimization/OptimizedExpressionCache | 双缓存职责重叠且 Calculator 只用其一 |
| ARCH-2 | 架构 | 🟠 中等 | Optimization/CompiledExpression vs Visitors/EvaluationVisitor | 求值逻辑复制粘贴，聚合函数表三处重复 |
| ARCH-3 | 架构 | 🟠 中等 | MathEval vs MathEval.Fast | 两套独立实现无共享规约层 |
| ARCH-4 | 架构 | ⚪ 轻微 | Visitors/EvaluationVisitor.cs | 标量索引回退泄漏优化器实现细节到用户语义 |
| ARCH-5 | 架构 | ⚪ 轻微 | Context/ExpressionContext.cs | 每次构造都注册全部内置函数 |
| ARCH-6 | 架构 | ⚪ 轻微 | AST/ | AST 为可变 class，优化器跨节点共享实例 |
| OPT-1 | 优化 | — | Internal/LruCache.cs | GetOrAdd 持锁调用 factory，阻塞所有缓存操作 |
| OPT-2 | 优化 | — | Calculator.cs | EnsureCompiled 并发首跑重复编译 |
| OPT-3 | 优化 | — | Optimization/ConstantFolder.cs | 不折叠函数调用，且在 IndexPushdown 之前运行 |
| OPT-4 | 优化 | — | Fast/VM/BytecodeVM.cs | Call 指令每次 new double[] 堆分配 |
| OPT-5 | 优化 | — | Fast/Core/FastEvaluator.cs | CallBuiltInFunction Span 转 double[] 堆分配 |
| OPT-6 | 优化 | — | Optimization/CompiledExpression.cs | 大量反射 GetMethod 调用 |
| OPT-7 | 优化 | — | Expression.cs | 静态入口每次新建 ExpressionContext 并注册内置函数 |
| OPT-8 | 优化 | — | Optimization/OptimizedExpressionCache.cs | CompileOptimization 不改 AST 但导致 AST 重复缓存 |

---

## 五、优先级建议

### 第一优先级：行为正确性（必须修复）

1. **BUG-A**：编译模式短路求值丢失 — 解释/编译语义不等价，用户可能遭遇静默错误
2. **BUG-B**：NaN 真值相反 — 主/Fast 行为分歧，无交叉测试覆盖
3. **BUG-C**：Fast 大小写不敏感 — 与 BUG-11 修复决策矛盾

### 第二优先级：健壮性（应尽快修复）

4. **BUG-D**：IndexPushdown 对用户函数不安全下推 — 用户函数语义被静默篡改
5. **BUG-E**：Parser 深度限制绕过 — 可致进程崩溃
6. **BUG-F**：缓存 AST 丢失 — 导致重解析
7. **ARCH-2**：求值逻辑复制粘贴 — 防止未来同类 BUG（BUG-A 即因此产生）

### 第三优先级：架构改善（迭代改进）

8. **ARCH-1**：合并双缓存
9. **ARCH-3**：抽取共享规约层
10. **ARCH-4**：标量索引语义
11. **ARCH-5/6**：上下文注册优化 + AST 不可变

### 第四优先级：性能优化

12. **OPT-1~8**：按性能瓶颈优先级逐步优化

---

> 本报告新增 7 个 BUG（3 严重 + 3 中等 + 1 轻微）、6 个架构问题、8 个优化建议。与已有第三方报告的 14 个问题（均已修复）无重叠。
