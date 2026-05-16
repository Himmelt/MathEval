# Tasks

- [ ] Task 1: 项目骨架搭建
  - [ ] SubTask 1.1: 创建解决方案 MathEval.sln 和类库项目 src/MathEval/MathEval.csproj（.NET 8）
  - [ ] SubTask 1.2: 创建测试项目 tests/MathEval.Tests/MathEval.Tests.csproj（xUnit）
  - [ ] SubTask 1.3: 配置解决方案结构，添加项目引用

- [ ] Task 2: Token 词法分析器
  - [ ] SubTask 2.1: 定义 TokenType 枚举（布尔、数值、字符串、运算符、标识符、插值相关 Token，含位运算符 &, |, ~, <<, >>, xor 关键字）
  - [ ] SubTask 2.2: 定义 Token 类（类型、值、位置信息）
  - [ ] SubTask 2.3: 实现 Tokenizer 类，支持数值字面量（十进制整数/浮点/科学计数法、十六进制 0x、八进制 0o、二进制 0b）、布尔字面量、字符串字面量（单/双引号、转义字符）、运算符（含位运算符和 xor 关键字）、标识符
  - [ ] SubTask 2.4: 实现插值字符串的词法分析（$ 前缀、{} 嵌套、{{ }} 转义）

- [ ] Task 3: AST 节点定义
  - [ ] SubTask 3.1: 定义 ExpressionBase 抽象基类和 IExpressionVisitor 接口
  - [ ] SubTask 3.2: 实现 ValueExpression（布尔、数值、字符串常量）
  - [ ] SubTask 3.3: 实现 Identifier（符号引用，统一标识直接值和延迟值符号）
  - [ ] SubTask 3.4: 实现 BinaryExpression（二元运算，含运算符类型枚举，含位运算 &, |, xor, <<, >>）
  - [ ] SubTask 3.5: 实现 UnaryExpression（一元运算，含运算符类型枚举，含按位取反 ~）
  - [ ] SubTask 3.6: 实现 FunctionCall（函数调用，含参数列表）
  - [ ] SubTask 3.7: 实现 InterpolatedString（插值字符串，含文本段和表达式段）

- [ ] Task 4: Parser 语法分析器
  - [ ] SubTask 4.1: 定义 BinaryOperatorType 枚举（含 BitwiseAnd, BitwiseOr, BitwiseXor, LeftShift, RightShift）和 UnaryOperatorType 枚举（含 BitwiseNot）
  - [ ] SubTask 4.2: 实现递归下降解析器，按优先级层次解析表达式（Primary → Unary → Power → Multiplicative → Additive → Shift → BitwiseAnd → BitwiseXor → BitwiseOr → Relational → Equality → And → Or）
  - [ ] SubTask 4.3: 实现函数调用解析（标识符后跟括号和参数列表）
  - [ ] SubTask 4.4: 实现插值字符串解析（文本段与表达式段交替）
  - [ ] SubTask 4.5: 实现错误报告（包含位置信息的 ParseException）

- [ ] Task 5: ExpressionContext 上下文系统
  - [ ] SubTask 5.1: 实现 ExpressionContext 类（符号和函数的存储，使用 ConcurrentDictionary 保证线程安全）
  - [ ] SubTask 5.2: 实现 Set(name, value) 方法注册直接值符号，Set(name, Func<object>) 方法注册延迟值符号
  - [ ] SubTask 5.3: 实现 SetFunction 方法注册函数（弱类型委托、Func<>、Delegate 三种方式）
  - [ ] SubTask 5.4: 实现 Remove 方法（动态删除符号或函数）
  - [ ] SubTask 5.5: 实现上下文继承机制（CreateChild 方法，子上下文可访问父上下文，同名覆盖但不影响父）
  - [ ] SubTask 5.6: 实现内置符号（PI, E, INF, NaN）和内置数学函数的默认上下文
  - [ ] SubTask 5.7: 实现 Func<> 和 Delegate 强类型函数注册（自动包装为统一调用接口，支持 1~4 参数）

- [ ] Task 6: EvaluationVisitor 求值器
  - [ ] SubTask 6.1: 实现 EvaluationVisitor，遍历 AST 计算表达式值（每次求值创建独立实例，不共享可变状态）
  - [ ] SubTask 6.2: 实现算术运算求值（+, -, *, /, //, %, ^），含乘方小数/负数指数、0^0 约定、负底数非整数指数错误
  - [ ] SubTask 6.3: 实现位运算求值（&, |, xor, ~, <<, >>，数值截断为 long 后运算，结果以 double 返回，布尔自动转换）
  - [ ] SubTask 6.4: 实现 + 运算符完整行为（string+string 拼接、string+number 拼接、number+string 拼接、string+bool 拼接、bool+string 拼接、number+number 加法、number+bool 加法、bool+number 加法、bool+bool 加法）
  - [ ] SubTask 6.5: 实现关系运算求值（==, !=, >, <, >=, <=，含同类型比较、不同类型等于/不等于、不同类型关系比较抛异常、字符串字典序、NaN 比较）
  - [ ] SubTask 6.6: 实现逻辑运算求值（and/&&, or/||, not/!，含短路求值，非布尔操作数抛 TypeMismatchException）
  - [ ] SubTask 6.7: 实现一元运算求值（+正号, -取负, not/!逻辑非, ~按位取反，含类型检查和错误）
  - [ ] SubTask 6.8: 实现函数调用求值（从上下文查找函数并执行，支持弱类型委托和强类型 Func<>/Delegate）
  - [ ] SubTask 6.9: 实现符号求值（Identifier 节点查找符号，直接值直接返回，延迟值调用委托获取）
  - [ ] SubTask 6.10: 实现插值字符串求值（拼接文本段和表达式段结果）
  - [ ] SubTask 6.11: 实现特殊值行为（NaN 参与算术/比较、INF 参与算术/比较）
  - [ ] SubTask 6.12: 实现运行时错误处理（除以零、取模零、函数未找到、类型不匹配、负数移位量）

- [ ] Task 7: Expression 主入口 API
  - [ ] SubTask 7.1: 实现 Expression 类（快捷 Eval / Eval<T> 静态方法）
  - [ ] SubTask 7.2: 实现 ExpressionBuilder（Fluent Builder 模式，With / WithFunction / WithOptions / Build）
  - [ ] SubTask 7.3: 实现 ICalculator 接口和 Calculator 类（可复用计算器，Set / Evaluate，线程安全）
  - [ ] SubTask 7.4: 实现表达式缓存机制（ConcurrentDictionary，可配置禁用，线程安全）

- [ ] Task 10: Lambda 编译器
  - [ ] SubTask 10.1: 实现 LambdaCompilerVisitor，将 AST 转换为 System.Linq.Expressions.Expression 树
  - [ ] SubTask 10.2: 实现算术/位运算/关系/逻辑/一元运算的 Lambda 表达式生成
  - [ ] SubTask 10.3: 实现符号访问的 Lambda 表达式生成（通过 ExpressionContext 参数调用 GetSymbolValue 方法）
  - [ ] SubTask 10.4: 实现函数调用的 Lambda 表达式生成（通过 ExpressionContext 参数调用 InvokeFunction 方法）
  - [ ] SubTask 10.5: 实现短路求值的 Lambda 表达式生成（and/or 编译为 if-else 条件分支）
  - [ ] SubTask 10.6: 实现插值字符串的 Lambda 表达式生成（StringBuilder 拼接）
  - [ ] SubTask 10.7: 实现运行时错误处理的 Lambda 表达式生成（除零检查、类型检查等）
  - [ ] SubTask 10.8: 实现 Expression.Compile<TDelegate> 方法，将表达式编译为强类型委托
  - [ ] SubTask 10.9: 集成到 Calculator 类，默认使用 Lambda 编译模式，支持 NoLambdaCompilation 选项回退

- [ ] Task 8: 内置数学函数库
  - [ ] SubTask 8.1: 实现三角函数（sin, cos, tan, asin, acos, atan, atan2）
  - [ ] SubTask 8.2: 实现对数函数（ln/log, log10, log2）
  - [ ] SubTask 8.3: 实现取整函数（ceil, floor, round, truncate）
  - [ ] SubTask 8.4: 实现其他数学函数（abs, sqrt, exp, sign, max, min, pow）

- [ ] Task 9: 单元测试
  - [ ] SubTask 9.1: 词法分析器测试（各类 Token 识别、位运算符识别、xor 关键字、错误处理）
  - [ ] SubTask 9.2: 进制字面量测试（十六进制 0x、八进制 0o、二进制 0b 解析、混合运算、非法字符、前缀后无数字、大小写不敏感）
  - [ ] SubTask 9.3: 语法分析器测试（运算符优先级、结合性、位运算优先级、xor 解析、错误恢复）
  - [ ] SubTask 9.4: 算术运算测试（加减乘除整除取模乘方、负数整除/取模、乘方小数/负数指数、0^0、边界行为）
  - [ ] SubTask 9.5: + 运算符完整行为测试（9 种操作数组合、string-string 拼接、string-number 拼接等）
  - [ ] SubTask 9.6: 位运算测试（&, |, xor, ~, <<, >>、布尔参与位运算、移位量掩码、负数移位量）
  - [ ] SubTask 9.7: 关系与等于测试（同类型比较、不同类型等于/不等于、字符串字典序、区分大小写、NaN 比较）
  - [ ] SubTask 9.8: 逻辑运算测试（and/&&, or/||, not/!、短路求值、符号等价、非布尔操作数错误）
  - [ ] SubTask 9.9: 一元运算测试（+正号, -取负, not/!, ~按位取反、类型错误）
  - [ ] SubTask 9.10: 特殊值测试（NaN 算术/比较、INF 算术/比较）
  - [ ] SubTask 9.11: 字符串插值测试（基本插值、格式说明符、转义、嵌套函数）
  - [ ] SubTask 9.12: 上下文系统测试（符号注册：直接值/延迟值、覆盖、删除、动态添加、继承、隔离）
  - [ ] SubTask 9.13: 函数注册代理测试（弱类型委托、Func<>、Delegate 注册和调用）
  - [ ] SubTask 9.14: 内置数学函数测试（各类函数正确性）
  - [ ] SubTask 9.15: Expression API 测试（快捷 API、Builder API、缓存）
  - [ ] SubTask 9.16: 多线程安全测试（并发求值、并发上下文修改、并发缓存访问）
  - [ ] SubTask 9.17: Lambda 编译测试（基本求值、短路求值、符号访问、延迟值符号、函数调用、运行时错误、线程安全）
  - [ ] SubTask 9.18: Lambda 编译 vs Visitor 模式结果一致性测试（所有运算场景两种模式结果相同）
  - [ ] SubTask 9.19: 性能基准测试（Lambda 编译 vs Visitor 模式的重复求值性能对比）

# Task Dependencies
- [Task 2] depends on [Task 1]
- [Task 3] depends on [Task 1]
- [Task 4] depends on [Task 2, Task 3]
- [Task 5] depends on [Task 1]
- [Task 6] depends on [Task 3, Task 5]
- [Task 7] depends on [Task 4, Task 5, Task 6]
- [Task 8] depends on [Task 5]
- [Task 10] depends on [Task 3, Task 5, Task 7]
- [Task 9] depends on [Task 7, Task 8, Task 10]
