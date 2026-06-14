# 数组元素索引操作 (array[index]) 支持分析 Spec

## Why
MathEval 和 MathEval.Fast 当前仅支持标量类型（long/double/bool/string），无法处理数组类型的上下文变量和数组索引表达式。用户需要通过 `context.Set("arrayName", array)` 注册数组变量，并在表达式中使用 `arrayName[0]`、`arrayName[i]` 等索引操作访问元素。本分析报告将分别评估 MathEval 和 MathEval.Fast 支持数组索引操作的改造难度、复杂度及性能影响，并参考 NFun 库的数组实现方案。

## What Changes
- 分析 MathEval（AST 模式）支持数组索引的改造方案
- 分析 MathEval.Fast（零 AST 模式）支持数组索引的改造方案
- 参考 NFun 的数组支持架构，提炼可借鉴的设计思路
- 评估改造对性能的影响
- 将分析报告输出到飞书文档

## Impact
- Affected specs: MathEval 词法分析器、语法分析器、AST 节点体系、求值器、类型系统、上下文管理；MathEval.Fast 扫描器、求值器、字节码编译器、字节码虚拟机、JIT 编译器
- Affected code: MathEval/ 全部模块, MathEval.Fast/ 全部模块

## ADDED Requirements
### Requirement: 数组索引操作分析报告
系统 SHALL 生成一份详细的分析报告，覆盖以下维度：

#### Scenario: MathEval 改造分析
- **WHEN** 分析 MathEval 支持 array[index] 的改造方案
- **THEN** 报告应包含：词法层改造点、语法层改造点、AST 节点扩展、求值器改造、类型系统扩展、上下文管理改造、改造难度评级、改造复杂度评估

#### Scenario: MathEval.Fast 改造分析
- **WHEN** 分析 MathEval.Fast 支持 array[index] 的改造方案
- **THEN** 报告应包含：扫描器改造点、求值器改造、字节码编译器改造、字节码虚拟机改造、JIT 编译器改造、改造难度评级、改造复杂度评估

#### Scenario: NFun 参考分析
- **WHEN** 参考 NFun 库的数组支持实现
- **THEN** 报告应包含：NFun 数组类型系统设计、NFun 数组索引解析机制、NFun 数组求值机制、可借鉴的设计思路

#### Scenario: 性能影响评估
- **WHEN** 评估数组索引支持对性能的影响
- **THEN** 报告应包含：MathEval 性能影响分析、MathEval.Fast 性能影响分析、优化建议

#### Scenario: 报告输出到飞书文档
- **WHEN** 分析报告完成
- **THEN** 报告 SHALL 通过 lark-doc 技能发送到飞书文档
