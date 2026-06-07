# MathEval.Fast 性能优化方案

## 概述

基于 BenchmarkDotNet 测试结果和 MathEvaluator 架构研究，对 MathEval.Fast 进行性能优化。目标：将单次求值从 ~8μs 降至 ~500ns 级别，接近 MathEvaluator 的性能水平；同时提供缓存和 JIT 编译能力，由调用方按需选择。

## 现状分析

### 当前性能（BenchmarkDotNet, i7-9750H）

| 场景 | Fast | MathEvaluator(参考) | 差距 |
|------|------|---------------------|------|
| 简单算术(NoCache) | 8.2 μs | ~465 ns | 17x |
| 函数调用(NoCache) | 14.9 μs | ~362 ns | 41x |
| 逻辑运算(NoCache) | 10.8 μs | ~483 ns | 22x |

### 根因分析：FastEval 的性能瓶颈

| 瓶颈 | 位置 | 预估额外开销 | 说明 |
|------|------|------------|------|
| `new FastEvaluator` + `new FastScanner` | `FastEval.EvalDouble()` | ~50-100 ns | class 每次堆分配，MathEvaluator 用 struct |
| `identifierSpan.ToString()` | `FastEvaluator.EvalIdentifierOrFunction()` | ~40-80 ns × 2次 | Span 优势被 ToString 丢弃，MathEvaluator 用 Trie 零分配 |
| `List<double>` + `[.. args]` | `FastEvaluator.EvalFunctionCall()` | ~30-50 ns | 每次函数调用分配 List + 数组拷贝 |
| `Dictionary<string, Func>` 查找 | `BuiltInFunctions.TryGetFunction()` | ~20-30 ns | 需要字符串 key，MathEvaluator 用 Trie |
| `Dictionary<string, double>` 查找 | `BuiltInConstants.TryGetValue()` | ~20-30 ns | 同上 |
| `MatchKeyword` 重复扫描 | `FastEvaluator.MatchKeyword()` | ~20-40 ns | 每次关键字匹配都遍历字符串，Trie 一次遍历 |
| BuiltInOperators 静态方法调用 | 各运算方法 | ~10-20 ns | 简单运算如 Add/Subtract 可内联，但当前通过静态方法调用 |

---

## 第一阶段：微优化（零架构变更，预估提升 2-3x）

> 不改变整体架构，只消除明显的分配和冗余操作

### 1.1 FastEvaluator 改为 ref struct

**文件**: `MathEval.Fast\Core\FastEvaluator.cs`, `MathEval.Fast\FastEval.cs`

**当前**:
```csharp
internal sealed class FastEvaluator(string expression, ...) {
    private FastScanner _scanner = new(expression);
    // ...
}
```

**优化为**:
```csharp
internal ref struct FastEvaluator {
    private readonly string _expression;
    private int _position;
    private readonly IReadOnlyDictionary<string, double>? _variables;
    private bool _skipMode;
    // FastScanner 的逻辑内联到 FastEvaluator 中，消除 struct 嵌套
}
```

**收益**: 消除 FastEvaluator 和 FastScanner 的堆分配，改为栈上分配。

**注意**: ref struct 不能作为 class 字段存储，但 `FastEval.EvalDouble()` 是同步调用，直接在栈上创建即可。

### 1.2 消除 identifierSpan.ToString() — 用 Span 直接比较

**文件**: `MathEval.Fast\Core\FastEvaluator.cs`, `MathEval.Fast\BuiltIn\BuiltInFunctions.cs`, `MathEval.Fast\BuiltIn\BuiltInConstants.cs`

**当前**:
```csharp
if (_scanner.Peek() == '(') return EvalFunctionCall(identifierSpan.ToString());  // 分配!
if (BuiltInConstants.TryGetValue(identifierSpan.ToString(), out var constValue))  // 分配!
```

**优化为**:
```csharp
// BuiltInFunctions 新增 Span 查找方法
public static bool TryGetFunction(ReadOnlySpan<char> name, out Func<double[], double>? func)

// BuiltInConstants 新增 Span 查找方法
public static bool TryGetValue(ReadOnlySpan<char> name, out double value)

// EvalIdentifierOrFunction 中：
if (_scanner.Peek() == '(') return EvalFunctionCall(identifierSpan);  // 传 Span
if (BuiltInConstants.TryGetValue(identifierSpan, out var constValue)) return constValue;
```

**实现方式**: 内部用固定长度的 if-else 链，按长度分组后逐字符比较：

```csharp
// BuiltInConstants 优化示例
public static bool TryGetValue(ReadOnlySpan<char> name, out double value) {
    switch (name.Length) {
        case 1:
            if (name[0] == 'E') { value = Math.E; return true; }
            if (name[0] == 'π') { value = Math.PI; return true; }
            break;
        case 2:
            if (name[0] == 'P' && name[1] == 'I') { value = Math.PI; return true; }
            break;
        case 3:
            if (EqualsLower3(name, "NaN")) { value = double.NaN; return true; }
            if (EqualsLower3(name, "INF")) { value = double.PositiveInfinity; return true; }
            break;
        case 4:
            if (EqualsLower4(name, "true")) { value = 1.0; return true; }
            break;
        case 5:
            if (EqualsLower5(name, "false")) { value = 0.0; return true; }
            break;
    }
    value = 0;
    return false;
}
```

**收益**: 消除 2 次字符串分配（常量查找 + 函数查找），预计节省 ~80-160 ns。

### 1.3 函数参数用 stackalloc 替代 List<double>

**文件**: `MathEval.Fast\Core\FastEvaluator.cs`, `MathEval.Fast\BuiltIn\BuiltInFunctions.cs`

**当前**:
```csharp
var args = new List<double>();
// ...
return CallBuiltInFunction(name, [.. args]);  // List + 数组展开
```

**优化为**:
```csharp
Span<double> buffer = stackalloc double[8];  // 大多数函数参数 <= 8 个
int count = 0;
// ...
buffer[count++] = EvalExpression();
// ...
return CallBuiltInFunction(name, buffer.Slice(0, count));
```

同时修改 `BuiltInFunctions` 接受 `ReadOnlySpan<double>` 而非 `double[]`。

**收益**: 消除 List<double> 堆分配 + 数组拷贝，预计节省 ~30-50 ns。

### 1.4 内联简单运算符

**文件**: `MathEval.Fast\Core\FastEvaluator.cs`

**当前**: 所有运算通过 `BuiltInOperators.Add(left, right)` 等静态方法调用。

**优化**: 对最简单的运算直接内联：

```csharp
// 当前
left = BuiltInOperators.Add(left, right);

// 优化为
left = left + right;
```

保留复杂运算（位运算、Power、Modulo、NaN 比较等）的方法调用。

**内联列表**:
- `Add` → `left + right`
- `Subtract` → `left - right`
- `Multiply` → `left * right`
- `Divide` → `left / right`
- `Negate` → `-operand`
- `LessThan` → `left < right ? 1.0 : 0.0`
- `GreaterThan` → `left > right ? 1.0 : 0.0`
- `LessThanOrEqual` → `left <= right ? 1.0 : 0.0`
- `GreaterThanOrEqual` → `left >= right ? 1.0 : 0.0`

**保留方法调用**: Equal/NotEqual（NaN 处理）、Power、Modulo、IntegerDivide、位运算

**收益**: 消除方法调用开销，JIT 可直接生成内联算术指令，预计节省 ~10-20 ns/运算。

### 1.5 MatchKeyword 优化 — 合并到标识符解析

**文件**: `MathEval.Fast\Core\FastEvaluator.cs`

**当前**: `MatchKeyword` 在多个 Eval* 方法中被调用（and/or/not/xor/mod），每次都重新扫描字符串。

**优化**: 将关键字识别移到 `EvalPrimary` 中，在标识符解析时一次性判断：

```csharp
private double EvalPrimary() {
    // ...
    if (FastScanner.IsIdentifierStart(ch)) return EvalIdentifierOrKeyword();
    // ...
}

private double EvalIdentifierOrKeyword() {
    var span = ReadIdentifierSpan();

    // 关键字快速路径（按长度分组）
    switch (span.Length) {
        case 2:
            if (EqualsLower(span, "or")) return EvalLogicalOr_Continue(span);
            break;
        case 3:
            if (EqualsLower(span, "and")) return EvalLogicalAnd_Continue(span);
            if (EqualsLower(span, "not")) return BuiltInOperators.Not(EvalUnary());
            if (EqualsLower(span, "xor")) return EvalBitwiseXor_Continue(span);
            if (EqualsLower(span, "mod")) return EvalMultiplicative_Continue_Mod(span);
            break;
    }

    // 函数/常量/变量
    SkipWhitespace();
    if (Peek() == '(') return EvalFunctionCall(span);
    if (BuiltInConstants.TryGetValue(span, out var constValue)) return constValue;
    return LookupVariable(span);
}
```

**收益**: 消除 `MatchKeyword` 的重复扫描，减少分支判断次数，预计节省 ~20-40 ns。

**注意**: 这个改动需要重新设计逻辑运算符的解析方式。当前 `and`/`or`/`not`/`xor`/`mod` 作为关键字在各自的 Eval* 方法中通过 `MatchKeyword` 匹配，改为标识符解析后，需要调整控制流。一种可行方案是将这些关键字视为**前缀运算符**而非中缀运算符，在 `EvalPrimary` 中处理。

---

## 第二阶段：字节码 VM + 缓存（预估重复求值提升 5-10x）

> 首次解析生成指令序列，重复求值直接执行指令

### 2.1 指令集定义

**新文件**: `MathEval.Fast\VM\OpCode.cs`

```csharp
internal enum OpCode : byte {
    // 栈操作
    PushConst,     // 压入常量 (double operand)
    LoadVar,       // 加载变量 (string operand = 变量名)

    // 算术运算（双目）
    Add, Sub, Mul, Div, IntDiv, Mod, MathMod, Pow,
    // 算术运算（单目）
    Negate, LogicalNot, BitwiseNot,
    // 位运算
    BitwiseOr, BitwiseAnd, BitwiseXor, LeftShift, RightShift,
    // 比较
    Equal, NotEqual, LessThan, LessOrEqual, GreaterThan, GreaterOrEqual,
    // 逻辑
    LogicalAnd, LogicalOr,
    // 函数调用
    Call,          // (byte operand = 函数ID, byte operand = 参数数量)
    // 控制流
    JumpIfFalse,   // (int operand = 跳转目标)
    Jump,          // (int operand = 跳转目标)
    // 三元运算
    CondTrue,      // 条件为真时跳转
    CondFalse,     // 条件为假时跳转
}
```

**新文件**: `MathEval.Fast\VM\Instruction.cs`

```csharp
internal struct Instruction {
    public OpCode OpCode;
    public double DoubleOperand;  // PushConst 的值
    public string? StringOperand; // LoadVar 的变量名
    public int IntOperand;        // 跳转目标 / 函数参数数量
    public byte FunctionId;       // 内置函数 ID
}
```

### 2.2 编译器：表达式 → 指令序列

**新文件**: `MathEval.Fast\VM\BytecodeCompiler.cs`

复用第一阶段优化后的 FastEvaluator 扫描逻辑，但输出指令序列而非直接求值。核心改造：

- 遇到数字 → `PushConst`
- 遇到变量 → `LoadVar`
- 遇到运算符 → 递归编译左右操作数 + 输出对应 OpCode
- 遇到函数 → 递归编译参数 + `Call`
- 遇到短路逻辑 → `JumpIfFalse` + 回填跳转目标

### 2.3 VM 执行器

**新文件**: `MathEval.Fast\VM\BytecodeVM.cs`

```csharp
internal static class BytecodeVM {
    public static double Execute(ReadOnlySpan<Instruction> instructions, IReadOnlyDictionary<string, double>? variables);
}
```

核心是一个紧凑的 switch 循环，用 `Span<double>` 作为栈，无递归、无虚方法分派。

### 2.4 缓存层（LRU 淘汰）

**新文件**: `MathEval.Fast\VM\InstructionCache.cs`

```csharp
internal static class InstructionCache {
    // LRU 缓存，默认容量 256
    private static readonly LruCache<string, Instruction[]> _cache = new(256);

    public static Instruction[] GetOrCompile(string expression);
    public static void Clear();
    public static void SetCapacity(int capacity);
}
```

**LRU 缓存实现**:

**新文件**: `MathEval.Fast\VM\LruCache.cs`

```csharp
internal class LruCache<TKey, TValue> where TKey : notnull {
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _map;
    private readonly LinkedList<CacheItem> _list;  // 最近使用的在前

    public void Add(TKey key, TValue value);     // 超出容量时淘汰尾部
    public bool TryGet(TKey key, out TValue value); // 命中时移到头部
    public void Clear();
}
```

**缓存策略**:
- **Key**: 表达式字符串（精确匹配）
- **淘汰**: LRU，超出容量时淘汰最久未使用
- **默认容量**: 256 条（可配置）
- **两个只有局部不同的表达式**：各自独立缓存，不共享（因为指令序列不同，共享子表达式需要 DAG 结构，复杂度过高）
- **线程安全**: 读写加锁

### 2.5 公共 API 扩展

**文件**: `MathEval.Fast\FastEval.cs`

```csharp
/// <summary>
/// 缓存求值：首次解析生成指令序列并缓存，后续直接执行
/// </summary>
public static double EvalDoubleCached(string expression, IReadOnlyDictionary<string, double>? variables = null);

/// <summary>
/// 清除指令缓存
/// </summary>
public static void ClearCache();

/// <summary>
/// 设置缓存容量
/// </summary>
public static void SetCacheCapacity(int capacity);
```

---

## 第三阶段：JIT 编译（预估重复求值提升 100x+）

> 由调用方显式选择编译，编译后执行速度 ~3-5 ns

### 3.1 DynamicMethod 编译器

**新文件**: `MathEval.Fast\Jit\JitCompiler.cs`

使用 `DynamicMethod` + `ILGenerator` 直接生成 IL：

```csharp
internal static class JitCompiler {
    /// <summary>
    /// 将指令序列编译为原生委托
    /// 编译耗时约 ~2,000-5,000ns，编译后执行 ~3-5ns
    /// </summary>
    public static Func<IReadOnlyDictionary<string, double>?, double> Compile(ReadOnlySpan<Instruction> instructions);
}
```

**编译流程**:
1. 创建 `DynamicMethod`，签名 `double(IReadOnlyDictionary<string, double>?)`
2. 遍历指令序列，逐条生成 IL：
   - `PushConst` → `ldc.r8 <value>`
   - `Add` → `add`
   - `Sub` → `sub`
   - `Call sin` → `call Math.Sin`
   - `LoadVar` → `ldarg.0; call Dictionary.TryGetValue; ...`
3. `ILGenerator.CreateDelegate()` 生成可执行委托

**与 Expression Trees (lambda) 的区别**:
- 目的相同：都是生成可执行委托
- DynamicMethod 直接写 IL，编译更快（~2,000-5,000ns vs ~20,000-100,000ns）
- 编译后执行速度相同（~3-5ns）
- DynamicMethod 代码更底层但更高效

### 3.2 编译结果缓存

JIT 编译结果也存入 LRU 缓存，与指令序列缓存共享同一个 key（表达式字符串）。

**新文件**: `MathEval.Fast\Jit\JitCache.cs`

```csharp
internal static class JitCache {
    private static readonly LruCache<string, CompiledEntry> _cache = new(64);

    private class CompiledEntry {
        public Instruction[] Instructions;  // 指令序列
        public Func<IReadOnlyDictionary<string, double>?, double>? CompiledFunc;  // JIT 编译结果（可选）
    }

    public static Instruction[] GetOrCompileInstructions(string expression);
    public static Func<IReadOnlyDictionary<string, double>?, double> GetOrCompileJit(string expression);
    public static void Clear();
}
```

### 3.3 公共 API 扩展

**文件**: `MathEval.Fast\FastEval.cs`

```csharp
/// <summary>
/// 编译表达式为原生委托
/// 编译耗时约 2,000-5,000ns，编译后执行约 3-5ns
/// 适合同一表达式需要大量重复求值的场景
/// </summary>
public static Func<IReadOnlyDictionary<string, double>?, double> Compile(string expression);

/// <summary>
/// 编译并缓存，后续调用直接返回缓存的委托
/// </summary>
public static Func<IReadOnlyDictionary<string, double>?, double> CompileCached(string expression);
```

**调用方使用示例**:
```csharp
// 单次求值 — 直接解释执行
var result = FastEval.EvalDouble("sin(0.5) + cos(0.3)");

// 重复求值同一表达式 — 使用缓存
for (int i = 0; i < 10000; i++) {
    var result = FastEval.EvalDoubleCached("sin(x) + cos(y)", vars);
}

// 高频重复求值 — JIT 编译
var fn = FastEval.CompileCached("sin(x) + cos(y)");
for (int i = 0; i < 1000000; i++) {
    var result = fn(vars);
}
```

---

## 实施计划

### 阶段一：微优化

| 步骤 | 内容 | 涉及文件 |
|------|------|----------|
| 1 | FastEvaluator 改为 ref struct，内联 FastScanner | `FastEvaluator.cs`, `FastEval.cs`, 删除 `FastScanner.cs` |
| 2 | 消除 ToString()，BuiltInFunctions/Constants 支持 Span 查找 | `FastEvaluator.cs`, `BuiltInFunctions.cs`, `BuiltInConstants.cs` |
| 3 | 函数参数用 stackalloc | `FastEvaluator.cs`, `BuiltInFunctions.cs` |
| 4 | 内联简单运算符 | `FastEvaluator.cs` |
| 5 | MatchKeyword 合并到标识符解析 | `FastEvaluator.cs` |
| 6 | 运行测试 + Benchmark 验证 | - |
| 7 | **Git 提交** — `feat: FastEval 微优化 - ref struct + Span + stackalloc + 内联运算符` | - |

### 阶段二：字节码 VM + 缓存

| 步骤 | 内容 | 涉及文件 |
|------|------|----------|
| 8 | 定义 OpCode + Instruction | 新建 `VM\OpCode.cs`, `VM\Instruction.cs` |
| 9 | 实现 LruCache | 新建 `VM\LruCache.cs` |
| 10 | 实现 BytecodeCompiler | 新建 `VM\BytecodeCompiler.cs` |
| 11 | 实现 BytecodeVM 执行器 | 新建 `VM\BytecodeVM.cs` |
| 12 | 实现 InstructionCache | 新建 `VM\InstructionCache.cs` |
| 13 | 扩展 FastEval 公共 API | `FastEval.cs` |
| 14 | 运行测试 + Benchmark 验证 | - |
| 15 | **Git 提交** — `feat: 字节码 VM + LRU 缓存` | - |

### 阶段三：JIT 编译

| 步骤 | 内容 | 涉及文件 |
|------|------|----------|
| 16 | 实现 JitCompiler | 新建 `Jit\JitCompiler.cs` |
| 17 | 实现 JitCache | 新建 `Jit\JitCache.cs` |
| 18 | 扩展 FastEval 公共 API | `FastEval.cs` |
| 19 | 运行测试 + Benchmark 验证 | - |
| 20 | **Git 提交** — `feat: DynamicMethod JIT 编译` | - |

## 假设与决策

1. **阶段一不改变公共 API** — `FastEval.EvalDouble()` 签名不变，优化完全内部化
2. **阶段二新增 `EvalDoubleCached()` 方法** — 不修改原有方法语义，缓存是显式 opt-in
3. **阶段三由调用方显式选择 JIT** — 不自动分层，提供 `Compile()` / `CompileCached()` 方法
4. **JIT 使用 DynamicMethod + ILGenerator** — 编译更快（~2,000-5,000ns），比 Expression Trees 快 5-10x
5. **缓存策略为 LRU 淘汰** — 默认容量 256 条，超出时淘汰最久未使用；JIT 缓存默认 64 条
6. **缓存按表达式字符串精确匹配** — 两个只有局部不同的表达式各自独立缓存
7. **内置函数表保持硬编码** — 不引入自定义函数注册机制，保持 Fast 版的简洁定位
8. **变量查找仍用 Dictionary** — 变量名动态性太强，Trie 优势不明显
9. **每个阶段独立验证** — 完成一个阶段后运行全部测试 + Benchmark，确认无回归再进入下一阶段

## 验证步骤

每个阶段完成后：

1. 运行 `dotnet test` 确保所有现有测试通过
2. 运行 BenchmarkDotNet 对比优化前后性能
3. 检查内存分配是否减少（MemoryDiagnoser 的 Allocated 列）
4. 确认公共 API 无破坏性变更
