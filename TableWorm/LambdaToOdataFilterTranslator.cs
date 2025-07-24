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
            var (needsSubQueries, expressionSide) = NeedsSubQueries(expr);
            if (needsSubQueries)
            {
                ThrowIfNotSameProperty(expr, expressionSide);
                var binaryExpression = (BinaryExpression)expr;
                var otherExpr = (expressionSide != Helper.ExpressionSide.Right ? binaryExpression.Right : binaryExpression.Left);
                var memberExpr = (MemberExpression)(expressionSide != Helper.ExpressionSide.Right ? binaryExpression.Left : binaryExpression.Right);

                var operand = OperandMap[expr.NodeType];
                var tableType = memberExpr.Expression!.Type;
                
                var subQueryKey = memberExpr.Expression.ToString();
                if (!subqueries.TryGetValue(subQueryKey, out var subQueryNode))
                {
                    subQueryNode = new QueryNode { TableModelType = tableType, SubQueryId = Guid.NewGuid() };
                    subqueries.Add(subQueryKey, subQueryNode);
                }

                var left = GetStringFromExpression(memberExpr);
                var right = GetStringFromExpression(otherExpr);

                var subQueryString = $"{left.QueryString} {operand} {right.QueryString}";
                if (string.IsNullOrEmpty(subQueryNode.QueryString))
                {
                    subQueryNode.QueryString = subQueryString;
                }
                else
                {
                    subQueryNode.QueryString = $"({subQueryNode.QueryString}) and ({subQueryString})";
                }

                return $"${subQueryNode.SubQueryId}";
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
                    var member = (MemberExpression)expr;
                    var name = UnwrapColumnName(member);
                    return name;
                case ExpressionType.Constant:
                    var constant = (ConstantExpression)expr;
                    var value = constant.Value;
                    return $"'{value}'";
                case ExpressionType.Convert when expr is UnaryExpression { Operand: MemberExpression memberExpr }:
                    var type = memberExpr.Type;
                    var method = memberExpr.Member.MemberType;
                    if (method is MemberTypes.Property)
                    {
                        var memberContent = type.GetProperty(memberExpr.Member.Name)!.GetMethod?.Invoke(null, []);
                        if (memberContent is DateTime or DateTimeOffset)
                        {
                            return $"'{memberContent:O}'";
                        }
                    }
                    return "";
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

        private static (bool, Helper.ExpressionSide?) NeedsSubQueries(Expression expr)
        {
            return expr switch
            {
                BinaryExpression
                {
                    Left: MemberExpression { Expression: not null and not ParameterExpression and not ConstantExpression },
                    Right: MemberExpression { Expression: not null and not ParameterExpression and not ConstantExpression }
                } => (true, Helper.ExpressionSide.Both),
                BinaryExpression { Left: MemberExpression { Expression: not null and not ParameterExpression and not ConstantExpression } } => (true,
                    Helper.ExpressionSide.Left),
                BinaryExpression { Right: MemberExpression { Expression: not null and not ParameterExpression and not ConstantExpression } } => (true,
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

                return $"'{((DateTimeOffset)val!):O}'";
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