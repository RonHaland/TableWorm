using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using TableWorm.Attributes;
using TableWorm.Models;

[assembly: InternalsVisibleTo("TableWorm.Tests")]
namespace TableWorm;

internal static class LambdaToOdataFilterTranslator
{
    public static QueryNode GetStringFromExpression(Expression expr)
    {
        var (needsSubQueries, expressionSide) = NeedsSubQueries(expr);
        if (needsSubQueries)
        {
            ThrowIfNotSameProperty(expr, expressionSide);
            var binaryExpression = (BinaryExpression)expr;
            var otherExpr = (expressionSide != Helper.ExpressionSide.Right ? binaryExpression.Right :  binaryExpression.Left);
            var memberExpr = (MemberExpression)(expressionSide != Helper.ExpressionSide.Right ? binaryExpression.Left :  binaryExpression.Right);
            
            var operand = OperandMap[expr.NodeType];
            var tableType = memberExpr.Expression!.Type;
            var subQueryId = Guid.NewGuid();

            var queryString = $"${ subQueryId.ToString() }";
            var left = GetStringFromExpression(memberExpr);
            var right = GetStringFromExpression(otherExpr);
            return new QueryNode(queryString)
            {
                SubQueries =
                [
                    new QueryNode($"{left.QueryString} {operand} {right.QueryString}")
                    {
                        TableModelType = tableType,
                        SubQueryId = subQueryId
                    }
                ]
            };
        }
        switch (expr.NodeType)
        {
            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
                {
                    var binaryExpr = (BinaryExpression)expr;
                    var operand = OperandMap[expr.NodeType];
                    var left = GetStringFromExpression(binaryExpr.Left);
                    var right = GetStringFromExpression(binaryExpr.Right);
                    return new QueryNode($"{left.QueryString} {operand} {right.QueryString}")
                    {
                        SubQueries = [..left.SubQueries, ..right.SubQueries]
                    };
                }
            case ExpressionType.AndAlso:
                {
                    var binaryExpr = (BinaryExpression)expr;var left = GetStringFromExpression(binaryExpr.Left);
                    var right = GetStringFromExpression(binaryExpr.Right);
                    return new QueryNode($"({left.QueryString}) and ({right.QueryString})")
                    {
                        SubQueries = [..left.SubQueries, ..right.SubQueries]
                    };
                }
            case ExpressionType.OrElse:
                {
                    var binaryExpr = (BinaryExpression)expr;
                    var left = GetStringFromExpression(binaryExpr.Left);
                    var right = GetStringFromExpression(binaryExpr.Right);
                    return new QueryNode($"({left.QueryString}) or ({right.QueryString})")
                    {
                        SubQueries = [..left.SubQueries, ..right.SubQueries]
                    };
                }
            case ExpressionType.MemberAccess:
                var member = (MemberExpression)expr;
                var name = UnwrapColumnName(member);
                return new QueryNode(name);
            case ExpressionType.Constant:
                var constant = (ConstantExpression)expr;
                var value = constant.Value;
                return new QueryNode($"'{value}'");
            default:
                throw new InvalidOperationException($"Unsupported node type '{expr.NodeType}' in expression tree");
        }
    }

    private static void ThrowIfNotSameProperty(Expression expr, Helper.ExpressionSide? expressionSide)
    {
        if (expressionSide != Helper.ExpressionSide.Both) return;
        var binaryExpr = (BinaryExpression)expr;
        var left = (MemberExpression)binaryExpr.Left;
        var right = (MemberExpression)binaryExpr.Right;
        
        var leftProperty = (MemberExpression)left.Expression!;
        var rightProperty = (MemberExpression)right.Expression!;
        var fromSameProperty = leftProperty.Member.Name == rightProperty.Member.Name;
        if (!fromSameProperty)
        {
            throw new InvalidOperationException("Cannot compare across child properties");
        }
    }

    //if either side of a binary expression is accessing members (properties) that are not directly from the
    //parameter of the lambda function, i.e. members from child classes, we will need make and execute subqueries to
    //get the Ids of the children
    private static (bool, Helper.ExpressionSide?) NeedsSubQueries(Expression expr)
    {
        return expr switch
        {
            BinaryExpression
                {
                    Left: MemberExpression { Expression: not null and not ParameterExpression }, 
                    Right: MemberExpression { Expression: not null and not ParameterExpression }
                } => (true, Helper.ExpressionSide.Both),
            BinaryExpression { Left: MemberExpression { Expression: not null and not ParameterExpression } } => (true,
                Helper.ExpressionSide.Left),
            BinaryExpression { Right: MemberExpression { Expression: not null and not ParameterExpression } } => (true,
                Helper.ExpressionSide.Right),
            _ => (false, null)
        };
    }

    private static string UnwrapColumnName(MemberExpression member)
    {
        var expressions = StackExpressions(member, []);
        var myProperty = expressions.Pop();
        var foreignKey = myProperty.Member.GetCustomAttribute<TableForeignKeyAttribute>();
        var isForeignKey = foreignKey != null;

        if (myProperty.Expression == null && (myProperty.Type.IsAssignableTo(typeof(DateTime)) || myProperty.Type.IsAssignableTo(typeof(DateTimeOffset))))
        {
            var prop = (PropertyInfo)member.Member;
            var myObj = Activator.CreateInstance(myProperty.Type, []);
            var val = prop.GetValue(myObj);

            return $"'{(DateTimeOffset)val!:O}'";
        }

        if (myProperty.Expression is ConstantExpression constant)
        {
            var value = constant.Value?.GetType().GetField(member.Member.Name)?.GetValue(constant.Value);

            return $"'{value}'";
        }

        var name = foreignKey?.Name ?? member.Member.Name;
        if (name == "Id") name = "RowKey";
        if (name == "ModifiedDate") name = "Timestamp";

        return name;
    }

    private static Stack<MemberExpression> StackExpressions(MemberExpression expr, Stack<MemberExpression> stack)
    {
        stack.Push(expr);
        if (expr.Expression != null && expr.Expression is MemberExpression expression)
        {
            return StackExpressions(expression, stack);
        }
        return stack;
    }

    private static readonly Dictionary<ExpressionType, string> OperandMap = new() {
        { ExpressionType.Equal, "eq" },
        { ExpressionType.LessThan, "lt" },
        { ExpressionType.LessThanOrEqual, "le" },
        { ExpressionType.GreaterThan, "gt" },
        { ExpressionType.GreaterThanOrEqual, "ge" },
        { ExpressionType.NotEqual, "ne" }
    };
}
