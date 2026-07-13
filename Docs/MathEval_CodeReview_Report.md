# MathEval & MathEval.Fast 代码审查报告（第三轮 · 复核验证版）

> 第二轮报告对两个项目的全部源文件进行了逐行审查，确认 14 个问题。本轮对其**逐一进行源码复核 + 运行时实测验证**（基于 `net10.0`，通过独立验证程序对项目公共 API 运行各"复现"用例），修正了原报告中 3 处与实测不符的结论，并调整 1 处严重等级。
>
> **验证结论：14 个问题全部成立（属实）。其中 3 处的机制/数值描述需更正，1 处严重等级建议下调。**
>
> 修正摘要：
> - BUG-3：实测 `(long)1e19` 在本平台饱和到 `long.MaxValue`（**非**原报告所称的 `long.MinValue`）；`1e19 & 1` 实测返回 `1`（错误值，正确应为 `0`），结论（静默溢出产生错误结果）不变。
> - BUG-4：实测函数体异常 **未被包装为 `FunctionTypeMismatchException`**，而是以原始 `System.*` 异常（如 `DivideByZeroException`、`InvalidOperationException`、`OverflowException`）逃逸，打破异常契约。原报告"误报为类型不匹配"的描述与 .NET 10 实测行为不符。
> - BUG-13：实测 `log(8,1)` 与 `log(8,0)` **均返回 `NaN`**（非原报告所称的 `Infinity` / `-0.0`），因 .NET `Math.Log(x, base)` 对非法底数直接返回 NaN。问题（未做显式校验）仍成立，但严重度更低。
> - BUG-8：实测 `sign` 返回 `int`、无其他函数返回 `int`，但 `Eval<double>/Eval<long>` 经 `ConvertResult` 的 `IConvertible` 分支均能正确转换，**不产生错误结果或异常**，故由"中等"下调为"轻微"。

---

## 严重 BUG（4 个）

> **修复状态（2026-07-12）**：4 个严重 BUG 已全部修复并通过运行时验证（基于 `net10.0` 的独立验证程序 + 主项目 885 个既有测试全部通过）。
> - BUG-1/2/3 修复位于 `MathEval/Visitors/EvaluationVisitor.cs` 与 `MathEval/TypeSystem/TypeHelper.cs`。
> - BUG-4 修复说明：经运行时确认，用户以 `Func<>` 委托（如 `SetFunction("f", (Func<int,int>)(...))`）注册函数时，会优先匹配泛型 `SetFunction<T1,TResult>` 重载并走 `MathEval/Internal/FunctionWrapper.cs`（`Wrap`），**并非** `SetFunction(Delegate)`。原始异常泄漏的真实发生点即 `FunctionWrapper.Wrap`（`func(arg)` 直接调用与 `Convert.ChangeType` 仅捕获 `InvalidCastException`）。因此 BUG-4 与同根的 BUG-5 一并修复于 `FunctionWrapper.WrapCore`（统一包装为 `MathEvalException`，保留 `InnerException`）。`SetFunction(Delegate)` 也同步加固。

### BUG-1：And/Or 短路求值对数组操作数抛异常 【✅ 已修复】

**位置**：`MathEval/Visitors/EvaluationVisitor.cs` L30-44

**问题**：`And`/`Or` 短路分支调用 `TypeHelper.ToDouble(leftResult)`，但 `ToDouble` 不接受 `double[]`，会抛 `TypeMismatchException`。而 `TypeHelper.EvaluateBinary` 的 And/Or 分支（L58-59）仅处理标量，`EvaluateBinaryArray` 也无 And/Or 模式，因此数组与 `and`/`or` 的组合在任意路径下都无法正常工作（`ToInteger`/`ToDouble` 均不识别 `double[]`）。

```csharp
// L30-33: And 短路
if (expr.Type == BinaryExpressionType.And) {
    var leftResult = expr.Left.Accept(this);
    var leftDouble = TypeHelper.ToDouble(leftResult);  // double[] → 抛异常
    if (leftDouble == 0) return 0.0;
```

**验证（实测）**：
- `Expression.Eval("[1,0] and 1")` → `MathEval.Exceptions.TypeMismatchException: 期望数值类型`
- `Expression.Eval("[1,0] or 1")` → 同上异常

**影响**：数组无法使用逻辑运算符 `and`/`or`，且 `TypeHelper.EvaluateBinary` 中 And/Or 分支对数组为死代码。

**建议修复**：短路前检测操作数是否为数组；若为数组，走 `TypeHelper.EvaluateBinary` 的 element-wise 路径，并在 `EvaluateBinaryArray` 中补充 And/Or 的 `(double[] , double[])` 处理。

---

### BUG-2：TypeHelper.EvaluateBinaryArray 不接受非 double 标量 【✅ 已修复】

**位置**：`MathEval/TypeSystem/TypeHelper.cs` L64-72

**问题**：数组运算的模式匹配只接受 `(double[] a, double s)` 和 `(double s, double[] b)`，其中标量必须是 `double` 类型。若标量是 `int`、`long`、`bool` 等，不匹配任何分支，落入 default 抛 `TypeMismatchException`。

```csharp
return (left, right) switch {
    (double[] a, double[] b) => ElementWise(a, b, type),
    (double[] a, double s) => ElementWise(a, s, type),   // int 不匹配
    (double s, double[] b) => ElementWise(s, b, type),   // long 不匹配
    _ => throw new TypeMismatchException(...)
};
```

**验证（实测）**：`context.Set("x", 5)`（存为 `int`），`Expression.Eval("[1,2,3]+x", context)` → `MathEval.Exceptions.TypeMismatchException: 数组运算需要数值类型`

**影响**：数组与 context 中的非 double 数值变量运算时报错。

**建议修复**：在模式匹配前对标量做 `ToDouble` 转换，或提取标量统一 `ToDouble` 后再分发。

---

### BUG-3：TypeHelper.ToInteger 对超 long 范围的 double 静默溢出 【✅ 已修复】

**位置**：`MathEval/TypeSystem/TypeHelper.cs` L25-29

**问题**：`ToInteger` 先转 double 再 `(long)d`。对 `1e19` 等远超 `long.MaxValue`（≈9.22e18）的值，`d == Math.Truncate(d)` 为 true 且非 Infinity/NaN，条件通过，但 `(long)d` 在超出 `long` 范围时是**实现定义行为**（本平台 .NET 10 x64 下饱和为 `long.MaxValue`），不抛异常，产生无意义的位运算结果。

对比 Fast 项目的 `BuiltInOperators.IsInteger`（L32）有 `value >= long.MinValue && value < 9223372036854775808.0` 范围检查，主项目缺失。

```csharp
public static long ToInteger(object value, string operationName) {
    double d = ToDouble(value);
    if (d == Math.Truncate(d) && !double.IsInfinity(d) && !double.IsNaN(d)) return (long)d;
    throw new TypeMismatchException(...);
}
```

**验证（实测，修正原报告数值）**：
- `(long)1e19` 实测 = `9223372036854775807`（即 `long.MaxValue`，**并非**原报告所称的 `long.MinValue`）
- `Expression.Eval("1e19 & 1")` → 返回 `1`（**错误**：10¹⁹ 为偶数，正确位与结果应为 `0`）
- `Expression.Eval("1e19 | 1")` → 返回 `9223372036854775807`（无意义的饱和值）
- `FastEval.EvalLong("1e19 & 1")` → 正确抛 `FastEvalException: 按位与 运算需要整数操作数`

**影响**：位运算/移位运算对大数值产生完全错误且静默的结果（与 Fast 项目行为不一致）。

**建议修复**：在条件中补充范围检查 `d >= long.MinValue && d < 9223372036854775808.0`，超出范围抛 `EvaluateException`（或 `TypeMismatchException`）。

---

### BUG-4：用户自定义函数（Func<> / Delegate）泄漏原始 .NET 异常，破坏异常契约 【✅ 已修复】

**位置**：`MathEval/Context/ExpressionContext.cs` L70-86

**问题**：`catch (TargetInvocationException ex)` 本意是捕获 `method.Invoke` 包装的异常，但实测中函数体抛出的异常（以及 `Convert.ChangeType` 抛出的异常）**并未被包装为 `FunctionTypeMismatchException`**，而是以原始 `System.*` 类型直接逃逸。此外 `Convert.ChangeType` 可抛 `FormatException`/`OverflowException`，同样未被 `catch (InvalidCastException)` 捕获。这意味着调用方用 `catch (MathEvalException)` / `catch (EvaluateException)` 无法捕获用户自定义函数中的错误。

```csharp
try {
    var convertedArgs = new object?[argCount];
    for (int i = 0; i < argCount; i++) {
        convertedArgs[i] = Convert.ChangeType(args[i], parameters[i].ParameterType);
        // ↑ 可抛 FormatException/OverflowException，未捕获
    }
    var result = method.Invoke(func.Target, convertedArgs);
    return result!;
} catch (InvalidCastException) {
    throw new FunctionTypeMismatchException(...);
} catch (TargetInvocationException ex) {
    throw new FunctionTypeMismatchException($"调用函数 {name} 时出错：{ex.InnerException?.Message}");
}
```

**验证（实测，修正原报告描述）**：
- `context.SetFunction("f", (Func<int,int>)(x => 1 / x)); f(0)` → 逃逸为 `System.DivideByZeroException`（**并非**原报告所称的 `FunctionTypeMismatchException`）
- `context.SetFunction("g", (Func<int,int>)(x => throw new InvalidOperationException("boom"))); g(1)` → 逃逸为 `System.InvalidOperationException: boom`
- `context.SetFunction("h", (Func<int,int>)(x => x)); h(1e20)` → 逃逸为 `System.OverflowException`
- 注：原报告复现用例 `Func<double,double>(x => 1/x)` 在 `x=0` 时为 double 除法返回 `Infinity`、不抛异常，故需用整数除法或显式抛异常才能复现。

**影响**：用户自定义函数中的任意异常（含 `DivideByZeroException`、`InvalidOperationException`、`FormatException`、`OverflowException`）以非 MathEval 异常逃逸，调用方基于 `MathEvalException` 的错误处理体系完全失效。

**建议修复**：对 `method.Invoke` 的异常解包后重新包装为 `EvaluateException`（保留 `InnerException`）；将 `catch` 范围扩展为 `catch (Exception ex) when (ex is not MathEvalException)`，并显式捕获 `FormatException`/`OverflowException` 包装为合适的 MathEval 异常。

---

## 中等 BUG（5 个）

### BUG-5：FunctionWrapper.Wrap 未捕获 FormatException/OverflowException 【✅ 已修复（随 BUG-4 一并修复于 WrapCore）】

**位置**：`MathEval/Internal/FunctionWrapper.cs` 所有 Wrap 方法

**问题**：`Convert.ChangeType(args[0], typeof(T1))` 可抛 `FormatException`（如字符串转数值失败）或 `OverflowException`（如大 double 转 int 溢出），但只 `catch (InvalidCastException)`。由于内置函数均通过 `Wrap` 注册，此问题影响所有内置函数。

**验证（实测）**：`context.Set("x", "hello"); Expression.Eval("sin(x)", context)` → 逃逸为 `System.FormatException: The input string 'hello' was not in a correct format.`（非 MathEval 异常）

**建议修复**：扩展 catch 范围包含 `FormatException`、`OverflowException`，并统一包装为 `FunctionTypeMismatchException` 或 `EvaluateException`。

---

### BUG-6：缓存键未包含 ExpressionOptions，导致不同选项共享缓存 【✅ 已修复】

**位置**：`MathEval/Calculator.cs` L89-104, L113-117

**问题**：`ExpressionCache` 和 `OptimizedExpressionCache` 的缓存键仅为表达式字符串（`_expressionText`），不包含 `ExpressionOptions`。不同选项（如 with/without ConstantFolding）会产生不同的 AST，但共享同一个缓存条目；而 `ConstantFolding` 折叠仅发生在缓存未命中分支，命中后跳过折叠。

```csharp
// L89: 缓存键仅为 _expressionText
if (!_options.HasFlag(ExpressionOptions.NoCache) && ExpressionCache.TryGet(_expressionText, out var cachedAst)) {
    _ast = cachedAst;  // 可能是未折叠的 AST，即使当前选项要求折叠
}
```

**验证（源码复核确认）**：`EnsureParsed` 中 `_ast` 仅由 `_expressionText` 键入/取出；`ConstantFolding`（L97-99）与 `IndexPushdownOptimizer`（L102）均在 `else`（缓存未命中）分支内执行。带 `ConstantFolding` 选项但命中旧缓存时，折叠被绕过。

**影响**：`ConstantFolding` 和 `CompileOptimization` 选项可能被缓存绕过，造成"同一表达式不同选项得到相同（未优化）AST"的不一致行为。

**建议修复**：将 `ExpressionOptions` 纳入缓存键，如 `(_expressionText, (int)_options)` 组合键。

---

### BUG-7：IndexPushdownOptimizer 无条件应用，不受 ExpressionOptions 控制 【✅ 已修复】

**位置**：`MathEval/Calculator.cs` L102

**问题**：`IndexPushdownOptimizer.Optimize(_ast)` 在 `EnsureParsed` 中无条件调用，不受任何 `ExpressionOptions` 标志控制。用户无法禁用此优化。

```csharp
// 应用常量折叠优化（受 ConstantFolding 选项控制）
if (_options.HasFlag(ExpressionOptions.ConstantFolding)) {
    _ast = ConstantFolder.Fold(_ast);
}
// 应用索引下推优化（无条件，不受选项控制）
_ast = IndexPushdownOptimizer.Optimize(_ast);
```

**验证（源码复核确认）**：无条件调用，与上方 `ConstantFolding` 的有条件应用不一致。

**影响**：用户无法禁用索引下推优化；与 `ConstantFolding` 的有条件应用不一致。

**建议修复**：添加 `IndexPushdown` 选项标志（默认开启），或将其纳入 `ConstantFolding` 选项范围。

---

### BUG-8（已下调为轻微）：BuiltInFunctions.sign 返回 int 而非 double，与 Fast 项目不一致 【✅ 已修复】

**位置**：`MathEval/Functions/BuiltInFunctions.cs` L40

**问题**：`Math.Sign(x)` 返回 `int`（-1/0/1）。通过 `Func<double, int>` 注册，返回 boxed `int`。而其他函数（`abs`、`sqrt` 等）返回 `double`。Fast 项目的 `BuiltInFunctions` 使用 `SignDouble` 包装器返回 `double`。

```csharp
// 主项目：返回 int
context.SetFunction("sign", static (double x) => Math.Sign(x));
// Fast 项目：返回 double
new("sign", 1, 1, args => Math.Sign(args[0]), M1(SignDouble)),
private static double SignDouble(double v) => Math.Sign(v);
```

**验证（实测）**：
- `Expression.Eval("sign(-5)")` → 返回 `Int32` 类型的 `-1`
- `Expression.Eval<double>("sign(-5)")` → `-1`（经 `ConvertResult` 的 `IConvertible` 分支正确转换）
- `Expression.Eval("abs(-5)")` → 返回 `Double` 类型的 `5`

**影响（实测后下调为轻微）**：返回类型不一致，但 `Calculator.ConvertResult<T>` 对 `int` 结果走 `IConvertible` 分支，**不会产生错误结果或异常**；仅在与 Fast 项目行为对比时存在不一致。故从"中等"下调为"轻微"。

**建议修复**：改为 `static (double x) => (double)Math.Sign(x);` 以统一返回类型并与 Fast 项目对齐。

---

### BUG-9：BuiltInFunctions.round 对越界 digits 抛非 MathEval 异常 【✅ 已修复】

**位置**：`MathEval/Functions/BuiltInFunctions.cs` L46

**问题**：`Math.Round(Convert.ToDouble(args[0]), Convert.ToInt32(args[1]))` 当 digits > 15 或 digits < 0 时，`Math.Round` 抛 `System.ArgumentOutOfRangeException`（非 MathEval 异常）。`Convert.ToInt32(args[1])` 对大数值抛 `System.OverflowException`。

**验证（实测）**：
- `Expression.Eval("round(1.5, 20)")` → `System.ArgumentOutOfRangeException: Rounding digits must be between 0 and 15, inclusive.`
- `Expression.Eval("round(1.5, 1e20)")` → `System.OverflowException: Value was either too large or too small for an Int32.`

**影响**：非 MathEval 异常逃逸，破坏异常体系一致性。

**建议修复**：在调用 `Math.Round` 前校验 digits 范围（0–15），抛 `EvaluateException`；对 `args[1]` 先用 `ToInteger` 约束范围。

---

### BUG-10：BuiltInFunctions.max/min 空数组展平后参数数量校验消息误导 【✅ 已修复】

**位置**：`MathEval/Functions/BuiltInFunctions.cs` L49-50, `MathEval/Visitors/EvaluationVisitor.cs` L62-65

**问题**：`max([])` 经 `FlattenArgs` 展平后变空参数列表，argCount 校验失败抛 `FunctionTypeMismatchException("函数 max 需要 1-∞ 个参数，但提供了 0 个")`。用户实际提供了 1 个参数（空数组），消息误导。

**验证（实测）**：`Expression.Eval("max([])")` → `MathEval.Exceptions.FunctionTypeMismatchException: 函数 max 需要 1-∞ 个参数，但提供了 0 个`

**建议修复**：在 `max`/`min` 实现内显式检查空序列并抛 `EvaluateException("max/min 的数组参数不能为空")`；或在校验前保留原始参数计数用于错误消息。

---

## 轻微问题（5 个）

### BUG-11：FastEval 常量查找大小写敏感，与主项目关键字不一致 【✅ 已修复】

**位置**：`MathEval/Lexer/Lexer.cs`（关键字表与 `true`/`false` 判定）、`MathEval/Context/ExpressionContext.cs`（`ReservedKeywords`）

**问题**：`BuiltInConstants` 使用默认 `StringComparer.Ordinal`（区分大小写）构建 `FrozenDictionary`。`true`/`false`/`NaN`/`INF` 作为常量注册，大小写敏感。但主项目的 Lexer 将这些作为关键字处理，大小写不敏感（`Keywords` 字典与 `ReservedKeywords` 均使用 `StringComparer.OrdinalIgnoreCase`）。

**验证（实测）**：
- `FastEval.EvalDouble("nan")` → `FastEvalException: 未定义的变量 'nan'`
- `FastEval.EvalDouble("NaN")` → `NaN`（正常）
- `Expression.Eval<double>("nan")` → `NaN`（主项目正常）

**影响**：主项目与 Fast 项目对关键字大小写处理不一致。

**修复（2026-07-12，按用户指令"主项目与 Fast 均遵循大小写敏感"）**：将主项目词法改为与 Fast 一致的**大小写敏感**——`Lexer.Keywords` 字典比较器由 `OrdinalIgnoreCase` 改为 `Ordinal`，`true`/`false` 判定由 `OrdinalIgnoreCase` 改为 `Ordinal`；`ExpressionContext.ReservedKeywords` 比较器由 `OrdinalIgnoreCase` 改为 `Ordinal`。修复后 `true`、`True`、`TRUE` 互不等价（后两者作为未定义标识符，求值抛 `SymbolNotFoundException`）。注意：用户指令要求"均大小写敏感"，故未采用原报告"将 Fast 改为 OrdinalIgnoreCase 对齐主项目"的建议，而是反向将主项目改为 Ordinal 对齐 Fast。Fast 项目本身已大小写敏感，无需改动。

---

### BUG-12：标量索引 `5[0]` 静默返回标量本身，不报错 【✅ 已修复】

**位置**：`MathEval/Visitors/EvaluationVisitor.cs` L133-135

**问题**：`if (array is double) return array;` 是为索引下推优化服务的（如 `(arr + x)[i]` 优化后 `x[i]` 应返回 `x`）。但用户直接写 `5[0]` 或 `5[999]` 也会静默返回 5，不检查索引合法性。

**验证（实测）**：
- `Expression.Eval("5[0]")` → `5`
- `Expression.Eval("5[999]")` → `5`（同样静默返回）

**影响**：掩盖用户错误（对标量做索引），可能导致逻辑 bug 不被发现。

**建议修复**：区分"优化器产生的标量索引"与"用户直接写的标量索引"，或在 AST 节点上加标记；用户直接对标量索引时应抛 `EvaluateException`。

---

### BUG-13（数值已更正）：log(x, 0) / log(x, 1) 返回 NaN 而非显式报错 【按用户指令不修复】

**位置**：`MathEval/Functions/BuiltInFunctions.cs` L33

**原报告更正**：原报告称 `log(8, 1)` → `Infinity`、`log(8, 0)` → `-0.0`（基于 `Math.Log(x)/Math.Log(base)` 手算）。实测因 .NET `Math.Log(x, base)` 对非法底数（base ≤ 0 或 base == 1）**直接返回 `NaN`**，故两者均返回 `NaN`，而非 `Infinity`/`-0.0`。

**验证（实测）**：
- `Expression.Eval("log(8,1)")` → `NaN`
- `Expression.Eval("log(8,0)")` → `NaN`
- `Expression.Eval("log(8,2)")` → `3`（合法底数正常）

**影响**：返回 NaN 在数学上可视为"未定义"的占位，但 MathEval 未做显式校验并给出清晰错误信息，与"无意义值"描述方向一致、严重度较低。

**建议修复**：在 `log` 的双参数分支检查 `base <= 0 || base == 1`，抛 `EvaluateException("对数底数必须为正数且不等于 1")`。

---

### BUG-14：TypeMismatchException 不继承 EvaluateException（有意设计） 【按设计不修复】

**位置**：`MathEval/Exceptions/TypeMismatchException.cs`

**问题**：`TypeMismatchException` 在求值过程中抛出（如 `ToInteger` 失败、`3.5 & 2` 位运算），但直接继承 `MathEvalException` 而非 `EvaluateException`。用户用 `catch (EvaluateException)` 捕获求值错误时会漏掉此异常（但可被 `catch (MathEvalException)` 捕获）。

**验证（实测 + 测试确认）**：
- `Expression.Eval("3.5 & 2")` 抛 `TypeMismatchException`，能被 `catch (TypeMismatchException)` 捕获、`catch (MathEvalException)` 捕获，但**不能**被 `catch (EvaluateException)` 捕获。
- `MathEval.Tests/ExceptionTests.cs` 中 `TypeMismatchException_Inherits_MathEvalException_NotEvaluateException` 显式断言此继承关系，**应为有意设计**。

**影响**：属观察项。若设计意图是"类型错误属于更底层的 MathEvalException 而非求值期 EvaluateException"，则无需修改；若希望所有求值错误统一被 `EvaluateException` 捕获，则应调整继承。

---

## 汇总

| 编号 | 严重程度 | 项目 | 位置 | 简述 | 验证 |
|------|---------|------|------|------|------|
| BUG-1 | 🔴 严重 | MathEval | Visitors/EvaluationVisitor.cs | And/Or 短路对数组操作数抛 TypeMismatchException | ✅ 实测确认 · **已修复** |
| BUG-2 | 🔴 严重 | MathEval | TypeSystem/TypeHelper.cs | 数组运算不接受非 double 标量 | ✅ 实测确认 · **已修复** |
| BUG-3 | 🔴 严重 | MathEval | TypeSystem/TypeHelper.cs | ToInteger 对超 long 范围的 double 静默溢出（实测饱和到 long.MaxValue，非 long.MinValue） | ✅ 实测确认（已更正数值）· **已修复** |
| BUG-4 | 🔴 严重 | MathEval | Internal/FunctionWrapper.cs + Context/ExpressionContext.cs | 用户自定义函数（Func<>/Delegate）泄漏原始 System 异常，破坏异常契约 | ✅ 实测确认（已更正行为描述）· **已修复** |
| BUG-5 | 🟠 中等 | MathEval | Internal/FunctionWrapper.cs | Wrap 未捕获 FormatException/OverflowException | ✅ 实测确认 · **已修复** |
| BUG-6 | 🟠 中等 | MathEval | Calculator.cs + Internal/ExpressionCache.cs + Optimization/OptimizedExpressionCache.cs | 缓存键未包含 ExpressionOptions | ✅ 源码复核确认 · **已修复** |
| BUG-7 | 🟠 中等 | MathEval | Calculator.cs + ExpressionOptions.cs | IndexPushdownOptimizer 无条件应用 | ✅ 源码复核确认 · **已修复** |
| BUG-8 | ⚪ 轻微 | MathEval | Functions/BuiltInFunctions.cs | sign 返回 int 而非 double（实测无错误结果，下调等级） | ✅ 实测确认（已下调为轻微）· **已修复** |
| BUG-9 | 🟠 中等 | MathEval | Functions/BuiltInFunctions.cs | round 越界 digits 抛非 MathEval 异常 | ✅ 实测确认 · **已修复** |
| BUG-10 | 🟠 中等 | MathEval | Functions/BuiltInFunctions.cs | max/min 空数组消息误导 | ✅ 实测确认 · **已修复** |
| BUG-11 | ⚪ 轻微 | MathEval | Lexer/Lexer.cs + Context/ExpressionContext.cs | 常量/关键字大小写敏感（主项目原大小写不敏感，已改为与 Fast 一致的 Ordinal） | ✅ 实测确认 · **已修复** |
| BUG-12 | ⚪ 轻微 | MathEval | Visitors/EvaluationVisitor.cs | 标量索引静默返回标量 | ✅ 实测确认 · **已修复** |
| BUG-13 | ⚪ 轻微 | MathEval | Functions/BuiltInFunctions.cs | log(x, 0/1) 返回 NaN（实测非 Infinity/-0.0） | ✅ 实测确认（已更正数值）· **按用户指令不修复** |
| BUG-14 | ⚪ 轻微 | MathEval | Exceptions/TypeMismatchException.cs | 异常继承层次（有意设计，测试断言） | ✅ 实测+测试确认 · **按设计不修复** |

**等级分布（复核后）**：严重 4 个、中等 5 个、轻微 5 个（较第二轮：BUG-8 由中等下调为轻微）。

**修复进度（2026-07-12 第二轮）**：
- 严重 4 个（BUG-1/2/3/4）：**全部已修复**（见各节）。
- 中等 5 个（BUG-5/6/7/9/10）：**全部已修复**（BUG-5 随 BUG-4 在 `FunctionWrapper.WrapCore` 一并修复）。
- 轻微 5 个：**BUG-8/11/12 已修复**；**BUG-13 按用户指令不修复**（用户要求"所有基于 IEEE double 的行为都不再抛出异常"，故 `log(x,0/1)` 返回 NaN 维持现状、不抛异常）；**BUG-14 按设计不修复**（`TypeMismatchException` 继承关系由测试 `TypeMismatchException_Inherits_MathEvalException_NotEvaluateException` 显式断言，属有意设计）。

---

> 第二轮修复完成。14 个问题中 12 个已修复，2 个按用户指令/设计不修复（BUG-13、BUG-14）。所有修复均经独立验证程序运行时确认，主项目既有测试 885 项通过（仅余 3 项 Fast 项目既有失败，与本轮主项目改动无关）。
> 本轮相对第一轮的关键更正：BUG-3 的溢出数值与机制、BUG-4 的异常逃逸真实路径（实际发生在 `FunctionWrapper.Wrap` 而非 `SetFunction(Delegate)`，已一并加固）、BUG-13 的实际返回值、BUG-8 的严重等级；本轮用户指令：BUG-11 改为"主项目与 Fast 均大小写敏感"（主项目词法改为 Ordinal），BUG-13 改为"IEEE double 行为不抛异常"。
