using MathEval.AST;
using MathEval.Exceptions;
using Xunit;

using BinaryExpressionType = MathEval.Parser.BinaryExpressionType;
using UnaryExpressionType = MathEval.Parser.UnaryExpressionType;

namespace MathEval.Tests;

public class ParserTests {
    private static LogicalExpression Parse(string text) {
        var lexer = new Lexer.Lexer(text);
        var parser = new Parser.Parser(lexer);
        return parser.Parse();
    }

    [Fact]
    public void SimpleExpression_ParsesSuccessfully() {
        var ast = Parse("1 + 2");
        var binary = Assert.IsType<BinaryExpression>(ast);
        Assert.Equal(BinaryExpressionType.Plus, binary.Type);
        Assert.IsType<ValueExpression>(binary.Left);
        Assert.IsType<ValueExpression>(binary.Right);
    }

    [Fact]
    public void OperatorPrecedence_MultiplyBeforeAdd() {
        var ast = Parse("2 + 3 * 4");
        var add = Assert.IsType<BinaryExpression>(ast);
        Assert.Equal(BinaryExpressionType.Plus, add.Type);

        Assert.IsType<ValueExpression>(add.Left);

        var multiply = Assert.IsType<BinaryExpression>(add.Right);
        Assert.Equal(BinaryExpressionType.Multiply, multiply.Type);
    }

    [Fact]
    public void PowerIsRightAssociative() {
        var ast = Parse("2 ^ 3 ^ 2");
        var outer = Assert.IsType<BinaryExpression>(ast);
        Assert.Equal(BinaryExpressionType.Power, outer.Type);

        Assert.IsType<ValueExpression>(outer.Left);

        var inner = Assert.IsType<BinaryExpression>(outer.Right);
        Assert.Equal(BinaryExpressionType.Power, inner.Type);
    }

    [Fact]
    public void FunctionCall_ParsesAsFunctionCall() {
        var ast = Parse("sqrt(4)");
        var call = Assert.IsType<FunctionCall>(ast);
        Assert.Equal("sqrt", call.Name);
        Assert.Single(call.Arguments);
    }

    [Fact]
    public void NestedFunction_ParsesSuccessfully() {
        var ast = Parse("max(min(1,2),3)");
        var outer = Assert.IsType<FunctionCall>(ast);
        Assert.Equal("max", outer.Name);
        Assert.Equal(2, outer.Arguments.Count);

        var inner = Assert.IsType<FunctionCall>(outer.Arguments[0]);
        Assert.Equal("min", inner.Name);
        Assert.Equal(2, inner.Arguments.Count);
    }

    [Fact]
    public void Ternary_ParsesAsConditionalExpression() {
        var ast = Parse("true ? 1 : 2");
        var cond = Assert.IsType<ConditionalExpression>(ast);
        Assert.IsType<ValueExpression>(cond.Condition);
        Assert.IsType<ValueExpression>(cond.TrueExpression);
        Assert.IsType<ValueExpression>(cond.FalseExpression);
    }

    [Fact]
    public void NestedTernary_IsRightAssociative() {
        var ast = Parse("true ? 1 : false ? 2 : 3");
        var outer = Assert.IsType<ConditionalExpression>(ast);
        Assert.IsType<ValueExpression>(outer.Condition);
        Assert.IsType<ValueExpression>(outer.TrueExpression);

        var inner = Assert.IsType<ConditionalExpression>(outer.FalseExpression);
        Assert.IsType<ValueExpression>(inner.Condition);
        Assert.IsType<ValueExpression>(inner.TrueExpression);
        Assert.IsType<ValueExpression>(inner.FalseExpression);
    }

    [Fact]
    public void EmptyExpression_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Parse(""));
    }

    [Fact]
    public void InvalidSyntax_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Parse("+ +"));
    }

    [Fact]
    public void DepthLimit_ThrowsParseException() {
        var parts = new string[1025];
        for (int i = 0; i < 1025; i++) parts[i] = "2";
        var expr = string.Join("^", parts);
        Assert.Throws<ParseException>(() => Parse(expr));
    }

    [Fact]
    public void Identifier_ParsesAsIdentifier() {
        var ast = Parse("x");
        var id = Assert.IsType<Identifier>(ast);
        Assert.Equal("x", id.Name);
    }

    [Fact]
    public void UnaryNegation_ParsesAsUnaryExpression() {
        var ast = Parse("-5");
        var unary = Assert.IsType<UnaryExpression>(ast);
        Assert.Equal(UnaryExpressionType.Negate, unary.Type);
        Assert.IsType<ValueExpression>(unary.Operand);
    }

    [Fact]
    public void ParenthesizedExpression_ParsesInnerExpression() {
        var ast = Parse("(1 + 2)");
        var binary = Assert.IsType<BinaryExpression>(ast);
        Assert.Equal(BinaryExpressionType.Plus, binary.Type);
    }
}