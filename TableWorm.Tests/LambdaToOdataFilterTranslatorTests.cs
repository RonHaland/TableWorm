using System.Linq.Expressions;

namespace TableWorm.Tests;

public class LambdaToOdataFilterTranslatorTests
{
    [Fact]
    public void GetStringFromExpression_EqualOperator_ReturnsCorrectOdata()
    {
        Expression<Func<TestModel, bool>> expr = m => m.Id == "testId";
        var result = LambdaToOdataFilterTranslator.GetStringFromExpression((BinaryExpression)expr.Body);
        Assert.Equal("RowKey eq 'testId'", result.QueryString);
    }

    [Fact]
    public void GetStringFromExpression_GreaterThanOperator_ReturnsCorrectOdata()
    {
        Expression<Func<TestModel, bool>> expr = m => m.Number > 10;
        var result = LambdaToOdataFilterTranslator.GetStringFromExpression((BinaryExpression)expr.Body);
        Assert.Equal("Number gt '10'", result.QueryString);
    }
    
    [Fact]
    public void GetStringFromExpression_LessThanOperator_ReturnsCorrectOdata()
    {
        Expression<Func<TestModel, bool>> expr = m => m.Number < 10;
        var result = LambdaToOdataFilterTranslator.GetStringFromExpression((BinaryExpression)expr.Body);
        Assert.Equal("Number lt '10'", result.QueryString);
    }

    [Fact]
    public void GetStringFromExpression_NotEqualOperator_ReturnsCorrectOdata()
    {
        Expression<Func<TestModel, bool>> expr = m => m.Name != "John";
        var result = LambdaToOdataFilterTranslator.GetStringFromExpression((BinaryExpression)expr.Body);
        Assert.Equal("Name ne 'John'", result.QueryString);
    }

    [Fact]
    public void GetStringFromExpression_GreaterThanOrEqualOperator_ReturnsCorrectOdata()
    {
        Expression<Func<TestModel, bool>> expr = m => m.Number >= 10;
        var result = LambdaToOdataFilterTranslator.GetStringFromExpression((BinaryExpression)expr.Body);
        Assert.Equal("Number ge '10'", result.QueryString);
    }
    
    [Fact]
    public void GetStringFromExpression_LessThanOrEqualOperator_ReturnsCorrectOdata()
    {
        Expression<Func<TestModel, bool>> expr = m => m.Number <= 10;
        var result = LambdaToOdataFilterTranslator.GetStringFromExpression((BinaryExpression)expr.Body);
        Assert.Equal("Number le '10'", result.QueryString);
    }
}

public class TestModel : TableModel
{
    public string Name { get; set; }
    public int Number { get; set; }
}