using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Parser;
using MathEval.Visitors;

namespace MathEval.TypeSystem;

/// <summary>
/// 静态 Kind 推断（StrictTypes，见设计文档 §7）：
/// 求值前对整棵 AST 做一遍类型检查，与 TypeHelper 运行时分发规则保持一致——
/// 所有静态报出的错误都是运行时必然发生的错误（保守正确，不误报）；
/// 死分支（条件表达式的两侧、And/Or 短路的右侧）同样被检查。
/// 返回根节点的 Kind：null 表示无法静态确定（延迟符号/无签名函数等），由运行时兜底。
/// </summary>
internal sealed class KindInferencePass(ExpressionContext context) : IExpressionVisitor<MathKind?> {
    private readonly ExpressionContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private bool _pureNumber = true;

    /// <summary>
    /// 对整棵树执行推断：返回根节点 Kind（null = 无法静态确定），
    /// 以及整棵树是否为纯 Number（所有节点 Kind 均为 Number、无 Unknown——特化编译的前提）
    /// </summary>
    public static (MathKind? Kind, bool PureNumber) Infer(LogicalExpression ast, ExpressionContext context) {
        var pass = new KindInferencePass(context);
        var kind = ast.Accept(pass);
        return (kind, pass._pureNumber && kind == MathKind.Number);
    }

    /// <summary>节点结果跟踪：任何非 Number 节点（含 Unknown）都会使整棵树退出纯 Number 判定</summary>
    private MathKind? Track(MathKind? kind) {
        if (kind != MathKind.Number) _pureNumber = false;
        return kind;
    }

    public MathKind? Visit(ValueExpression expr) => Track(MathValue.TryKindOf(expr.Value));

    public MathKind? Visit(Identifier expr) {
        // StrictTypes 语义：符号缺失在求值前即报出（与运行时异常类型一致）
        if (_context.TryGetSymbolKind(expr.Name, out var kind)) return Track(kind);
        throw new SymbolNotFoundException(expr.Name);
    }

    public MathKind? Visit(BinaryExpression expr) {
        var left = expr.Left.Accept(this);
        var right = expr.Right.Accept(this);
        return Track(InferBinary(expr.Type, left, right));
    }

    public MathKind? Visit(UnaryExpression expr) {
        var operand = expr.Operand.Accept(this);
        return Track(operand switch {
            null => null,
            MathKind.Number or MathKind.NumberArray => operand,   // 标量/逐元素广播，Kind 不变
            _ => throw new TypeMismatchException($"一元运算符 {expr.Type} 不支持 {KindName(operand)}",
                "number|number[]", KindName(operand)),
        });
    }

    public MathKind? Visit(FunctionCall expr) {
        if (!_context.TryGetFunctionEntry(expr.Name, out var entry))
            throw new FunctionNotFoundException(expr.Name);

        var argKinds = new MathKind?[expr.Arguments.Count];
        for (int i = 0; i < argKinds.Length; i++)
            argKinds[i] = expr.Arguments[i].Accept(this);

        CheckParameterKinds(expr.Name, entry, argKinds);

        // 聚合函数：展平归约，结果由 ResultKind 决定（内置 max/min → Number；未签名 → 无法确定）
        if (entry.Flags.HasFlag(FunctionFlags.Aggregate)) return Track(entry.ResultKind);

        if (entry.ResultKind == null) return Track(null);

        // ElementWise 广播：任一参数为数组时，逐元素调用的结果为 ResultKind 的数组形态
        foreach (var k in argKinds) {
            if (k == MathKind.NumberArray) return Track(entry.ResultKind == MathKind.Number ? MathKind.NumberArray : null);
            if (k == MathKind.TextArray) return Track(entry.ResultKind == MathKind.Text ? MathKind.TextArray : null);
        }
        return Track(entry.ResultKind);
    }

    public MathKind? Visit(ConditionalExpression expr) {
        var condition = expr.Condition.Accept(this);
        if (condition != null && condition != MathKind.Number)
            throw new TypeMismatchException("条件表达式需要数值条件", "number", KindName(condition));

        // 两分支均被检查（含死分支）；顶层允许分支 Kind 不同（各自独立返回），此时结果无法静态确定
        var trueKind = expr.TrueExpression.Accept(this);
        var falseKind = expr.FalseExpression.Accept(this);
        return Track(trueKind != null && trueKind == falseKind ? trueKind : null);
    }

    public MathKind? Visit(ArrayLiteralExpression expr) {
        var kinds = new MathKind?[expr.Elements.Count];
        for (int i = 0; i < kinds.Length; i++)
            kinds[i] = expr.Elements[i].Accept(this);

        if (kinds.Length == 0) return Track(MathKind.NumberArray);   // 空数组 → number[]（与 BuildArrayLiteral 一致）

        var first = kinds[0];
        if (first == null) {
            // 首元素不确定：已知元素间一致性仍可检查
            MathKind? known = null;
            foreach (var k in kinds) {
                if (k == null) continue;
                if (known == null) known = k;
                else if (known != k) throw InconsistentElement(known!, k);
            }
            return Track(null);
        }

        if (first is not (MathKind.Number or MathKind.Text))
            throw new TypeMismatchException("数组字面量元素必须是 number 或 text", "number|text", KindName(first));

        // 首元素已知：后续已知元素必须同 Kind（Unknown 元素留给运行时兜底）；
        // 断言含义为"若求值成功，Kind 必为该数组形态"
        for (int i = 1; i < kinds.Length; i++) {
            var k = kinds[i];
            if (k != null && k != first) throw InconsistentElement(first, k, i);
        }
        return Track(first == MathKind.Number ? MathKind.NumberArray : MathKind.TextArray);
    }

    public MathKind? Visit(ArrayIndexExpression expr) {
        var array = expr.Array.Accept(this);
        var index = expr.Index.Accept(this);

        if (index != null && index != MathKind.Number)
            throw new TypeMismatchException("数组索引需要数值类型", "number", KindName(index));

        return Track(array switch {
            null => null,
            MathKind.NumberArray => MathKind.Number,
            MathKind.TextArray => MathKind.Text,
            MathKind.Number or MathKind.Text when expr.IsSynthetic => array,   // 合成索引标量回退
            _ => throw new TypeMismatchException("索引操作需要数组类型", "array", KindName(array)),
        });
    }

    public MathKind? Visit(InterpolatedString expr) {
        foreach (var segment in expr.Segments) {
            if (segment is not ExpressionSegment exprSeg) continue;
            var kind = exprSeg.Expression.Accept(this);
            if (exprSeg.FormatSpec != null && kind != null && kind != MathKind.Number)
                throw new EvaluateException($"格式说明符 '{exprSeg.FormatSpec}' 只能用于数值类型");
        }
        return Track(MathKind.Text);
    }

    // ---- 二元运算推断（与 TypeHelper.EvaluateBinary 的运行时分发顺序一致）----

    private static MathKind? InferBinary(BinaryExpressionType type, MathKind? left, MathKind? right) {
        // And/Or：IsTruthy 仅接受 Number（两侧含死分支右侧）
        if (type is BinaryExpressionType.And or BinaryExpressionType.Or) {
            CheckLogicalOperand(left);
            CheckLogicalOperand(right);
            return MathKind.Number;
        }

        bool isComparison = type is BinaryExpressionType.Equal or BinaryExpressionType.NotEqual
            or BinaryExpressionType.LessThan or BinaryExpressionType.LessThanOrEqual
            or BinaryExpressionType.GreaterThan or BinaryExpressionType.GreaterThanOrEqual;

        // 双侧已知 → 精确判定
        if (left != null && right != null) {
            if (left == MathKind.Number && right == MathKind.Number) return MathKind.Number;

            if (left == MathKind.Text && right == MathKind.Text)
                return type == BinaryExpressionType.Plus ? MathKind.Text
                    : isComparison ? MathKind.Number
                    : throw TextOpUnsupported(type);

            if (left == MathKind.NumberArray || right == MathKind.NumberArray) {
                var other = left == MathKind.NumberArray ? right : left;
                if (other is MathKind.Text or MathKind.TextArray)
                    throw new TypeMismatchException("数组运算需要数值类型", "number|number[]",
                        $"{KindName(left)}, {KindName(right)}");
                return MathKind.NumberArray;
            }

            if (left == MathKind.TextArray || right == MathKind.TextArray) {
                var other = left == MathKind.TextArray ? right : left;
                if (other is MathKind.Number or MathKind.NumberArray)
                    throw new TypeMismatchException("文本数组运算需要文本类型", "text|text[]",
                        $"{KindName(left)}, {KindName(right)}");
                if (type == BinaryExpressionType.Plus) return MathKind.TextArray;
                if (isComparison) return MathKind.NumberArray;
                throw TextArrayOpUnsupported(type);
            }

            // Number × Text 等标量混合
            throw new TypeMismatchException($"运算符 {type} 不支持 {KindName(left)} 与 {KindName(right)}",
                "number|text|array", $"{KindName(left)}, {KindName(right)}");
        }

        // 单侧 Unknown → 保守：只报"无论 Unknown 为何 Kind 都必然失败"的组合
        var known = left ?? right;
        if (known == MathKind.Text && type != BinaryExpressionType.Plus && !isComparison)
            throw TextOpUnsupported(type);
        if (known == MathKind.TextArray && type != BinaryExpressionType.Plus && !isComparison)
            throw TextArrayOpUnsupported(type);
        return null;
    }

    private static void CheckLogicalOperand(MathKind? kind) {
        if (kind != null && kind != MathKind.Number)
            throw new TypeMismatchException("逻辑运算需要数值类型", "number", KindName(kind));
    }

    /// <summary>参数 Kind 与函数签名比对（数组参数对应标量签名视为广播，合法）</summary>
    private static void CheckParameterKinds(string name, ExpressionContext.FunctionEntry entry, MathKind?[] argKinds) {
        var paramKinds = entry.ParamKinds;
        if (paramKinds == null || paramKinds.Length != argKinds.Length) return;   // 变参/无签名：不检查

        for (int i = 0; i < argKinds.Length; i++) {
            var arg = argKinds[i];
            var param = paramKinds[i];
            bool compatible = arg == null || param == null || arg == param
                || (arg == MathKind.NumberArray && param == MathKind.Number)
                || (arg == MathKind.TextArray && param == MathKind.Text);
            if (!compatible)
                throw new FunctionTypeMismatchException(
                    $"函数 {name} 第 {i + 1} 个参数期望 {KindName(param!)}，实际为 {KindName(arg!)}");
        }
    }

    private static TypeMismatchException TextOpUnsupported(BinaryExpressionType type)
        => new($"运算符 {type} 不支持 text 类型", "text(+|==|!=|<|<=|>|>=)", "text");

    private static TypeMismatchException TextArrayOpUnsupported(BinaryExpressionType type)
        => new($"运算符 {type} 不支持 text[] 类型", "text[](+|==|!=|<|<=|>|>=)", "text[]");

    private static TypeMismatchException InconsistentElement(MathKind? expected, MathKind? actual, int index = 0)
        => new("数组字面量元素类型必须一致", KindArrayName(expected),
            $"{KindName(actual)}（第 {index} 个元素）");

    private static string KindName(MathKind? kind) => kind switch {
        MathKind.Number => "number",
        MathKind.Text => "text",
        MathKind.NumberArray => "number[]",
        MathKind.TextArray => "text[]",
        null => "unknown",
        _ => ((MathKind)kind).ToString(),
    };

    private static string KindArrayName(MathKind? kind) => kind switch {
        MathKind.Number => "number[]",
        MathKind.Text => "text[]",
        _ => KindName(kind),
    };
}
