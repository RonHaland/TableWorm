using System.Linq.Expressions;
using TableWorm.Attributes;

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
    
    [Fact]
    public void GetStringFromExpression_ConstantFromObject_ReturnsCorrectOdata()
    {
        var sub = new TestModel { Number = 10, Name = "John" };
        Expression<Func<TestModel, bool>> expr = m => m.Number == sub.Number;
        var result = LambdaToOdataFilterTranslator.GetStringFromExpression((BinaryExpression)expr.Body);
        Assert.Equal("Number eq '10'", result.QueryString);
    }
    
    [Fact]
    public void GetStringFromExpression_ModifiedDateLtDateTimeNow_ReturnsCorrectOdata()
    {
        Expression<Func<TestModel, bool>> expr = m => m.ModifiedDate < DateTime.Now;
        var result = LambdaToOdataFilterTranslator.GetStringFromExpression((BinaryExpression)expr.Body);
        Assert.Contains("Timestamp lt '", result.QueryString);
    }
    
    [Fact]
    public void GetStringFromExpression_ModifiedDateGtDateTimeOffsetUtcNow_ReturnsCorrectOdata()
    {
        Expression<Func<TestModel, bool>> expr = m => m.ModifiedDate > DateTimeOffset.UtcNow;
        var result = LambdaToOdataFilterTranslator.GetStringFromExpression((BinaryExpression)expr.Body);
        Assert.Contains("Timestamp gt '", result.QueryString);
    }
    
    // [Fact]
    // public void Test()
    // {
    //     Expression<Func<Parent, bool>> expr = m => m.Child.Id == "none" && m.Id == "abc" && m.Child.PartitionKey == m.Child.Id && m.Child2.ModifiedDate < DateTimeOffset.Now;
    //     var result = LambdaToOdataFilterTranslator.GetStringFromExpression((BinaryExpression)expr.Body);
    //     Assert.Equal(2, result.SubQueries.Count);
    //     Assert.NotNull(result.QueryString);
    //     Assert.Equal(3, result.QueryString.Split("and").Length);
    // }
}

public class TestModel : TableModel
{
    public string Name { get; set; }
    public int Number { get; set; }
}

public class Parent : TableModel
{
    public Child Child { get; set; }
    public Child Child2 { get; set; }
}

public class Child : TableModel
{
    [TableParent]
    public Parent Parent { get; set; }
}