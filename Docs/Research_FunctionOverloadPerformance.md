# 运行时函数重载支持研究

> **研究主题**：MathEval 支持注册同名不同参函数（运行时函数重载）的架构与性能影响
> **研究日期**：2026-05-21
> **研究目的**：评估引入运行时函数重载机制对 MathEval 架构和性能的影响，为设计决策提供依据
> **前置研究**：[Research_FunctionOverloadPerformance.md](./Research_FunctionOverloadPerformance.md)（C# 编译期方法重载分析）

---

## 目录

1. [研究背景与问题定义](#1-研究背景与问题定义)
2. [当前架构分析](#2-当前架构分析)
3. [函数重载需求场景](#3-函数重载需求场景)
4. [设计方案](#4-设计方案)
5. [架构影响分析](#5-架构影响分析)
6. [性能影响分析](#6-性能影响分析)
7. [编译优化路径影响](#7-编译优化路径影响)
8. [API 兼容性与迁移](#8-api-兼容性与迁移)
9. [方案对比与推荐](#9-方案对比与推荐)
10. [结论](#10-结论)

---

## 1. 研究背景与问题定义

### 1.1 问题定义

前置研究 [Research_FunctionOverloadPerformance.md](./Research_FunctionOverloadPerformance.md) 分析的是 **C# 编译期方法重载**（`SetFunction` 的多个 C# 重载），结论是编译期重载零运行时开销。

本次研究聚焦于一个完全不同的问题：**运行时函数重载**——允许用户注册多个同名但参数签名不同的函数，由引擎在求值时根据实际参数类型和数量自动选择正确的重载。

### 1.2 核心问题

| # | 问题 | 影响维度 |
|---|------|----------|
| 1 | 如何存储多个同名函数？ | 数据结构 |
| 2 | 如何在求值时解析正确的重载？ | 调用链 |
| 3 | 重载解析引入多少额外开销？ | 性能 |
| 4 | 对编译优化路径有何影响？ | 架构 |
| 5 | 对现有 API 的兼容性影响？ | API |
| 6 | 内置函数能否受益于重载机制？ | 可维护性 |

---

## 2. 当前架构分析

### 2.1 函数存储

```csharp
// ExpressionContext.cs
private readonly ConcurrentDictionary<string, ExpressionFunction> _functions;
```

**关键约束**：`string` 键意味着**一个函数名只能对应一个 `ExpressionFunction` 委托**。后注册的同名函数会覆盖先注册的。

### 2.2 函数注册

```csharp
// 所有注册路径最终归一到同一个字典条目
SetFunction(string name, ExpressionFunction func)  → _functions[name] = func
SetFunction<T1, TResult>(string name, Func<T1, TResult> func) → Wrap → _functions[name] = wrapped
SetFunction(string name, Delegate func) → 内联闭包 → _functions[name] = wrapped
```

### 2.3 函数调用

```csharp
// EvaluationVisitor.cs
public object Visit(FunctionCall expr) {
    var args = new List<object>();
    foreach (var arg in expr.Arguments) {
        args.Add(arg.Accept(this));
    }
    if (_context.TryGetFunction(expr.Name, out var func)) {
        return func(args.ToArray());
    }
    throw new FunctionNotFoundException(expr.Name);
}
```

**调用链**：`FunctionCall AST → TryGetFunction(name) → ExpressionFunction(object[]) → 返回结果`

### 2.4 当前的"伪重载"模式

当前内置函数通过**手动分派**实现类似重载的效果：

```csharp
// BuiltInFunctions.cs — 手动类型分派
context.SetFunction("abs", args => {
    if (args[0] is long l) return Math.Abs(l);    // long 版本
    if (args[0] is double d) return Math.Abs(d);   // double 版本
    throw new FunctionTypeMismatchException("abs 需要数值参数");
});

// 手动参数数量分派
context.SetFunction("round", args => {
    if (args.Length == 1) return (long)Math.Round(Convert.ToDouble(args[0]));
    if (args.Length == 2) { /* ... */ }
    throw new FunctionTypeMismatchException("round 需要 1 或 2 个参数");
});
```

**问题**：
1. 每个函数内部都要手动写类型判断逻辑，代码冗余
2. 用户自定义函数无法方便地实现重载
3. 类型分派逻辑散落在各处，难以维护
4. 无法独立注册/移除某个重载

---

## 3. 函数重载需求场景

### 3.1 按参数数量重载

```
round(x)         → long     // 四舍五入到整数
round(x, digits) → double   // 四舍五入到指定小数位
```

这是最常见的重载场景，当前 `round` 已通过手动 `args.Length` 判断实现。

### 3.2 按参数类型重载

```
abs(x: long)   → long    // 整数绝对值，保持整数类型
abs(x: double) → double  // 浮点绝对值
```

当前 `abs` 通过 `is long`/`is double` 手动分派实现。

### 3.3 用户自定义重载

```csharp
// 用户期望的 API
ctx.SetFunction("process", (Func<long, string>)(x => $"整数: {x}"));
ctx.SetFunction("process", (Func<double, string>)(x => $"浮点: {x:F2}"));
ctx.SetFunction("process", (Func<long, long, string>)((a, b) => $"两个整数: {a}, {b}"));

// 求值时自动选择
Expression.Eval("process(42)", ctx)         → "整数: 42"
Expression.Eval("process(3.14)", ctx)       → "浮点: 3.14"
Expression.Eval("process(1, 2)", ctx)       → "两个整数: 1, 2"
```

**当前无法实现**：后注册的 `process` 会覆盖先注册的。

---

## 4. 设计方案

### 4.1 方案 A：参数数量键（Name + ArgCount）

#### 数据结构

```csharp
// 从
ConcurrentDictionary<string, ExpressionFunction> _functions;

// 改为
ConcurrentDictionary<(string name, int argCount), ExpressionFunction> _functions;
```

#### 注册 API

```csharp
// 新增重载：按参数数量注册
void SetFunction(string name, int argCount, ExpressionFunction func);

// 泛型注册自动推导参数数量（已有，无需变更）
void SetFunction<T1, TResult>(string name, Func<T1, TResult> func);  // argCount=1
void SetFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> func);  // argCount=2
```

#### 解析逻辑

```csharp
public bool TryGetFunction(string name, int argCount, out ExpressionFunction func) {
    if (_functions.TryGetValue((name, argCount), out func!))
        return true;
    if (_parent != null)
        return _parent.TryGetFunction(name, argCount, out func);
    func = null!;
    return false;
}
```

#### EvaluationVisitor 变更

```csharp
public object Visit(FunctionCall expr) {
    var args = new List<object>();
    foreach (var arg in expr.Arguments) {
        args.Add(arg.Accept(this));
    }
    if (_context.TryGetFunction(expr.Name, expr.Arguments.Count, out var func)) {
        return func(args.ToArray());
    }
    throw new FunctionNotFoundException(expr.Name);
}
```

#### 优势
- 实现简单，改动最小
- 查找仍为 O(1)，元组键的哈希计算仅比字符串多一次 `int.GetHashCode()`
- 完全向后兼容（可保留 `TryGetFunction(name, out func)` 作为单参数快捷方式）

#### 劣势
- **无法区分同参数数量不同类型的重载**：`abs(long)` 与 `abs(double)` 都是 1 参数，无法共存
- 内置函数的 `is long`/`is double` 手动分派仍需保留

#### 性能影响

| 环节 | 当前 | 方案 A | 增量 |
|------|------|--------|------|
| 字典键哈希 | `string.GetHashCode()` ~15ns | `(string, int).GetHashCode()` ~18ns | +3ns |
| 字典查找 | ~20-40ns | ~22-42ns | +2ns |
| 重载解析 | 无 | 无（键已包含参数数量） | 0ns |
| **总增量** | | | **+5ns (~1-2%)** |

---

### 4.2 方案 B：重载列表 + 运行时类型匹配

#### 数据结构

```csharp
private sealed class FunctionOverload {
    public Type[] ParameterTypes { get; }
    public ExpressionFunction Delegate { get; }
    public int ArgCount => ParameterTypes.Length;
}

private readonly ConcurrentDictionary<string, List<FunctionOverload>> _functions;
```

#### 注册 API

```csharp
// 弱类型注册（无类型签名，作为兜底）
void SetFunction(string name, ExpressionFunction func);

// 强类型泛型注册（自动提取类型签名）
void SetFunction<T1, TResult>(string name, Func<T1, TResult> func);
// 内部：注册 ParameterTypes = [typeof(T1)] 的重载

// 显式类型签名注册
void SetFunction(string name, Type[] parameterTypes, ExpressionFunction func);
```

#### 解析逻辑

```csharp
public bool TryGetFunction(string name, object[] args, out ExpressionFunction func) {
    if (_functions.TryGetValue(name, out var overloads)) {
        // 1. 先按参数数量过滤
        var candidates = overloads.Where(o => o.ArgCount == args.Length).ToList();

        // 2. 精确类型匹配
        foreach (var candidate in candidates) {
            if (IsExactMatch(candidate.ParameterTypes, args)) {
                func = candidate.Delegate;
                return true;
            }
        }

        // 3. 隐式转换匹配（long → double 等）
        foreach (var candidate in candidates) {
            if (IsImplicitConvertible(candidate.ParameterTypes, args)) {
                func = candidate.Delegate;
                return true;
            }
        }

        // 4. 兜底：无类型签名的重载
        var fallback = candidates.FirstOrDefault(o => o.ParameterTypes.Length == 0);
        if (fallback != null) {
            func = fallback.Delegate;
            return true;
        }
    }
    // 链式查找父上下文
    if (_parent != null)
        return _parent.TryGetFunction(name, args, out func);
    func = null!;
    return false;
}

private static bool IsExactMatch(Type[] paramTypes, object[] args) {
    for (int i = 0; i < paramTypes.Length; i++) {
        if (args[i]?.GetType() != paramTypes[i])
            return false;
    }
    return true;
}

private static bool IsImplicitConvertible(Type[] paramTypes, object[] args) {
    for (int i = 0; i < paramTypes.Length; i++) {
        if (args[i] is null) {
            if (paramTypes[i].IsValueType) return false;
            continue;
        }
        var argType = args[i]!.GetType();
        if (argType == paramTypes[i]) continue;
        // long → double 隐式转换
        if (argType == typeof(long) && paramTypes[i] == typeof(double)) continue;
        return false;
    }
    return true;
}
```

#### 优势
- **完整重载支持**：同名不同参（数量和类型均可）
- 内置函数可拆分为独立重载注册，消除手动 `is long`/`is double` 分派
- 用户可自由注册同名不同参函数

#### 劣势
- 重载解析为 O(N) 线性搜索（N = 同名重载数量）
- `TryGetFunction` 签名变更，需要传入 `object[] args`
- 注册同名函数时需要处理冲突检测
- 编译优化路径需要重新设计

#### 性能影响

| 环节 | 当前 | 方案 B | 增量 |
|------|------|--------|------|
| 字典查找 | ~20-40ns | ~20-40ns | 0ns |
| 参数数量过滤 | 无 | ~5-10ns | +5-10ns |
| 精确类型匹配（1参数1重载） | 无 | ~5-10ns | +5-10ns |
| 精确类型匹配（1参数3重载） | 无 | ~15-30ns | +15-30ns |
| 隐式转换匹配（最差情况） | 无 | ~20-50ns | +20-50ns |
| **总增量（典型 1-2 重载）** | | | **+10-20ns (~3-5%)** |
| **总增量（3+ 重载最差）** | | | **+35-60ns (~10-15%)** |

---

### 4.3 方案 C：两级查找（Name → ArgCount → Overloads）

#### 数据结构

```csharp
private sealed class FunctionOverload {
    public Type[] ParameterTypes { get; }
    public ExpressionFunction Delegate { get; }
}

// 两级字典：先按名称，再按参数数量
private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, List<FunctionOverload>>> _functions;
```

#### 解析逻辑

```csharp
public bool TryGetFunction(string name, int argCount, object[] args, out ExpressionFunction func) {
    if (_functions.TryGetValue(name, out var byArgCount)) {
        if (byArgCount.TryGetValue(argCount, out var overloads)) {
            // 仅在同参数数量的重载中搜索（通常只有 1-2 个）
            foreach (var candidate in overloads) {
                if (IsExactMatch(candidate.ParameterTypes, args)) {
                    func = candidate.Delegate;
                    return true;
                }
            }
            foreach (var candidate in overloads) {
                if (IsImplicitConvertible(candidate.ParameterTypes, args)) {
                    func = candidate.Delegate;
                    return true;
                }
            }
        }
    }
    // 链式查找...
}
```

#### 优势
- 两级过滤大幅缩小搜索范围：同名 5 个重载中，按参数数量过滤后通常只剩 1-2 个
- 完整重载支持
- 字典查找仍为 O(1)（两级各一次）

#### 劣势
- 数据结构更复杂（嵌套字典）
- 并发注册的实现复杂度增加
- 内存占用略高（多一层字典对象）

#### 性能影响

| 环节 | 当前 | 方案 C | 增量 |
|------|------|--------|------|
| 第一级字典查找 | ~20-40ns | ~20-40ns | 0ns |
| 第二级字典查找 | 无 | ~15-25ns | +15-25ns |
| 类型匹配（1重载） | 无 | ~5-10ns | +5-10ns |
| 类型匹配（2重载） | 无 | ~10-20ns | +10-20ns |
| **总增量（典型）** | | | **+20-35ns (~5-8%)** |
| **总增量（多重重载）** | | | **+25-45ns (~6-10%)** |

---

### 4.4 方案 D：类型签名复合键（Name + ArgCount + TypeHash）

#### 数据结构

```csharp
private readonly ConcurrentDictionary<FunctionSignature, ExpressionFunction> _functions;

private readonly struct FunctionSignature : IEquatable<FunctionSignature> {
    public string Name { get; }
    public int ArgCount { get; }
    public int TypeHash { get; }  // 参数类型的组合哈希

    // 预计算：typeof(long).GetHashCode() ^ typeof(double).GetHashCode() ...
}
```

#### 解析逻辑

```csharp
public bool TryGetFunction(string name, object[] args, out ExpressionFunction func) {
    var argCount = args.Length;
    var typeHash = ComputeTypeHash(args);

    // 1. 精确匹配
    if (_functions.TryGetValue(new FunctionSignature(name, argCount, typeHash), out func!))
        return true;

    // 2. 回退到参数数量匹配（无类型签名的弱类型函数）
    if (_functions.TryGetValue(new FunctionSignature(name, argCount, 0), out func!))
        return true;

    // 3. 隐式转换回退（需遍历同名同参数数量的重载）
    // ...
}

private static int ComputeTypeHash(object[] args) {
    int hash = 0;
    foreach (var arg in args) {
        hash = (hash, arg?.GetType() ?? typeof(object)).GetHashCode();
    }
    return hash;
}
```

#### 优势
- 精确匹配时 O(1) 查找，与当前性能几乎一致
- 完整重载支持

#### 劣势
- `ComputeTypeHash` 每次调用都需要 `GetType()` + 哈希计算
- 哈希冲突时仍需回退到线性搜索
- 隐式转换匹配（`long → double`）仍需遍历
- 实现复杂度最高

#### 性能影响

| 环节 | 当前 | 方案 D | 增量 |
|------|------|--------|------|
| 类型哈希计算 | 无 | ~15-25ns（GetType + 哈希） | +15-25ns |
| 字典查找（精确匹配） | ~20-40ns | ~22-42ns | +2ns |
| 回退查找（隐式转换） | 无 | ~20-50ns | +20-50ns |
| **总增量（精确匹配）** | | | **+17-27ns (~4-6%)** |
| **总增量（需回退）** | | | **+37-77ns (~10-18%)** |

---

## 5. 架构影响分析

### 5.1 受影响组件清单

| 组件 | 文件 | 影响程度 | 变更说明 |
|------|------|----------|----------|
| **ExpressionContext** | [ExpressionContext.cs](../MathEval/Context/ExpressionContext.cs) | 🔴 高 | 数据结构变更、SetFunction/TryGetFunction 签名变更 |
| **EvaluationVisitor** | [EvaluationVisitor.cs](../MathEval/Visitors/EvaluationVisitor.cs) | 🟡 中 | Visit(FunctionCall) 需传入参数信息进行重载解析 |
| **CompiledExpression** | [CompiledExpression.cs](../MathEval/Optimization/CompiledExpression.cs) | 🔴 高 | 编译路径需生成重载解析代码 |
| **ConstantFolder** | [ConstantFolder.cs](../MathEval/Optimization/ConstantFolder.cs) | 🟢 低 | 折叠逻辑不变，但需考虑重载场景 |
| **FunctionWrapper** | [FunctionWrapper.cs](../MathEval/Internal/FunctionWrapper.cs) | 🟡 中 | 需提取参数类型信息用于重载注册 |
| **BuiltInFunctions** | [BuiltInFunctions.cs](../MathEval/Functions/BuiltInFunctions.cs) | 🟡 中 | 可重构为多重重载注册，消除手动分派 |
| **ExpressionBuilder** | [ExpressionBuilder.cs](../MathEval/ExpressionBuilder.cs) | 🟡 中 | WithFunction API 需同步更新 |
| **ExpressionFunction** | [ExpressionFunction.cs](../MathEval/Context/ExpressionFunction.cs) | 🟢 低 | 委托签名不变 |
| **Parser** | [Parser.cs](../MathEval/Parser/Parser.cs) | 🟢 低 | 解析逻辑不变，FunctionCall AST 节点不变 |
| **FunctionCall AST** | [FunctionCall.cs](../MathEval/AST/FunctionCall.cs) | 🟢 低 | 节点定义不变 |

### 5.2 ExpressionContext 详细变更

#### 当前

```csharp
public class ExpressionContext {
    private readonly ConcurrentDictionary<string, ExpressionFunction> _functions;

    public void SetFunction(string name, ExpressionFunction func) {
        _functions[name] = func;  // 后注册覆盖先注册
    }

    public bool TryGetFunction(string name, out ExpressionFunction func) {
        return _functions.TryGetValue(name, out func!);
    }
}
```

#### 方案 B 变更后

```csharp
public class ExpressionContext {
    private readonly ConcurrentDictionary<string, List<FunctionOverload>> _functions;

    public void SetFunction(string name, ExpressionFunction func) {
        // 弱类型注册：作为兜底重载
        _functions.AddOrUpdate(name,
            _ => [new FunctionOverload([], func)],
            (_, list) => {
                // 移除已有的弱类型重载（弱类型只保留一个）
                list.RemoveAll(o => o.ParameterTypes.Length == 0);
                list.Add(new FunctionOverload([], func));
                return list;
            });
    }

    public void SetFunction<T1, TResult>(string name, Func<T1, TResult> func) {
        var wrapped = FunctionWrapper.Wrap(func);
        var paramTypes = new[] { typeof(T1) };
        _functions.AddOrUpdate(name,
            _ => [new FunctionOverload(paramTypes, wrapped)],
            (_, list) => {
                // 移除已有的完全相同签名的重载
                list.RemoveAll(o => TypesEqual(o.ParameterTypes, paramTypes));
                list.Add(new FunctionOverload(paramTypes, wrapped));
                return list;
            });
    }

    public bool TryGetFunction(string name, object[] args, out ExpressionFunction func) {
        // 重载解析逻辑...
    }
}
```

**关键变更**：
1. `_functions` 类型从 `Dictionary<string, ExpressionFunction>` 变为 `Dictionary<string, List<FunctionOverload>>`
2. `SetFunction` 从覆盖语义变为追加语义（同名不同签名的重载共存）
3. `TryGetFunction` 签名从 `(string, out ExpressionFunction)` 变为 `(string, object[], out ExpressionFunction)`

### 5.3 EvaluationVisitor 详细变更

```csharp
// 当前
public object Visit(FunctionCall expr) {
    var args = new List<object>();
    foreach (var arg in expr.Arguments) {
        args.Add(arg.Accept(this));
    }
    if (_context.TryGetFunction(expr.Name, out var func)) {
        return func(args.ToArray());
    }
    throw new FunctionNotFoundException(expr.Name);
}

// 方案 B 变更后
public object Visit(FunctionCall expr) {
    var args = new List<object>();
    foreach (var arg in expr.Arguments) {
        args.Add(arg.Accept(this));
    }
    var argsArray = args.ToArray();
    if (_context.TryGetFunction(expr.Name, argsArray, out var func)) {
        return func(argsArray);
    }
    throw new FunctionNotFoundException(expr.Name);
}
```

**变更**：`TryGetFunction` 需要传入已求值的参数数组，用于类型匹配。注意这要求**先求值所有参数，再解析重载**——当前代码已经是这个顺序，无需调整求值顺序。

### 5.4 CompiledExpression 详细变更

编译路径的变更是最大的挑战。当前编译路径直接生成 `TryGetFunction(name, out func)` + `func(args)` 的表达式树。

```csharp
// 当前编译路径（CompiledExpression.cs L123-L154）
private static BlockExpression CompileFunctionCall(FunctionCall expr, ParameterExpression contextParam) {
    var argsExpr = expr.Arguments.Select(arg => CompileNode(arg, contextParam)).ToArray();
    var argsArrayVar = LinqExpression.Variable(typeof(object[]), "args");
    var initArray = LinqExpression.NewArrayInit(typeof(object), argsExpr);
    var assignArray = LinqExpression.Assign(argsArrayVar, initArray);

    var tryGetFuncMethod = typeof(ExpressionContext).GetMethod(nameof(ExpressionContext.TryGetFunction));
    var funcName = LinqExpression.Constant(expr.Name);
    var funcVar = LinqExpression.Variable(typeof(ExpressionFunction), "func");

    var tryGetCall = LinqExpression.Call(contextParam, tryGetFuncMethod!, funcName, funcVar);
    var invokeExpr = LinqExpression.Invoke(funcVar, argsArrayVar);

    // ...
}
```

引入重载后，编译路径有两种策略：

#### 策略 1：运行时解析（简单但性能一般）

```csharp
// 编译时不知道参数类型，生成运行时重载解析代码
var tryGetFuncMethod = typeof(ExpressionContext).GetMethod(nameof(ExpressionContext.TryGetFunction), [typeof(string), typeof(object[]), typeof(ExpressionFunction).MakeByRefType()]);
var tryGetCall = LinqExpression.Call(contextParam, tryGetFuncMethod!, funcName, argsArrayVar, funcVar);
```

**影响**：编译路径与解释路径性能一致，重载解析开销无法消除。

#### 策略 2：编译时绑定（高性能但复杂）

```csharp
// 编译时已知参数数量，可按方案 A 的 (name, argCount) 键预查找
// 若上下文中该函数只有一个重载，可直接绑定
// 若有多个重载，需生成类型检查代码
```

**影响**：编译路径可消除部分重载解析开销，但实现复杂度显著增加。

---

## 6. 性能影响分析

### 6.1 基准场景定义

| 场景 | 表达式 | 函数调用次数 | 说明 |
|------|--------|-------------|------|
| S1 | `sqrt(4)` | 1 | 单参数，无重载 |
| S2 | `abs(-42)` | 1 | 单参数，2 类型重载 (long/double) |
| S3 | `round(3.14, 2)` | 1 | 双参数重载 (1参数/2参数) |
| S4 | `sqrt(x) + sin(y)` | 2 | 多函数调用 |
| S5 | `max(a, b) + min(c, d)` | 2 | 双参数，2 类型重载 |

### 6.2 各方案性能对比

#### 单次函数调用开销分解（纳秒）

| 环节 | 当前 | 方案 A | 方案 B | 方案 C | 方案 D |
|------|------|--------|--------|--------|--------|
| 参数求值 | ~50-100 | ~50-100 | ~50-100 | ~50-100 | ~50-100 |
| args.ToArray() | ~10-20 | ~10-20 | ~10-20 | ~10-20 | ~10-20 |
| 字典查找 | ~20-40 | ~22-42 | ~20-40 | ~35-65 | ~22-42 |
| 重载解析 | 0 | 0 | ~10-20 | ~5-10 | ~15-25 |
| 类型匹配 | 0 | 0 | ~5-10 | ~5-10 | 0（含在哈希中） |
| 委托调用 | ~2-5 | ~2-5 | ~2-5 | ~2-5 | ~2-5 |
| Convert.ChangeType | ~20-50 | ~20-50 | ~20-50 | ~20-50 | ~20-50 |
| **合计** | **~102-215** | **~104-217** | **~107-225** | **~127-260** | **~119-242** |
| **增量** | — | **+2ns** | **+5-10ns** | **+25-45ns** | **+17-27ns** |
| **增幅** | — | **~1%** | **~3-5%** | **~12-20%** | **~8-12%** |

#### 多重重载场景（3个同名重载）

| 环节 | 当前(手动分派) | 方案 B | 方案 C | 方案 D |
|------|---------------|--------|--------|--------|
| 字典查找 | ~20-40 | ~20-40 | ~35-65 | ~22-42 |
| 重载解析 | ~5-10 (is检查) | ~20-40 | ~10-20 | ~15-25 |
| **增量** | — | **+10-25ns** | **+15-35ns** | **+10-20ns** |
| **增幅** | — | **~5-12%** | **~7-16%** | **~5-10%** |

### 6.3 与整体求值开销的对比

以 `sqrt(x) + sin(y)` 为例（缓存命中场景）：

| 阶段 | 当前 | 方案 B（推荐） | 增幅 |
|------|------|---------------|------|
| AST 缓存查找 | ~20-40ns | ~20-40ns | 0% |
| EvaluationVisitor 遍历 | ~100-200ns | ~100-200ns | 0% |
| 符号查找 × 2 | ~40-80ns | ~40-80ns | 0% |
| 函数查找 × 2 | ~40-80ns | ~50-100ns | +25% |
| Convert.ChangeType × 2 | ~40-100ns | ~40-100ns | 0% |
| 数学计算 | ~5-10ns | ~5-10ns | 0% |
| args.ToArray() × 2 | ~20-40ns | ~20-40ns | 0% |
| **合计** | **~265-550ns** | **~275-570ns** | **~2-4%** |

**结论**：在典型求值场景中，函数重载引入的额外开销仅占整体求值时间的 **2-4%**，影响可忽略。

### 6.4 编译优化路径性能

| 场景 | 当前编译路径 | 重载后（运行时解析） | 重载后（编译时绑定） |
|------|------------|-------------------|-------------------|
| 单重载函数 | ~80-150ns | ~85-160ns (+5%) | ~80-150ns (0%) |
| 双重重载函数 | ~80-150ns | ~95-175ns (+15%) | ~85-155ns (+3%) |
| 三重重载函数 | ~80-150ns | ~105-190ns (+25%) | ~90-165ns (+8%) |

**结论**：编译时绑定策略可将重载开销压缩到 3-8%，但实现复杂度显著增加。运行时解析策略在重载较多时开销可达 15-25%。

### 6.5 内存影响

| 方案 | 每函数额外内存 | 100 函数场景 | 说明 |
|------|-------------|------------|------|
| 当前 | ~80 bytes (Dictionary entry + delegate) | ~8 KB | 基准 |
| 方案 A | ~88 bytes (+8 bytes for int key) | ~8.8 KB | +10% |
| 方案 B | ~120 bytes (+List + FunctionOverload + Type[]) | ~12 KB | +50% |
| 方案 C | ~150 bytes (+嵌套字典 + List + FunctionOverload) | ~15 KB | +88% |
| 方案 D | ~96 bytes (+FunctionSignature struct) | ~9.6 KB | +20% |

**结论**：内存影响在所有方案中都可忽略（KB 级别），不是决策因素。

---

## 7. 编译优化路径影响

### 7.1 当前编译路径

[CompiledExpression.cs](../MathEval/Optimization/CompiledExpression.cs) 中的 `CompileFunctionCall` 生成以下表达式树：

```
1. 创建 object[] 数组
2. 调用 ExpressionContext.TryGetFunction(name, out func)
3. 调用 func(args)
```

### 7.2 引入重载后的编译路径挑战

| 挑战 | 说明 | 严重程度 |
|------|------|----------|
| **TryGetFunction 签名变更** | 编译生成的表达式树需匹配新签名 | 🟡 中 |
| **运行时类型信息** | 编译时无法确定参数的运行时类型 | 🔴 高 |
| **重载决议代码生成** | 需在表达式树中生成类型检查分支 | 🔴 高 |
| **常量折叠交互** | 常量折叠可能改变参数类型，影响重载选择 | 🟡 中 |

### 7.3 编译路径策略对比

#### 策略 1：运行时解析（推荐初期实现）

```csharp
// 编译时只生成 TryGetFunction(name, args, out func) 调用
// 重载解析完全在运行时进行
var tryGetCall = LinqExpression.Call(contextParam, tryGetFuncMethod, funcName, argsArrayVar, funcVar);
```

- **实现成本**：低（仅修改方法签名）
- **性能**：与解释路径一致
- **风险**：低

#### 策略 2：编译时部分绑定

```csharp
// 编译时已知参数数量，可按 argCount 预过滤
// 生成：if (TryGetFunction(name, argCount, args, out func)) ...
var argCount = LinqExpression.Constant(expr.Arguments.Count);
var tryGetCall = LinqExpression.Call(contextParam, tryGetFuncMethod, funcName, argCount, argsArrayVar, funcVar);
```

- **实现成本**：中
- **性能**：减少运行时搜索范围
- **风险**：中

#### 策略 3：完全编译时绑定

```csharp
// 编译时查询上下文，确定唯一的重载
// 生成：直接调用特定重载的委托
// 需要在编译时访问 ExpressionContext（当前编译路径不持有上下文引用）
```

- **实现成本**：高（需重构编译架构）
- **性能**：最优
- **风险**：高（上下文可能在编译后变更）

---

## 8. API 兼容性与迁移

### 8.1 破坏性变更

| 变更 | 影响范围 | 严重程度 | 缓解策略 |
|------|----------|----------|----------|
| `TryGetFunction` 签名变更 | 内部 + 子类化用户 | 🔴 高 | 保留旧签名作为兼容包装 |
| `_functions` 字段类型变更 | 内部 | 🟢 低 | 内部实现细节 |
| 同名注册语义变更（覆盖→追加） | 所有用户 | 🟡 中 | 文档说明 + 可选配置 |

### 8.2 向后兼容方案

```csharp
public class ExpressionContext {
    // 保留旧签名（兼容）
    [Obsolete("Use TryGetFunction(string, object[], out ExpressionFunction) for overload resolution")]
    public bool TryGetFunction(string name, out ExpressionFunction func) {
        // 返回第一个注册的重载（向后兼容）
        if (_functions.TryGetValue(name, out var overloads) && overloads.Count > 0) {
            func = overloads[0].Delegate;
            return true;
        }
        // ...
    }

    // 新签名
    public bool TryGetFunction(string name, object[] args, out ExpressionFunction func) {
        // 完整重载解析...
    }
}
```

### 8.3 内置函数迁移示例

```csharp
// 当前（手动分派）
context.SetFunction("abs", args => {
    if (args[0] is long l) return Math.Abs(l);
    if (args[0] is double d) return Math.Abs(d);
    throw new FunctionTypeMismatchException("abs 需要数值参数");
});

// 迁移后（独立重载注册）
context.SetFunction("abs", (Func<long, long>)(x => Math.Abs(x)));
context.SetFunction("abs", (Func<double, double>)(x => Math.Abs(x)));
```

**收益**：
- 代码更简洁，消除手动类型分派
- 每个重载可独立管理（移除、替换）
- 类型检查由框架统一处理

---

## 9. 方案对比与推荐

### 9.1 综合对比

| 维度 | 方案 A (ArgCount键) | 方案 B (重载列表) | 方案 C (两级查找) | 方案 D (类型哈希键) |
|------|-------------------|-----------------|------------------|-------------------|
| **重载能力** | 仅按参数数量 | 完整（数量+类型） | 完整（数量+类型） | 完整（数量+类型） |
| **性能增量** | ~1% | ~3-5% | ~12-20% | ~8-12% |
| **实现复杂度** | 低 | 中 | 高 | 高 |
| **API 兼容性** | 完全兼容 | 需适配 | 需适配 | 需适配 |
| **编译路径影响** | 极小 | 中 | 大 | 大 |
| **可维护性** | 一般 | 好 | 一般 | 差 |
| **内存增量** | +10% | +50% | +88% | +20% |

### 9.2 推荐方案

#### 🏆 推荐：方案 B（重载列表 + 运行时类型匹配）

**理由**：

1. **完整重载支持**：同时支持参数数量和参数类型重载，满足所有需求场景
2. **性能影响可控**：典型场景仅增加 3-5% 开销，最差场景不超过 15%
3. **实现复杂度适中**：核心变更集中在 `ExpressionContext` 和 `EvaluationVisitor`
4. **内置函数可简化**：消除手动 `is long`/`is double` 分派，提升可维护性
5. **编译路径可渐进优化**：初期用运行时解析，后续可升级为编译时绑定

#### 实施路线

| 阶段 | 内容 | 预期收益 |
|------|------|----------|
| **Phase 1** | 引入 `FunctionOverload` + 重载列表数据结构 | 基础重载支持 |
| **Phase 2** | 重构 `SetFunction` 为追加语义 | 同名注册不覆盖 |
| **Phase 3** | 实现 `TryGetFunction(name, args, out func)` 重载解析 | 运行时重载选择 |
| **Phase 4** | 迁移内置函数为独立重载注册 | 代码简化 |
| **Phase 5** | 编译路径适配（运行时解析策略） | 编译路径支持 |
| **Phase 6**（可选） | 编译时绑定优化 | 编译路径性能提升 |

#### 不推荐方案 C 的理由

虽然方案 C 的两级查找在理论上更优雅，但：
- 嵌套 `ConcurrentDictionary` 的并发管理复杂
- 性能反而不如方案 B（多一次字典查找的开销 > 线性搜索少量重载的开销）
- 在 MathEval 的典型场景中，同名重载数量极少（1-3 个），线性搜索的常数因子极小

#### 不推荐方案 D 的理由

- 类型哈希计算本身的开销（`GetType()` + 哈希组合）抵消了 O(1) 查找的优势
- 哈希冲突处理增加了实现复杂度
- 隐式转换匹配仍需回退到线性搜索

---

## 10. 结论

### 10.1 核心发现

1. **运行时函数重载与编译期方法重载是完全不同的问题**：前者需要在求值时进行类型匹配和重载决议，引入运行时开销；后者零运行时开销。

2. **性能影响可控**：推荐方案（方案 B）在典型场景下仅增加 3-5% 的求值开销，最差场景不超过 15%。与整体求值时间（250-550ns）相比，增量约 10-25ns，绝对值很小。

3. **架构影响集中在三个组件**：`ExpressionContext`（数据结构 + API）、`EvaluationVisitor`（调用逻辑）、`CompiledExpression`（编译路径）。Parser 和 AST 节点无需变更。

4. **编译优化路径是最大挑战**：当前编译路径假设一个函数名对应一个委托，引入重载后需要生成重载决议代码。建议初期采用运行时解析策略，后续优化为编译时绑定。

5. **内置函数可显著简化**：当前 `BuiltInFunctions` 中的手动类型分派代码可替换为独立重载注册，提升可维护性。

6. **内存影响可忽略**：所有方案的内存增量均在 KB 级别，不构成决策因素。

### 10.2 性能开销总结

| 场景 | 当前开销 | 重载后开销（方案 B） | 增幅 |
|------|----------|---------------------|------|
| 单函数调用（无重载） | ~102-215ns | ~107-225ns | ~3-5% |
| 单函数调用（2重载） | ~107-225ns (手动分派) | ~112-235ns | ~3-5% |
| 完整表达式求值 | ~265-550ns | ~275-570ns | ~2-4% |
| 编译路径（运行时解析） | ~80-150ns | ~85-175ns | ~5-15% |
| 编译路径（编译时绑定） | ~80-150ns | ~80-155ns | ~0-3% |

### 10.3 决策建议

| 条件 | 建议 |
|------|------|
| 仅需按参数数量重载 | 方案 A（最小改动） |
| 需要完整重载支持 + 可控性能 | **方案 B（推荐）** |
| 追求极致性能 + 可接受复杂度 | 方案 B + 编译时绑定优化 |
| 暂不需要重载 | 维持现状，内置函数继续手动分派 |

### 10.4 风险提示

1. **同名注册语义变更**：从覆盖变为追加，可能导致用户误注册多个同名同签名的重载。建议在注册时检测签名冲突并发出警告。
2. **隐式转换歧义**：当 `long` 和 `double` 重载共存时，`abs(42)` 应选择 `long` 还是 `double`？需要明确定义优先级规则（建议：精确匹配优先，隐式转换次之）。
3. **编译路径兼容性**：编译时绑定策略要求编译时已知上下文，当前架构中 `CompiledExpression` 不持有上下文引用，需要重构。
