using MathEval.Exceptions;
using MathEval.Options;
using Xunit;

namespace MathEval.Tests.Functions;

/// <summary>
/// 内置函数覆盖补全：补齐 BuiltInEntries 中此前零测试的 30 个函数。
/// <br/>
/// 覆盖类别：
/// <list type="bullet">
///   <item>角度制三角函数：sind/cosd/tand/asind/acosd/atand/atan2d</item>
///   <item>双曲函数：sinh/cosh/tanh/asinh/acosh/atanh</item>
///   <item>余割/正割/余切：csc/sec/cot/acsc/asec/acot（弧度）</item>
///   <item>角度制余割系：cscd/secd/cotd/acscd/asecd/acotd</item>
///   <item>数值处理：lg/clamp/lerp</item>
///   <item>聚合函数：count/avg/mean/std（含展平混合参数）</item>
/// </list>
/// 每个类别覆盖：已知数学恒等式正例、数组逐元素广播、Strict 静态类型拦截、解释/编译双模式一致性。
/// </summary>
public class BuiltInFunctionTests {
    private static ExpressionOptions Strict => ExpressionOptions.StrictTypes;

    #region 角度制三角函数

    [Fact]
    public void DegreeTrig_BasicValues() {
        Assert.Equal(0.0, Expression.Eval<double>("sind(0)"), 10);
        Assert.Equal(1.0, Expression.Eval<double>("sind(90)"), 10);
        Assert.Equal(0.5, Expression.Eval<double>("sind(30)"), 10);
        Assert.Equal(1.0, Expression.Eval<double>("cosd(0)"), 10);
        Assert.Equal(0.5, Expression.Eval<double>("cosd(60)"), 10);
        Assert.Equal(-1.0, Expression.Eval<double>("cosd(180)"), 10);
        Assert.Equal(1.0, Expression.Eval<double>("tand(45)"), 10);
        Assert.Equal(0.0, Expression.Eval<double>("tand(0)"), 10);
    }

    [Fact]
    public void DegreeTrig_InverseValues() {
        Assert.Equal(30.0, Expression.Eval<double>("asind(0.5)"), 10);
        Assert.Equal(90.0, Expression.Eval<double>("asind(1)"), 10);
        Assert.Equal(60.0, Expression.Eval<double>("acosd(0.5)"), 10);
        Assert.Equal(90.0, Expression.Eval<double>("acosd(0)"), 10);
        Assert.Equal(45.0, Expression.Eval<double>("atand(1)"), 10);
        Assert.Equal(0.0, Expression.Eval<double>("atand(0)"), 10);
    }

    [Fact]
    public void DegreeTrig_Atan2Values() {
        Assert.Equal(45.0, Expression.Eval<double>("atan2d(1, 1)"), 10);
        Assert.Equal(-45.0, Expression.Eval<double>("atan2d(-1, 1)"), 10);
        Assert.Equal(180.0, Expression.Eval<double>("atan2d(0, -1)"), 10);
        Assert.Equal(90.0, Expression.Eval<double>("atan2d(1, 0)"), 10);
    }

    [Fact]
    public void DegreeTrig_Broadcast() {
        var result = Expression.Eval<double[]>("sind([0, 30, 90])");
        Assert.Equal(0.0, result[0], 10);
        Assert.Equal(0.5, result[1], 10);
        Assert.Equal(1.0, result[2], 10);
    }

    [Fact]
    public void DegreeTrig_StrictTypeError() {
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("sind('a')", options: Strict));
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("atan2d('a', 1)", options: Strict));
    }

    #endregion

    #region 双曲函数

    [Fact]
    public void Hyperbolic_ZeroValues() {
        Assert.Equal(0.0, Expression.Eval<double>("sinh(0)"), 10);
        Assert.Equal(1.0, Expression.Eval<double>("cosh(0)"), 10);
        Assert.Equal(0.0, Expression.Eval<double>("tanh(0)"), 10);
        Assert.Equal(0.0, Expression.Eval<double>("asinh(0)"), 10);
        Assert.Equal(0.0, Expression.Eval<double>("acosh(1)"), 10);
        Assert.Equal(0.0, Expression.Eval<double>("atanh(0)"), 10);
    }

    [Fact]
    public void Hyperbolic_KnownValues() {
        Assert.Equal(1.1752011936438014, Expression.Eval<double>("sinh(1)"), 10);
        Assert.Equal(1.5430806348152437, Expression.Eval<double>("cosh(1)"), 10);
        Assert.Equal(0.7615941559557649, Expression.Eval<double>("tanh(1)"), 10);
        Assert.Equal(0.881373587019543, Expression.Eval<double>("asinh(1)"), 10);
        Assert.Equal(1.3169578969248166, Expression.Eval<double>("acosh(2)"), 10);
        Assert.Equal(0.5493061443340549, Expression.Eval<double>("atanh(0.5)"), 10);
    }

    [Fact]
    public void Hyperbolic_IdentityRelation() {
        // cosh²x - sinh²x = 1
        Assert.Equal(1.0, Expression.Eval<double>("pow(cosh(1), 2) - pow(sinh(1), 2)"), 10);
        // sinh(x)/cosh(x) = tanh(x)
        Assert.Equal(Expression.Eval<double>("tanh(1)"), Expression.Eval<double>("sinh(1) / cosh(1)"), 10);
    }

    [Fact]
    public void Hyperbolic_Broadcast() {
        var result = Expression.Eval<double[]>("sinh([0, 1])");
        Assert.Equal(0.0, result[0], 10);
        Assert.Equal(1.1752011936438014, result[1], 10);
    }

    [Fact]
    public void Hyperbolic_StrictTypeError() {
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("sinh('a')", options: Strict));
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("acosh('a')", options: Strict));
    }

    #endregion

    #region 余割/正割/余切（弧度）

    [Fact]
    public void Reciprocal_RadianValues() {
        Assert.Equal(2.0, Expression.Eval<double>("csc(PI/6)"), 10);   // 1/sin(30°)
        Assert.Equal(1.0, Expression.Eval<double>("csc(PI/2)"), 10);   // 1/sin(90°)
        Assert.Equal(2.0, Expression.Eval<double>("sec(PI/3)"), 10);   // 1/cos(60°)
        Assert.Equal(1.0, Expression.Eval<double>("sec(0)"), 10);
        Assert.Equal(1.0, Expression.Eval<double>("cot(PI/4)"), 10);   // 1/tan(45°)
    }

    [Fact]
    public void Reciprocal_InverseValues() {
        Assert.Equal(Math.PI / 2, Expression.Eval<double>("acsc(1)"), 10);
        Assert.Equal(0.0, Expression.Eval<double>("asec(1)"), 10);
        Assert.Equal(Math.PI / 4, Expression.Eval<double>("acot(1)"), 10);
        // acot(0) = atan(∞) = π/2（IEEE double 除法语义，1.0/0.0 = +∞）
        Assert.Equal(Math.PI / 2, Expression.Eval<double>("acot(0)"), 10);
    }

    [Fact]
    public void Reciprocal_DegreeVersions() {
        Assert.Equal(1.0, Expression.Eval<double>("cscd(90)"), 10);
        Assert.Equal(1.0, Expression.Eval<double>("secd(0)"), 10);
        Assert.Equal(1.0, Expression.Eval<double>("cotd(45)"), 10);
        Assert.Equal(90.0, Expression.Eval<double>("acscd(1)"), 10);
        Assert.Equal(0.0, Expression.Eval<double>("asecd(1)"), 10);
        Assert.Equal(45.0, Expression.Eval<double>("acotd(1)"), 10);
    }

    [Fact]
    public void Reciprocal_Broadcast() {
        var result = Expression.Eval<double[]>("csc([PI/6, PI/2])");
        Assert.Equal(2.0, result[0], 10);
        Assert.Equal(1.0, result[1], 10);
    }

    [Fact]
    public void Reciprocal_StrictTypeError() {
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("csc('a')", options: Strict));
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("acotd('a')", options: Strict));
    }

    #endregion

    #region 数值处理：lg/clamp/lerp

    [Fact]
    public void Lg_BasicValues() {
        Assert.Equal(0.0, Expression.Eval<double>("lg(1)"), 10);
        Assert.Equal(1.0, Expression.Eval<double>("lg(10)"), 10);
        Assert.Equal(2.0, Expression.Eval<double>("lg(100)"), 10);
        Assert.Equal(3.0, Expression.Eval<double>("lg(1000)"), 10);
    }

    [Fact]
    public void Clamp_Boundaries() {
        Assert.Equal(1.0, Expression.Eval<double>("clamp(0, 1, 3)"), 10);  // 低于下界
        Assert.Equal(2.0, Expression.Eval<double>("clamp(2, 1, 3)"), 10);  // 区间内
        Assert.Equal(3.0, Expression.Eval<double>("clamp(5, 1, 3)"), 10);  // 高于上界
        Assert.Equal(1.0, Expression.Eval<double>("clamp(1, 1, 3)"), 10);  // 边界含端点
        Assert.Equal(3.0, Expression.Eval<double>("clamp(3, 1, 3)"), 10);  // 边界含端点
        Assert.Equal(-1.0, Expression.Eval<double>("clamp(-2.5, -1, 1)"), 10);
    }

    [Fact]
    public void Lerp_EndpointsAndMid() {
        Assert.Equal(10.0, Expression.Eval<double>("lerp(10, 20, 0)"), 10);     // t=0 → a
        Assert.Equal(20.0, Expression.Eval<double>("lerp(10, 20, 1)"), 10);     // t=1 → b
        Assert.Equal(15.0, Expression.Eval<double>("lerp(10, 20, 0.5)"), 10);   // 中点
        Assert.Equal(25.0, Expression.Eval<double>("lerp(0, 100, 0.25)"), 10);  // 四分之一
        Assert.Equal(0.0, Expression.Eval<double>("lerp(-10, 10, 0.5)"), 10);
    }

    [Fact]
    public void Numeric_Broadcast() {
        // clamp 数组：标量参数保持、数组参数逐元素广播
        var clampResult = Expression.Eval<double[]>("clamp([0, 5, 10], 1, 3)");
        Assert.Equal(new double[] { 1, 3, 3 }, clampResult);
        // lerp t 为数组
        var lerpResult = Expression.Eval<double[]>("lerp(0, 20, [0, 0.5, 1])");
        Assert.Equal(new double[] { 0, 10, 20 }, lerpResult);
        // lg 数组
        var lgResult = Expression.Eval<double[]>("lg([1, 10, 100])");
        Assert.Equal(new double[] { 0, 1, 2 }, lgResult);
    }

    [Fact]
    public void Numeric_StrictTypeError() {
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("lg('a')", options: Strict));
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("clamp('a', 1, 2)", options: Strict));
        Assert.Throws<FunctionArgumentTypeException>(() => Expression.Eval("lerp(1, 2, 'a')", options: Strict));
    }

    #endregion

    #region 聚合函数：count/avg/mean/std

    [Fact]
    public void Count_ScalarArrayMixed() {
        Assert.Equal(3.0, Expression.Eval<double>("count(1, 2, 3)"), 10);
        Assert.Equal(4.0, Expression.Eval<double>("count([1, 2, 3, 4])"), 10);
        Assert.Equal(1.0, Expression.Eval<double>("count(5)"), 10);
        Assert.Equal(3.0, Expression.Eval<double>("count(1, [2, 3])"), 10);   // 标量+数组展平
    }

    [Fact]
    public void Avg_And_Mean_Alias() {
        Assert.Equal(2.0, Expression.Eval<double>("avg(1, 2, 3)"), 10);
        Assert.Equal(2.0, Expression.Eval<double>("avg([1, 2, 3])"), 10);
        Assert.Equal(2.0, Expression.Eval<double>("avg(2, [2, 2])"), 10);
        // avg 与 mean 互为别名
        Assert.Equal(3.0, Expression.Eval<double>("mean(2, 4)"), 10);
        Assert.Equal(2.5, Expression.Eval<double>("mean([1, 2, 3, 4])"), 10);
        Assert.Equal(2.0, Expression.Eval<double>("mean(1, [2, 3])"), 10);
    }

    [Fact]
    public void Std_PopulationVariance() {
        // 总体标准差：经典数据集 {2,4,4,4,5,5,7,9} → σ=2
        Assert.Equal(2.0, Expression.Eval<double>("std([2, 4, 4, 4, 5, 5, 7, 9])"), 10);
        // 全同值 → 0
        Assert.Equal(0.0, Expression.Eval<double>("std(1, 1, 1)"), 10);
        // 单值 → 0
        Assert.Equal(0.0, Expression.Eval<double>("std(5)"), 10);
        // {1,2,3} → sqrt(2/3)
        Assert.Equal(Math.Sqrt(2.0 / 3.0), Expression.Eval<double>("std([1, 2, 3])"), 10);
        // 多参数展平合并
        Assert.Equal(2.0, Expression.Eval<double>("std([2, 4, 4, 4, 5], [5, 7, 9])"), 10);
    }

    [Fact]
    public void Aggregate_FlattenMixed() {
        Assert.Equal(6.0, Expression.Eval<double>("sum(1, [2, 3])"), 10);
        Assert.Equal(2.0, Expression.Eval<double>("mean(1, [2, 3])"), 10);
        Assert.Equal(9.0, Expression.Eval<double>("max([1, 4, 9], 5)"), 10);
        Assert.Equal(1.0, Expression.Eval<double>("min([1, 4, 9], 5)"), 10);
    }

    [Fact]
    public void Aggregate_TypeError() {
        // 聚合函数 ParamKinds 为 null：非法类型在运行时经 ToDouble 归一化抛 TypeMismatchException
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("avg('a')"));
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("std('a')"));
    }

    #endregion

    #region 解释/编译双模式一致性

    [Theory]
    [InlineData("sind(90)")]
    [InlineData("asind(0.5)")]
    [InlineData("atan2d(1, 1)")]
    [InlineData("sinh(1)")]
    [InlineData("acosh(2)")]
    [InlineData("csc(PI/6)")]
    [InlineData("acot(1)")]
    [InlineData("cscd(90)")]
    [InlineData("acscd(1)")]
    [InlineData("lg(100)")]
    [InlineData("clamp(5, 1, 3)")]
    [InlineData("lerp(0, 10, 0.5)")]
    [InlineData("count([1, 2, 3, 4])")]
    [InlineData("avg(1, 2, 3)")]
    [InlineData("mean([1, 2, 3, 4])")]
    [InlineData("std([2, 4, 4, 4, 5, 5, 7, 9])")]
    public void Compiled_MatchesInterpret(string expression) {
        var interpret = Expression.Eval<double>(expression);
        var compiled = Expression.OptimizedEval<double>(expression);
        Assert.Equal(interpret, compiled, 10);
    }

    [Fact]
    public void Compiled_Broadcast() {
        Assert.Equal(new double[] { 1, 3, 3 }, Expression.OptimizedEval<double[]>("clamp([0, 5, 10], 1, 3)"));
        Assert.Equal(new double[] { 0, 1, 2 }, Expression.OptimizedEval<double[]>("lg([1, 10, 100])"));
    }

    [Fact]
    public void Compiled_Strict_Combined() {
        var opts = ExpressionOptions.StrictTypes | ExpressionOptions.CompileOptimization | ExpressionOptions.ConstantFolding;
        Assert.Equal(2.0, Expression.Eval<double>("std([2, 4, 4, 4, 5, 5, 7, 9])", options: opts), 10);
        Assert.Equal(15.0, Expression.Eval<double>("lerp(10, 20, 0.5)", options: opts), 10);
    }

    #endregion
}
