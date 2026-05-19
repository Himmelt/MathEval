using MathEval.Context;
using MathEval.Exceptions;
using Xunit;
using InvalidOpException = MathEval.Exceptions.InvalidOperationException;

namespace MathEval.Tests;

public class ContextTests {
    [Fact]
    public void Set_DirectValue_ReturnsValue() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 42L);
        Assert.Equal(42L, Expression.Eval<long>("x", ctx));
    }

    [Fact]
    public void Set_LazyValue_ReturnsValue() {
        var ctx = new ExpressionContext();
        ctx.Set("y", () => 99L);
        Assert.Equal(99L, Expression.Eval<long>("y", ctx));
    }

    [Fact]
    public void Set_SameNameTwice_OverridesValue() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 1L);
        ctx.Set("x", 2L);
        Assert.Equal(2L, Expression.Eval<long>("x", ctx));
    }

    [Fact]
    public void SetFunction_WeakTyped_Works() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("add1", (ExpressionFunction)(args => (long)args[0] + 1));
        Assert.Equal(43L, Expression.Eval<long>("add1(42)", ctx));
    }

    [Fact]
    public void SetFunction_StrongTyped_Works() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("doubleIt", (Func<double, double>)(x => x * 2));
        Assert.Equal(10.0, Expression.Eval<double>("doubleIt(5)", ctx));
    }

    [Fact]
    public void SetFunction_Delegate_Works() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("mul", (Delegate)(Func<double, double, double>)((a, b) => a * b));
        Assert.Equal(12.0, Expression.Eval<double>("mul(3, 4)", ctx));
    }

    [Fact]
    public void Child_SeesParentSymbols() {
        var parent = new ExpressionContext();
        parent.Set("x", 42L);
        var child = parent.CreateChild();
        Assert.Equal(42L, Expression.Eval<long>("x", child));
    }

    [Fact]
    public void Child_Additions_InvisibleToParent() {
        var parent = new ExpressionContext();
        var child = parent.CreateChild();
        child.Set("y", 99L);
        Assert.Throws<SymbolNotFoundException>(() => Expression.Eval<long>("y", parent));
    }

    [Fact]
    public void Child_RemoveParentItem_IsSilent() {
        var parent = new ExpressionContext();
        parent.Set("x", 42L);
        var child = parent.CreateChild();
        child.Remove("x");
        Assert.Equal(42L, Expression.Eval<long>("x", child));
    }

    [Fact]
    public void Parent_Modifications_AffectChild() {
        var parent = new ExpressionContext();
        var child = parent.CreateChild();
        parent.Set("x", 42L);
        Assert.Equal(42L, Expression.Eval<long>("x", child));
    }

    [Fact]
    public void Child_Override_DoesNotAffectParent() {
        var parent = new ExpressionContext();
        parent.Set("x", 1L);
        var child = parent.CreateChild();
        child.Set("x", 2L);
        Assert.Equal(2L, Expression.Eval<long>("x", child));
        Assert.Equal(1L, Expression.Eval<long>("x", parent));
    }

    [Fact]
    public void MultiLevelInheritance_GrandchildSeesAncestorSymbols() {
        var gp = new ExpressionContext();
        gp.Set("a", 1L);
        var parent = gp.CreateChild();
        parent.Set("b", 2L);
        var child = parent.CreateChild();
        child.Set("c", 3L);
        Assert.Equal(1L, Expression.Eval<long>("a", child));
        Assert.Equal(2L, Expression.Eval<long>("b", child));
        Assert.Equal(3L, Expression.Eval<long>("c", child));
    }

    [Fact]
    public void Set_ReservedKeyword_ThrowsInvalidOperationException() {
        var ctx = new ExpressionContext();
        Assert.Throws<InvalidOpException>(() => ctx.Set("true", 1L));
    }

    [Fact]
    public void Set_AfterSetFunction_ThrowsInvalidOperationException() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("myFunc", (ExpressionFunction)(args => args[0]));
        Assert.Throws<InvalidOpException>(() => ctx.Set("myFunc", 42L));
    }

    [Fact]
    public void SetFunction_AfterSet_Succeeds() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 1L);
        ctx.SetFunction("x", (ExpressionFunction)(args => 42L));
        Assert.Equal(42L, Expression.Eval<long>("x(0)", ctx));
    }

    [Fact]
    public void Remove_Symbol_ThenAccess_ThrowsSymbolNotFoundException() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 42L);
        ctx.Remove("x");
        Assert.Throws<SymbolNotFoundException>(() => Expression.Eval<long>("x", ctx));
    }

    [Fact]
    public void Remove_Function_ThenAccess_ThrowsFunctionNotFoundException() {
        var ctx = new ExpressionContext();
        ctx.Remove("abs");
        Assert.Throws<FunctionNotFoundException>(() => Expression.Eval<long>("abs(-1)", ctx));
    }
}
