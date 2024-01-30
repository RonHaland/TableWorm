using System.Linq.Expressions;

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
                    return $"{GetStringFromExpression(binaryExpr.Left)} or {GetStringFromExpression(binaryExpr.Right)}";
                }
            case ExpressionType.MemberAccess:
                var member = (MemberExpression)expr;
                var name = member.Member.Name;
                if (name == "Id") name = "RowKey";
                return name;
            case ExpressionType.Constant:
                var constant = (ConstantExpression)expr;
                var value = constant.Value;
                return $"'{value}'";
            default:
                throw new InvalidOperationException($"Unsupported node type '{expr.NodeType}' in expression tree");
        }
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
