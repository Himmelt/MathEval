# MathEval 表达式计算器 — 产品需求文档

> **版本**：v1.2
> **最后更新**：2026-05-18
> **状态**：已发布

---

## 目录

1. [项目概述](#1-项目概述)
   - 1.1 [项目背景](#11-项目背景)
   - 1.2 [项目特性](#12-项目特性)
2. [功能需求](#2-功能需求)
   - 2.1 [类型系统](#21-类型系统)
   - 2.2 [词法与语法规则](#22-词法与语法规则)
   - 2.3 [运算符支持](#23-运算符支持)
   - 2.4 [字符串插值](#24-字符串插值)
   - 2.5 [上下文系统](#25-上下文系统)
   - 2.6 [函数注册代理](#26-函数注册代理)
   - 2.7 [内置数学函数与常量](#27-内置数学函数与常量)
   - 2.8 [Expression 主入口 API](#28-expression-主入口-api)
   - 2.9 [AST 与 Visitor 模式](#29-ast-与-visitor-模式)
   - 2.10 [表达式缓存](#210-表达式缓存)
3. [非功能需求](#3-非功能需求)
   - 3.1 [多线程安全](#31-多线程安全)
   - 3.2 [错误处理](#32-错误处理)
4. [设计决策记录](#4-设计决策记录)
   - 4.1 [数值类型策略](#41-数值类型策略)
   - 4.2 [`^` 表示乘方的设计原理](#42-^-表示乘方的设计原理)
   - 4.3 [布尔参与算术与比较的类型转换策略](#43-布尔参与算术与比较的类型转换策略)
5. [附录](#5-附录)
   - 5.1 [术语表](#51-术语表)

---

## 1. 项目概述

### 1.1 项目背景

已知两个优秀的 .NET 表达式计算开源项目，[NFun](https://github.com/tmteam/NFun) 和 [NCalc](https://github.com/ncalc/ncalc)，但它们各有侧重：NFun 拥有强大的类型推断、字符串插值和上下文继承机制，但 API 较重、语言特性过多；NCalc 拥有简洁的 API、灵活的扩展机制和成熟的 Visitor 模式，但缺乏字符串插值和上下文继承。我们需要一个轻量级表达式计算器，融合两者优势，仅聚焦布尔、数值、字符串三种类型的表达式计算，同时支持多种数学运算、逻辑运算、函数注册、强类型代理注册、符号注册、动态上下文操作和继承，以及多线程安全。

### 1.2 项目特性

MathEval 是一个面向 .NET 10+ 的轻量级表达式计算引擎，核心特性：

- 支持布尔（`bool`）、数值（`number`）、字符串（`string`）三种类型
- 完整的运算符体系：算术、关系、逻辑、位运算
- 类似 C# 风格字符串插值（`$"...{expr}..."`）
- 上下文继承与符号系统（直接值 + 延迟值`Func<T>`）
- 强类型函数注册代理（`Func<T, object> 或 Delegate` 作为函数参数）
- [AST](#ast) + [Visitor](#visitor) 可扩展架构
- 表达式缓存
- 多线程安全

---

## 2. 功能需求

### 2.1 类型系统

系统 [SHALL](#shall) 仅支持以下三种值类型：

| 类型 | 关键字 | 示例 |
|------|--------|------|
| 布尔 | `bool` | `true`, `false` |
| 数值 | `number` | `42`, `3.14`, `1e-5` |
| 字符串 | `string` | `'hello'`, `"world"` |

数值类型内部采用 `long` + `double` 双类型策略：
- 整数字面量和整数运算结果使用 `long`（64位有符号整数）表示
- 浮点字面量和涉及浮点数的运算结果使用 `double`（64位双精度浮点数）表示

位运算操作时，数值会被视为 64 位有符号整数（long）进行运算，结果返回 `long` 类型。

#### 2.1.1 数值字面量格式

系统 [SHALL](#shall) 支持以下数值字面量格式：

| 格式 | 前缀 | 允许的数字字符 | 示例 | 十进制值 |
|------|------|----------------|------|----------|
| 十进制 | 无 | `0-9` | `42`, `3.14`, `1e-5` | 42, 3.14, 0.00001 |
| 十六进制 | `0x` / `0X` | `0-9`, `a-f`, `A-F` | `0xFF`, `0x1A3` | 255, 419 |
| 八进制 | `0o` / `0O` | `0-7` | `0o77`, `0o12` | 63, 10 |
| 二进制 | `0b` / `0B` | `0-1` | `0b1010`, `0b11111111` | 10, 255 |

**规则**：
- 十六进制、八进制、二进制字面量仅支持整数值，不支持小数点或指数部分
- 前缀后的数字字符必须符合对应进制的合法范围，否则抛出 `ParseException`
- 前缀后必须至少有一个数字字符（`0x`、`0o`、`0b` 单独出现抛出 `ParseException`）
- 所有进制的整数字面量统一转换为 `long` 类型
- 十六进制、八进制、二进制字面量支持 `+`(一元) 和 `-`(一元) 前缀（如 `-0xFF` = -255）

#### 2.1.2 场景

##### 字面量解析

| 类型 | 输入示例 | 结果类型 | 结果值 | 说明 |
|------|----------|----------|--------|------|
| 布尔 | `true`, `false` | bool | - | 直接解析为布尔值 |
| 十进制整数 | `42`, `-100` | long | 42, -100 | 无小数点，精确整数 |
| 十进制浮点 | `3.14`, `1e-5` | double | 3.14, 0.00001 | 含小数点或指数 |
| 十六进制 | `0xFF`, `0XaB` | long | 255, 171 | 前缀和数字均不区分大小写 |
| 八进制 | `0o77` | long | 63 | 前缀 `0o` 或 `0O` |
| 二进制 | `0b1010` | long | 10 | 前缀 `0b` 或 `0B` |
| 字符串 | `'hello'`, `"world"` | string | - | 单引号或双引号包裹，支持转义字符 |

#### 2.1.3 字符串转义规则

字符串字面量支持以下转义字符：

| 转义序列 | 含义 | 示例 | 结果 |
|----------|------|------|------|
| `\'` | 单引号 | `'He said \'hello\''` | `He said 'hello'` |
| `\"` | 双引号 | `"He said \"hello\""` | `He said "hello"` |
| `\\` | 反斜杠 | `'C:\\path\\file'` | `C:\path\file` |
| `\n` | 换行符 | `'line1\nline2'` | 两行文本 |
| `\r` | 回车符 | - | - |
| `\t` | 水平制表符 | `'col1\tcol2'` | 列之间有制表符 |
| `\b` | 退格符 | - | - |
| `\f` | 换页符 | - | - |
| `\0` | 空字符 | - | - |
| `\xHH` | 十六进制字符（HH 为两位十六进制数） | `'\x41'` | `A` |
| `\uHHHH` | Unicode 字符（HHHH 为四位十六进制数） | `'\u03C0'` | `π` |

**注意**：
- 在单引号字符串中，单引号必须转义，双引号可以不转义
- 在双引号字符串中，双引号必须转义，单引号可以不转义
- 未识别的转义序列抛出 `ParseException`（采用严格策略）
- 例如：`"\q"` → `ParseException`（`q` 不是有效的转义字符）

#### 2.1.4 运算场景

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 不同进制混合运算 | `0xFF + 0o10 + 0b1010` | `273` (long) | 255 + 8 + 10 |
| 十六进制与位运算 | `0xFF & 0x0F` | `15` (long) | 255 & 15 |
| 二进制与位运算 | `0b1100 & 0b1010` | `8` (long) | 12 & 10 |
| 十六进制负数 | `-0xFF` | `-255` (long) | 一元负号 |
| 字符串拼接 | `'x' + 42` | `'x42'` (string) | 数值自动转字符串 |
| 布尔参与算术 | `true + 1` | `2` (long) | true→1, false→0 |
| 布尔与浮点算术 | `true + 1.5` | `2.5` (double) | true→1, 类型向上传播 |
| 位运算结果 | `5 & 3` | `1` (long) | 在 long 上直接运算 |

#### 2.1.5 错误场景

| 错误类型 | 输入示例 | 异常 | 说明 |
|----------|----------|------|------|
| 非法十六进制数字 | `0xGH` | ParseException | G、H 不是合法十六进制数字 |
| 非法八进制数字 | `0o89` | ParseException | 8、9 不是合法八进制数字 |
| 非法二进制数字 | `0b12` | ParseException | 2 不是合法二进制数字 |
| 前缀后无数字 | `0x`, `0o`, `0b` | ParseException | 前缀后必须至少有一个数字 |

#### 2.1.6 数值类型推断规则

| 条件 | 结果类型 | 示例 | 说明 |
|------|----------|------|------|
| 字面量含小数点或指数 | double | `3.14`, `1e5`, `2.0` | 显式浮点格式 |
| 字面量为纯整数 | long | `42`, `0xFF`, `0b1010` | 无小数点和指数 |
| 运算涉及 double 操作数 | double | `3.14 + 1`, `2.0 * 3` | 类型向上传播 |
| 纯整数运算且结果精确 | long | `5 + 3`, `0xFF & 0x0F` | 整数域封闭 |
| 除法运算 `/` | double | `7 / 2` → `3.5` | 浮点除法 |
| 整除运算 `//`（纯整数操作数） | long | `7 // 2` → `3` | 结果为 long |
| 整除运算 `//`（含 double 操作数） | long | `7.5 // 2` → `3` | double 操作数隐式转换为 long（舍去小数部分）后计算，结果为 long |
| 取模运算 `%`（纯整数操作数） | long | `7 % 3` → `1` | 整数取模 |
| 取模运算 `%`（含 double 操作数） | double | `7.5 % 2` → `1.5` | 浮点取模 |
| 位运算 | long | `5 & 3` → `1` | 内部用 long 计算 |
| 乘方结果非整数 | double | `2 ^ 0.5` → `1.414...` | 浮点结果 |
| 布尔参与算术运算 | long（若另一操作数为 long） | `true + 1` → `2` ，`true + true` → `2` | 布尔转整数（true→1, false→0） |
| 布尔参与算术运算 | double（若另一操作数为 double） | `true + 1.5` → `2.5` | 布尔转整数后类型向上传播 |

**类型推断优先级**（从高到低）：
1. 显式浮点字面量 → double
2. 显式整数字面量 → long
3. 运算传播：任一操作数为 double → 结果为 double
4. 特殊运算符：`/` → double，`//` → long
5. 默认：整数运算 → long

#### 2.1.7 溢出行为

| 类型 | 溢出行为 | 示例 | 结果 |
|------|----------|------|------|
| `long` 运算溢出 | 抛出 `OverflowException` | `9223372036854775807 + 1` | `OverflowException` |
| `double` 运算溢出 | 产生 `INF` | `1e308 * 10` | `INF` |

**注意**：此处的 `OverflowException` 为 MathEval 自定义异常（继承自 `EvaluateException`），非 .NET BCL 的 `System.OverflowException`。

#### 2.1.8 表达式限制

- 表达式字符串最大长度：**4096** 字符
- AST 嵌套深度最大：**1024** 层
- 超出限制时抛出 `ParseException`

---

### 2.2 词法与语法规则

#### 2.2.1 词法规则（Lexer）

**Token 类型**：
- 标识符（Identifier）：首字符为字母（大小写均可）、下划线或 Unicode 字母（包括中文），后续字符可为字母、数字、下划线或 Unicode 字母/数字
- 关键字（Keyword）：`true`, `false`, `and`, `or`, `not`, `xor`, `NaN`, `INF`
- 数字字面量（Number）：十进制、十六进制（`0x`）、八进制（`0o`）、二进制（`0b`）
- 字符串字面量（String）：单引号或双引号包裹，支持转义字符
- 运算符（Operator）：`+`, `-`, `*`, `/`, `//`, `%`, `^`, `&`, `|`, `<<`, `>>`, `==`, `!=`, `<`, `>`, `<=`, `>=`, `?`, `:`, `!`, `~`
- 分隔符（Delimiter）：`(`, `)`, `,`

**标识符与关键字的区分**：
- 关键字不区分大小写，但建议使用小写
- 关键字禁止用作标识符
- **注意**：关键字不区分大小写（如 `AND`、`and`、`And` 均为同一关键字），但标识符区分大小写（如 `MyVar` 与 `myvar` 是不同的标识符）。请勿将关键字的任意大小写形式用作标识符

#### 2.2.2 EBNF 语法（表达式文法）

```ebnf
(* 基本词法 *)
digit        = "0" | "1" | ... | "9" ;
letter       = "A" | "B" | ... | "Z" | "a" | "b" | ... | "z" | "_" ;
hexDigit     = digit | "A" | "B" | ... | "F" ;
integer      = digit+ ;
fraction     = "." integer ;
exponent     = ("E" | "e") ["+" | "-"] integer ;
number       = integer [fraction] [exponent]
              | "0x" hexDigit+
              | "0o" ["0"-"7"]+
              | "0b" ("0" | "1")+ ;
identifier   = letter (letter | digit)* ;
string       = "'" (escape | ~"'"任意字符)* "'"
              | '"' (escape | ~'"'任意字符)* '"' ;
(* 表达式 *)
keyword     = "true" | "false" | "and" | "or" | "not" | "xor" | "NaN" | "INF" ;
primary     = number | keyword | identifier | string | interpolatedString
              | "(" expression ")" ;
functionCall = identifier "(" [expression ("," expression)*] ")" ;
interpolatedString = "$" ("'" interpolatedContent* "'"
              | '"' interpolatedContent* '"') ;
formatSpec = ("D" | "d" | "E" | "e" | "F" | "f" | "G" | "g" | "X" | "x") digit* ;
interpolatedContent = "{" expression [":" formatSpec] "}"
              | "{{" | "}}" | ~( "{" | "}" ) ;
unary        = ("+" | "-" | "not" | "!" | "~") unary
              | primary | functionCall ;
power        = unary ["^" power] ;
multiplicative = power (("*" | "/" | "//" | "%") power)* ;
additive     = multiplicative (("+" | "-") multiplicative)* ;
shift        = additive (("<<" | ">>") additive)* ;
bitwiseAnd   = shift ("&" shift)* ;
bitwiseXor   = bitwiseAnd ("xor" bitwiseAnd)* ;
bitwiseOr    = bitwiseXor ("|" bitwiseXor)* ;
relational   = bitwiseOr (("<" | ">" | "<=" | ">=") bitwiseOr)* ;
equality     = relational (("==" | "!=") relational)* ;
logicalAnd   = equality (("and" | "&&") equality)* ;
logicalOr    = logicalAnd (("or" | "||") logicalAnd)* ;
conditional  = logicalOr ["?" expression ":" expression] ;
expression   = conditional ;
```

---

### 2.3 运算符支持

系统 SHALL 支持以下运算符，按优先级从高到低排列：

| 优先级 | 运算符 | 含义 | 结合性 | 适用类型 |
|--------|--------|------|--------|----------|
| 1 | `()` | 分组 | - | 所有 |
| 2 | `+` | 正号（恒等） | 右结合 | number |
| 2 | `-` | 取负 | 右结合 | number |
| 2 | `not` / `!` | 逻辑非 | 右结合 | bool |
| 2 | `~` | 按位取反 | 右结合 | number |
| 3 | `^` | 乘方 | 右结合 | number |
| 4 | `*` | 乘法 | 左结合 | number |
| 4 | `/` | 除法（浮点） | 左结合 | number |
| 4 | `//` | 整除 | 左结合 | number |
| 4 | `%` | 取模（取余） | 左结合 | number |
| 5 | `+` | 加法 / 字符串拼接 | 左结合 | number, string |
| 5 | `-` | 减法 | 左结合 | number |
| 6 | `<<` | 左移 | 左结合 | number |
| 6 | `>>` | 右移 | 左结合 | number |
| 7 | `&` | 按位与 | 左结合 | number |
| 8 | `xor` | 按位异或 | 左结合 | number |
| 9 | `\|` | 按位或 | 左结合 | number |
| 10 | `>` | 大于 | 左结合 | number, string |
| 10 | `<` | 小于 | 左结合 | number, string |
| 10 | `>=` | 大于等于 | 左结合 | number, string |
| 10 | `<=` | 小于等于 | 左结合 | number, string |
| 11 | `==` | 等于 | 左结合 | 所有 |
| 11 | `!=` | 不等于 | 左结合 | 所有 |
| 12 | `and` / `&&` | 逻辑与（短路） | 左结合 | bool |
| 13 | `or` / `\|\|` | 逻辑或（短路） | 左结合 | bool |
| 14 | `?:` | 三元条件 | 右结合 | bool, 所有 |

#### 2.3.1 运算符符号与关键字说明

| 运算符对 | 关系 | 说明 |
|----------|------|------|
| `and` 与 `&&` | 完全等价 | 逻辑与，`and` 为关键字形式，`&&` 为符号形式 |
| `or` 与 `\|\|` | 完全等价 | 逻辑或，`or` 为关键字形式，`\|\|` 为符号形式 |
| `not` 与 `!` | 完全等价 | 逻辑非，`not` 为关键字形式，`!` 为符号形式 |
| `&` 与 `&&` | **完全不同** | `&` 是按位与（位运算），`&&` 是逻辑与（布尔短路运算），不可混用 |
| `\|` 与 `\|\|` | **完全不同** | `\|` 是按位或（位运算），`\|\|` 是逻辑或（布尔短路运算），不可混用 |
| `^` 与 `xor` | **完全不同** | `^` 是乘方（算术运算），`xor` 是按位异或（位运算）。因 `^` 已用于乘方，异或使用 `xor` 关键字 |
| `?:` | 三元条件运算符 | 条件表达式 `condition ? trueValue : falseValue`，也称为三元运算符 |

#### 2.3.2 三元运算符（条件运算符）

**语法**：`condition ? expressionIfTrue : expressionIfFalse`

**求值规则**：
- 首先求值 `condition`，结果必须为 `bool` 类型，否则抛出 `TypeMismatchException`
- 如果 `condition` 为 `true`，则求值并返回 `expressionIfTrue`（`expressionIfFalse` 不会被求值）
- 如果 `condition` 为 `false`，则求值并返回 `expressionIfFalse`（`expressionIfTrue` 不会被求值）
- 三元运算符是**右结合**的：`a ? b ? c : d : e` 等价于 `a ? (b ? c : d) : e`

**类型规则**：
- 三元运算符采用**短路类型推断策略**：结果类型由实际被选中的分支表达式的类型决定（未选中的分支不会被求值，其类型不影响结果类型）
- 如果被选中的分支为 `number` 类型，结果类型遵循 2.1.6 节的数值类型推断规则（与另一未选中分支的类型无关）
- 如果被选中的分支为 `string` 或 `bool` 类型，结果类型即为该分支的类型
- 如果两个分支类型相同，返回该类型

**与其他运算符的交互**：
- 三元运算符优先级最低，低于 `or`/`||`
- 在表达式 `a ? b : c ? d : e` 中，先计算右侧的嵌套三元运算符

#### 2.3.3 运算符类型兼容性详细说明

**算术运算符**（`+`(二元), `-`(二元), `*`, `/`, `//`, `%`, `^`）：
- 操作数必须为 number 类型
- 布尔值参与算术运算时自动转换：`true` → `1`，`false` → `0`（优先转换为 long；若另一操作数为 double，则类型向上传播为 double）
- `+` 运算符在至少一个操作数为 string 时执行拼接，另一个操作数自动转为字符串（详见下方字符串拼接规则）

**`+` 运算符完整行为规则**（按优先级从高到低匹配）：

| 左操作数 | 右操作数 | 行为 | 示例 | 结果 |
|----------|----------|------|------|------|
| string | string | 字符串拼接 | `'a' + 'b'` | `'ab'` |
| string | number | 拼接（number 转字符串） | `'x' + 42` | `'x42'` |
| number | string | 拼接（number 转字符串） | `42 + 'x'` | `'42x'` |
| string | bool | 拼接（bool 转字符串） | `'v:' + true` | `'v:True'` |
| bool | string | 拼接（bool 转字符串） | `true + '!'` | `'True!'` |
| number | number | 算术加法 | `3 + 4` | `7` |
| number | bool | 算术加法（bool → number） | `5 + true` | `6` |
| bool | number | 算术加法（bool → number） | `false + 3` | `3` |
| bool | bool | 算术加法（bool → number） | `true + true` | `2` |

**`-` 运算符**：
- 仅适用于 number 类型操作数
- bool 操作数自动转换：`true` → `1`，`false` → `0`（优先转换为 long；若另一操作数为 double，则类型向上传播为 double）
- string 操作数不可使用，抛出 `TypeMismatchException`

**整除运算符 `//`**：
- 操作数会自动转换为 long 类型（double 操作数舍去小数部分后转换），运算结果始终为 `long` 类型
- 例如：`7.5` 舍去小数部分转为 long 得 `7`，然后 `7 // 2 = 3`（long）

**取模运算符 `%`**：
- 纯整数操作数（long + long）：返回 `long`
- 含 double 操作数：返回 `double`（浮点取模）
- 例如：`7 % 3` → `1`（long），`7.5 % 2` → `1.5`（double）

**位运算符**（`&`, `|`, `xor`, `~`, `<<`, `>>`）：
- 操作数必须为 number 类型，运算前截断为 64 位有符号整数（long）
- bool 操作数自动转换：`true` → `1`，`false` → `0`，然后截断为 long
- 运算结果以 `long` 类型返回（整数值）
- `<<` 为逻辑左移，右侧补零；`>>` 为算术右移（保留符号位）
- `<<` 和 `>>` 的右操作数（移位量）为非负整数，负数移位量抛出 `EvaluateException`
- 移位量超过 63 时自动掩码为 `shift % 64`
- 算术右移示例：`-4 >> 1` → `-2`（-4 的二进制右移1位）

**关系运算符**（`>`, `<`, `>=`, `<=`）：
- 同类型比较：number 与 number、string 与 string
- string 比较使用字典序（按字符编码逐字符比较，区分大小写）
- 不同类型比较（number 与 string、bool 与 number 等）抛出 `TypeMismatchException`

**等于/不等于运算符**（`==`, `!=`）：
- 同类型比较：直接比较值
  - number：数值相等性比较（`1.0 == 1` 为 `true`）
  - string：区分大小写的精确匹配
  - bool：`true == true`、`false == false` 为 `true`
- 不同类型比较（number 与 bool、string 与 number 等）：始终返回 `false`（`==`）/ `true`（`!=`），不抛异常
- **重要**：`bool` 与 `number` 之间**不会**进行数值转换比较。`true == 1` → `false`，`false == 0` → `false`
- 这与布尔参与算术运算时的自动转换不同（算术中 `true + 1 = 2`），但比较操作不进行类型转换。详见 [4.3 布尔参与算术与比较的类型转换策略](#43-布尔参与算术与比较的类型转换策略)

**逻辑运算符**（`and`/`&&`, `or`/`||`）：
- 操作数必须为 bool 类型，非 bool 操作数抛出 `TypeMismatchException`（不自动转换）
- `and`/`&&`：左操作数为 `false` 时短路，不计算右操作数
- `or`/`||`：左操作数为 `true` 时短路，不计算右操作数

**一元运算符**（`+`, `-`, `not`/`!`, `~`）：
- `+`(一元)：仅适用于 number，返回原值
- `-`(一元)：仅适用于 number，返回相反数
- `not`/`!`：仅适用于 bool，返回逻辑非。`not` 和 `!` 完全等价
- `~`：仅适用于 number，截断为 long 后按位取反，结果以 `long` 返回

**乘方运算符**（`^`）：
- 左操作数（底数）：任意 number
- 右操作数（指数）：任意 number（支持小数和负数）
- `2 ^ -1` → `0.5`，`9 ^ 0.5` → `3.0`
- `0 ^ 0` → `1`（数学约定）
- 底数为负数且指数为非整数时抛出 `EvaluateException`（如 `(-4) ^ 0.5`）

#### 2.3.4 特殊值行为（NaN / INF）

| 运算 | 表达式 | 行为 |
|------|--------|------|
| NaN 参与算术 | `NaN + 1` | 结果为 `NaN` |
| NaN 参与比较 | `NaN == NaN` | 结果为 `false`（NaN 不等于任何值，包括自身） |
| NaN 参与比较 | `NaN != NaN` | 结果为 `true` |
| INF 参与算术 | `INF + 1` | 结果为 `INF` |
| INF 减 INF | `INF - INF` | 结果为 `NaN` |
| INF 参与比较 | `INF > 1` | 结果为 `true` |
| 0 乘 INF | `0 * INF` | 结果为 `NaN` |

**注意**：`NaN` 和 `INF` 是关键字，不可通过 `Set` 方法覆盖。在上下文中注册同名符号将抛出 `ParseException`

#### 2.3.5 边界行为说明

| 运算 | 边界情况 | 行为 |
|------|----------|------|
| `/` | 除以零 | 抛出 `DivisionByZeroException` |
| `//` | 除以零 | 抛出 `DivisionByZeroException` |
| `%` | 取模零 | 抛出 `DivisionByZeroException` |
| `^` | 0 的负数次幂 | 抛出 `EvaluateException`（结果为无穷） |
| `^` | 负底数 + 非整数指数 | 抛出 `EvaluateException`（如 `(-4) ^ 0.5`） |
| `//` | 负数整除 | 向零取整：`-7 // 2 = -3`（非 -4） |
| `%` | 负数取模 | 结果符号与左操作数一致：`-7 % 3 = -1`，`7 % -3 = 1` |
| `<<` | 负数移位量 | 抛出 `EvaluateException` |
| `>>` | 负数移位量 | 抛出 `EvaluateException` |
| `<<` / `>>` | 移位量 ≥ 64 | 自动掩码：`shift % 64` |
| `-`(一元) | 对字符串取负 | 抛出 `TypeMismatchException` |
| `not`/`!` | 对数值取非 | 抛出 `TypeMismatchException` |
| `~` | 对字符串取反 | 抛出 `TypeMismatchException` |
| `and`/`or` | 非布尔操作数 | 抛出 `TypeMismatchException` |

#### 2.3.6 场景

##### 算术运算与优先级

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 基本算术运算 | `3 + 4 * 2` | `11` | 乘法优先级高于加法 |
| 复杂优先级交互 | `2 + 3 * 4 ^ 2` | `50` | 先乘方 4^2=16，再乘法 3*16=48，再加法 2+48=50 |
| 左结合性（减法） | `10 - 3 - 2` | `5` | 左结合：(10-3)-2=5，而非 10-(3-2)=9 |
| 左结合性（除法） | `100 / 10 / 2` | `5` | 左结合：(100/10)/2=5，而非 100/(10/2)=20 |

##### 乘方运算

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 乘方右结合 | `2 ^ 3 ^ 2` | `512` | 右结合：2^(3^2) = 2^9 |
| 小数指数 | `9 ^ 0.5` | `3.0` | - |
| 负数指数 | `2 ^ -1` | `0.5` | - |
| 零的零次方 | `0 ^ 0` | `1` | 数学约定 |
| 负底数非整数指数 | `(-4) ^ 0.5` | `EvaluateException` | - |

##### 整除与取模

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 整除运算 | `7 // 2` | `3` | - |
| 负数整除 | `-7 // 2` | `-3` | 向零取整 |
| 浮点整除 | `7.5 // 2` | `3` | 7.5 舍去小数 → 7，7 // 2 = 3 |
| 取模运算 | `7 % 3` | `1` | 纯整数取模，返回 long |
| 浮点取模 | `7.5 % 2` | `1.5` | 含 double 操作数，返回 double |
| 负数取模 | `-7 % 3` | `-1` | 结果符号与左操作数一致 |

##### 除法与除零

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 除法运算 | `7 / 2` | `3.5` | - |
| 除以零 | `1 / 0` | `DivisionByZeroException` | - |
| 整除除以零 | `1 // 0` | `DivisionByZeroException` | - |
| 取模零 | `5 % 0` | `DivisionByZeroException` | - |

##### 字符串拼接

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 字符串拼接 | `'hello' + ' ' + 'world'` | `'hello world'` | - |
| 字符串与数值拼接 | `'value: ' + 42` | `'value: 42'` | - |
| 数值与字符串拼接 | `3.14 + ' is pi'` | `'3.14 is pi'` | - |
| 字符串与布尔拼接 | `'flag: ' + true` | `'flag: True'` | - |
| 布尔与字符串拼接 | `true + '!'` | `'True!'` | - |
| 数值与布尔算术加法 | `5 + true` | `6` | true → 1，算术加法 |
| 布尔与布尔算术加法 | `true + true` | `2` | true → 1，算术加法 |
| 字符串减法 | `'hello' - 1` | `TypeMismatchException` | - |

##### 比较运算

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 关系比较 | `3 > 2` | `true` | - |
| 字符串比较 | `'abc' < 'abd'` | `true` | 字典序比较 |
| 字符串比较区分大小写 | `'A' == 'a'` | `false` | 区分大小写 |
| 数值等于比较 | `1.0 == 1` | `true` | - |
| 字符串等于比较 | `'hello' == 'hello'` | `true` | - |
| 布尔等于比较 | `true == true` | `true` | - |
| 布尔不等于比较 | `true != false` | `true` | - |
| 不同类型等于比较 | `1 == '1'` | `false` | 不同类型始终不等 |
| 不同类型不等于比较 | `1 != '1'` | `true` | - |
| 不同类型关系比较 | `1 > '1'` | `TypeMismatchException` | - |

##### 逻辑运算

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 逻辑运算 | `true and false` | `false` | - |
| 短路求值（and） | `false and (1 / 0 == 0)` | `false` | 不计算右侧表达式 |
| 短路求值（or） | `true or (1 / 0 == 0)` | `true` | 不计算右侧表达式 |
| and 与 && 等价 | `true && false` | `false` | 与 `true and false` 完全等价 |
| or 与 \|\| 等价 | `false \|\| true` | `true` | 与 `false or true` 完全等价 |
| 逻辑非 not | `not true` | `false` | - |
| 逻辑非 ! 等价于 not | `!false` | `true` | `!` 与 `not` 完全等价 |
| 非布尔操作数 | `1 and 2` | `TypeMismatchException` | 逻辑运算要求 bool 操作数 |

##### 一元运算

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 一元正号 | `+5` | `5` | - |
| 一元取负 | `-5` | `-5` | - |
| 对字符串取负 | `-'hello'` | `TypeMismatchException` | - |
| 对数值取逻辑非 | `not 1` | `TypeMismatchException` | - |

##### 位运算

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 按位与运算 | `5 & 3` | `1` | 0101 & 0011 = 0001 |
| 按位或运算 | `5 \| 3` | `7` | 0101 \| 0011 = 0111 |
| 按位异或运算 | `5 xor 3` | `6` | 0101 xor 0011 = 0110 |
| 布尔参与位运算 | `true & 6` | `0` | true → 1，1 & 6 = 0001 & 0110 = 0000 |
| 按位与（& 与 && 区别） | `3 & 5` | `1` | 位运算 |
| 逻辑与（& 与 && 区别） | `true && true` | `true` | 布尔运算 |
| 按位取反运算 | `~5` | `-6` | 按 64 位整数取反 |
| 左移运算 | `1 << 4` | `16` | - |
| 右移运算 | `16 >> 2` | `4` | - |
| 移位量掩码 | `1 << 64` | `1` | 64 % 64 = 0，即 1 << 0 = 1 |
| 负数移位量 | `1 << -1` | `EvaluateException` | - |

##### 特殊值（NaN 与 INF）

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| NaN 参与算术 | `NaN + 1` | `NaN` | - |
| NaN 不等于自身 | `NaN == NaN` | `false` | - |
| NaN 不等于自身（!=） | `NaN != NaN` | `true` | - |
| INF 参与算术 | `INF + 1` | `INF` | - |
| INF 减 INF | `INF - INF` | `NaN` | - |
| INF 参与比较 | `INF > 1` | `true` | - |
| 0 乘 INF | `0 * INF` | `NaN` | - |

##### 三元条件运算符

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 基本三元运算 | `true ? 1 : 2` | `1` | 条件为 true |
| 条件为 false | `false ? 1 : 2` | `2` | 条件为 false |
| 嵌套三元（右结合） | `true ? false ? 1 : 2 : 3` | `2` | 等价于 `true ? (false ? 1 : 2) : 3` |
| 嵌套三元（右侧） | `false ? 1 : true ? 2 : 3` | `2` | 等价于 `false ? 1 : (true ? 2 : 3)` |
| 条件中使用比较 | `x > 0 ? x : -x` | `5` | 当 x=5 时 |
| 条件中使用逻辑运算 | `x > 0 and x < 10 ? 'valid' : 'invalid'` | `'valid'` | 当 x=5 时 |
| 短路求值（true 分支） | `true ? (1 / 0 > 0) : 0` | `DivisionByZeroException` | 除以零在 true 分支 |
| 短路求值（false 分支） | `false ? 0 : (1 / 0 > 0)` | `DivisionByZeroException` | 除以零在 false 分支 |
| 不同数值类型分支 | `true ? 1 : 2.5` | `1` (long) | 选中 true 分支值 1，短路类型推断为 long |
| 字符串分支 | `x > 0 ? 'positive' : 'non-positive'` | `'positive'` | 当 x=5 时 |
| 混合类型分支（数+串） | `true ? 42 : 'hello'` | `42` (long) | 选中 true 分支 42，短路类型推断为 long |
| 混合类型分支（串+数） | `true ? 'value: ' : 100` | `'value: '` (string) | 选中 true 分支 'value:'，短路类型推断为 string |
| 布尔与数值分支 | `flag ? 1 : 0` | `1` | flag=true |
| 非布尔条件 | `1 ? 2 : 3` | `TypeMismatchException` | 条件必须为 bool |
| 嵌套在表达式中 | `1 + (x > 0 ? x : -x)` | `6` | 当 x=5 时 |

##### 三元运算符边界行为

| 场景 | 表达式 | 行为 |
|------|--------|------|
| 条件非布尔 | `1 ? 2 : 3` | `TypeMismatchException` |
| 左分支除以零 | `true ? (1 / 0) : 0` | `DivisionByZeroException` |
| 右分支除以零 | `false ? 0 : (1 / 0)` | `DivisionByZeroException` |
| 右结合性 | `1 > 0 ? 2 : 3 > 0 ? 4 : 5` | `2` | 等价于 `1 > 0 ? 2 : (3 > 0 ? 4 : 5)` |

---

### 2.4 字符串插值

系统 SHALL 支持类似 C# 的字符串插值语法，使用 `$` 前缀标记插值字符串，`{}` 内嵌入表达式：

#### 2.4.1 语法规则
- 插值字符串以 `$` 前缀开始，后跟双引号或单引号
- `{expression}` 内可嵌入任意合法表达式
- `{{` 和 `}}` 分别表示字面量 `{` 和 `}`
- 插值表达式内支持格式说明符：`{value:format}`，如 `{3.14159:F2}` → `3.14`

#### 2.4.2 格式说明符规范

格式说明符仅支持简单的数值格式化，不支持复杂的格式化选项（如货币格式、百分比格式、自定义格式字符串等）。

**注意**：字符串插值仅支持 `bool`、`number`、`string` 三种类型。格式说明符仅适用于数值类型，对非数值类型（如 `bool`/`string`）使用格式说明符，将抛出 `EvaluateException`。使用不支持的格式说明符（如 `C`/`c`、`N`/`n`、`P`/`p` 及自定义格式字符串），将在解析时抛出 `ParseException`。

**数值格式说明符**：

| 格式符 | 说明 | 示例 | 结果 |
|--------|------|------|------|
| `D`/`d` | 十进制格式（整数） | `{42:D5}` | `00042` |
| `E`/`e` | 科学计数法 | `{123.45:E2}` | `1.23E+002` |
| `F`/`f` | 固定点 | `{3.14159:F2}` | `3.14` |
| `G`/`g` | 常规格式 | `{123.45:G4}` | `123.5` |
| `X`/`x` | 十六进制（整数） | `{255:X4}` | `00FF` |

**不支持**的格式说明符：`C`/`c`（货币）、`N`/`n`（带千位分隔符）、`P`/`p`（百分比）以及自定义格式字符串。使用不支持的格式说明符将在解析时抛出 `ParseException`。

#### 2.4.3 场景

| 场景 | 表达式 | 条件 | 结果 |
|------|--------|------|------|
| 基本插值 | `$"Hello, {name}!"` | `name = "World"` | `"Hello, World!"` |
| 表达式插值 | `$"2 + 3 = {2 + 3}"` | - | `"2 + 3 = 5"` |
| 格式说明符 | `$"Pi = {3.14159:F2}"` | - | `"Pi = 3.14"` |
| 转义花括号 | `$"{{not interpolated}}"` | - | `"{not interpolated}"` |
| 嵌套函数调用 | `$"sqrt(4) = {sqrt(4)}"` | - | `"sqrt(4) = 2"` |

---

### 2.5 上下文系统

系统 SHALL 提供 `ExpressionContext` 类，支持符号（Symbol）和函数的注册与上下文继承。

#### 2.5.1 核心概念：符号（Symbol）

在表达式中，所有以字母组成的名字（如 `PI`、`x`、`now`）都是**标识符**，从表达式视角看没有区别——它们都是"名字"。上下文中的**符号**就是这些名字的绑定，统一通过 `Set` 方法注册。

符号的值来源有两种：

| 值来源 | 注册方式 | 求值行为 | 示例 |
|--------|----------|----------|------|
| 直接值 | `Set(name, value)` | 每次返回注册时的固定值 | `Set("PI", 3.14159)` |
| 延迟值 | `Set(name, Func<object>)` | 每次求值调用委托获取最新值 | `Set("now", () => DateTime.Now.ToString())` |

**设计原则**：
- 表达式中不区分"常量"和"变量"——所有标识符都是只读的（表达式无赋值操作）
- "常量"和"变量"的区别仅在于宿主代码是否再次调用 `Set` 修改值，这是使用约定，不是类型系统的约束
- 延迟值符号在表达式中以标识符形式使用（如 `now`），不需要括号调用（如 `now()`），这与函数调用语法不同

#### 2.5.2 标识符命名规则

系统 SHALL 遵循以下标识符命名规则：

| 规则 | 描述 | 示例 |
|------|------|------|
| **首字符** | 必须是字母（大小写均可）、下划线 `_` 或 Unicode 字母（包括中文） | `x`, `_value`, `π`, `变量1` |
| **后续字符** | 可以是字母、数字、下划线或 Unicode 字母/数字 | `x1`, `my_value`, `变量_2` |
| **大小写敏感** | 标识符区分大小写 | `MyVar` 与 `myvar` 是不同的标识符 |
| **保留字** | 不能使用关键字（`true`、`false`、`and`、`or`、`not`、`xor`、`NaN`、`INF`） | 不能命名为 `true` 或 `and` |
| **长度限制** | 无长度限制，但建议保持简洁 | - |

**注意**：
- 关键字（`true`、`false`、`and`、`or`、`not`、`xor`、`NaN`、`INF`）不区分大小写（如 `AND`、`and`、`And` 均为同一关键字），禁止用作标识符
- 标识符区分大小写（如 `MyVar` 与 `myvar` 是不同的标识符），请勿将关键字的任意大小写形式用作标识符
- 建议使用有意义的英文标识符以提高代码可读性

#### 2.5.3 API 设计

```csharp
var ctx = new ExpressionContext();

// 注册直接值符号
ctx.Set("PI", 3.14159265);        // 数值
ctx.Set("greeting", "hello");     // 字符串
ctx.Set("flag", true);            // 布尔

// 注册延迟值符号（每次求值时调用委托）
ctx.Set("now", () => DateTime.Now.ToString());      // Func<object> 返回 string
ctx.Set("counter", () => counter++);                 // Func<object> 返回 number
ctx.Set("isEnabled", () => CheckEnabled());          // Func<object> 返回 bool

// 覆盖已有符号（直接值 → 延迟值 或 延迟值 → 直接值 均可）
ctx.Set("PI", 3.14);               // 覆盖为新的直接值
ctx.Set("x", () => GetX());        // 覆盖为延迟值

// 删除符号和函数
ctx.Remove("x");
ctx.Remove("sqrt");

// 注册函数（函数使用 name(args) 语法调用，与符号的 name 语法不同）
ctx.SetFunction("sqrt", args => Math.Sqrt(Convert.ToDouble(args[0])));
ctx.SetFunction("doubleIt", (Func<double, double>)(x => x * 2));
```

#### 2.5.4 符号与函数的区别

| 特性 | 符号（Symbol） | 函数（Function） |
|------|----------------|-------------------|
| 表达式中语法 | 标识符：`PI`、`now` | 函数调用：`sqrt(4)`、`max(3, 5)` |
| 注册方法 | `Set(name, value)` / `Set(name, func)` | `SetFunction(name, delegate)` |
| 参数 | 无 | 1~N 个参数 |
| 典型用途 | 常量、变量、配置值、动态状态 | 计算、转换、业务逻辑 |
| 删除 | 支持 `Remove(name)` | 支持 `Remove(name)` |

**命名空间规则**：
- 符号与函数共享同一命名空间，同名会产生冲突
- **函数注册优先级更高**：若已通过 `SetFunction` 注册了函数 `f`，则调用 `Set("f", value)` 会抛出 `InvalidOperationException`
- 符号不能覆盖已注册的函数，但函数可以覆盖已注册的符号（后注册的函数覆盖先注册的同名符号）

#### 2.5.5 上下文继承

系统 SHALL 支持上下文继承，子上下文可以访问父上下文中的符号和函数。

**`CreateChild()` 方法签名**：`ExpressionContext CreateChild()`

**继承规则**：

| 规则 | 说明 |
|------|------|
| 子上下文可见父上下文 | 子上下文可访问父上下文所有符号和函数 |
| 子上下文新增不可见父上下文 | 子上下文新增的符号/函数对父上下文不可见 |
| 子上下文不可删除父上下文继承项 | 子上下文调用 `Remove` 只会操作自己的上下文，自然就无法删除从父上下文继承的符号，静默无错误，不抛异常 |
| 父上下文修改影响子上下文 | 父上下文后续修改（新增、覆盖符号/函数）会影响已创建的子上下文(子有同名覆盖的除外，被遮蔽了) |
| 子上下文覆盖不影响父上下文 | 子上下文通过 `Set` 覆盖同名符号时，父上下文中同名符号的值不受影响 |
| 多层继承 | 支持多层继承（孙上下文等），规则逐层递归适用 |

#### 2.5.6 场景

| 场景 | 操作 | 结果 |
|------|------|------|
| 注册直接值符号 | `context.Set("x", 42.0)` | 表达式中使用 `x` 时，每次求值返回 `42.0` |
| 注册延迟值符号 | `context.Set("now", () => DateTime.Now.ToString())` | 表达式中使用 `now`（不带括号）时，每次求值调用委托获取最新值 |
| 覆盖符号 | 已注册 `x = 10`，然后 `context.Set("x", 20.0)` | 后续求值使用新值 `20` |
| 直接值覆盖为延迟值 | 已注册 `x = 10`，然后 `context.Set("x", () => GetDynamicX())` | 后续求值调用委托获取值 |
| 删除符号 | `context.Remove("x")` 删除已注册的符号 | 后续求值中引用 `x` 将抛出 `SymbolNotFoundException` |
| 删除函数 | `context.Remove("sqrt")` 删除已注册的函数 `sqrt`，后续求值中引用 `sqrt(4)` 将抛出 `FunctionNotFoundException` |
| 动态添加符号 | 在表达式构建后 `context.Set("y", 100.0)` | 后续使用包含 `y` 的表达式可正确求值 |
| 上下文继承 | `childContext = parentContext.CreateChild()` | 子上下文可访问父上下文所有符号和函数；子上下文同名项覆盖父上下文项但不影响父上下文 |
| 上下文隔离 | 在子上下文中通过 `Set` 修改符号值 | 父上下文中同名符号的值不受影响 |
| 父上下文修改传播 | 父上下文新增符号 `z` | 子上下文可访问新增的 `z` |
| 多层继承 | `grandChild = childContext.CreateChild()` | 孙上下文可访问子上下文和父上下文的所有符号和函数 |

---

### 2.6 函数注册代理

系统 SHALL 支持通过 `Func<>` 和 `Delegate` 方式注册函数，提供强类型和弱类型两种注册途径。

**注意**：系统不支持函数重载。同名函数后注册覆盖前注册。

#### 2.6.1 注册方式

**方式一：弱类型委托（ExpressionFunction）**
```csharp
context.SetFunction("add", args => (double)args[0] + (double)args[1]);
```
- 参数为 `object[]`，返回 `object`
- 灵活但无类型安全

**方式二：强类型 Func<>**
```csharp
// 支持 Func<T1, TResult> 到 Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult>
context.SetFunction("doubleIt", (Func<double, double>)(x => x * 2));
context.SetFunction("add", (Func<double, double, double>)((a, b) => a + b));
context.SetFunction("clamp", (Func<double, double, double, double>)((v, min, max) => Math.Max(min, Math.Min(max, v))));
```
- 参数和返回值类型由泛型参数确定
- 运行时自动将 `object` 参数转换为目标类型
- 支持 1~8 个参数的 Func<> 泛型

**方式三：Delegate 委托**
```csharp
context.SetFunction("add", (Delegate)(Func<double, double, double>)((a, b) => a + b));
```
- 适用于需要统一处理不同签名函数的场景
- 内部通过反射提取参数信息和调用委托

#### 2.6.2 同名函数覆盖

同名函数后注册覆盖前注册，无论注册方式是否相同：

```csharp
// 先注册弱类型委托
ctx.SetFunction("add", args => (double)args[0] + (double)args[1]);
// 后注册强类型 Func<>，覆盖前者
ctx.SetFunction("add", (Func<double, double, double>)((a, b) => a + b));
```

#### 2.6.3 函数调用参数不匹配

当函数调用的参数数量或类型与注册的函数签名不匹配时，抛出 `FunctionTypeMismatchException`。

| 场景 | 注册方式 | 调用表达式 | 结果 |
|------|----------|------------|------|
| 参数数量不足 | `Func<double, double, double>` | `add(1)` | `FunctionTypeMismatchException` |
| 参数数量过多 | `Func<double, double, double>` | `add(1, 2, 3)` | `FunctionTypeMismatchException` |
| 参数类型不匹配 | `Func<double, double, double>` | `add("a", "b")` | `FunctionTypeMismatchException` |

#### 2.6.4 场景

| 场景 | 注册方式 | 表达式 | 结果 |
|------|----------|--------|------|
| Func<double, double> 注册 | `context.SetFunction("neg", (Func<double, double>)(x => -x))` | `neg(5)` | `-5` |
| Func<double, double, double> 注册 | `context.SetFunction("add", (Func<double, double, double>)((a, b) => a + b))` | `add(3, 4)` | `7` |
| Delegate 注册 | `context.SetFunction("mul", (Delegate)(Func<double, double, double>)((a, b) => a * b))` | `mul(3, 4)` | `12` |
| Func<> 参数类型自动转换 | 注册 `Func<double, double>` 函数，传入整数值 | - | 整数值自动转换为 double 后传入委托 |
| 同名函数覆盖 | 先注册弱类型，后注册强类型 | - | 使用强类型版本 |

---

### 2.7 内置数学函数与常量

#### 2.7.1 内置数学函数

系统 SHALL 提供以下内置数学函数，作为默认上下文的一部分：

| 函数 | 签名 | 参数类型 | 返回类型 | 说明 | 边界行为 |
|------|------|----------|----------|------|----------|
| `abs(x)` | number → number | `long`/`double` | 与输入类型一致 | 绝对值 | 无特殊边界 |
| `sqrt(x)` | number → number | `long`/`double` | `double` | 平方根 | x < 0 时抛出 `EvaluateException` |
| `sin(x)` | number → number | `long`/`double` | `double` | 正弦（弧度） | 无特殊边界 |
| `cos(x)` | number → number | `long`/`double` | `double` | 余弦（弧度） | 无特殊边界 |
| `tan(x)` | number → number | `long`/`double` | `double` | 正切（弧度） | 无特殊边界 |
| `asin(x)` | number → number | `long`/`double` | `double` | 反正弦 | x ∉ [-1, 1] 时抛出 `EvaluateException` |
| `acos(x)` | number → number | `long`/`double` | `double` | 反余弦 | x ∉ [-1, 1] 时抛出 `EvaluateException` |
| `atan(x)` | number → number | `long`/`double` | `double` | 反正切 | 无特殊边界 |
| `atan2(y, x)` | (number, number) → number | `long`/`double` | `double` | 双参数反正切 | 无特殊边界 |
| `exp(x)` | number → number | `long`/`double` | `double` | e 的幂 | 无特殊边界 |
| `ln(x)` | number → number | `long`/`double` | `double` | 自然对数 | x ≤ 0 时抛出 `EvaluateException` |
| `log(x)` | number → number | `long`/`double` | `double` | 自然对数（ln 的别名） | x ≤ 0 时抛出 `EvaluateException` |
| `log10(x)` | number → number | `long`/`double` | `double` | 以 10 为底对数 | x ≤ 0 时抛出 `EvaluateException` |
| `log2(x)` | number → number | `long`/`double` | `double` | 以 2 为底对数 | x ≤ 0 时抛出 `EvaluateException` |
| `ceil(x)` | number → number | `long`/`double` | `long` | 向上取整 | 无特殊边界 |
| `floor(x)` | number → number | `long`/`double` | `long` | 向下取整 | 无特殊边界 |
| `round(x)` | number → number | `long`/`double` | `long` | 四舍五入取整 | 无特殊边界 |
| `round(x, d)` | (number, number) → number | `long`/`double` | `double` | 四舍五入到 d 位小数 | d 必须为非负整数，否则抛出 `EvaluateException` |
| `truncate(x)` | number → number | `long`/`double` | `long` | 截断取整 | 无特殊边界 |
| `sign(x)` | number → number | `long`/`double` | `long` | 符号（-1, 0, 1） | 无特殊边界 |
| `max(a, b)` | (number, number) → number | `long`/`double` | 与输入类型一致 | 最大值 | 任一参数为 NaN 时抛出 `EvaluateException` |
| `min(a, b)` | (number, number) → number | `long`/`double` | 与输入类型一致 | 最小值 | 任一参数为 NaN 时抛出 `EvaluateException` |
| `pow(x, y)` | (number, number) → number | `long`/`double` | `double` | 幂运算（等价于 x ^ y） | x < 0 且 y 非整数时抛出 `EvaluateException` |

**类型处理规则**：
- 所有内置数学函数均接受 `long` 或 `double` 类型的参数
- `long` 类型参数会自动转换为 `double` 进行计算（除 `abs`、`max`、`min` 外）
- `abs` 函数若参数为 `long` 则返回 `long`，若参数为 `double` 则返回 `double`
- `max`、`min` 函数若两个参数均为 `long` 则返回 `long`，否则返回 `double`

#### 2.7.2 内置常量

系统 SHALL 提供以下内置数学常量：

| 常量 | 值 | 说明 |
|------|-----|------|
| `PI` | 3.14159265358979 | 圆周率 |
| `E` | 2.71828182845905 | 自然常数 |
| `INF` | double.PositiveInfinity | 正无穷（关键字，不可覆盖） |
| `NaN` | double.NaN | 非数值（关键字，不可覆盖） |

**注意**：`NaN` 和 `INF` 是关键字，不可通过 `Set` 方法覆盖。在上下文中注册同名符号将抛出 `ParseException`。`PI` 和 `E` 是默认上下文中的符号，可以通过 `Set` 方法覆盖。

#### 2.7.3 场景

| 场景 | 表达式 | 结果 | 说明 |
|------|--------|------|------|
| 三角函数 | `sin(0)` | `0` | - |
| 对数函数 | `ln(e)` | `1` | e 为内置常量 |
| 绝对值（long） | `abs(-42)` | `42` (long) | 输入 long，返回 long |
| 绝对值（double） | `abs(-3.14)` | `3.14` (double) | 输入 double，返回 double |
| max（long + long） | `max(3, 5)` | `5` (long) | 两个参数均为 long，返回 long |
| max（long + double） | `max(3, 5.5)` | `5.5` (double) | 参数中含 double，返回 double |
| min（double + long） | `min(3.14, 2)` | `2.0` (double) | 参数中含 double，返回 double |

---

### 2.8 Expression 主入口 API

系统 SHALL 提供简洁的 `Expression` 类作为主入口（参考 NCalc 的简洁 API + NFun 的 Fluent Builder）。

**空表达式行为**：传入空字符串或纯空白字符串时抛出 `ParseException`。

#### 2.8.1 快捷 API

**静态方法**（无上下文或可选上下文）：

```csharp
// 无上下文计算
object result1 = Expression.Eval("2 + 3 * 4");           // 14 (long)
double result2 = Expression.Eval<double>("sqrt(16)");     // 4.0

// 可选上下文参数
var ctx = new ExpressionContext();
ctx.Set("x", 10.0);
object result3 = Expression.Eval("x * 2", ctx);           // 20
double result4 = Expression.Eval<double>("x * 2", ctx);   // 20.0
```

**实例方法**（上下文存储在实例中）：

```csharp
// 通过 Builder 创建实例，上下文已绑定
var calc = Expression.Builder
    .With("x", 10.0)
    .Build("x * 2 + 1");

// 实例方法无需传入上下文
object result1 = calc.Eval();           // 21
double result2 = calc.Eval<double>();   // 21.0

// 可通过实例方法修改上下文中的符号
calc.Set("x", 20.0);
object result3 = calc.Eval();                  // 41
```

**`Eval<T>()` 返回类型转换规则**：
- 表达式内部按照类型推断规则计算结果类型
- `Eval()` 返回 `object`，结果保持内部推断的类型
- `Eval<T>()` 将内部结果转换为指定的泛型类型 `T`

#### 2.8.2 Builder API

```csharp
// Fluent Builder 模式
var calc = Expression.Builder
    .With("rate", 0.05)
    .With("now", () => DateTime.Now.ToString())
    .WithFunction("tax", args => args[0] * 0.05)
    .WithFunction("doubleIt", (Func<double, double>)(x => x * 2))
    .Build("price * (1 + tax(rate))");

calc.Set("price", 100.0);
var result1 = calc.Eval();  // 105
```

#### 2.8.3 ICalculator 接口

Builder API 的 `Build()` 方法返回 `ICalculator` 实例，接口定义如下：

```csharp
public interface ICalculator
{
    // 求值表达式，返回 object（按内部推断类型）
    object Eval();

    // 求值表达式，返回指定类型 T（自动转换结果类型）
    T Eval<T>();

    // 设置上下文中的符号值
    void Set(string name, object value);

    // 设置上下文中的延迟值符号
    void Set(string name, Func<object> value);

    // 删除符号和函数
    void Remove(string name);
}
```

#### 2.8.4 场景

| 场景 | 调用方式 | 结果 |
|------|----------|------|
| 快捷计算 | `Expression.Eval("1 + 2")` | `3` (long) |
| 泛型计算 | `Expression.Eval<double>("1 + 2")` | `3.0` (double) |
| 泛型计算 | `Expression.Eval<long>("1 + 2")` | `3` (long) |
| Builder 模式 | Builder 链式配置并 Build | 返回可复用的 `ICalculator` 实例，支持多次求值 |
| 空表达式 | `Expression.Eval("")` | `ParseException` |
| 纯空白表达式 | `Expression.Eval("   ")` | `ParseException` |

---

### 2.9 AST 与 Visitor 模式

系统 SHALL 基于 AST + Visitor 模式设计（参考 NCalc），支持扩展：

#### 2.9.1 AST 节点类型
- `ValueExpression` — 常量值（布尔、数值、字符串）
- `Identifier` — 变量/常量/伪常量引用
- `BinaryExpression` — 二元运算
- `UnaryExpression` — 一元运算
- `FunctionCall` — 函数调用
- `InterpolatedString` — 插值字符串（包含文本段和表达式段）
- `ConditionalExpression` — 三元条件表达式（`condition ? trueExpr : falseExpr`）

#### 2.9.2 Visitor 接口

```csharp
public interface IExpressionVisitor<out T>
{
    T Visit(ValueExpression expr);
    T Visit(Identifier expr);
    T Visit(BinaryExpression expr);
    T Visit(UnaryExpression expr);
    T Visit(FunctionCall expr);
    T Visit(InterpolatedString expr);
    T Visit(ConditionalExpression expr);
}
```

#### 2.9.3 场景

| 场景 | 操作 | 结果 |
|------|------|------|
| 自定义 Visitor | 实现 `IExpressionVisitor` 接口 | 可以遍历 AST 执行自定义逻辑（如序列化、优化、代码生成） |

---

### 2.10 表达式缓存

系统 SHALL 支持表达式解析结果缓存（参考 NCalc），避免重复解析相同字符串：

- 默认启用线程安全的 `ConcurrentDictionary` 缓存
- 可通过 `ExpressionOptions.NoCache` 禁用
- 缓存键为表达式字符串
- `ExpressionOptions` 的完整定义将在项目架构规划中详细说明

| 场景 | 条件 | 结果 |
|------|------|------|
| 缓存命中 | 同一表达式字符串被多次解析 | 第二次及后续直接从缓存获取 AST，不重新解析 |

---

## 3. 非功能需求

### 3.1 多线程安全

系统 SHALL 保证多线程环境下的安全使用：

#### 3.1.1 线程安全策略

1. **ExpressionContext 读写安全**：变量、常量、伪常量、函数的存储使用 `ConcurrentDictionary`，保证并发读写安全
2. **求值过程安全**：`EvaluationVisitor` 每次求值创建独立实例，不共享可变状态；`ExpressionContext` 在求值过程中每次符号读取通过 `ConcurrentDictionary` 原子操作完成，不保证整个表达式求值使用一致的上下文视图
3. **表达式缓存安全**：AST 缓存使用 `ConcurrentDictionary`，保证并发访问安全
4. **ICalculator 线程安全**：`Calculator` 实例的 `Eval()` 方法可从多个线程并发调用，内部通过 `ConcurrentDictionary` 原子操作读取上下文，不保证各次求值使用一致的上下文快照

#### 3.1.2 场景

| 场景 | 条件 | 结果 |
|------|------|------|
| 并发求值 | 多个线程同时调用同一 `Calculator` 实例的 `Eval()` 方法 | 各线程独立求值，互不干扰，结果正确 |
| 并发上下文修改 | 一个线程修改上下文变量，另一个线程同时求值 | 求值线程每次符号读取通过 `ConcurrentDictionary` 原子操作完成，可能读取到修改前的值或修改后的值，但不会导致异常或数据损坏 |
| 并发缓存访问 | 多个线程首次解析同一表达式字符串 | 缓存正确工作，只有一个线程执行解析，其他线程等待或使用缓存结果 |

---

### 3.2 错误处理

系统 SHALL 提供清晰的错误信息，包含错误类型、位置和描述。所有异常消息均使用中文。

#### 3.2.1 异常层次结构

所有自定义异常均继承自 `MathEvalException` 基类，层次结构如下：

```csharp
MathEvalException (基类)
├── ParseException
├── EvaluateException
│   ├── FunctionNotFoundException
│   ├── FunctionTypeMismatchException
│   ├── SymbolNotFoundException
│   ├── DivisionByZeroException
│   ├── OverflowException
│   └── InvalidOperationException
└── TypeMismatchException
```

#### 3.2.2 异常类型说明

| 异常类型 | 基类 | 触发场景 | 包含信息 |
|----------|------|----------|----------|
| `MathEvalException` | - | 所有 MathEval 异常的基类 | 错误消息 |
| `ParseException` | `MathEvalException` | 词法/语法错误 | 行号、列号、错误位置描述 |
| `EvaluateException` | `MathEvalException` | 运行时计算错误 | 错误消息、相关上下文 |
| `FunctionNotFoundException` | `EvaluateException` | 调用未注册的函数 | 函数名 |
| `FunctionTypeMismatchException` | `EvaluateException` | 函数调用参数数量或类型与注册的函数签名不匹配 | 函数名、期望参数信息、实际参数信息 |
| `SymbolNotFoundException` | `EvaluateException` | 引用未定义的符号 | 符号名 |
| `DivisionByZeroException` | `EvaluateException` | 除以零错误 | 错误位置 |
| `OverflowException` | `EvaluateException` | 数值运算溢出（long 类型） | 运算描述、溢出值 |
| `InvalidOperationException` | `EvaluateException` | 无效的运算操作 | 操作描述 |
| `TypeMismatchException` | `MathEvalException` | 类型不匹配 | 期望类型、实际类型、操作描述 |

#### 3.2.3 场景

| 场景 | 表达式 | 异常 | 说明 |
|------|--------|------|------|
| 语法错误 | `2 + * 3` | `ParseException` | 包含错误位置信息 |
| 除以零 | `1 / 0` | `DivisionByZeroException` | - |
| 函数未找到 | `unknownFunc(1)` | `FunctionNotFoundException` | 包含函数名 |
| 函数参数不匹配 | `add(1)` （注册为双参数函数） | `FunctionTypeMismatchException` | 包含函数名和参数信息 |
| 符号未找到 | `undefinedVar` | `SymbolNotFoundException` | 包含符号名 |
| 类型不匹配 | `'hello' - 1` | `TypeMismatchException` | 包含期望类型和实际类型 |

---

## 4. 设计决策记录

### 4.1 数值类型策略

> **已确定方案**：采用 `long + double` 双类型策略

**设计原因**：
- 整数精确：避免大数整数在 `double` 中的精度丢失
- 位运算自然：位运算直接在 `long` 上操作，语义清晰
- 语义正确：整除 `//` 和取模 `%` 返回整数，符合数学直觉

**具体规则**：
- 整数字面量 → `long`，浮点字面量 → `double`
- `long + long → long`，`long + double → double`（其他算术运算同理）
- 位运算（`&`、`|`、`xor`、`~`、`<<`、`>>`）直接在 `long` 上操作，返回 `long`
- 整除 `//` 操作数自动转换为 long 类型 （double 操作数舍去小数部分后转换），返回 `long`
- 取模 `%`：纯整数操作数返回 `long`，含 double 操作数返回 `double`
- 除法 `/` 始终返回 `double`
- `long` 运算溢出时抛出 `OverflowException`，`double` 运算溢出时产生 `INF`
- 内置数学函数中 `abs`、`max`、`min` 返回类型与输入类型一致，其余始终返回 `double`

### 4.2 `^` 表示乘方的设计原理

> **已确定方案**：`^` 表示乘方，`xor` 表示按位异或

**设计原因**：
- MathEval 定位为数学表达式计算引擎，乘方是数学中最常见的运算之一，使用 `^` 符号符合数学书写习惯（如 `x^2` 表示 x 的平方）
- 虽然在 C/C#/Java 等 C 族语言中 `^` 表示按位异或，但表达式计算器的目标用户更倾向于数学场景而非编程场景
- 使用 `xor` 关键字表示按位异或，语义清晰且不会与乘方混淆
- 这一设计也与 Excel、MATLAB、Wolfram 等数学工具的惯例一致

### 4.3 布尔参与算术与比较的类型转换策略

> **已确定方案**：布尔参与算术运算时自动转换为数值，但参与比较运算时不进行类型转换

**设计原因**：
- **算术运算自动转换**：在数学和编程中，布尔值参与算术运算是常见需求（如 `true + 1 = 2`），自动转换提升了表达式编写的便利性
- **比较运算不转换**：`true == 1` 返回 `false` 是有意为之的设计。如果比较运算也自动转换，则 `true == 1` 和 `false == 0` 都会返回 `true`，这会模糊类型边界，导致 `if (x == true)` 和 `if (x == 1)` 语义混淆
- **设计原则**：比较操作应严格区分类型，不同类型的值不应被视为相等；算术操作则允许类型提升以保持计算流畅性

---

## 5. 附录

### 5.1 术语表

本节对文档中使用的专业术语和缩写进行说明。

| 术语 | 全称 | 说明 |
|------|------|------|
| [SHALL] | - | RFC 2119 规范关键词，表示"必须"。在需求文档中表示强制性要求，该功能必须实现，不可省略。与之对应的还有 SHOULD（应该）、MAY（可以）等，分别表示推荐性和可选性要求。 |
| [AST] | Abstract Syntax Tree（抽象语法树） | 一种树形数据结构，用于表示源代码的语法结构。在表达式计算器中，表达式字符串首先被词法分析器（Lexer）分解为标记（Token），然后由语法分析器（Parser）构建为 AST。AST 的每个节点代表一个语法结构（如二元运算、函数调用、常量值等），后续通过遍历 AST 进行求值、优化或代码生成。 |
| [Visitor] | Visitor Pattern（访问者模式） | 一种行为型设计模式，用于在不改变数据结构的前提下定义新操作。在表达式计算器中，AST 节点类型固定（如 `BinaryExpression`、`FunctionCall` 等），但可能需要多种操作（求值、序列化、优化、代码生成等）。通过 Visitor 模式，每种操作实现为一个 Visitor 类，遍历 AST 时根据节点类型调用对应的 Visit 方法，实现操作与数据结构的解耦。 |

> **参考**：
> - RFC 2119: Key words for use in RFCs to Indicate Requirement Levels - https://www.rfc-editor.org/rfc/rfc2119
> - Design Patterns: Elements of Reusable Object-Oriented Software (GoF) - Visitor Pattern
