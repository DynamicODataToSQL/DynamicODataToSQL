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
        private readonly bool _tryToParseDates;

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterClauseBuilder"/> class.
        /// </summary>
        /// <param name="query">query.</param>
        public FilterClauseBuilder(Query query, bool tryToParseDates)
        {
            _query = query;
            _tryToParseDates = tryToParseDates;
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
                        var lb = new FilterClauseBuilder(q, _tryToParseDates);
                        var lq = left.Accept(lb);
                        if (nodeIn.OperatorKind == BinaryOperatorKind.Or)
                        {
                            lq = lq.Or();
                        }
                        var rb = new FilterClauseBuilder(lq, _tryToParseDates);
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
                            var lb = new FilterClauseBuilder(q, _tryToParseDates);
                            return left.Accept(lb);
                        });
                        left = (left as UnaryOperatorNode).Operand;
                    }
                    if (right.Kind == QueryNodeKind.Constant)
                    {
                        var value = GetConstantValue(right);
                        if (left.Kind == QueryNodeKind.SingleValueFunctionCall)
                        {
                            var functionNode = left as SingleValueFunctionCallNode;
                            _query = ApplyFunction(_query, functionNode, op, value);
                        }
                        else
                        {
                            string column = GetColumnName(left);
                            _query = _query.Where(column, op, value);
                        }
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

            var caseSensitive = true;
            var columnName = GetColumnName(nodes[0]);

            // managing case where there is toupper or tolower function call inside first parameter
            if (nodes[0].Kind == QueryNodeKind.Convert)
            {
                var paramNode = (nodes[0] as ConvertNode).Source;
                if (paramNode.Kind == QueryNodeKind.SingleValueFunctionCall)
                {
                    var functionNode = paramNode as SingleValueFunctionCallNode;

                    var functionName = functionNode.Name.ToUpperInvariant();
                    if (functionName == "TOUPPER" || functionName == "TOLOWER")
                    {
                        caseSensitive = false;
                        columnName = GetColumnName(functionNode.Parameters.FirstOrDefault());
                    }
                }
            }

            switch (nodeIn.Name.ToLowerInvariant())
            {
                case "contains":
                    return _query.WhereContains(columnName, (string)GetConstantValue(nodes[1]), caseSensitive);

                case "endswith":
                    return _query.WhereEnds(columnName, (string)GetConstantValue(nodes[1]), caseSensitive);

                case "startswith":
                    return _query.WhereStarts(columnName, (string)GetConstantValue(nodes[1]), caseSensitive);

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

        private  Query ApplyFunction(Query query, SingleValueFunctionCallNode leftNode, string operand, object rightValue)
        {
            var columnName = GetColumnName(leftNode.Parameters.FirstOrDefault());
            switch (leftNode.Name.ToUpperInvariant())
            {
                case "YEAR":
                case "MONTH":
                case "DAY":
                case "HOUR":
                case "MINUTE":
                    query = query.WhereDatePart(leftNode.Name, columnName, operand, rightValue);
                    break;
                case "DATE":
                    query = query.WhereDate(columnName, operand, rightValue is DateTime date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture.DateTimeFormat) : rightValue);
                    break;
                case "TIME":
                    query = query.WhereTime(columnName, operand, rightValue is DateTime time ? time.ToString("HH:mm", CultureInfo.InvariantCulture.DateTimeFormat) : rightValue);
                    break;
                case "TOUPPER":
                case "TOLOWER":
                    query = query.WhereLike(columnName, rightValue, false);
                    break;
                case "INDEXOF":
                    var nodes = leftNode.Parameters.ToArray();
                    var caseSensitive = true;

                    if (nodes[0].Kind == QueryNodeKind.Convert)
                    {
                        var paramNode = (nodes[0] as ConvertNode).Source;
                        if (paramNode.Kind == QueryNodeKind.SingleValueFunctionCall)
                        {
                            var functionNode = paramNode as SingleValueFunctionCallNode;

                            var functionName = functionNode.Name.ToUpperInvariant();
                            if (functionName == "TOUPPER" || functionName == "TOLOWER")
                            {
                                caseSensitive = false;
                                columnName = GetColumnName(functionNode.Parameters.FirstOrDefault());
                            }
                        }
                    }
                    if (rightValue.Equals(-1))
                    {
                        query = query.WhereNotContains(columnName, (string)GetConstantValue(nodes[1]), caseSensitive);
                    }
                    else
                    {
                        query = query.WhereContains(columnName, (string)GetConstantValue(nodes[1]), caseSensitive);
                    }
                    break;
                default:
                    break;
            }

            return query;
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

        private object GetConstantValue(QueryNode node)
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
                    if (_tryToParseDates && ConvertToDateTimeUTC(trimedValue, out var dateTime))
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
