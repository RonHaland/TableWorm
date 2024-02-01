using AzureTableContext.Attributes;
using System.Linq.Expressions;
using System.Reflection;

namespace AzureTableContext;

internal static class LamdaToOdataTranslator
{
    public static string GetStringFromExpression(Expression expr)
    {
        if (expr == null)
        {
            return "";
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
                    var leftParam = binaryExpr.Left;
                    var rightParam = binaryExpr.Right;
                    var operand = _operandMap[expr.NodeType];
                    return $"{GetStringFromExpression(leftParam)} {operand} {GetStringFromExpression(rightParam)}";
                }
            case ExpressionType.AndAlso:
                {
                    var binaryExpr = (BinaryExpression)expr;
                    return $"({GetStringFromExpression(binaryExpr.Left)}) and ({GetStringFromExpression(binaryExpr.Right)})";
                }
            case ExpressionType.OrElse:
                {
                    var binaryExpr = (BinaryExpression)expr;
                    return $"({GetStringFromExpression(binaryExpr.Left)}) or ({GetStringFromExpression(binaryExpr.Right)})";
                }
            case ExpressionType.MemberAccess:
                var member = (MemberExpression)expr;
                var name = UnwrapColumnName(member);
                return name;
            case ExpressionType.Constant:
                var constant = (ConstantExpression)expr;
                var value = constant.Value;
                return $"'{value}'";
            default:
                throw new InvalidOperationException($"Unsupported node type '{expr.NodeType}' in expression tree");
        }
    }

    private static string UnwrapColumnName(MemberExpression member)
    {
        var expressions = StackExpressions(member, []);
        var myProperty = expressions.Pop();
        var foreignKey = myProperty.Member.GetCustomAttribute<TableForeignKeyAttribute>();
        var isForeignKey = foreignKey != null;

        if (myProperty.Expression == null && (myProperty.Type.IsAssignableTo(typeof(DateTime)) || myProperty.Type.IsAssignableTo(typeof(DateTimeOffset))))
        {
            var prop = ((PropertyInfo)member.Member);
            var myObj = Activator.CreateInstance(myProperty.Type, []);
            var val = prop.GetValue(myObj);

            return $"'{(DateTimeOffset)val!:O}'";
        }

        var name = foreignKey?.Name ?? member.Member.Name;
        if (name == "Id") name = "RowKey";
        if (name == "CreatedAt") name = "Timestamp";

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

    private static Dictionary<ExpressionType, string> _operandMap = new() {
        { ExpressionType.Equal, "eq" },
        { ExpressionType.LessThan, "lt" },
        { ExpressionType.LessThanOrEqual, "le" },
        { ExpressionType.GreaterThan, "gt" },
        { ExpressionType.GreaterThanOrEqual, "ge" },
        { ExpressionType.NotEqual, "ne" }
    };
}
