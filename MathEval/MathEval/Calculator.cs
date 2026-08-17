using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Optimization;
using MathEval.Options;
using MathEval.Visitors;

namespace MathEval;

public partial class Calculator(string expression, ExpressionContext context, ExpressionOptions options = ExpressionOptions.None) {

    private LogicalExpression? _ast;
    private CompiledExpression? _compiledExpression;
    private EvaluationVisitor? _visitor;
    private Func<ExpressionContext, double>? _specializedFunc;
    private long _strictCheckedVersion = -1;
    private readonly ExpressionOptions _options = options;
    private readonly ExpressionContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly string _expressionText = expression ?? throw new ArgumentNullException(nameof(expression));

    /// <summary>
    /// 最大嵌套深度（控制表达式嵌套层次上限，防止栈溢出）
    /// 默认 <see cref="Parser.Parser.DefaultMaxDepth"/> = 1024
    /// </summary>
    public int MaxNestingDepth { get; set; } = Parser.Parser.DefaultMaxDepth;

    public object Eval() {
        EnsureParsed();

        // StrictTypes：求值前静态 Kind 检查（含死分支）；整棵树纯 Number 时走特化委托
        if (_options.HasFlag(ExpressionOptions.StrictTypes)) {
            EnsureStrictChecked();
            if (_specializedFunc != null) return _specializedFunc(_context);
        }

        // 如果启用了编译优化，使用编译后的委托
        if (_options.HasFlag(ExpressionOptions.CompileOptimization)) {
            EnsureCompiled();
            return _compiledExpression!.Evaluate(_context);
        }

        // 否则使用原始的 Visitor 模式（复用 visitor 实例减少 GC 压力 OPT-2）。
        // 内核值流为 MathValue，出口经 ToObject 装箱为 object 返回
        var visitor = _visitor ??= new EvaluationVisitor(_context);
        return _ast!.Accept(visitor).ToObject();
    }

    public T Eval<T>() {
        var result = Eval();
        return ConvertResult<T>(result);
    }

    private static T ConvertResult<T>(object result) {
        if (result is T typedResult) return typedResult;

        var targetType = typeof(T);

        if (result is double d) {
            if (targetType == typeof(double)) return (T)(object)d;
            if (targetType == typeof(float)) return (T)(object)(float)d;
            if (targetType == typeof(bool)) return (T)(object)(d != 0);
            if (targetType == typeof(string)) return (T)(object)d.ToString();
            if (targetType == typeof(int)) return (T)(object)(int)d;
            if (targetType == typeof(long)) return (T)(object)(long)d;
            if (targetType == typeof(decimal)) return (T)(object)(decimal)d;
            if (targetType == typeof(sbyte)) return (T)(object)(sbyte)d;
            if (targetType == typeof(byte)) return (T)(object)(byte)d;
            if (targetType == typeof(short)) return (T)(object)(short)d;
            if (targetType == typeof(ushort)) return (T)(object)(ushort)d;
            if (targetType == typeof(uint)) return (T)(object)(uint)d;
            if (targetType == typeof(ulong)) return (T)(object)(ulong)d;
        }

        if (result is double[] arr) {
            if (targetType == typeof(double[])) return (T)(object)arr;
            if (targetType == typeof(List<double>)) return (T)(object)arr.ToList();
        }

        if (result is string[] strArr) {
            if (targetType == typeof(string[])) return (T)(object)strArr;
            if (targetType == typeof(List<string>)) return (T)(object)strArr.ToList();
        }

        // Handle non-double numeric types from context variables
        if (result is IConvertible conv) {
            try {
                return (T)Convert.ChangeType(conv, targetType);
            } catch (InvalidCastException) { } catch (FormatException) { } catch (System.OverflowException) { }
        }

        throw new TypeMismatchException(
            $"无法将 {result?.GetType().Name ?? "null"} 转换为 {typeof(T).Name}",
            typeof(T).Name, result?.GetType().Name ?? "null");
    }

    private void EnsureParsed() {
        if (_ast != null) return;

        if (string.IsNullOrWhiteSpace(_expressionText)) throw new SyntaxException(MathEvalErrorCode.EmptyExpression, "表达式不能为空或仅包含空白字符", 0);

        // OPT-7: 使用 GetOrAdd 代替 TryGet + Set，避免并发首跑时重复解析同一表达式。
        // BUG-审核：缓存键必须包含解析指纹（折叠选项位），否则
        // 不同 options 会互相污染缓存条目（如未折叠版本被 ConstantFolding 用户命中）
        if (_options.HasFlag(ExpressionOptions.NoCache)) {
            _ast = ParseAndOptimize();
        } else {
            _ast = OptimizedExpressionCache.GetOrAdd(BuildCacheKey(), _ => ParseAndOptimize());
        }
    }

    /// <summary>
    /// 解析指纹：影响 AST 形态的全部因子（当前仅常量折叠选项位）。
    /// 与表达式文本共同构成缓存键，保证同文本不同解析配置互不污染
    /// </summary>
    private string BuildCacheKey() {
        int flags = _options.HasFlag(ExpressionOptions.ConstantFolding) ? 1 : 0;
        return $"{_expressionText}\u0000{flags:X}";
    }

    /// <summary>
    /// 解析表达式并应用优化（常量折叠）
    /// </summary>
    private LogicalExpression ParseAndOptimize() {
        var lexer = new Lexer.Lexer(_expressionText);
        var parser = new Parser.Parser(lexer, MaxNestingDepth);
        var ast = parser.Parse();

        if (_options.HasFlag(ExpressionOptions.ConstantFolding)) {
            ast = ConstantFolder.Fold(ast);
        }

        return ast;
    }

    private void EnsureCompiled() {
        if (_compiledExpression != null) return;
        EnsureParsed();

        // OPT-8: 使用 GetOrAddCompiled 代替 TryGetCompiled + SetCompiled，
        // 内部双重检查锁定避免并发首跑重复编译。
        // 缓存键与 EnsureParsed 一致（含解析指纹），AST 形态不同的条目互不串用
        if (_options.HasFlag(ExpressionOptions.NoCache)) {
            _compiledExpression = new CompiledExpression(_ast!);
        } else {
            _compiledExpression = OptimizedExpressionCache.GetOrAddCompiled(
                BuildCacheKey(),
                _ => _ast!,
                ast => new CompiledExpression(ast)
            );
        }
    }

    /// <summary>
    /// StrictTypes 检查（幂等）：以 SymbolVersion 判定是否需要重查——
    /// 上下文符号/函数变更后重推断；纯 Number 特化委托按推断结论获取/复用。
    /// 推断失败（无效类型组合、符号/函数缺失）在求值前抛出
    /// </summary>
    private void EnsureStrictChecked() {
        var version = _context.SymbolVersion;
        if (_strictCheckedVersion == version) return;

        bool folded = _options.HasFlag(ExpressionOptions.ConstantFolding);
        var (_, pureNumber) = StrictTypeCache.InferKind(_expressionText, _context, _ast!, folded);
        _specializedFunc = StrictTypeCache.GetOrCompileSpecialized(_expressionText, _context, _ast!, pureNumber, folded);
        _strictCheckedVersion = version;
    }
}