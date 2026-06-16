# MathEval 类型系统重构与数组支持 Spec

## Why
重构 MathEval 类型系统，将 long/double/bool/string 四类型统一为 double/double[] 双类型，移除字符串运算，新增数组支持。

## What Changes
- Lexer: 合并 Integer/Float/Boolean → Number，新增 LeftBracket/RightBracket，移除 String/InterpolatedString
- Parser: 统一 Number 处理，移除字符串/插值解析，新增数组常量/索引解析
- AST: 移除 InterpolatedString 等 4 个类，新增 ArrayLiteralExpression/ArrayIndexExpression
- TypeHelper: 重写为纯 double 运算 + 数组运算
- EvaluationVisitor: 适配新类型体系，新增数组求值 + 函数广播
- Calculator: 适配 ConvertResult
- CompiledExpression: 移除插值编译，新增数组编译
- ConstantFolder: 新增数组节点折叠
- 新增 IndexPushdownOptimizer: 索引下推优化

## ADDED Requirements
### Requirement: 统一数值类型
系统 SHALL 将 bool/long/double 统一为 double。true→1.0, false→0.0, 整数直接解析为 double。

### Requirement: 数组常量
系统 SHALL 支持 [expr, expr, ...] 语法创建 double[] 数组常量。

### Requirement: 数组索引
系统 SHALL 支持 expr[index] 语法访问数组元素。index 可以是任意表达式。

### Requirement: 数组逐元素运算
系统 SHALL 支持数组与标量/数组的逐元素运算（广播机制）。

### Requirement: 函数数组广播
系统 SHALL 支持函数参数为数组时自动逐元素求值。

### Requirement: 索引下推优化
系统 SHALL 在求值前将 (arr*scalar)[i] 等模式优化为 arr[i]*scalar，避免全量计算。

### Requirement: 字符串运算移除
系统 SHALL 不再支持表达式内的字符串运算和插值字符串。
