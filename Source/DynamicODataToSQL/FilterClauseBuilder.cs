namespace DynamicODataToSQL
{
    using System;
    using System.Globalization;
    using System.Linq;
    using Microsoft.OData.UriParser;
    using SqlKata;

    /// <summary>
    /// FilterClauseBuilder
    /// </summary>
    public class FilterClauseBuilder : QueryNodeVisitor<Query>
    {
        private const DateTimeStyles DATETIMESTYLES = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces;
        private Query _query;

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterClauseBuilder"/> class.
        /// </summary>
        /// <param name="query">query.</param>
        public FilterClauseBuilder(Query query)
        {
            _query = query;
        }        

        /// <inheritdoc/>
        public override Query Visit(BinaryOperatorNode nodeIn)
        {
            var left = nodeIn.Left;
            if (left.Kind == QueryNodeKind.Convert)
            {
                left = (left as ConvertNode).Source;
            }
            var right = nodeIn.Right;
            if (right.Kind == QueryNodeKind.Convert)
            {
                right = (right as ConvertNode).Source;
            }

            switch (nodeIn.OperatorKind)
            {
                case BinaryOperatorKind.Or:
                case BinaryOperatorKind.And:
                    _query = _query.Where(q =>
                    {
                        var lb = new FilterClauseBuilder(q);
                        var lq = left.Accept(lb);
                        if (nodeIn.OperatorKind == BinaryOperatorKind.Or)
                        {
                            lq = lq.Or();
                        }
                        var rb = new FilterClauseBuilder(lq);
                        return right.Accept(rb);
                    });
                    break;

                case BinaryOperatorKind.Equal:
                case BinaryOperatorKind.NotEqual:
                case BinaryOperatorKind.GreaterThan:
                case BinaryOperatorKind.GreaterThanOrEqual:
                case BinaryOperatorKind.LessThan:
                case BinaryOperatorKind.LessThanOrEqual:
                    string op = GetOperatorString(nodeIn.OperatorKind);
                    if (left.Kind == QueryNodeKind.UnaryOperator)
                    {
                        _query = _query.Where(q =>
                        {
                            var lb = new FilterClauseBuilder(q);
                            return left.Accept(lb);
                        });
                        left = (left as UnaryOperatorNode).Operand;
                    }
                    if (right.Kind == QueryNodeKind.Constant)
                    {
                        var value = GetConstantValue(right);
                        string column = GetColumnName(left);
                        _query = _query.Where(column, op, value);
                    }
                    break;

                default:
                    return _query;
            }

            return _query;
        }

        /// <inheritdoc/>
        public override Query Visit(SingleValueFunctionCallNode nodeIn)
        {
            if (nodeIn is null)
            {
                throw new ArgumentNullException(nameof(nodeIn));
            }

            var nodes = nodeIn.Parameters.ToArray();

            switch (nodeIn.Name.ToLowerInvariant())
            {
                case "contains":
                    return _query.WhereContains(GetColumnName(nodes[0]), (string)GetConstantValue(nodes[1]), true);

                case "endswith":
                    return _query.WhereEnds(GetColumnName(nodes[0]), (string)GetConstantValue(nodes[1]), true);

                case "startswith":
                    return _query.WhereStarts(GetColumnName(nodes[0]), (string)GetConstantValue(nodes[1]), true);

                default:
                    return _query;
            }
        }

        /// <inheritdoc/>
        public override Query Visit(UnaryOperatorNode nodeIn)
        {
            switch (nodeIn.OperatorKind)
            {
                case UnaryOperatorKind.Not:
                    _query = _query.Not();
                    if (nodeIn.Operand.Kind == QueryNodeKind.SingleValueFunctionCall || nodeIn.Operand.Kind == QueryNodeKind.BinaryOperator)
                    {
                        return nodeIn.Operand.Accept(this);
                    }

                    return _query;

                default:
                    return _query;
            }
        }

        private static bool ConvertToDateTimeUTC(string dateTimeString, out DateTime? dateTime)
        {
            if (DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture.DateTimeFormat, DATETIMESTYLES, out var dateTimeValue))
            {
                dateTime = dateTimeValue;
                return true;
            }
            else
            {
                dateTime = null;
                return false;
            }
        }

        private static string GetColumnName(QueryNode node)
        {
            var column = string.Empty;
            if (node.Kind == QueryNodeKind.Convert)
            {
                node = (node as ConvertNode).Source;
            }

            if (node.Kind == QueryNodeKind.SingleValuePropertyAccess)
            {
                column = (node as SingleValuePropertyAccessNode).Property.Name.Trim();
            }

            if (node.Kind == QueryNodeKind.SingleValueOpenPropertyAccess)
            {
                column = (node as SingleValueOpenPropertyAccessNode).Name.Trim();
            }

            return column;
        }

        private static object GetConstantValue(QueryNode node)
        {
            if (node.Kind == QueryNodeKind.Convert)
            {
                return GetConstantValue((node as ConvertNode).Source);
            }
            else if (node.Kind == QueryNodeKind.Constant)
            {
                var value = (node as ConstantNode).Value;
                if (value is string)
                {
                    var trimedValue = value.ToString().Trim();
                    if (ConvertToDateTimeUTC(trimedValue, out var dateTime))
                    {
                        return dateTime.Value;
                    }

                    return trimedValue;
                }

                return value;
            }
            else if (node.Kind == QueryNodeKind.CollectionConstant)
            {
                return (node as CollectionConstantNode).Collection.Select(cn => GetConstantValue(cn));
            }

            return null;
        }

        private static string GetOperatorString(BinaryOperatorKind operatorKind)
        {
            switch (operatorKind)
            {
                case BinaryOperatorKind.Equal:
                    return "=";

                case BinaryOperatorKind.NotEqual:
                    return "<>";

                case BinaryOperatorKind.GreaterThan:
                    return ">";

                case BinaryOperatorKind.GreaterThanOrEqual:
                    return ">=";

                case BinaryOperatorKind.LessThan:
                    return "<";

                case BinaryOperatorKind.LessThanOrEqual:
                    return "<=";

                case BinaryOperatorKind.Or:
                    return "or";

                case BinaryOperatorKind.And:
                    return "and";

                case BinaryOperatorKind.Add:
                    return "+";

                case BinaryOperatorKind.Subtract:
                    return "-";

                case BinaryOperatorKind.Multiply:
                    return "*";

                case BinaryOperatorKind.Divide:
                    return "/";

                case BinaryOperatorKind.Modulo:
                    return "%";

                default:
                    return string.Empty;
            }
        }
    }
}
