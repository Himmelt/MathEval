# MathEval 表达式计算器 Spec

## Why

当前项目中存在 NFun 和 NCalc 两个优秀的 .NET 表达式计算开源项目，但各有侧重：NFun 拥有强大的类型推断、字符串插值和上下文继承机制，但 API 较重、语言特性过多；NCalc 拥有简洁的 API、灵活的扩展机制和成熟的 Visitor 模式，但缺乏字符串插值和上下文继承。需要一个轻量级表达式计算器，融合两者优势，仅聚焦布尔、数值、字符串三种类型的表达式计算，同时支持位运算、强类型代理注册、伪常量、动态上下文操作和多线程安全。

## What Changes

- 创建 MathEval 核心库项目（.NET 8+ 类库）
- 实现词法分析器（Tokenizer）：支持布尔、数值、字符串字面量及运算符 Token
- 实现语法分析器（Parser）：基于递归下降解析，生成 AST
- 实现 AST 节点体系：BinaryExpression、UnaryExpression、FunctionCall、Identifier、ValueExpression、InterpolatedString
- 实现 Visitor 模式：EvaluationVisitor（求值）、SerializationVisitor（序列化）
- 实现 ExpressionContext 上下文系统：支持符号（直接值/延迟值）、函数的注册与继承
- 实现字符串插值模式：`$"abc{expr}def"` 语法
- 实现算术运算符：`+`、`-`、`*`、`/`、`//`、`%`、`^`
- 实现位运算符：`&`、`|`、`~`、`<<`、`>>`
- 实现关系运算符：`==`、`!=`、`>`、`<`、`>=`、`<=`
- 实现逻辑运算符：`and`/`&&`、`or`/`||`、`not`/`!`
- 实现内置数学函数库：三角函数、对数函数、绝对值等
- 实现 Fluent Builder API：链式配置常量、函数、上下文继承
- 实现 Expression 主入口类：简洁的一行式 API
- 实现强类型代理注册：支持 `Func<>` 和 `Delegate` 注册函数
- 实现伪常量：注册 `Func<bool/number/string>` 作为常量名使用 → **已合并到符号系统，统一为延迟值符号**
- 实现动态上下文操作：运行时动态增删改变量/常量
- 实现多线程安全：上下文读写和求值过程的线程安全保障
- 实现 Lambda 编译：将 AST 编译为 `Func<ExpressionContext, object>` 委托，提升重复求值性能

## Impact

- 新建项目：`src/MathEval/MathEval.csproj`
- 新建测试项目：`tests/MathEval.Tests/MathEval.Tests.csproj`
- 解决方案文件：`MathEval.sln`

## ADDED Requirements

### Requirement: 类型系统

系统 SHALL 仅支持以下三种值类型：

| 类型 | 关键字 | 示例 |
|------|--------|------|
| 布尔 | `bool` | `true`, `false` |
| 数值 | `number` | `42`, `3.14`, `1e-5` |
| 字符串 | `string` | `'hello'`, `"world"` |

数值类型内部使用 `double` 作为统一表示（参考 NCalc 的 `DecimalAsDefault` 思路，但默认使用 double 以获得更好的数学函数兼容性）。

位运算操作时，数值会被视为 64 位整数（long）进行运算，结果仍以 `double` 类型返回（整数值）。

#### 数值字面量格式

系统 SHALL 支持以下数值字面量格式：

| 格式 | 前缀 | 允许的数字字符 | 示例 | 十进制值 |
|------|------|----------------|------|----------|
| 十进制 | 无 | `0-9` | `42`, `3.14`, `1e-5` | 42, 3.14, 0.00001 |
| 十六进制 | `0x` / `0X` | `0-9`, `a-f`, `A-F` | `0xFF`, `0x1A3` | 255, 419 |
| 八进制 | `0o` / `0O` | `0-7` | `0o77`, `0o12` | 63, 10 |
| 二进制 | `0b` / `0B` | `0-1` | `0b1010`, `0b11111111` | 10, 255 |

**规则**：
- 十六进制、八进制、二进制字面量仅支持整数值，不支持小数点或指数部分
- 前缀后的数字字符必须符合对应进制的合法范围，否则抛出 ParseException
- 前缀后必须至少有一个数字字符（`0x`、`0o`、`0b` 单独出现抛出 ParseException）
- 所有进制的字面量最终统一转换为 `double` 类型
- 十六进制、八进制、二进制字面量支持 `+`(一元) 和 `-`(一元) 前缀（如 `-0xFF` = -255）

#### Scenario: 布尔字面量
- **WHEN** 表达式包含 `true` 或 `false`
- **THEN** 解析为布尔类型值

#### Scenario: 十进制数值字面量
- **WHEN** 表达式包含整数或浮点数字面量
- **THEN** 统一解析为 number 类型（double）

#### Scenario: 十六进制字面量
- **WHEN** 表达式包含 `0xFF`
- **THEN** 解析为 number 类型，值为 `255.0`

#### Scenario: 十六进制大小写不敏感
- **WHEN** 表达式包含 `0XaB`
- **THEN** 解析为 number 类型，值为 `171.0`（前缀和数字均不区分大小写）

#### Scenario: 八进制字面量
- **WHEN** 表达式包含 `0o77`
- **THEN** 解析为 number 类型，值为 `63.0`

#### Scenario: 二进制字面量
- **WHEN** 表达式包含 `0b1010`
- **THEN** 解析为 number 类型，值为 `10.0`

#### Scenario: 不同进制混合运算
- **WHEN** 表达式为 `0xFF + 0o10 + 0b1010`
- **THEN** 结果为 `255 + 8 + 10 = 273.0`

#### Scenario: 十六进制与位运算
- **WHEN** 表达式为 `0xFF & 0x0F`
- **THEN** 结果为 `15.0`（255 & 15 = 15）

#### Scenario: 二进制与位运算
- **WHEN** 表达式为 `0b1100 & 0b1010`
- **THEN** 结果为 `8.0`（12 & 10 = 8）

#### Scenario: 十六进制负数
- **WHEN** 表达式为 `-0xFF`
- **THEN** 结果为 `-255.0`

#### Scenario: 非法十六进制数字
- **WHEN** 表达式包含 `0xGH`
- **THEN** 抛出 ParseException

#### Scenario: 非法八进制数字
- **WHEN** 表达式包含 `0o89`
- **THEN** 抛出 ParseException

#### Scenario: 非法二进制数字
- **WHEN** 表达式包含 `0b12`
- **THEN** 抛出 ParseException

#### Scenario: 前缀后无数字
- **WHEN** 表达式包含 `0x`（无后续数字）
- **THEN** 抛出 ParseException

#### Scenario: 字符串字面量
- **WHEN** 表达式包含单引号或双引号包裹的字符串
- **THEN** 解析为字符串类型值

#### Scenario: 类型隐式转换
- **WHEN** 字符串与数值使用 `+` 运算符
- **THEN** 数值自动转为字符串进行拼接

#### Scenario: 布尔参与算术运算
- **WHEN** 布尔值参与算术运算（如 `true + 1`）
- **THEN** 布尔值转换为数值（true=1, false=0）

#### Scenario: 位运算类型转换
- **WHEN** 数值参与位运算（如 `5 & 3`）
- **THEN** 数值截断为 64 位整数执行位运算，结果以 double 返回（如 `1.0`）

---

### Requirement: 运算符支持

系统 SHALL 支持以下运算符，按优先级从高到低排列：

| 优先级 | 运算符 | 含义 | 结合性 | 适用类型 |
|--------|--------|------|--------|----------|
| 1（最高） | `()` | 分组 | - | 所有 |
| 2 | `+`(一元) | 正号（恒等） | 右结合 | number |
| 2 | `-`(一元) | 取负 | 右结合 | number |
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
| 13（最低） | `or` / `\|\|` | 逻辑或（短路） | 左结合 | bool |

#### 运算符符号与关键字说明

| 运算符对 | 关系 | 说明 |
|----------|------|------|
| `and` 与 `&&` | 完全等价 | 逻辑与，`and` 为关键字形式，`&&` 为符号形式 |
| `or` 与 `\|\|` | 完全等价 | 逻辑或，`or` 为关键字形式，`\|\|` 为符号形式 |
| `not` 与 `!` | 完全等价 | 逻辑非，`not` 为关键字形式，`!` 为符号形式 |
| `&` 与 `&&` | **完全不同** | `&` 是按位与（位运算），`&&` 是逻辑与（布尔短路运算），不可混用 |
| `\|` 与 `\|\|` | **完全不同** | `\|` 是按位或（位运算），`\|\|` 是逻辑或（布尔短路运算），不可混用 |
| `^` 与 `xor` | **完全不同** | `^` 是乘方（算术运算），`xor` 是按位异或（位运算）。因 `^` 已用于乘方，异或使用 `xor` 关键字 |

#### 运算符类型兼容性详细说明

**算术运算符**（`+`(二元), `-`(二元), `*`, `/`, `//`, `%`, `^`）：
- 操作数必须为 number 类型
- 布尔值参与算术运算时自动转换：`true` → `1.0`，`false` → `0.0`
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
- bool 操作数自动转换：`true` → `1.0`，`false` → `0.0`
- string 操作数不可使用，抛出 TypeMismatchException

**位运算符**（`&`, `|`, `xor`, `~`, `<<`, `>>`）：
- 操作数必须为 number 类型，运算前截断为 64 位有符号整数（long）
- bool 操作数自动转换：`true` → `1`，`false` → `0`，然后截断为 long
- 运算结果以 double 类型返回（整数值）
- `<<` 和 `>>` 的右操作数（移位量）为非负整数，负数移位量抛出 EvaluateException
- 移位量超过 63 时自动掩码为 `shift % 64`

**关系运算符**（`>`, `<`, `>=`, `<=`）：
- 同类型比较：number 与 number、string 与 string
- string 比较使用字典序（按字符编码逐字符比较，区分大小写）
- 不同类型比较（number 与 string、bool 与 number 等）抛出 TypeMismatchException

**等于/不等于运算符**（`==`, `!=`）：
- 同类型比较：直接比较值
  - number：数值相等性比较（`1.0 == 1` 为 `true`）
  - string：区分大小写的精确匹配
  - bool：`true == true`、`false == false` 为 `true`
- 不同类型比较（number 与 bool、string 与 number 等）：始终返回 `false`（`==`）/ `true`（`!=`），不抛异常

**逻辑运算符**（`and`/`&&`, `or`/`||`）：
- 操作数必须为 bool 类型，非 bool 操作数抛出 TypeMismatchException（不自动转换）
- `and`/`&&`：左操作数为 `false` 时短路，不计算右操作数
- `or`/`||`：左操作数为 `true` 时短路，不计算右操作数

**一元运算符**（`+`, `-`, `not`/`!`, `~`）：
- `+`(一元)：仅适用于 number，返回原值
- `-`(一元)：仅适用于 number，返回相反数
- `not`/`!`：仅适用于 bool，返回逻辑非。`not` 和 `!` 完全等价
- `~`：仅适用于 number，截断为 long 后按位取反，结果以 double 返回

**乘方运算符**（`^`）：
- 左操作数（底数）：任意 number
- 右操作数（指数）：任意 number（支持小数和负数）
- `2 ^ -1` → `0.5`，`9 ^ 0.5` → `3.0`
- `0 ^ 0` → `1`（数学约定）
- 底数为负数且指数为非整数时抛出 EvaluateException（如 `(-4) ^ 0.5`）

#### 特殊值行为（NaN / INF）

| 运算 | 表达式 | 行为 |
|------|--------|------|
| NaN 参与算术 | `NaN + 1` | 结果为 `NaN` |
| NaN 参与比较 | `NaN == NaN` | 结果为 `false`（NaN 不等于任何值，包括自身） |
| NaN 参与比较 | `NaN != NaN` | 结果为 `true` |
| INF 参与算术 | `INF + 1` | 结果为 `INF` |
| INF 减 INF | `INF - INF` | 结果为 `NaN` |
| INF 参与比较 | `INF > 1` | 结果为 `true` |
| 0 乘 INF | `0 * INF` | 结果为 `NaN` |

#### 边界行为说明

| 运算 | 边界情况 | 行为 |
|------|----------|------|
| `/` | 除以零 | 抛出 EvaluateException |
| `//` | 除以零 | 抛出 EvaluateException |
| `%` | 取模零 | 抛出 EvaluateException |
| `^` | 0 的负数次幂 | 抛出 EvaluateException（结果为无穷） |
| `^` | 负底数 + 非整数指数 | 抛出 EvaluateException（如 `(-4) ^ 0.5`） |
| `//` | 负数整除 | 向零取整：`-7 // 2 = -3`（非 -4） |
| `%` | 负数取模 | 结果符号与左操作数一致：`-7 % 3 = -1`，`7 % -3 = 1` |
| `<<` | 负数移位量 | 抛出 EvaluateException |
| `>>` | 负数移位量 | 抛出 EvaluateException |
| `<<` / `>>` | 移位量 ≥ 64 | 自动掩码：`shift % 64` |
| `-`(一元) | 对字符串取负 | 抛出 TypeMismatchException |
| `not`/`!` | 对数值取非 | 抛出 TypeMismatchException |
| `~` | 对字符串取反 | 抛出 TypeMismatchException |
| `and`/`or` | 非布尔操作数 | 抛出 TypeMismatchException |

#### Scenario: 算术运算
- **WHEN** 表达式为 `3 + 4 * 2`
- **THEN** 结果为 `11`（乘法优先级高于加法）

#### Scenario: 复杂优先级交互
- **WHEN** 表达式为 `2 + 3 * 4 ^ 2`
- **THEN** 结果为 `50`（先乘方 4^2=16，再乘法 3*16=48，再加法 2+48=50）

#### Scenario: 左结合性（减法）
- **WHEN** 表达式为 `10 - 3 - 2`
- **THEN** 结果为 `5`（左结合：(10-3)-2=5，而非 10-(3-2)=9）

#### Scenario: 左结合性（除法）
- **WHEN** 表达式为 `100 / 10 / 2`
- **THEN** 结果为 `5`（左结合：(100/10)/2=5，而非 100/(10/2)=20）

#### Scenario: 乘方运算
- **WHEN** 表达式为 `2 ^ 3 ^ 2`
- **THEN** 结果为 `512`（右结合：2^(3^2) = 2^9）

#### Scenario: 乘方小数指数
- **WHEN** 表达式为 `9 ^ 0.5`
- **THEN** 结果为 `3.0`

#### Scenario: 乘方负数指数
- **WHEN** 表达式为 `2 ^ -1`
- **THEN** 结果为 `0.5`

#### Scenario: 零的零次方
- **WHEN** 表达式为 `0 ^ 0`
- **THEN** 结果为 `1`（数学约定）

#### Scenario: 负底数非整数指数
- **WHEN** 表达式为 `(-4) ^ 0.5`
- **THEN** 抛出 EvaluateException

#### Scenario: 整除运算
- **WHEN** 表达式为 `7 // 2`
- **THEN** 结果为 `3`

#### Scenario: 负数整除
- **WHEN** 表达式为 `-7 // 2`
- **THEN** 结果为 `-3`（向零取整）

#### Scenario: 取模运算
- **WHEN** 表达式为 `7 % 3`
- **THEN** 结果为 `1`

#### Scenario: 负数取模
- **WHEN** 表达式为 `-7 % 3`
- **THEN** 结果为 `-1`（结果符号与左操作数一致）

#### Scenario: 除法运算
- **WHEN** 表达式为 `7 / 2`
- **THEN** 结果为 `3.5`

#### Scenario: 除以零
- **WHEN** 表达式为 `1 / 0`
- **THEN** 抛出 EvaluateException

#### Scenario: 整除除以零
- **WHEN** 表达式为 `1 // 0`
- **THEN** 抛出 EvaluateException

#### Scenario: 取模零
- **WHEN** 表达式为 `5 % 0`
- **THEN** 抛出 EvaluateException

#### Scenario: 字符串拼接
- **WHEN** 表达式为 `'hello' + ' ' + 'world'`
- **THEN** 结果为 `'hello world'`

#### Scenario: 字符串与数值拼接
- **WHEN** 表达式为 `'value: ' + 42`
- **THEN** 结果为 `'value: 42'`

#### Scenario: 数值与字符串拼接
- **WHEN** 表达式为 `3.14 + ' is pi'`
- **THEN** 结果为 `'3.14 is pi'`

#### Scenario: 字符串与布尔拼接
- **WHEN** 表达式为 `'flag: ' + true`
- **THEN** 结果为 `'flag: True'`

#### Scenario: 布尔与字符串拼接
- **WHEN** 表达式为 `true + '!'`
- **THEN** 结果为 `'True!'`

#### Scenario: 数值与布尔算术加法
- **WHEN** 表达式为 `5 + true`
- **THEN** 结果为 `6`（true → 1.0，算术加法）

#### Scenario: 布尔与布尔算术加法
- **WHEN** 表达式为 `true + true`
- **THEN** 结果为 `2`（true → 1.0，算术加法）

#### Scenario: 字符串减法
- **WHEN** 表达式为 `'hello' - 1`
- **THEN** 抛出 TypeMismatchException

#### Scenario: 关系比较
- **WHEN** 表达式为 `3 > 2`
- **THEN** 结果为 `true`

#### Scenario: 字符串比较
- **WHEN** 表达式为 `'abc' < 'abd'`
- **THEN** 结果为 `true`（字典序比较）

#### Scenario: 字符串比较区分大小写
- **WHEN** 表达式为 `'A' == 'a'`
- **THEN** 结果为 `false`（区分大小写）

#### Scenario: 数值等于比较
- **WHEN** 表达式为 `1.0 == 1`
- **THEN** 结果为 `true`

#### Scenario: 字符串等于比较
- **WHEN** 表达式为 `'hello' == 'hello'`
- **THEN** 结果为 `true`

#### Scenario: 布尔等于比较
- **WHEN** 表达式为 `true == true`
- **THEN** 结果为 `true`

#### Scenario: 布尔不等于比较
- **WHEN** 表达式为 `true != false`
- **THEN** 结果为 `true`

#### Scenario: 不同类型等于比较
- **WHEN** 表达式为 `1 == '1'`
- **THEN** 结果为 `false`（不同类型始终不等）

#### Scenario: 不同类型不等于比较
- **WHEN** 表达式为 `1 != '1'`
- **THEN** 结果为 `true`

#### Scenario: 不同类型关系比较
- **WHEN** 表达式为 `1 > '1'`
- **THEN** 抛出 TypeMismatchException

#### Scenario: 逻辑运算
- **WHEN** 表达式为 `true and false`
- **THEN** 结果为 `false`

#### Scenario: 逻辑运算短路求值（and）
- **WHEN** 表达式为 `false and (1 / 0 = 0)`
- **THEN** 结果为 `false`（不计算右侧表达式）

#### Scenario: 逻辑运算短路求值（or）
- **WHEN** 表达式为 `true or (1 / 0 = 0)`
- **THEN** 结果为 `true`（不计算右侧表达式）

#### Scenario: and 与 && 等价
- **WHEN** 表达式为 `true && false`
- **THEN** 结果为 `false`（与 `true and false` 完全等价）

#### Scenario: or 与 || 等价
- **WHEN** 表达式为 `false || true`
- **THEN** 结果为 `true`（与 `false or true` 完全等价）

#### Scenario: 逻辑非 not
- **WHEN** 表达式为 `not true`
- **THEN** 结果为 `false`

#### Scenario: 逻辑非 ! 等价于 not
- **WHEN** 表达式为 `!false`
- **THEN** 结果为 `true`（`!` 与 `not` 完全等价）

#### Scenario: 逻辑运算非布尔操作数
- **WHEN** 表达式为 `1 and 2`
- **THEN** 抛出 TypeMismatchException（逻辑运算要求 bool 操作数）

#### Scenario: 一元正号
- **WHEN** 表达式为 `+5`
- **THEN** 结果为 `5`

#### Scenario: 一元取负
- **WHEN** 表达式为 `-5`
- **THEN** 结果为 `-5`

#### Scenario: 对字符串取负
- **WHEN** 表达式为 `-'hello'`
- **THEN** 抛出 TypeMismatchException

#### Scenario: 对数值取逻辑非
- **WHEN** 表达式为 `not 1`
- **THEN** 抛出 TypeMismatchException

#### Scenario: 按位与运算
- **WHEN** 表达式为 `5 & 3`
- **THEN** 结果为 `1`（0101 & 0011 = 0001）

#### Scenario: 按位或运算
- **WHEN** 表达式为 `5 | 3`
- **THEN** 结果为 `7`（0101 | 0011 = 0111）

#### Scenario: 按位异或运算
- **WHEN** 表达式为 `5 xor 3`
- **THEN** 结果为 `6`（0101 xor 0011 = 0110）

#### Scenario: 布尔参与位运算
- **WHEN** 表达式为 `true & 6`
- **THEN** 结果为 `0`（true → 1，1 & 6 = 0001 & 0110 = 0000）

#### Scenario: & 与 && 区别
- **WHEN** 表达式为 `3 & 5`（按位与）
- **THEN** 结果为 `1`（位运算）
- **WHEN** 表达式为 `true && true`（逻辑与）
- **THEN** 结果为 `true`（布尔运算）

#### Scenario: 按位取反运算
- **WHEN** 表达式为 `~5`
- **THEN** 结果为 `-6`（按 64 位整数取反）

#### Scenario: 左移运算
- **WHEN** 表达式为 `1 << 4`
- **THEN** 结果为 `16`

#### Scenario: 右移运算
- **WHEN** 表达式为 `16 >> 2`
- **THEN** 结果为 `4`

#### Scenario: 移位量掩码
- **WHEN** 表达式为 `1 << 64`
- **THEN** 结果为 `1`（64 % 64 = 0，即 1 << 0 = 1）

#### Scenario: 负数移位量
- **WHEN** 表达式为 `1 << -1`
- **THEN** 抛出 EvaluateException

#### Scenario: NaN 参与算术
- **WHEN** 表达式为 `NaN + 1`
- **THEN** 结果为 `NaN`

#### Scenario: NaN 不等于自身
- **WHEN** 表达式为 `NaN == NaN`
- **THEN** 结果为 `false`

#### Scenario: NaN 不等于自身（!=）
- **WHEN** 表达式为 `NaN != NaN`
- **THEN** 结果为 `true`

#### Scenario: INF 参与算术
- **WHEN** 表达式为 `INF + 1`
- **THEN** 结果为 `INF`

#### Scenario: INF 减 INF
- **WHEN** 表达式为 `INF - INF`
- **THEN** 结果为 `NaN`

#### Scenario: INF 参与比较
- **WHEN** 表达式为 `INF > 1`
- **THEN** 结果为 `true`

#### Scenario: 0 乘 INF
- **WHEN** 表达式为 `0 * INF`
- **THEN** 结果为 `NaN`

---

### Requirement: 字符串插值

系统 SHALL 支持类似 C# 的字符串插值语法，使用 `$` 前缀标记插值字符串，`{}` 内嵌入表达式：

#### 语法规则
- 插值字符串以 `$` 前缀开始，后跟双引号或单引号
- `{expression}` 内可嵌入任意合法表达式
- `{{` 和 `}}` 分别表示字面量 `{` 和 `}`
- 插值表达式内支持格式说明符：`{value:format}`，如 `{3.14159:F2}` → `3.14`

#### Scenario: 基本插值
- **WHEN** 表达式为 `$"Hello, {name}!"`
- **THEN** 若 `name = "World"`，结果为 `"Hello, World!"`

#### Scenario: 表达式插值
- **WHEN** 表达式为 `$"2 + 3 = {2 + 3}"`
- **THEN** 结果为 `"2 + 3 = 5"`

#### Scenario: 格式说明符
- **WHEN** 表达式为 `$"Pi = {3.14159:F2}"`
- **THEN** 结果为 `"Pi = 3.14"`

#### Scenario: 转义花括号
- **WHEN** 表达式为 `$"{{not interpolated}}"`
- **THEN** 结果为 `"{not interpolated}"`

#### Scenario: 嵌套函数调用
- **WHEN** 表达式为 `$"sqrt(4) = {sqrt(4)}"`
- **THEN** 结果为 `"sqrt(4) = 2"`

---

### Requirement: 上下文系统

系统 SHALL 提供 ExpressionContext 类，支持符号（Symbol）和函数的注册与上下文继承。

#### 核心概念：符号（Symbol）

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

#### API 设计
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

// 删除符号
ctx.Remove("x");

// 注册函数（函数使用 name(args) 语法调用，与符号的 name 语法不同）
ctx.SetFunction("sqrt", args => Math.Sqrt(Convert.ToDouble(args[0])));
ctx.SetFunction("doubleIt", (Func<double, double>)(x => x * 2));
```

#### 符号与函数的区别

| 特性 | 符号（Symbol） | 函数（Function） |
|------|----------------|-------------------|
| 表达式中语法 | 标识符：`PI`、`now` | 函数调用：`sqrt(4)`、`max(3, 5)` |
| 注册方法 | `Set(name, value)` / `Set(name, func)` | `SetFunction(name, delegate)` |
| 参数 | 无 | 1~N 个参数 |
| 典型用途 | 常量、变量、配置值、动态状态 | 计算、转换、业务逻辑 |

#### Scenario: 注册直接值符号
- **WHEN** 通过 `context.Set("x", 42.0)` 注册符号
- **THEN** 表达式 `x + 1` 计算结果为 `43`

#### Scenario: 注册延迟值符号
- **WHEN** 通过 `context.Set("now", () => DateTime.Now.ToString())` 注册符号
- **THEN** 表达式中使用 `now`（不带括号）时，每次求值调用委托获取最新值

#### Scenario: 覆盖符号
- **WHEN** 已注册 `x = 10`，然后调用 `context.Set("x", 20.0)`
- **THEN** 后续求值使用新值 `20`

#### Scenario: 直接值覆盖为延迟值
- **WHEN** 已注册 `x = 10`，然后调用 `context.Set("x", () => GetDynamicX())`
- **THEN** 后续求值调用委托获取值

#### Scenario: 删除符号
- **WHEN** 调用 `context.Remove("x")` 删除已注册的符号
- **THEN** 后续求值中引用 `x` 将抛出 EvaluateException

#### Scenario: 动态添加符号
- **WHEN** 在表达式构建后调用 `context.Set("y", 100.0)` 添加新符号
- **THEN** 后续使用包含 `y` 的表达式可正确求值

#### Scenario: 上下文继承
- **WHEN** 创建子上下文 `childContext = parentContext.CreateChild()`
- **THEN** 子上下文可以访问父上下文中注册的所有符号和函数
- **THEN** 子上下文中注册的同名项覆盖父上下文中的项，但不影响父上下文

#### Scenario: 上下文隔离
- **WHEN** 在子上下文中通过 `Set` 修改符号值
- **THEN** 父上下文中同名符号的值不受影响

---

### Requirement: 函数注册代理

系统 SHALL 支持通过 `Func<>` 和 `Delegate` 方式注册函数，提供强类型和弱类型两种注册途径。

#### 注册方式

**方式一：弱类型委托（ExpressionFunction）**
```csharp
context.SetFunction("add", args => (double)args[0] + (double)args[1]);
```
- 参数为 `object[]`，返回 `object`
- 灵活但无类型安全

**方式二：强类型 Func<>**
```csharp
// 支持 Func<T1, TResult> 到 Func<T1, T2, T3, T4, TResult>
context.SetFunction("doubleIt", (Func<double, double>)(x => x * 2));
context.SetFunction("add", (Func<double, double, double>)((a, b) => a + b));
context.SetFunction("clamp", (Func<double, double, double, double>)((v, min, max) => Math.Max(min, Math.Min(max, v))));
```
- 参数和返回值类型由泛型参数确定
- 运行时自动将 `object` 参数转换为目标类型
- 支持 1~4 个参数的 Func<> 泛型

**方式三：Delegate 委托**
```csharp
context.SetFunction("add", (Delegate)(Func<double, double, double>)((a, b) => a + b));
```
- 适用于需要统一处理不同签名函数的场景
- 内部通过反射提取参数信息和调用委托

#### Scenario: Func<double, double> 注册
- **WHEN** 注册 `context.SetFunction("neg", (Func<double, double>)(x => -x))`
- **THEN** 表达式 `neg(5)` 结果为 `-5`

#### Scenario: Func<double, double, double> 注册
- **WHEN** 注册 `context.SetFunction("add", (Func<double, double, double>)((a, b) => a + b))`
- **THEN** 表达式 `add(3, 4)` 结果为 `7`

#### Scenario: Delegate 注册
- **WHEN** 注册 `context.SetFunction("mul", (Delegate)(Func<double, double, double>)((a, b) => a * b))`
- **THEN** 表达式 `mul(3, 4)` 结果为 `12`

#### Scenario: Func<> 参数类型自动转换
- **WHEN** 注册 `Func<double, double>` 函数，但传入整数值
- **THEN** 整数值自动转换为 double 后传入委托

---

### Requirement: 内置数学函数

系统 SHALL 提供以下内置数学函数，作为默认上下文的一部分：

| 函数 | 签名 | 说明 |
|------|------|------|
| `abs(x)` | number → number | 绝对值 |
| `sqrt(x)` | number → number | 平方根 |
| `sin(x)` | number → number | 正弦（弧度） |
| `cos(x)` | number → number | 余弦（弧度） |
| `tan(x)` | number → number | 正切（弧度） |
| `asin(x)` | number → number | 反正弦 |
| `acos(x)` | number → number | 反余弦 |
| `atan(x)` | number → number | 反正切 |
| `atan2(y, x)` | (number, number) → number | 双参数反正切 |
| `exp(x)` | number → number | e 的幂 |
| `ln(x)` | number → number | 自然对数 |
| `log(x)` | number → number | 自然对数（ln 的别名） |
| `log10(x)` | number → number | 以 10 为底对数 |
| `log2(x)` | number → number | 以 2 为底对数 |
| `ceil(x)` | number → number | 向上取整 |
| `floor(x)` | number → number | 向下取整 |
| `round(x)` | number → number | 四舍五入取整 |
| `round(x, d)` | (number, number) → number | 四舍五入到 d 位小数 |
| `truncate(x)` | number → number | 截断取整 |
| `sign(x)` | number → number | 符号（-1, 0, 1） |
| `max(a, b)` | (number, number) → number | 最大值 |
| `min(a, b)` | (number, number) → number | 最小值 |
| `pow(x, y)` | (number, number) → number | 幂运算（等价于 x ^ y） |

#### Scenario: 三角函数
- **WHEN** 表达式为 `sin(0)`
- **THEN** 结果为 `0`

#### Scenario: 对数函数
- **WHEN** 表达式为 `ln(e)`
- **THEN** 结果为 `1`（e 为内置常量）

#### Scenario: 绝对值
- **WHEN** 表达式为 `abs(-42)`
- **THEN** 结果为 `42`

---

### Requirement: 内置常量

系统 SHALL 提供以下内置数学常量：

| 常量 | 值 | 说明 |
|------|-----|------|
| `PI` | 3.14159265358979 | 圆周率 |
| `E` | 2.71828182845905 | 自然常数 |
| `INF` | double.PositiveInfinity | 正无穷 |
| `NaN` | double.NaN | 非数值 |

---

### Requirement: Expression 主入口 API

系统 SHALL 提供简洁的 Expression 类作为主入口（参考 NCalc 的简洁 API + NFun 的 Fluent Builder）：

#### 快捷 API
```csharp
// 一行式计算
object result = Expression.Eval("2 + 3 * 4");           // 14
double result = Expression.Eval<double>("sqrt(16)");     // 4

// 带上下文计算
var ctx = new ExpressionContext();
ctx.Set("x", 10.0);
object result = Expression.Eval("x * 2", ctx);           // 20
```

#### Builder API
```csharp
// Fluent Builder 模式
var calc = Expression.Builder
    .With("rate", 0.05)
    .With("now", () => DateTime.Now.ToString())
    .WithFunction("tax", args => args[0] * 0.05)
    .WithFunction("doubleIt", (Func<double, double>)(x => x * 2))
    .Build("price * (1 + tax(rate))");

calc.Set("price", 100.0);
var result = calc.Evaluate();  // 105
```

#### Scenario: 快捷计算
- **WHEN** 调用 `Expression.Eval("1 + 2")`
- **THEN** 返回 `3.0`

#### Scenario: 泛型计算
- **WHEN** 调用 `Expression.Eval<double>("sqrt(16)")`
- **THEN** 返回 `4.0`

#### Scenario: Builder 模式
- **WHEN** 使用 Builder 链式配置并 Build
- **THEN** 返回可复用的 ICalculator 实例，支持多次求值

---

### Requirement: 多线程安全

系统 SHALL 保证多线程环境下的安全使用：

#### 线程安全策略

1. **ExpressionContext 读写安全**：变量、常量、伪常量、函数的存储使用 `ConcurrentDictionary`，保证并发读写安全
2. **求值过程安全**：`EvaluationVisitor` 每次求值创建独立实例，不共享可变状态；`ExpressionContext` 在求值时提供快照读取
3. **表达式缓存安全**：AST 缓存使用 `ConcurrentDictionary`，保证并发访问安全
4. **ICalculator 线程安全**：`Calculator` 实例的 `Evaluate()` 方法可从多个线程并发调用，内部通过上下文快照隔离各次求值

#### Scenario: 并发求值
- **WHEN** 多个线程同时调用同一 `Calculator` 实例的 `Evaluate()` 方法
- **THEN** 各线程独立求值，互不干扰，结果正确

#### Scenario: 并发上下文修改
- **WHEN** 一个线程修改上下文变量，另一个线程同时求值
- **THEN** 求值线程使用修改前的快照或修改后的值均可，但不会导致异常或数据损坏

#### Scenario: 并发缓存访问
- **WHEN** 多个线程首次解析同一表达式字符串
- **THEN** 缓存正确工作，只有一个线程执行解析，其他线程等待或使用缓存结果

---

### Requirement: 错误处理

系统 SHALL 提供清晰的错误信息，包含错误类型、位置和描述：

| 异常类型 | 触发场景 |
|----------|----------|
| `ParseException` | 词法/语法错误，包含行号和列号 |
| `EvaluateException` | 运行时计算错误（如除以零、函数未找到） |
| `TypeMismatchException` | 类型不匹配（如对字符串执行减法） |

#### Scenario: 语法错误
- **WHEN** 表达式为 `2 + * 3`
- **THEN** 抛出 ParseException，包含错误位置信息

#### Scenario: 除以零
- **WHEN** 表达式为 `1 / 0`
- **THEN** 抛出 EvaluateException

#### Scenario: 函数未找到
- **WHEN** 表达式为 `unknownFunc(1)`
- **THEN** 抛出 EvaluateException，包含函数名

---

### Requirement: AST 与 Visitor 模式

系统 SHALL 基于 AST + Visitor 模式设计（参考 NCalc），支持扩展：

#### AST 节点类型
- `ValueExpression` — 常量值（布尔、数值、字符串）
- `Identifier` — 变量/常量/伪常量引用
- `BinaryExpression` — 二元运算
- `UnaryExpression` — 一元运算
- `FunctionCall` — 函数调用
- `InterpolatedString` — 插值字符串（包含文本段和表达式段）

#### Visitor 接口
```csharp
public interface IExpressionVisitor<out T>
{
    T Visit(ValueExpression expr);
    T Visit(Identifier expr);
    T Visit(BinaryExpression expr);
    T Visit(UnaryExpression expr);
    T Visit(FunctionCall expr);
    T Visit(InterpolatedString expr);
}
```

#### Scenario: 自定义 Visitor
- **WHEN** 用户实现 IExpressionVisitor 接口
- **THEN** 可以遍历 AST 执行自定义逻辑（如序列化、优化、代码生成）

---

### Requirement: 表达式缓存

系统 SHALL 支持表达式解析结果缓存（参考 NCalc），避免重复解析相同字符串：

- 默认启用线程安全的 ConcurrentDictionary 缓存
- 可通过 `ExpressionOptions.NoCache` 禁用
- 缓存键为表达式字符串

#### Scenario: 缓存命中
- **WHEN** 同一表达式字符串被多次解析
- **THEN** 第二次及后续直接从缓存获取 AST，不重新解析

---

### Requirement: Lambda 编译

系统 SHALL 支持将 AST 编译为 .NET 委托（`System.Linq.Expressions` → `Func<ExpressionContext, object>`），以大幅提升重复求值的执行性能。

#### 设计原理

默认的 `EvaluationVisitor` 模式通过虚方法分发遍历 AST，每次求值都需要遍历整棵树，存在以下性能瓶颈：
- 虚方法调用的间接分支预测失败
- 大量小对象的堆分配（AST 节点）
- 类型判断和模式匹配的开销

Lambda 编译将 AST 一次性转换为 `Expression<TDelegate>` 并编译为原生机器码，后续求值直接执行编译后的委托，性能接近手写 C# 代码。

#### 编译策略

1. **编译时机**：在 `Build()` 或首次 `Evaluate()` 时触发编译，编译结果缓存在 `ICalculator` 实例中
2. **编译目标**：`Func<ExpressionContext, object>` — 接收上下文，返回求值结果
3. **符号访问**：编译后的委托通过 `ExpressionContext` 参数在运行时查找符号值（支持直接值和延迟值）
4. **函数调用**：编译后的委托通过 `ExpressionContext` 参数在运行时查找并调用注册的函数
5. **短路求值**：`and`/`or` 编译为 `if-else` 条件分支，保留短路语义
6. **错误处理**：编译时无法检测的运行时错误（如除以零、类型不匹配）在委托执行时抛出对应异常

#### API 设计

```csharp
// 默认使用 Lambda 编译模式
var calc = Expression.Builder
    .With("x", 10.0)
    .Build("x * 2 + 1");
var result = calc.Evaluate();  // 21 — 内部使用编译后的委托

// 禁用 Lambda 编译，回退到 Visitor 模式
var calcVisitor = Expression.Builder
    .With("x", 10.0)
    .WithOptions(ExpressionOptions.NoLambdaCompilation)
    .Build("x * 2 + 1");
var result = calcVisitor.Evaluate();  // 21 — 内部使用 EvaluationVisitor

// 编译为强类型委托
var func = Expression.Compile<Func<ExpressionContext, double>>("x * 2 + 1");
var ctx = new ExpressionContext();
ctx.Set("x", 10.0);
var result = func(ctx);  // 21.0
```

#### 性能预期

| 模式 | 首次求值 | 后续求值 | 适用场景 |
|------|----------|----------|----------|
| Visitor 模式 | 快（仅解析） | 慢（遍历 AST） | 一次性计算、调试 |
| Lambda 编译 | 慢（编译开销） | 快（原生代码） | 重复求值、高性能场景 |

预期 Lambda 编译后的重复求值性能比 Visitor 模式提升 **10~50 倍**（参考 NCalc.LambdaCompilation 基准测试数据）。

#### Scenario: Lambda 编译基本求值
- **WHEN** 使用 Builder.Build 构建计算器并调用 Evaluate
- **THEN** 内部自动编译为委托，结果正确

#### Scenario: Lambda 编译短路求值
- **WHEN** 表达式为 `false and (1 / 0 = 0)`，使用 Lambda 编译模式
- **THEN** 短路求值正确工作，不计算右侧，结果为 `false`

#### Scenario: Lambda 编译符号访问
- **WHEN** 表达式引用上下文中的符号 `x`，使用 Lambda 编译模式
- **THEN** 编译后的委托通过 ExpressionContext 参数正确查找符号值

#### Scenario: Lambda 编译延迟值符号
- **WHEN** 符号 `now` 注册为延迟值 `() => DateTime.Now.ToString()`，使用 Lambda 编译模式
- **THEN** 每次求值调用委托获取最新值

#### Scenario: Lambda 编译函数调用
- **WHEN** 表达式调用注册的函数 `sqrt(16)`，使用 Lambda 编译模式
- **THEN** 编译后的委托正确调用上下文中的函数

#### Scenario: 禁用 Lambda 编译
- **WHEN** 使用 `ExpressionOptions.NoLambdaCompilation` 选项
- **THEN** 回退到 EvaluationVisitor 模式求值

#### Scenario: 编译为强类型委托
- **WHEN** 调用 `Expression.Compile<Func<ExpressionContext, double>>("sqrt(16)")`
- **THEN** 返回强类型委托，调用后返回 `4.0`

#### Scenario: Lambda 编译运行时错误
- **WHEN** 表达式为 `1 / 0`，使用 Lambda 编译模式
- **THEN** 编译成功，但执行时抛出 EvaluateException

#### Scenario: Lambda 编译线程安全
- **WHEN** 多个线程并发调用同一编译后的委托
- **THEN** 各线程独立求值，结果正确

---

### Requirement: 数值类型策略（待定）

> **注意**：此需求当前使用"统一 double"方案，待用户确认是否切换为"long + double 双类型"方案。

当前方案：所有数值统一使用 `double` 表示。

备选方案（long + double 双类型）：
- 整数字面量 → `long`，浮点字面量 → `double`
- `long ⊕ long → long`，`long ⊕ double → double`
- 位运算直接在 `long` 上操作
- 整除 `//` 和取模 `%` 返回 `long`
- 除法 `/` 始终返回 `double`
- 数学函数始终返回 `double`

优势：整数精确、位运算自然、语义正确（`7//2=3` 而非 `3.0`）
