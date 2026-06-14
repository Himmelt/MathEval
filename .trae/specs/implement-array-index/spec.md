# MathEval 一维数组索引操作实现 Spec

## Why
MathEval 当前仅支持标量类型（long/double/bool/string），用户需要通过 context.Set("arr", array) 注册一维数组变量，并在表达式中使用 arr[0]、arr[i] 等索引语法访问元素。带 [] 索引的数组本质上相当于特殊处理的"symbol"。

## What Changes
- Lexer: 新增 LeftBracket / RightBracket Token 类型
- Parser: 在标识符解析后增加 [index] 检测，将 arr[i] 解析为 ArrayIndexExpression AST 节点
- AST: 新增 ArrayIndexExpression 节点类，扩展 IExpressionVisitor 接口
- EvaluationVisitor: 新增 Visit(ArrayIndexExpression) 方法，从上下文获取数组并按索引取值
- TypeHelper: 扩展类型判断支持数组元素
- ExpressionContext: 无需修改（object 类型天然支持数组）
- 单元测试: 覆盖各种数组索引场景

## Impact
- Affected code: MathEval/Lexer/, MathEval/Parser/, MathEval/AST/, MathEval/Visitors/, MathEval/TypeSystem/

## ADDED Requirements

### Requirement: 数组索引表达式解析
系统 SHALL 支持在表达式中使用 `identifier[expression]` 语法访问一维数组元素。

#### Scenario: 常量索引
- **WHEN** 用户注册 `context.Set("arr", new double[]{1,2,3})` 并求值表达式 `"arr[0]"`
- **THEN** 结果为 1.0

#### Scenario: 变量索引
- **WHEN** 用户注册 `context.Set("arr", new double[]{1,2,3})` 和 `context.Set("i", 2)` 并求值表达式 `"arr[i]"`
- **THEN** 结果为 3.0

#### Scenario: 表达式索引
- **WHEN** 用户注册 `context.Set("arr", new double[]{1,2,3})` 和 `context.Set("i", 1)` 并求值表达式 `"arr[i+1]"`
- **THEN** 结果为 3.0

#### Scenario: 数组元素参与运算
- **WHEN** 用户注册数组并求值 `"arr[0] + arr[1]"`
- **THEN** 结果为 3.0

#### Scenario: 索引越界
- **WHEN** 求值 `"arr[5]"` 且数组长度为 3
- **THEN** 抛出求值异常

#### Scenario: 非数组变量使用索引
- **WHEN** 对标量变量使用索引 `"x[0]"`
- **THEN** 抛出类型不匹配异常

### Requirement: 仅支持一维数组
系统 SHALL 仅支持一维数组（IList）的元素索引操作，不支持多维数组、切片或链式索引。

### Requirement: 数组索引作为特殊 symbol
带 [] 索引的数组访问本质上相当于特殊处理的 symbol——在求值时从上下文获取数组对象，再按索引取值。不需要引入新的类型系统或类型推断。
