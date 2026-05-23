# MathEval 代码审查报告

> 审查时间：2026-05-23  
> 审查范围：MathEval 主库全部源代码  
> 测试验证：2026-05-23，新增 `CrossValidationTests.cs`（107 个测试），全量测试 637 个

---

## 总体评估

**代码质量评分：4/5**

项目架构清晰，使用了 Visitor 模式、Builder 模式等良好设计，代码注释完善。通过交叉验证测试发现 2 个确认 Bug，其余多为设计层面的改进建议。

---

## 测试验证结果

新增 `CrossValidationTests.cs` 对两套求值器进行交叉验证，并针对审查问题逐一编写验证测试。全量测试 **637 个，634 通过，3 失败**：

| 失败测试 | 对应问题 | 结论 |
|---------|---------|------|
| `PI_MatchesMathPI` | #8 PI/E 精度 | **确认 Bug** |
| `E_MatchesMathE` | #8 PI/E 精度 | **确认 Bug** |
| `Power_NegativeBaseNegativeExponent_Long` | #15 EvaluatePower 过度限制 | **确认 Bug（比预期更严重）** |

**已通过测试证伪的问题：**

| 原问题编号 | 内容 | 结论 |
|-----------|------|------|
| ~~原 #1~~ | `&&`/`||` 解析错误 | **误报**：`ParseLogicalAnd` 调用的是 `ParseEquality`（非 `ParseBitwiseAnd`），`&&`/`||` 在逻辑层正确识别，测试通过 |
| ~~原 #5~~ | FastEval 缺 NaN/INF 处理 | **不成立**：交叉验证全部通过，IEEE 754 双精度浮点运算天然保证 NaN/INF 行为一致 |
| ~~原 #14~~ | Lexer 十六进制转义抛 FormatException | **不成立**：测试 `Lexer_InvalidHexEscape_ThrowsParseException` 通过，`Convert.ToInt32` 的异常被正确包装为 `ParseException` |

---

## 确认 Bug（测试失败）

### 1. 内置常量 PI/E 精度不足

**严重等级：** High  
**位置：** `Functions/BuiltInFunctions.cs` L11-L12  
**测试：** `CrossValidationTests.PI_MatchesMathPI`、`CrossValidationTests.E_MatchesMathE`

**问题：** 硬编码 `3.14159265358979`（14 位）和 `2.71828182845905`（14 位），与 `Math.PI`（3.14159265358979**31**）和 `Math.E`（2.71828182845905**16**）存在精度差异。

**测试输出：**
```
Expected: 3.1415926535897931
Actual:   3.14159265358979
```

**修复：**
```csharp
context.Set("PI", Math.PI);
context.Set("E", Math.E);
```

---

### 2. EvaluatePower 对负底数+负整数指数的过度限制

**严重等级：** High  
**位置：** `TypeSystem/TypeHelper.cs` L225  
**测试：** `CrossValidationTests.Power_NegativeBaseNegativeExponent_Long`

**问题：** `(-2) ^ (-3)` 在数学上等于 `-0.125`，是合法的运算。但代码无条件拒绝所有负底数+负指数的情况：

```csharp
if (l1 < 0 && l2 < 0) throw new EvaluateException("不能对负数求负数次幂");
```

**测试输出：**
```
MathEval.Exceptions.EvaluateException : 不能对负数求负数次幂
```

**说明：** 真正应拒绝的是负底数+非整数指数（如 `(-2)^0.5`），而负整数指数（如 `(-2)^(-3) = -0.125`）是合法运算。此外，紧随其后的死代码检查 `l1 < 0 && l2 != (long)Math.Floor((double)l2)` 因 `l2` 是 `long` 类型，条件永远为 `false`。

**修复建议：**
```csharp
// 删除 if (l1 < 0 && l2 < 0) 的无条件拒绝
// 死代码检查可一并移除（l2 是 long，始终为整数）
```

---

## 设计层面问题（无法通过单元测试验证）

以下问题属于非功能性质量属性，无法通过功能测试发现，需通过代码审查、压力测试或静态分析识别。

### 3. Calculator 存在线程安全问题

**严重等级：** Medium  
**位置：** `Calculator.cs` L48-L68

`EnsureParsed()` 和 `EnsureCompiled()` 使用 "检查-然后执行" 模式（check-then-act），在多线程环境下存在竞态条件。`_ast` 和 `_compiledExpression` 字段没有同步保护，可能导致重复解析/编译或引用不一致。

**建议：** 若 Calculator 仅用于单线程场景，请在文档中明确说明；否则考虑使用 `Lazy<T>` 或加锁。

---

### 4. 静态缓存无大小限制，存在内存泄漏风险

**严重等级：** Medium  
**位置：** `Internal/ExpressionCache.cs` L8、`Optimization/OptimizedExpressionCache.cs` L11

两个静态 `ConcurrentDictionary` 缓存没有大小上限或淘汰策略。在长时间运行的服务中，如果表达式是动态生成的，缓存会无限增长。

**建议：** 考虑使用 LRU 缓存策略或 `MemoryCache`，或至少在文档中说明 `Clear()` 方法的使用场景。

---

### 5. OptimizedExpressionCache.GetOrAddCompiled 线程安全问题

**严重等级：** Medium  
**位置：** `Optimization/OptimizedExpressionCache.cs` L70-L87

第 84-85 行 `entry.Compiled = compiledExpr` 在没有同步的情况下修改共享条目。多个线程可能同时编译同一表达式，且写入可能相互覆盖。

---

### 6. TypeHelper.EvaluateEqual 潜在 NullReferenceException

**严重等级：** Low  
**位置：** `TypeSystem/TypeHelper.cs` L289

```csharp
if (left.GetType() != right.GetType()) return false;
```

当 `left` 或 `right` 为 `null` 时，`GetType()` 会抛出 `NullReferenceException`。当前代码路径中不易触发（求值器通常不产生 null 值），但作为公共静态方法应做防御性处理。

---

### 7. FastEvaluator 中 `_skipMode` 处理不一致

**严重等级：** Low  
**位置：** `Fast/FastEvaluator{T}.cs`

**问题：** 三元运算符短路求值时，`EvalMultiplicative` 中的 `Multiply`/`Divide` 检查了 `_skipMode`，但 `EvalAdditive` 和 `EvalPower` 不检查。

**测试结论：** 交叉验证测试 `Ternary_SkipMode_ComplexExprInSkippedBranch` 和 `Ternary_SkipMode_PowerInSkippedBranch` 均通过，当前场景下不会引发异常。但不一致的代码风格增加维护风险。

---

## 改进建议（非 Bug）

### 8. FastScanner 十六进制/八进制/二进制解析使用 Substring

**位置：** `Fast/FastScanner.cs` L137-L159

`ReadHexAsLong` 等方法使用 `_text.Substring()` 分配新字符串，违背 "零字符串分配" 设计目标。可改用 `Convert.ToInt64(ReadOnlySpan<char>, int)`。

---

### 9. FastEvaluator.LookupVariable 使用线性遍历

**位置：** `Fast/FastEvaluator{T}.cs` L381-L389

变量查找通过遍历整个字典匹配 `ReadOnlySpan<char>`，时间复杂度 O(n)。

---

### 10. FunctionWrapper 和 ExpressionBuilder 大量重复代码

**位置：** `Internal/FunctionWrapper.cs`（149 行）、`ExpressionBuilder.cs`（112 行）

8 个 `Wrap` 方法和 8 个 `WithFunction` 重载结构几乎相同。可考虑源码生成器减少重复。

---

### 11. ConstantFolder.FoldFunction 未实际实现

**位置：** `Optimization/ConstantFolder.cs` L64-L76

方法检测到所有参数都是常量后仍返回原始表达式，应标注 TODO。

---

### 12. FunctionCall.Arguments 和 InterpolatedString.Segments 暴露可变 List

**位置：** `AST/FunctionCall.cs` L22、`AST/InterpolatedString.cs` L16

外部代码可修改 AST 节点的内部集合，建议改用 `IReadOnlyList<T>`。

---

### 13. EvaluationVisitor 每次求值创建新实例

**位置：** `Calculator.cs` L28

Visitor 无状态（除 context 外），可复用同一实例减少 GC 压力。

---

## 汇总

| 类别 | 数量 | 说明 |
|------|------|------|
| 确认 Bug（测试失败） | 2 | PI/E 精度、EvaluatePower 过度限制 |
| 设计层面问题 | 5 | 线程安全、缓存策略、null 防护、skipMode |
| 改进建议 | 6 | 性能优化、代码重复、不可变性 |
| 已证伪 | 3 | `&&`/`||` 解析、NaN/INF 一致性、Lexer 转义 |

## 优先修复建议

1. **立即修复** PI/E 精度问题（#1），改用 `Math.PI` 和 `Math.E`
2. **尽快修复** EvaluatePower 过度限制（#2），允许负底数+负整数指数
3. **短期规划** 缓存淘汰策略（#4）和线程安全文档/修复（#3, #5）
