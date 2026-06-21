# MathEval & MathEval.Fast 代码审查报告

> 对两个项目的所有源文件进行了逐行审查，并通过交叉验证确认了 14 个问题。按严重程度分级：严重 5 个、中等 4 个、轻微 5 个。

---

## 严重 BUG（5 个）

### BUG-1：FastEvaluator 链式短路求值导致解析崩溃

**位置**：`FastEvaluator.cs`（`EvalLogicalOr`、`EvalLogicalAnd`）

**问题**：短路触发时直接 `return 1.0`/`return 0.0`，未消费链中剩余的操作符。

```csharp
// EvalLogicalOr
if (!_skipMode && ConvertToBool(left)) {
    _skipMode = true;
    EvalLogicalAnd();      // 仅跳过当前右操作数
    _skipMode = false;
    return 1.0;            // 直接返回！链中剩余的 || 未被消费
}
```

**复现**：`1 || 2 || 3`
- `left = 1`（true）→ 短路，跳过 `2`，`return 1.0`
- 回到 `Evaluate()` 时 `|| 3` 仍未消费 → 抛出 `意外的字符 '|'`

**影响**：任何含 3 个及以上操作数的 `||`/`&&`/`or`/`and` 链在短路时都会崩溃。

---

### BUG-2：FastEvaluator skipMode 下位运算/算术运算未跳过

**位置**：`FastEvaluator.cs`（位运算方法、`EvalAdditive`）

**问题**：`_skipMode` 仅在 `EvalMultiplicative`、`EvalPower`、`LookupVariable` 中检查。位运算方法（`EvalBitwiseOr/Xor/And/Shift`）、`EvalAdditive`、`EvalEquality`、`EvalRelational` 均未检查，会执行实际运算。

```csharp
// EvalBitwiseOr - 无 _skipMode 检查
left = BuiltInOperators.BitwiseOr(left, right);  // skipMode 下仍调用
```

**复现**：`1 ? 2 : 3.5 | x`（x 未定义）
- true 分支求值 `2` 后，skipMode 跳过 false 分支
- false 分支 `3.5 | x`：`LookupVariable(x)` 返回 0，但 `BuiltInOperators.BitwiseOr(3.5, 0)` 调用 `ToInt64(3.5)` → **抛异常**（3.5 非整数）

**影响**：短路跳过的分支若含位运算/算术运算且操作数不合法，会错误抛异常。

---

### BUG-3：TypeHelper.ToInteger 仅支持 double 类型

**位置**：`TypeHelper.cs`（`ToInteger` 方法）

**问题**：`ToDouble` 支持 12 种数值类型，但 `ToInteger` 仅处理 `double`，其余类型直接抛异常。

```csharp
public static long ToInteger(object value, string operationName) {
    if (value is double d) {           // 仅处理 double
        if (d == Math.Truncate(d) && !double.IsInfinity(d) && !double.IsNaN(d))
            return (long)d;
    }
    throw new TypeMismatchException(...);  // int/long/float 等全部抛异常
}
```

**复现**：通过 `SetVariable("x", 5)`（int 类型）设置变量后执行 `x & 3`
- `EvaluateBinary` 调用 `ToInteger(left=5(int), "按位与")` → **抛 TypeMismatchException**

**影响**：任何通过 API 设置的非 double 整数变量参与位运算/移位都会失败。

---

### BUG-4：IndexPushdownOptimizer 无条件下推索引到函数参数，破坏聚合函数语义

**位置**：`IndexPushdownOptimizer.cs`

**问题**：将 `f(a)[i]` 无条件转换为 `f(a[i])`，对聚合函数（sum/max/min）语义错误。

```csharp
// f(a)[i] → f(a[i])  对所有函数一视同仁
ArrayIndexExpression { Array: FunctionCall func, Index: var idx }
    => Optimize(new FunctionCall(func.Name,
        func.Arguments.Select(arg => arg is ValueExpression
            ? arg
            : new ArrayIndexExpression(arg, idx)).ToList())),
```

**复现**：`max([a, b])[0]`（a、b 为数组变量）
- 原始语义：`max([a, b])` 返回 element-wise 最大值数组，`[0]` 取首元素
- 优化后：`max([a[0], b[0]])` 返回标量最大值
- 两者结果不同

**影响**：聚合函数与索引组合时结果错误。且此优化在 `Calculator.EnsureParsed` 中**无条件调用**，不受 `ExpressionOptions` 控制。

---

### BUG-5：CompiledExpression 编译模式缺少数组广播

**位置**：`CompiledExpression.cs`（`CompileFunctionCall` 方法）

**问题**：`CompileFunctionCall` 直接调用 `ExpressionFunction(object[])`，未实现 `EvaluationVisitor` 中的数组广播逻辑。

```csharp
// CompiledExpression - 无数组广播
var invokeExpr = LinqExpression.Invoke(funcVar, argsArrayVar);
```

对比解释模式：

```csharp
// EvaluationVisitor - 有数组广播
var arrayArg = args.FirstOrDefault(a => a is double[]);
if (arrayArg is double[] arr) {
    // element-wise 广播
}
```

**影响**：`CompileOptimization` 标志启用后，`sin([1,2,3])` 等数组函数调用行为与解释模式不一致，可能抛异常或返回错误结果。

---

## 中等 BUG（4 个）

### ~~BUG-6~~（已修复）：BytecodeVM 除零不抛异常，与主项目行为不一致

**位置**：`BytecodeVM.cs`、`BuiltInOperators.cs`（`IntegerDivide` 方法）、`TypeHelper.cs`

**问题**：~~主项目 `TypeHelper.cs` 提前检查除零并抛 `DivisionByZeroException`，而 Fast VM 直接执行 IEEE 754 标准除法返回 Infinity/NaN。~~

**处理**：已修改主项目 `TypeHelper.cs`，移除所有除零预处理检查，统一遵循 IEEE 754 标准。`1/0` → `Infinity`，`0/0` → `NaN`，不再抛出 `DivisionByZeroException`。

| 表达式 | MathEval 主项目 | MathEval.Fast VM |
|--------|----------------|------------------|
| `1/0`  | `Infinity`（原为 `DivisionByZeroException`） | `Infinity` |
| `1//0` | `Infinity`（原为 `DivisionByZeroException`） | `Infinity` |

---

### ~~BUG-7~~（已修复）：BuiltInOperators.Power 未校验操作数

**位置**：`BuiltInOperators.cs`（`Power` 方法）、`TypeHelper.cs`

**问题**：~~主项目 `TypeHelper.cs` 对负数非整数次幂、零的负次幂抛异常，与 IEEE 754 标准不一致。~~

**处理**：已修改主项目 `TypeHelper.cs`，移除所有乘方预处理检查，统一遵循 IEEE 754 标准。`(-2)^0.5` → `NaN`，`0^-1` → `Infinity`。

| 表达式 | MathEval 主项目 | MathEval.Fast |
|--------|----------------|---------------|
| `(-2) ^ 0.5` | `NaN`（原为 `EvaluateException`） | `NaN` |
| `0 ^ -1`     | `Infinity`（原为 `EvaluateException`） | `Infinity` |

---

### ~~BUG-8~~（已修复）：CompiledExpression.CompileArrayIndex 未检查越界

**位置**：`CompiledExpression.cs`（`CompileArrayIndex` 方法）

**问题**：~~编译为原生数组访问，越界抛 `IndexOutOfRangeException`，而非友好的 `EvaluateException`。~~

**处理**：已修改 `CompileArrayIndex`，在数组访问前增加越界检查，越界时抛出 `EvaluateException`，与解释模式行为一致。

```csharp
// Bounds check: throw friendly EvaluateException instead of IndexOutOfRangeException
var indexOutOfRange = LinqExpression.OrElse(
    LinqExpression.LessThan(indexVar, LinqExpression.Constant(0)),
    LinqExpression.GreaterThanOrEqual(indexVar, arrayLen));
...
var throwOutOfRange = LinqExpression.Throw(
    LinqExpression.New(evalExCtor, errorMsg),
    typeof(object));

var safeArrayAccess = LinqExpression.Condition(
    indexOutOfRange,
    throwOutOfRange,
    LinqExpression.ArrayIndex(...),
    typeof(object));
```

---

### ~~BUG-9~~（已修复）：EvaluationVisitor 多数组广播未校验长度一致

**位置**：`EvaluationVisitor.cs`（`Visit` `FunctionCall`）

**问题**：~~数组广播仅取第一个数组参数的长度，其他数组参数直接用 `da[i]` 访问，未校验长度。~~

```csharp
var arrayArg = args.FirstOrDefault(a => a is double[]);  // 只取第一个数组
if (arrayArg is double[] arr) {
    var result = new double[arr.Length];
    for (int i = 0; i < arr.Length; i++) {
        for (int j = 0; j < args.Length; j++) {
            scalarArgs[j] = args[j] is double[] da ? da[i] : args[j];  // da[i] 可能越界
        }
    }
}
```

**处理**：已同时在 `EvaluationVisitor.cs` 和 `CompiledExpression.cs`（`CallFunctionWithBroadcast`）的广播循环前增加数组长度一致性校验，长度不一致时抛出友好的 `EvaluateException`。新增了 3 个验证测试覆盖解释模式、编译模式和正常广播场景。

**复现**：`max([1,2,3], [1,2])` → i=2 时 `da[2]` 越界，抛 `IndexOutOfRangeException` 而非长度不匹配的友好错误。

---

## 轻微问题（5 个）

### BUG-10：BytecodeVM 返回值使用 stack[0] 而非 stack[sp-1]

**位置**：`BytecodeVM.cs`（`Execute` 方法末尾）

```csharp
return sp > 0 ? stack[0] : 0.0;  // 应为 stack[sp - 1]
```

正常编译的字节码最终 `sp=1`，两者等价。但若因编译器缺陷导致栈不平衡，`stack[0]` 会返回错误值。`stack[sp-1]` 是更安全、语义更正确的写法。

---

### BUG-11：FastScanner.ReadDecimal 未校验无效数字格式

**位置**：`FastScanner.cs`（`ReadDecimal` 方法）

**问题**：`1e`、`1e+`、`.` 等无效格式会被扫描器接受，随后 `double.Parse` 抛 `FormatException` 而非 `FastEvalException`，违反异常一致性。

---

### BUG-12：OptimizedExpressionCache.GetOrAddCompiled 存在竞态条件

**位置**：`OptimizedExpressionCache.cs`（`GetOrAddCompiled` 方法）

```csharp
if (entry.Compiled != null) return entry.Compiled;
var compiledExpr = compileFactory(entry.Ast!);
entry.Compiled = compiledExpr;  // 多线程下可能重复编译
```

多线程同时进入时可能重复执行 `compileFactory`，浪费资源（结果仍正确）。

---

### BUG-13：ExpressionCache 无容量限制，存在内存泄漏风险

**位置**：`ExpressionCache.cs`、`OptimizedExpressionCache.cs`

```csharp
private static readonly ConcurrentDictionary<string, LogicalExpression> _cache = new();
```

静态缓存无容量上限，求值大量不同表达式时缓存无限增长。建议引入 LRU 策略（参考 MathEval.Fast 的 `LruCache`）。

---

### BUG-14：TypeMismatchException 继承层次可能引起混淆

**位置**：`TypeMismatchException.cs`

`TypeMismatchException` 在求值过程中抛出（如 `ToInteger` 失败），但直接继承 `MathEvalException` 而非 `EvaluateException`。用户用 `catch (EvaluateException)` 捕获求值错误时会漏掉此异常。

> 注：`ExceptionTests.cs` 明确验证了此继承关系，应为有意设计。列为观察项供参考。

---

## 汇总

| 编号 | 严重程度 | 项目 | 位置 | 简述 |
|------|---------|------|------|------|
| BUG-1 | 🔴 严重 | Fast | FastEvaluator.cs | 链式短路未消费剩余操作符，导致崩溃 |
| BUG-2 | 🔴 严重 | Fast | FastEvaluator.cs | skipMode 下位运算/算术未跳过 |
| BUG-3 | 🔴 严重 | MathEval | TypeHelper.cs | ToInteger 仅支持 double 类型 |
| BUG-4 | 🔴 严重 | MathEval | IndexPushdownOptimizer.cs | 索引下推破坏聚合函数语义 |
| BUG-5 | 🔴 严重 | MathEval | CompiledExpression.cs | 编译模式缺数组广播 |
| BUG-6 | 🟠 中等 | Fast | BytecodeVM.cs / BuiltInOperators.cs | 除零不抛异常 |
| BUG-7 | 🟠 中等 | Fast | BuiltInOperators.cs | 幂运算未校验操作数 |
| BUG-8 | 🟠 中等 | MathEval | CompiledExpression.cs | 数组索引未检查越界 |
| BUG-9 | 🟠 中等 | MathEval | EvaluationVisitor.cs | 多数组广播未校验长度 |
| BUG-10 | ⚪ 轻微 | Fast | BytecodeVM.cs | 返回值 stack[0] 应为 stack[sp-1] |
| BUG-11 | ⚪ 轻微 | Fast | FastScanner.cs | 数字格式校验缺失 |
| BUG-12 | ⚪ 轻微 | MathEval | OptimizedExpressionCache.cs | 缓存竞态条件 |
| BUG-13 | ⚪ 轻微 | MathEval | ExpressionCache.cs | 缓存无容量限制 |
| BUG-14 | ⚪ 轻微 | MathEval | TypeMismatchException.cs | 异常继承层次（有意设计） |

---

> ✅ 审查报告已完成。如需修复其中任何 BUG，可从最严重的 BUG-1 开始逐一修复。
