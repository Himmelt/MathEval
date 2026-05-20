# 函数重载性能开销研究

> **研究主题**：C# 方法重载（Overload）解析的性能开销及其对 MathEval 的影响
> **研究日期**：2026-05-20
> **研究目的**：评估 MathEval 中函数重载机制的性能影响，为后续优化提供决策依据

---

## 目录

1. [研究背景](#1-研究背景)
2. [C# 方法重载解析机制](#2-c-方法重载解析机制)
3. [MathEval 中的重载相关代码分析](#3-matheval-中的重载相关代码分析)
4. [性能开销分类与量化](#4-性能开销分类与量化)
5. [替代方案对比](#5-替代方案对比)
6. [对 MathEval 的优化建议](#6-对-matheval-的优化建议)
7. [结论](#7-结论)

---

## 1. 研究背景

MathEval 的 `ExpressionContext` 类提供了多个 `SetFunction` 重载方法，用于注册不同签名的函数：

```csharp
// 弱类型重载
void SetFunction(string name, ExpressionFunction func);

// Delegate 重载
void SetFunction(string name, Delegate func);

// 强类型泛型重载（1~8 个参数）
void SetFunction<T1, TResult>(string name, Func<T1, TResult> func);
void SetFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> func);
// ... 一直到 8 个参数
```

同时，`Set` 方法也存在重载：

```csharp
void Set(string name, object value);       // 直接值
void Set(string name, Func<object> value); // 延迟值
```

需要研究的关键问题：

1. **编译期重载解析开销**：C# 编译器在编译时确定调用哪个重载，运行时无额外开销
2. **运行时类型检查开销**：`FunctionWrapper.Wrap` 中的 `Convert.ChangeType` 和类型转换
3. **Delegate 重载的反射开销**：`SetFunction(string, Delegate)` 中的 `Method.Invoke` 调用
4. **泛型特化开销**：`FunctionWrapper` 的泛型方法在 JIT 编译后的实际表现

---

## 2. C# 方法重载解析机制

### 2.1 编译期解析（静态分派）

C# 的方法重载解析在**编译期**完成，属于静态分派。编译器根据参数的**静态类型**选择最佳重载：

```csharp
// 编译期确定调用 SetFunction(string, ExpressionFunction)
context.SetFunction("sqrt", args => Math.Sqrt(Convert.ToDouble(args[0])));

// 编译期确定调用 SetFunction<T1, TResult>(string, Func<T1, TResult>)
context.SetFunction("doubleIt", (Func<double, double>)(x => x * 2));

// 编译期确定调用 SetFunction(string, Delegate)
context.SetFunction("add", (Delegate)(Func<double, double, double>)((a, b) => a + b));
```

**关键结论**：编译期重载解析**零运行时开销**。JIT 生成的代码直接调用目标方法，不涉及任何运行时查找或分派。

### 2.2 重载解析的编译期成本

| 阶段 | 开销 | 影响范围 |
|------|------|----------|
| 编译期方法查找 | O(M) 其中 M 为同名重载数量 | 仅影响编译速度，不影响运行时 |
| 类型转换隐式插入 | 编译器可能插入隐式转换 | 运行时有转换开销，但与重载无关 |
| 泛型类型推断 | 编译期推断泛型参数 | 无运行时开销 |

### 2.3 与虚方法分派的区别

| 机制 | 解析时机 | 运行时开销 | 示例 |
|------|----------|------------|------|
| 方法重载 | 编译期 | **零** | `SetFunction("f", func)` |
| 虚方法分派 | 运行时 | vtable 查找 (~2-5ns) | `visitor.Visit(expr)` |
| 动态分派 (dynamic) | 运行时 | 反射缓存查找 (~50-200ns) | `dynamicObj.Method()` |
| 接口分派 | 运行时 | vtable 查找 (~2-5ns) | `IEnumerable.GetEnumerator()` |

**结论**：方法重载本身不引入任何运行时性能开销。

---

## 3. MathEval 中的重载相关代码分析

### 3.1 SetFunction 调用链分析

#### 路径 A：弱类型注册（ExpressionFunction）

```
SetFunction(string, ExpressionFunction)
  → _functions[name] = func    // ConcurrentDictionary 赋值
```

**开销**：仅字典操作，无额外转换。

#### 路径 B：强类型泛型注册（Func<,>）

```
SetFunction<T1, TResult>(string, Func<T1, TResult>)
  → FunctionWrapper.Wrap(func)           // 创建闭包
  → SetFunction(string, ExpressionFunction)  // 存入字典
```

**开销**：闭包创建 + 字典操作。`Wrap` 方法创建一个 `ExpressionFunction` 委托，内部包含：
- 参数数量检查
- `Convert.ChangeType` 类型转换
- 调用原始 `Func<T1, TResult>`
- 异常处理

#### 路径 C：Delegate 注册

```
SetFunction(string, Delegate)
  → 内联 lambda: args => { ... method.Invoke(...) ... }
  → SetFunction(string, ExpressionFunction)
```

**开销**：反射调用 `Method.Invoke` + `Convert.ChangeType` × N 次。这是**最慢**的路径。

### 3.2 运行时求值路径分析

函数调用在求值时的路径：

```
EvaluationVisitor.Visit(FunctionCall)
  → _context.TryGetFunction(name, out func)    // ConcurrentDictionary 查找
  → func(args.ToArray())                        // 调用 ExpressionFunction 委托
```

**关键观察**：无论注册时使用哪个 `SetFunction` 重载，求值时都统一调用 `ExpressionFunction(object[])` 委托。重载的差异仅体现在**注册时创建的闭包内部逻辑**。

### 3.3 FunctionWrapper.Wrap 的运行时开销

以 `Wrap<T1, TResult>` 为例：

```csharp
public static ExpressionFunction Wrap<T1, TResult>(Func<T1, TResult> func) {
    return args => {
        if (args.Length != 1)                              // 1. 参数数量检查
            throw new FunctionTypeMismatchException(...);
        try {
            var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));  // 2. 类型转换
            var result = func(arg1);                                 // 3. 调用原始函数
            return result!;                                          // 4. 返回结果
        } catch (InvalidCastException) {
            throw new FunctionTypeMismatchException(...);
        }
    };
}
```

**每次函数调用的开销**：

| 步骤 | 操作 | 预估耗时 |
|------|------|----------|
| 参数数量检查 | 整数比较 | ~1ns |
| Convert.ChangeType | 反射式类型转换 | ~20-50ns |
| 调用原始函数 | 委托调用 | ~2-5ns |
| 返回结果 | 引用返回 | ~0ns |
| **合计** | | **~25-55ns** |

### 3.4 Delegate 路径的额外开销

```csharp
SetFunction(string name, Delegate func) {
    var method = func.Method;                    // 反射获取 MethodInfo
    var parameters = method.GetParameters();     // 反射获取参数信息
    var argCount = parameters.Length;

    SetFunction(name, args => {
        var convertedArgs = new object?[argCount];
        for (int i = 0; i < argCount; i++) {
            convertedArgs[i] = Convert.ChangeType(args[i], parameters[i].ParameterType);
        }
        var result = method.Invoke(func.Target, convertedArgs);  // 反射调用
        return result!;
    });
}
```

**每次函数调用的额外开销**：

| 步骤 | 操作 | 预估耗时 |
|------|------|----------|
| 数组分配 | `new object?[N]` | ~10-20ns + GC 压力 |
| Convert.ChangeType × N | 反射式类型转换 | ~20-50ns × N |
| method.Invoke | 反射调用 | ~100-300ns |
| **合计（2参数）** | | **~150-420ns** |

---

## 4. 性能开销分类与量化

### 4.1 开销来源分类

| 类别 | 来源 | 是否可避免 | 量级 |
|------|------|------------|------|
| **编译期重载解析** | C# 编译器选择重载 | 不可避免，但无运行时开销 | 0ns |
| **Convert.ChangeType** | 强类型参数转换 | 可优化 | 20-50ns/参数 |
| **method.Invoke 反射** | Delegate 路径 | 可避免 | 100-300ns |
| **数组分配** | Delegate 路径的 convertedArgs | 可避免 | 10-20ns + GC |
| **ConcurrentDictionary 查找** | 函数名查找 | 不可避免（核心功能） | ~20-40ns |
| **args.ToArray()** | EvaluationVisitor 中参数数组创建 | 可优化 | ~10-20ns + GC |

### 4.2 与整体求值开销的对比

以表达式 `sqrt(x) + sin(y)` 为例，一次求值的典型开销分解：

| 阶段 | 操作 | 预估耗时 |
|------|------|----------|
| Lexer + Parser | 首次解析（后续走缓存） | ~5,000-20,000ns |
| AST 缓存查找 | ConcurrentDictionary | ~20-40ns |
| EvaluationVisitor 遍历 | 递归遍历 AST | ~100-200ns |
| 符号查找 × 2 | ConcurrentDictionary | ~40-80ns |
| 函数查找 × 2 | ConcurrentDictionary | ~40-80ns |
| Convert.ChangeType × 2 | 参数类型转换 | ~40-100ns |
| Math.Sqrt + Math.Sin | 实际数学计算 | ~5-10ns |
| args.ToArray() × 2 | 参数数组创建 | ~20-40ns + GC |
| **合计（缓存命中）** | | **~250-550ns** |

**结论**：`Convert.ChangeType` 占求值总时间的约 **10-20%**，是值得优化的目标。Delegate 路径的 `method.Invoke` 开销更大，应避免使用。

### 4.3 Convert.ChangeType 的内部实现

```csharp
// System.Convert.ChangeType 的简化逻辑
public static object ChangeType(object value, Type conversionType) {
    if (value == null) {
        if (conversionType.IsValueType) throw new InvalidCastException();
        return null;
    }
    var sourceType = value.GetType();
    if (sourceType == conversionType) return value;  // 快速路径
    // IConvertible 路径：大量 switch/if 分支
    if (value is IConvertible) {
        return conversionType.GetTypeCode() switch {
            TypeCode.Int64 => ((IConvertible)value).ToInt64(null),
            TypeCode.Double => ((IConvertible)value).ToDouble(null),
            // ... 其他类型
        };
    }
    throw new InvalidCastException();
}
```

**问题**：
1. 装箱/拆箱：`long` 和 `double` 值类型在 `object[]` 传递中被装箱
2. 类型检查：每次调用都进行 `sourceType == conversionType` 比较
3. IConvertible 分派：通过 `GetTypeCode()` + switch 分派

---

## 5. 替代方案对比

### 5.1 方案 A：直接类型检查替代 Convert.ChangeType

```csharp
// 当前实现
var arg1 = (T1)Convert.ChangeType(args[0], typeof(T1));

// 优化实现
public static ExpressionFunction Wrap<T1, TResult>(Func<T1, TResult> func) {
    return args => {
        if (args.Length != 1)
            throw new FunctionTypeMismatchException(...);
        T1 arg1;
        try {
            arg1 = args[0] is T1 t1 ? t1 : (T1)Convert.ChangeType(args[0], typeof(T1));
        } catch (InvalidCastException) {
            throw new FunctionTypeMismatchException(...);
        }
        return func(arg1)!;
    };
}
```

**优势**：当参数类型匹配时（如 `double` → `double`），跳过 `Convert.ChangeType`，直接拆箱。
**预估提升**：类型匹配场景下节省 ~20-40ns/参数。

### 5.2 方案 B：泛型特化函数签名

参考 MathEvaluator (AntonovAnton) 的做法，使用 `INumberBase<T>` 约束：

```csharp
// MathEvaluator 的做法
public void BindFunction<T>(Func<T, T> fn, char key)
    where T : struct, INumberBase<T>
```

**优势**：
- 编译期确定数值类型，无需运行时转换
- `INumberBase<T>` 提供统一的运算接口
- 值类型无装箱开销

**劣势**：
- 要求 .NET 7+（`INumberBase<T>` 在 .NET 7 引入）
- MathEval 当前目标为 .NET 10+，此约束可接受
- 需要重构整个类型系统

### 5.3 方案 C：表达式编译消除运行时转换

```csharp
// 在编译优化阶段，为强类型函数生成直接调用的表达式树
var argExpr = Expression.Convert(argsExpr[i], typeof(T1));
var callExpr = Expression.Call(funcMethod, argExpr);
```

**优势**：编译后完全消除类型检查和转换开销。
**预估提升**：函数调用路径节省 ~30-60ns。

### 5.4 方案 D：消除 Delegate 路径

移除 `SetFunction(string, Delegate)` 重载，强制用户使用强类型 `Func<>` 或弱类型 `ExpressionFunction`：

```csharp
// 移除
// void SetFunction(string name, Delegate func);

// 保留
void SetFunction(string name, ExpressionFunction func);
void SetFunction<T1, TResult>(string name, Func<T1, TResult> func);
```

**优势**：消除最慢的反射调用路径。
**劣势**：减少 API 灵活性。

### 5.5 方案对比总结

| 方案 | 实现复杂度 | 性能提升 | 兼容性 | 推荐度 |
|------|-----------|----------|--------|--------|
| A: is 模式匹配快速路径 | 低 | 10-20% | 完全兼容 | ⭐⭐⭐⭐⭐ |
| B: INumberBase<T> 重构 | 高 | 40-60% | 需 .NET 7+ | ⭐⭐⭐ |
| C: 编译优化消除转换 | 中 | 30-50% | 完全兼容 | ⭐⭐⭐⭐ |
| D: 移除 Delegate 路径 | 低 | 消除最差路径 | 破坏性变更 | ⭐⭐ |

---

## 6. 对 MathEval 的优化建议

### 6.1 短期优化（低风险，立即可行）

#### 6.1.1 FunctionWrapper 添加 is 模式匹配快速路径

在 `Wrap` 方法中，优先使用 `is` 模式匹配进行直接类型转换，仅在类型不匹配时回退到 `Convert.ChangeType`：

```csharp
public static ExpressionFunction Wrap<T1, TResult>(Func<T1, TResult> func) {
    return args => {
        if (args.Length != 1)
            throw new FunctionTypeMismatchException(...);
        try {
            var arg1 = args[0] is T1 t1
                ? t1
                : (T1)Convert.ChangeType(args[0], typeof(T1));
            return func(arg1)!;
        } catch (InvalidCastException) {
            throw new FunctionTypeMismatchException(...);
        }
    };
}
```

**预期效果**：当参数类型与注册类型一致时（最常见场景），跳过 `Convert.ChangeType`，节省 ~20-40ns/参数。

#### 6.1.2 内置函数避免 object[] 传递

当前内置函数（如 `abs`、`max`、`min`）在 `BuiltInFunctions.Register` 中已经使用了 `is long`/`is double` 模式匹配，这是正确的做法。但它们仍然通过 `ExpressionFunction(object[])` 委托调用，存在 `args.ToArray()` 的数组分配开销。

**建议**：在 `EvaluationVisitor.Visit(FunctionCall)` 中，对内置函数使用栈分配：

```csharp
// 优化前
var args = new List<object>();
foreach (var arg in expr.Arguments) {
    args.Add(arg.Accept(this));
}
return func(args.ToArray());

// 优化后（对参数数量已知的情况）
if (expr.Arguments.Count <= 8) {
    Span<object> args = stackalloc object[expr.Arguments.Count];
    for (int i = 0; i < expr.Arguments.Count; i++) {
        args[i] = expr.Arguments[i].Accept(this);
    }
    return func(args.ToArray()); // 仍需 ToArray，但避免了 List 开销
}
```

**注意**：`Span<object>` 无法直接传递给 `ExpressionFunction(object[])`，需要进一步重构接口才能完全消除堆分配。

### 6.2 中期优化（中等风险，需测试验证）

#### 6.2.1 编译优化中内联函数调用

在 `CompiledExpression` 中，对强类型注册的函数生成直接调用的表达式树，避免 `ExpressionFunction` 委托间接调用和 `Convert.ChangeType`：

```csharp
// 当前：通过 ExpressionFunction 委托调用
var invokeExpr = Expression.Invoke(funcVar, argsArrayVar);

// 优化：对强类型函数生成直接调用
// 需要在 ExpressionContext 中存储原始 Func<> 委托信息
```

#### 6.2.2 引入函数签名元数据

在 `ExpressionContext` 中存储函数的参数类型信息，使求值时可以跳过类型检查：

```csharp
private class FunctionEntry {
    public ExpressionFunction Delegate { get; }
    public Type[] ParameterTypes { get; }  // 新增：参数类型信息
    public bool IsStrictlyTyped { get; }    // 新增：是否强类型注册
}
```

### 6.3 长期优化（高风险，需架构调整）

#### 6.3.1 引入泛型求值接口

参考 MathEvaluator 的 `INumberBase<T>` 设计，为 MathEval 引入泛型求值路径：

```csharp
public interface IExpressionVisitor<T> where T : struct, INumberBase<T> {
    T Visit(ValueExpression expr);
    T Visit(BinaryExpression expr);
    // ...
}
```

**优势**：完全消除装箱/拆箱和类型转换开销。
**劣势**：需要大幅重构，且仅适用于数值类型场景。

#### 6.3.2 表达式编译为原生代码

使用 `FastExpressionCompiler` 或 .NET 8+ 的 `Emit` API 将表达式编译为更高效的原生代码：

```csharp
// 参考 MathEvaluator.FastExpressionCompiler 的做法
var fn = expression.CompileFast<Func<T, TResult>>();
```

---

## 7. 结论

### 7.1 核心发现

1. **方法重载本身零开销**：C# 的方法重载解析在编译期完成，运行时无额外开销。MathEval 中 `SetFunction` 的多个重载不会导致运行时性能损失。

2. **真正的性能瓶颈不在重载，而在类型转换**：`Convert.ChangeType` 和 `method.Invoke` 是函数调用路径中最大的开销来源，分别占单次函数调用时间的 ~40% 和 ~60%。

3. **Delegate 路径应避免使用**：`SetFunction(string, Delegate)` 的反射调用开销是强类型路径的 3-6 倍，应作为最后手段。

4. **内置函数已采用最优模式**：`BuiltInFunctions.Register` 中的 `is long`/`is double` 模式匹配是正确的优化方向。

### 7.2 优化优先级

| 优先级 | 优化项 | 预期收益 | 实施难度 |
|--------|--------|----------|----------|
| P0 | FunctionWrapper 添加 is 快速路径 | 10-20% 函数调用提速 | 低 |
| P1 | 编译优化中内联强类型函数 | 30-50% 编译路径提速 | 中 |
| P2 | 消除 args.ToArray() 堆分配 | 减少 GC 压力 | 中 |
| P3 | 引入 INumberBase<T> 泛型路径 | 40-60% 数值路径提速 | 高 |

### 7.3 与 MathEvaluator 的差距

MathEvaluator (AntonovAnton) 之所以能实现比 NCalc 快 10-13 倍的性能，核心原因并非函数重载处理方式，而是：

1. **无 AST 中间层**：直接递归求值，跳过 Lexer → Parser → AST → Visitor 管线
2. **ReadOnlySpan\<char\>**：避免字符串分配
3. **Trie 查找**：O(L) 的前缀树查找替代 O(1) 的字典查找（但避免了字符串哈希计算）
4. **INumberBase\<T\>**：编译期确定数值类型，消除运行时类型检查
5. **无 object 装箱**：全程使用泛型值类型

MathEval 选择 AST + Visitor 架构是为了**可扩展性**和**可维护性**，这是合理的架构权衡。在保持架构不变的前提下，通过上述优化可以将性能差距缩小到 2-3 倍以内。
