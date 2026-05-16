# Checklist

## 类型系统
- [ ] 布尔字面量 true/false 正确解析为布尔类型
- [ ] 十进制数值字面量统一解析为 number 类型（double）
- [ ] 单引号和双引号字符串正确解析为字符串类型
- [ ] 字符串与数值使用 + 时自动拼接（数值转字符串）
- [ ] 布尔值参与算术运算时自动转换（true=1, false=0）
- [ ] 位运算时数值截断为 long 执行运算，结果以 double 返回

## 进制字面量
- [ ] 十六进制字面量 0xFF 正确解析为 255.0
- [ ] 十六进制前缀和数字大小写不敏感（0XaB = 171.0）
- [ ] 八进制字面量 0o77 正确解析为 63.0
- [ ] 二进制字面量 0b1010 正确解析为 10.0
- [ ] 不同进制混合运算（0xFF + 0o10 + 0b1010 = 273.0）
- [ ] 十六进制与位运算（0xFF & 0x0F = 15.0）
- [ ] 二进制与位运算（0b1100 & 0b1010 = 8.0）
- [ ] 十六进制负数（-0xFF = -255.0）
- [ ] 非法十六进制数字抛出 ParseException（0xGH）
- [ ] 非法八进制数字抛出 ParseException（0o89）
- [ ] 非法二进制数字抛出 ParseException（0b12）
- [ ] 前缀后无数字抛出 ParseException（0x、0o、0b）
- [ ] 十六进制/八进制/二进制不支持小数点和指数

## 运算符 — 优先级与结合性
- [ ] 运算符优先级正确（乘方 > 乘除 > 加减 > 移位 > 位与 > 位异或 > 位或 > 关系 > 等于 > 逻辑与 > 逻辑或）
- [ ] 括号分组正确改变优先级
- [ ] 乘方 ^ 为右结合（2^3^2 = 512）
- [ ] 算术运算符 +, -, *, /, //, % 为左结合（10-3-2=5, 100/10/2=5）
- [ ] 复杂优先级交互正确（2+3*4^2 = 50）

## 运算符 — 算术运算
- [ ] 加法 + 正确计算（3+4=7）
- [ ] 减法 - 正确计算
- [ ] 乘法 * 正确计算
- [ ] 除法 / 返回浮点结果（7/2 = 3.5）
- [ ] 整除 // 返回整数部分（7//2 = 3）
- [ ] 负数整除向零取整（-7//2 = -3）
- [ ] 取模 % 正确计算（7%3 = 1）
- [ ] 负数取模结果符号与左操作数一致（-7%3 = -1）
- [ ] 乘方 ^ 正确计算（2^3 = 8）
- [ ] 乘方小数指数（9^0.5 = 3.0）
- [ ] 乘方负数指数（2^-1 = 0.5）
- [ ] 零的零次方（0^0 = 1）
- [ ] 负底数非整数指数抛出 EvaluateException

## 运算符 — + 运算符完整行为
- [ ] string + string = 字符串拼接（'a'+'b'='ab'）
- [ ] string + number = 拼接（'x'+42='x42'）
- [ ] number + string = 拼接（42+'x'='42x'）
- [ ] string + bool = 拼接（'v:'+true='v:True'）
- [ ] bool + string = 拼接（true+'!'='True!'）
- [ ] number + number = 算术加法（3+4=7）
- [ ] number + bool = 算术加法（5+true=6）
- [ ] bool + number = 算术加法（false+3=3）
- [ ] bool + bool = 算术加法（true+true=2）
- [ ] string - number 抛出 TypeMismatchException

## 运算符 — 位运算
- [ ] 按位与 & 正确计算（5 & 3 = 1）
- [ ] 按位或 | 正确计算（5 | 3 = 7）
- [ ] 按位异或 xor 正确计算（5 xor 3 = 6）
- [ ] 按位取反 ~ 正确计算（~5 = -6）
- [ ] 左移 << 正确计算（1 << 4 = 16）
- [ ] 右移 >> 正确计算（16 >> 2 = 4）
- [ ] 位运算优先级正确（位于加减之后、关系比较之前）
- [ ] 布尔参与位运算自动转换（true & 6 = 0）
- [ ] 移位量掩码（1 << 64 = 1）
- [ ] 负数移位量抛出 EvaluateException

## 运算符 — 关系与等于
- [ ] 关系运算符 >, <, >=, <= 返回布尔值
- [ ] 字符串比较使用字典序（'abc' < 'abd' = true）
- [ ] 字符串比较区分大小写（'A' == 'a' = false）
- [ ] 数值等于比较（1.0 == 1 = true）
- [ ] 字符串等于比较（'hello' == 'hello' = true）
- [ ] 布尔等于比较（true == true = true）
- [ ] 布尔不等于比较（true != false = true）
- [ ] 不同类型等于比较返回 false（1 == '1' = false）
- [ ] 不同类型不等于比较返回 true（1 != '1' = true）
- [ ] 不同类型关系比较抛出 TypeMismatchException

## 运算符 — 逻辑运算
- [ ] and/&& 正确计算（true and false = false）
- [ ] or/|| 正确计算（true or false = true）
- [ ] not/! 正确计算（not true = false）
- [ ] and 与 && 完全等价
- [ ] or 与 || 完全等价
- [ ] not 与 ! 完全等价
- [ ] and 短路求值（false and expr 不计算 expr）
- [ ] or 短路求值（true or expr 不计算 expr）
- [ ] 逻辑运算非布尔操作数抛出 TypeMismatchException（1 and 2）

## 运算符 — 一元运算
- [ ] 一元正号 + 正确计算（+5 = 5）
- [ ] 一元取负 - 正确计算（-5 = -5）
- [ ] 对字符串取负抛出 TypeMismatchException
- [ ] 对数值取逻辑非抛出 TypeMismatchException（not 1）

## 运算符 — 符号与关键字区分
- [ ] & 与 && 完全不同（& 是按位与，&& 是逻辑与）
- [ ] | 与 || 完全不同（| 是按位或，|| 是逻辑或）
- [ ] ^ 与 xor 完全不同（^ 是乘方，xor 是按位异或）

## 运算符 — 边界行为
- [ ] 除以零抛出 EvaluateException（1/0, 1//0, 5%0）
- [ ] 0 的负数次幂抛出 EvaluateException
- [ ] 负底数非整数指数抛出 EvaluateException

## 运算符 — 特殊值（NaN / INF）
- [ ] NaN 参与算术结果为 NaN（NaN + 1 = NaN）
- [ ] NaN 不等于自身（NaN == NaN = false）
- [ ] NaN != NaN 为 true
- [ ] INF 参与算术结果为 INF（INF + 1 = INF）
- [ ] INF 减 INF 结果为 NaN
- [ ] INF 参与比较（INF > 1 = true）
- [ ] 0 乘 INF 结果为 NaN

## 字符串插值
- [ ] 基本插值 $"Hello, {name}!" 正确求值
- [ ] 表达式插值 $"2+3={2+3}" 正确求值
- [ ] 格式说明符 {value:F2} 正确格式化
- [ ] 转义花括号 {{ }} 输出字面量 { }
- [ ] 插值中嵌套函数调用正确求值

## 上下文系统 — 符号
- [ ] Set(name, value) 注册直接值符号后表达式可引用
- [ ] Set(name, Func<object>) 注册延迟值符号后表达式以标识符形式使用
- [ ] 延迟值符号每次求值调用委托获取最新值
- [ ] Set 覆盖已有符号（直接值 → 新直接值）
- [ ] Set 覆盖已有符号（直接值 → 延迟值）
- [ ] Set 覆盖已有符号（延迟值 → 直接值）
- [ ] Remove 删除符号后引用抛出 EvaluateException
- [ ] 动态添加符号（表达式构建后仍可添加）
- [ ] CreateChild 创建的子上下文可访问父上下文的所有符号和函数
- [ ] 子上下文同名符号覆盖父上下文但不影响父
- [ ] 子上下文修改符号不影响父上下文

## 上下文系统 — 函数
- [ ] SetFunction 注册弱类型委托函数后表达式可调用
- [ ] SetFunction 注册 Func<> 强类型函数后表达式可调用
- [ ] SetFunction 注册 Delegate 函数后表达式可调用
- [ ] 函数与符号语法不同（函数用 name(args)，符号用 name）

## 函数注册代理
- [ ] 弱类型委托注册（ExpressionFunction，object[] 参数）
- [ ] Func<TResult> 注册（无参函数）
- [ ] Func<T1, TResult> 注册（1 参数函数）
- [ ] Func<T1, T2, TResult> 注册（2 参数函数）
- [ ] Func<T1, T2, T3, TResult> 注册（3 参数函数）
- [ ] Func<T1, T2, T3, T4, TResult> 注册（4 参数函数）
- [ ] Delegate 注册（通过反射提取参数和调用）
- [ ] Func<> 参数类型自动转换（int → double 等）

## 内置数学函数
- [ ] 三角函数 sin, cos, tan, asin, acos, atan, atan2 正确计算
- [ ] 对数函数 ln/log, log10, log2 正确计算
- [ ] 取整函数 ceil, floor, round, truncate 正确计算
- [ ] 其他函数 abs, sqrt, exp, sign, max, min, pow 正确计算
- [ ] 内置符号 PI, E, INF, NaN 可正确引用

## Expression API
- [ ] Expression.Eval 一行式计算正确
- [ ] Expression.Eval<T> 泛型计算正确
- [ ] Expression.Builder Fluent API 链式配置正确
- [ ] Builder 支持 With(name, value) 和 With(name, Func<object>) 方法
- [ ] Builder 支持 WithFunction 的 Func<> 和 Delegate 重载
- [ ] ICalculator 可复用计算器支持多次求值
- [ ] Calculator.Set 方法可修改符号值
- [ ] 表达式缓存机制正确工作（相同字符串不重复解析）
- [ ] 缓存可通过选项禁用

## 多线程安全
- [ ] ExpressionContext 使用 ConcurrentDictionary 存储符号和函数
- [ ] 多线程并发调用同一 Calculator 的 Evaluate 方法结果正确
- [ ] 一个线程修改上下文符号时另一个线程求值不导致异常
- [ ] 多线程首次解析同一表达式字符串时缓存正确工作
- [ ] EvaluationVisitor 每次求值创建独立实例

## 错误处理
- [ ] 语法错误抛出 ParseException 并包含位置信息
- [ ] 除以零抛出 EvaluateException
- [ ] 函数未找到抛出 EvaluateException 并包含函数名
- [ ] 类型不匹配抛出 TypeMismatchException

## AST 与 Visitor
- [ ] AST 节点类型完整（ValueExpression, Identifier, BinaryExpression, UnaryExpression, FunctionCall, InterpolatedString）
- [ ] BinaryOperatorType 包含位运算类型（BitwiseAnd, BitwiseOr, BitwiseXor, LeftShift, RightShift）
- [ ] UnaryOperatorType 包含按位取反类型（BitwiseNot）
- [ ] IExpressionVisitor 接口可被自定义实现
- [ ] EvaluationVisitor 正确遍历并求值所有节点类型
