using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Options;
using MathEval.TypeSystem;
using Xunit;

namespace MathEval.Tests.TypeSystem;

/// <summary>
/// 全场景覆盖：参数为字符串 / 返回值为字符串的自定义函数。
/// <br/>
/// 覆盖维度：
/// <list type="bullet">
///   <item>参数来源：单/双引号字面量、直接值变量、延迟值变量、嵌套函数返回值、字符串数组元素、插值字符串、条件表达式</item>
///   <item>参数组合：单字符串、多字符串、字符串+数值混合</item>
///   <item>返回值流向：直接返回、拼接、比较、嵌套传参、插值、条件分支、数组字面量元素</item>
///   <item>注册方式：强类型委托、Delegate 重载、原生 ExpressionFunction、显式 Kind 签名</item>
///   <item>求值模式：解释、编译、Strict、Strict+编译+常量折叠组合</item>
///   <item>错误场景：Strict 静态类型拦截、参数个数错误</item>
/// </list>
/// </summary>
public class StringFunctionTests {
    private static ExpressionOptions Strict => ExpressionOptions.StrictTypes;

    /// <summary>公共上下文：upper(string)→string、greet(string,string)→string、repeat(string,number)→string</summary>
    private static ExpressionContext NewCtx() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("upper", (string s) => s.ToUpperInvariant());
        ctx.SetFunction("greet", (string name, string suffix) => "Hi " + name + suffix);
        ctx.SetFunction("repeat", (string s, double n) => string.Concat(System.Linq.Enumerable.Repeat(s, (int)n)));
        return ctx;
    }

    #region 参数来源

    [Fact]
    public void Literal_SingleQuote() {
        Assert.Equal("HI", Expression.Eval<string>("upper('hi')", NewCtx()));
    }

    [Fact]
    public void Literal_DoubleQuote() {
        Assert.Equal("HI", Expression.Eval<string>("upper(\"hi\")", NewCtx()));
    }

    [Fact]
    public void Variable_DirectValue() {
        var ctx = NewCtx();
        ctx.Set("name", "tom");
        Assert.Equal("TOM", Expression.Eval<string>("upper(name)", ctx));
    }

    [Fact]
    public void Variable_LazyValue() {
        var ctx = NewCtx();
        ctx.Set("name", (Func<object>)(() => "tom"));
        Assert.Equal("TOM", Expression.Eval<string>("upper(name)", ctx));
    }

    [Fact]
    public void Variable_DirectValue_Strict() {
        var ctx = NewCtx();
        ctx.Set("name", "tom");
        Assert.Equal("TOM", Expression.Eval<string>("upper(name)", ctx, Strict));
    }

    [Fact]
    public void NestedFunction_ReturnAsArg() {
        Assert.Equal("HI A!", Expression.Eval<string>("upper(greet('a', '!'))", NewCtx()));
    }

    [Fact]
    public void NestedChain_ThreeLevels() {
        // repeat 返回 string → greet 参数；greet 返回 string → upper 参数
        Assert.Equal("HI XX!", Expression.Eval<string>("upper(greet(repeat('x', 2), '!'))", NewCtx()));
    }

    [Fact]
    public void StringArrayElement_AsArg() {
        var ctx = NewCtx();
        ctx.Set("arr", new[] { "aa", "bb" });
        Assert.Equal("BB", Expression.Eval<string>("upper(arr[1])", ctx));
    }

    [Fact]
    public void InterpolatedString_AsArg() {
        var ctx = NewCtx();
        ctx.Set("name", "bob");
        Assert.Equal("HI BOB", Expression.Eval<string>("upper($'hi {name}')", ctx));
    }

    [Fact]
    public void Conditional_AsArg() {
        var ctx = NewCtx();
        ctx.Set("x", 1.0);
        Assert.Equal("A", Expression.Eval<string>("upper(x > 0 ? 'a' : 'b')", ctx));
        ctx.Set("x", -1.0);
        Assert.Equal("B", Expression.Eval<string>("upper(x > 0 ? 'a' : 'b')", ctx));
    }

    #endregion

    #region 参数组合

    [Fact]
    public void MultiStringParams() {
        Assert.Equal("Hi Bob!", Expression.Eval<string>("greet('Bob', '!')", NewCtx()));
    }

    [Fact]
    public void MixedStringAndNumberParams() {
        Assert.Equal("ababab", Expression.Eval<string>("repeat('ab', 3)", NewCtx()));
    }

    #endregion

    #region 返回值流向

    [Fact]
    public void Return_UsedDirectly() {
        Assert.Equal("HI", Expression.Eval<string>("upper('hi')", NewCtx()));
    }

    [Fact]
    public void Return_InConcat() {
        Assert.Equal("HI!", Expression.Eval<string>("upper('hi') + '!'", NewCtx()));
    }

    [Fact]
    public void Return_InComparison() {
        Assert.Equal(1.0, Expression.Eval<double>("upper('a') == 'A'", NewCtx()));
        Assert.Equal(0.0, Expression.Eval<double>("upper('a') == 'a'", NewCtx()));
    }

    [Fact]
    public void Return_AsNestedArg() {
        Assert.Equal("HI A!", Expression.Eval<string>("upper(greet('a', '!'))", NewCtx()));
    }

    [Fact]
    public void Return_InInterpolation() {
        Assert.Equal("Hi a!", Expression.Eval<string>("$'{greet('a', '!')}'", NewCtx()));
    }

    [Fact]
    public void Return_InConditionalBranch() {
        var ctx = NewCtx();
        ctx.Set("x", 1.0);
        Assert.Equal("Hi a!", Expression.Eval<string>("x > 0 ? greet('a', '!') : 'b'", ctx));
        ctx.Set("x", -1.0);
        Assert.Equal("b", Expression.Eval<string>("x > 0 ? greet('a', '!') : 'b'", ctx));
    }

    [Fact]
    public void Return_AsArrayElement() {
        var result = Expression.Eval<string[]>("[greet('a', '!'), 'x']", NewCtx());
        Assert.Equal(new[] { "Hi a!", "x" }, result);
    }

    #endregion

    #region 注册方式

    [Fact]
    public void Register_StrongTypedDelegate() {
        // 强类型委托重载（含泛型捕获 Kind 签名）：默认用例即此方式
        Assert.Equal("HI", Expression.Eval<string>("upper('hi')", NewCtx()));
    }

    [Fact]
    public void Register_DelegateOverload() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("upperD", (Delegate)(Func<string, string>)(s => s.ToUpperInvariant()));
        Assert.Equal("HI", Expression.Eval<string>("upperD('hi')", ctx));
        // Delegate 重载同样捕获 Kind 签名：Strict 下 number 参数被静态拒绝
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("upperD(1)", ctx, Strict));
    }

    [Fact]
    public void Register_NativeExpressionFunction() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("upperN", args => ((string)args[0]!).ToUpperInvariant());
        Assert.Equal("HI", Expression.Eval<string>("upperN('hi')", ctx));
    }

    [Fact]
    public void Register_ExplicitKindSignature() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("len", args => (double)((string)args[0]!).Length,
            paramKinds: [MathKind.Text], resultKind: MathKind.Number);
        Assert.Equal(3.0, Expression.Eval<double>("len('abc')", ctx));
        // 显式 Text 签名：Strict 下 number 参数被静态拒绝
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("len(123)", ctx, Strict));
    }

    #endregion

    #region 求值模式

    [Fact]
    public void Compiled_StringParamFromVariable() {
        var ctx = NewCtx();
        ctx.Set("name", "tom");
        Assert.Equal("TOM", Expression.OptimizedEval<string>("upper(name)", ctx));
    }

    [Fact]
    public void Compiled_NestedStringFunction() {
        Assert.Equal("HI A!", Expression.OptimizedEval<string>("upper(greet('a', '!'))", NewCtx()));
    }

    [Fact]
    public void Compiled_MixedParams() {
        Assert.Equal("ababab", Expression.OptimizedEval<string>("repeat('ab', 3)", NewCtx()));
    }

    [Fact]
    public void Compiled_StringReturnInInterpolation() {
        Assert.Equal("Hi a!", Expression.OptimizedEval<string>("$'{greet('a', '!')}'", NewCtx()));
    }

    [Fact]
    public void Combined_StrictCompileFold_StringChain() {
        var opts = ExpressionOptions.StrictTypes | ExpressionOptions.CompileOptimization | ExpressionOptions.ConstantFolding;
        var ctx = NewCtx();
        ctx.Set("name", "tom");
        Assert.Equal("HI TOM!", Expression.Eval<string>("upper(greet(name, '!'))", ctx, opts));
    }

    #endregion

    #region 错误场景

    [Fact]
    public void Strict_NumberToTextParam_Throws() {
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("upper(1)", NewCtx(), Strict));
    }

    [Fact]
    public void Strict_TextToNumberParam_Throws() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("twice", (double x) => x * 2);
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("twice('a')", ctx, Strict));
    }

    [Fact]
    public void Strict_NestedMismatch_Throws() {
        // greet 第 1 参为 text，嵌套调用传 number 同样被静态拦截
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("upper(greet(1, '!'))", NewCtx(), Strict));
    }

    [Fact]
    public void WrongArity_Throws() {
        // 参数个数错误：解释模式与编译模式均抛 FunctionArityException
        Assert.Throws<FunctionArityException>(() => Expression.Eval("greet('a')", NewCtx()));
        Assert.Throws<FunctionArityException>(() => Expression.OptimizedEval("greet('a', '!', '?')", NewCtx()));
    }

    #endregion
}
