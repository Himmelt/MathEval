using MathEval.Exceptions;

namespace MathEval.TypeSystem;

/// <summary>
/// 求值内核的值 Kind（4 种，互斥、无提升规则，见设计文档 ADR-D2）
/// </summary>
public enum MathKind : byte {
    /// <summary>数值（double，bool 以 1.0/0.0 承载，见 ADR-D13）</summary>
    Number = 0,
    /// <summary>文本（string）</summary>
    Text = 1,
    /// <summary>数值一维数组（double[]）</summary>
    NumberArray = 2,
    /// <summary>文本一维数组（string[]）</summary>
    TextArray = 3,
}

/// <summary>
/// 求值内核的统一值表示：24 字节 tagged union struct（Kind 标签 + double 槽 + 引用槽，见 ADR-D1）。
/// 消除 v1 内核 object 装载带来的每次运算装箱开销；Number 主路径零装箱。
/// </summary>
public readonly struct MathValue {
    private readonly MathKind _kind;
    private readonly double _num;      // Number：值
    private readonly object? _ref;     // Text：string；NumberArray：double[]；TextArray：string[]

    private MathValue(MathKind kind, double num, object? refValue) {
        _kind = kind;
        _num = num;
        _ref = refValue;
    }

    /// <summary>值的 Kind</summary>
    public MathKind Kind => _kind;

    // ---- 工厂 ----

    public static MathValue Number(double value) => new(MathKind.Number, value, null);
    public static MathValue Text(string value) => new(MathKind.Text, 0, value);
    public static MathValue Array(double[] value) => new(MathKind.NumberArray, 0, value);
    public static MathValue Array(string[] value) => new(MathKind.TextArray, 0, value);

    // ---- 访问器（Kind 不匹配即抛 TypeMismatch，错误信息带期望与实际类型名）----

    /// <summary>以 Number 读取（double）</summary>
    public double AsNumber => _kind == MathKind.Number ? _num
        : throw new TypeMismatchException("期望 number 类型", "number", KindName);

    /// <summary>以 Text 读取（string）</summary>
    public string AsText => _kind == MathKind.Text ? (string)_ref!
        : throw new TypeMismatchException("期望 text 类型", "text", KindName);

    /// <summary>以 NumberArray 读取（double[]）</summary>
    public double[] AsNumberArray => _kind == MathKind.NumberArray ? (double[])_ref!
        : throw new TypeMismatchException("期望 number[] 类型", "number[]", KindName);

    /// <summary>以 TextArray 读取（string[]）</summary>
    public string[] AsTextArray => _kind == MathKind.TextArray ? (string[])_ref!
        : throw new TypeMismatchException("期望 text[] 类型", "text[]", KindName);

    /// <summary>Kind 的用户可读名称（错误信息用）</summary>
    public string KindName => _kind switch {
        MathKind.Number => "number",
        MathKind.Text => "text",
        MathKind.NumberArray => "number[]",
        MathKind.TextArray => "text[]",
        _ => _kind.ToString(),
    };

    // ---- 边界转换（宽边界，见 ADR-D8 / §5.3）----

    /// <summary>
    /// 边界归一化：将 API 边界的 object 值（全部基础类型）转换为 MathValue。
    /// 数值类型统一归 double；bool 数值化为 1.0/0.0；未知类型抛 TypeMismatch。
    /// </summary>
    public static MathValue FromObject(object? value) => value switch {
        null        => Text(""),                       // 沿用 v0 ToString(null)="" 语义
        double d    => Number(d),
        float f     => Number(f),
        long l      => Number(l),                      // >2^53 精度损失为已知代价（ADR-D4）
        int i       => Number(i),
        short s     => Number(s),
        sbyte sb    => Number(sb),
        byte b      => Number(b),
        ushort us   => Number(us),
        uint ui     => Number(ui),
        ulong ul    => Number(ul),
        bool bl     => Number(bl ? 1.0 : 0.0),         // bool 数值化（ADR-D13）
        decimal dec => Number((double)dec),
        string s    => Text(s),
        char c      => Text(c.ToString()),
        double[] a  => Array(a),
        string[] a  => Array(a),
        int[] a     => Array(System.Array.ConvertAll(a, x => (double)x)),
        long[] a    => Array(System.Array.ConvertAll(a, x => (double)x)),
        _ => throw new TypeMismatchException($"不支持的符号类型：{value.GetType().Name}",
            "number|text|array", value.GetType().Name),
    };

    /// <summary>
    /// 静态探测 object 值经 <see cref="FromObject"/> 归一化后的 Kind，
    /// 不实际转换值（无数组拷贝）。无法确定（未知类型）返回 null。
    /// 供 StrictTypes 静态推断使用（见设计文档 §7）
    /// </summary>
    public static MathKind? TryKindOf(object? value) => value switch {
        double or float or long or int or short or sbyte or byte or ushort or uint or ulong or bool or decimal
            => MathKind.Number,
        string or char => MathKind.Text,
        double[] or int[] or long[] => MathKind.NumberArray,   // int[]/long[] 归一化为 double[]
        string[] => MathKind.TextArray,
        null => MathKind.Text,                                  // FromObject(null) = Text("")
        _ => null,
    };

    /// <summary>
    /// 出口装箱视图：MathValue → object（double 装箱 / string / 数组引用）。
    /// 供 Calculator.Eval() 的 object 返回值与旧式 ExpressionFunction(object[]) 委托边界使用。
    /// </summary>
    public object ToObject() => _kind switch {
        MathKind.Number => _num,          // 装箱 double
        MathKind.Text => _ref!,
        MathKind.NumberArray => _ref!,
        MathKind.TextArray => _ref!,
        _ => throw new System.InvalidOperationException($"未知的 MathKind：{_kind}"),
    };

    public override string ToString() => _kind switch {
        MathKind.Number => _num.ToString(),
        MathKind.Text => (string)_ref!,
        _ => $"{KindName} (length={(_ref as System.Array)?.Length ?? 0})",
    };
}
