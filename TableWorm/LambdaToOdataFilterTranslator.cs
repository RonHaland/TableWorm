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
        var subqueries = new Dictionary<string, QueryNode>();
        var queryString = ProcessExpression(expr, subqueries);

        var rootNode = new QueryNode(queryString)
        {
            SubQueries = subqueries.Values.ToList()
        };

        return rootNode;
    }

    private static string ProcessExpression(Expression expr, Dictionary<string, QueryNode> subqueries)
    {
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
                var left = ProcessExpression(binaryExpr.Left, subqueries);
                var right = ProcessExpression(binaryExpr.Right, subqueries);
                return $"{left} {operand} {right}";
            }
            case ExpressionType.AndAlso:
            {
                var binaryExpr = (BinaryExpression)expr;
                var left = ProcessExpression(binaryExpr.Left, subqueries);
                var right = ProcessExpression(binaryExpr.Right, subqueries);

                if (right.StartsWith("$") && left.Contains(right)) return left;
                if (left.StartsWith("$") && right.Contains(left)) return right;

                return $"({left}) and ({right})";
            }
            case ExpressionType.OrElse:
            {
                var binaryExpr = (BinaryExpression)expr;
                var left = ProcessExpression(binaryExpr.Left, subqueries);
                var right = ProcessExpression(binaryExpr.Right, subqueries);
                    
                if (right.StartsWith("$") && left.Contains(right)) return left;
                if (left.StartsWith("$") && right.Contains(left)) return right;

                return $"({left}) or ({right})";
            }
            case ExpressionType.MemberAccess:
                var memberExpr = (MemberExpression)expr;
                    
                
                var temp = memberExpr;
                while(temp.Expression is MemberExpression inner)
                {
                    temp = inner;
                }

                if (temp.Expression is ParameterExpression) return UnwrapColumnName(memberExpr);
                var value = GetValueFromExpression(memberExpr);
                return value is (DateTime or DateTimeOffset) ? $"'{value:O}'" : $"'{value}'";

            case ExpressionType.Constant:
                var constant = (ConstantExpression)expr;
                return $"'{constant.Value}'";
            case ExpressionType.Convert:
                var convertValue = GetValueFromExpression(expr);
                return convertValue is (DateTime or DateTimeOffset) ? $"'{convertValue:O}'" : $"'{convertValue}'";
            default:
                throw new InvalidOperationException($"Unsupported node type '{expr.NodeType}' in expression tree");
        }
    }

    private static object? GetValueFromExpression(Expression expression)
    {
        var lambda = Expression.Lambda(expression);
        return lambda.Compile().DynamicInvoke();
    }

    private static string UnwrapColumnName(MemberExpression member)
    {
        var foreignKey = member.Member.GetCustomAttribute<TableForeignKeyAttribute>();
        var name = foreignKey?.Name ?? member.Member.Name;

        return name switch
        {
            "Id" => "RowKey",
            "ModifiedDate" => "Timestamp",
            _ => name
        };
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