namespace DynamicODataToSQL;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Microsoft.OData.UriParser;

using SqlKata;

/// <summary>
/// FilterClauseBuilder
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FilterClauseBuilder"/> class.
/// </remarks>
/// <param name="query">query.</param>
public class FilterClauseBuilder(Query query, bool tryToParseDates) : QueryNodeVisitor<Query>
{
    private const DateTimeStyles DATETIMESTYLES = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces;
    private Query _query = query;
    private readonly bool _tryToParseDates = tryToParseDates;

    /// <inheritdoc/>
    public override Query Visit(InNode nodeIn)
    {
        if (nodeIn.Right.Kind != QueryNodeKind.CollectionConstant)
        {
            throw new NotSupportedException("Non constant collection nodes are not supported by 'in' logical operator");
        }

        var leftColumnName = GetColumnName(nodeIn.Left);
        var rightValues = GetCollectionConstantValues(nodeIn.Right as CollectionConstantNode);

        return _query.WhereIn(leftColumnName, rightValues);
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
                var op = GetOperatorString(nodeIn.OperatorKind);
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
                        var column = GetColumnName(left);
                        _query = _query.Where(column, op, value);
                    }
                }
                break;
            case BinaryOperatorKind.Add:
                break;
            case BinaryOperatorKind.Subtract:
                break;
            case BinaryOperatorKind.Multiply:
                break;
            case BinaryOperatorKind.Divide:
                break;
            case BinaryOperatorKind.Modulo:
                break;
            case BinaryOperatorKind.Has:
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
        (caseSensitive, columnName) = GetInnerFunctionCallParameterColumn(nodes, caseSensitive, columnName);

        switch (nodeIn.Name.ToLowerInvariant())
        {
            case "contains":
                return _query.WhereContains(columnName, (string)GetConstantValue(nodes[1]), caseSensitive);

            case "endswith":
                return _query.WhereEnds(columnName, (string)GetConstantValue(nodes[1]), caseSensitive);

            case "startswith":
                return _query.WhereStarts(columnName, (string)GetConstantValue(nodes[1]), caseSensitive);

            case "matchespattern":
                var value = ((string)GetConstantValue(nodes[1]))
                    .Replace(".*", "%");

                if (value.StartsWith("%5E", StringComparison.InvariantCulture))
                {
                    value = value.Replace("%5E", "");
                }

                if (value[value.Length - 1] == '$')
                {
                    value = value.Substring(0, value.Length - 1);
                }

                return _query.WhereLike(columnName, value, caseSensitive);

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
                if (nodeIn.Operand.Kind is QueryNodeKind.SingleValueFunctionCall
                    or QueryNodeKind.BinaryOperator
                    or QueryNodeKind.In)
                {
                    return nodeIn.Operand.Accept(this);
                }

                return _query;
            case UnaryOperatorKind.Negate:
            default:
                return _query;
        }
    }

    private Query ApplyFunction(Query query, SingleValueFunctionCallNode leftNode, string operand, object rightValue)
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

                (caseSensitive, columnName) = GetInnerFunctionCallParameterColumn(nodes, caseSensitive, columnName);

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

    private static (bool CaseSensitive, string ColumnName) GetInnerFunctionCallParameterColumn(QueryNode[] nodes, bool caseSensitive, string columnName)
    {
        if (nodes[0].Kind == QueryNodeKind.Convert)
        {
            var paramNode = (nodes[0] as ConvertNode).Source;
            if (paramNode.Kind == QueryNodeKind.SingleValueFunctionCall)
            {
                return GetFunctionCallParameterInfo(caseSensitive, columnName, paramNode as SingleValueFunctionCallNode);
            }
        }
        else if (nodes[0].Kind == QueryNodeKind.SingleValueFunctionCall)
        {
            return GetFunctionCallParameterInfo(caseSensitive, columnName, nodes[0] as SingleValueFunctionCallNode);
        }

        return (caseSensitive, columnName);
    }

    private static (bool CaseSensitive, string ColumnName) GetFunctionCallParameterInfo(bool caseSensitive, string columnName, SingleValueFunctionCallNode paramNode)
    {
        var functionNode = paramNode;

        var functionName = functionNode.Name.ToUpperInvariant();
        if (functionName is "TOUPPER" or "TOLOWER")
        {
            caseSensitive = false;
            columnName = GetColumnName(functionNode.Parameters.FirstOrDefault());
        }

        return (caseSensitive, columnName);
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

        return column.Replace(ODataToSqlConverter.SPACESIGNREPLACEMENT, " ");
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
            return GetCollectionConstantValues(node as CollectionConstantNode);
        }

        return null;
    }

    private IEnumerable<object> GetCollectionConstantValues(CollectionConstantNode node) => node.Collection.Select(GetConstantValue);

    private static string GetOperatorString(BinaryOperatorKind operatorKind) => operatorKind switch
    {
        BinaryOperatorKind.Equal => "=",
        BinaryOperatorKind.NotEqual => "<>",
        BinaryOperatorKind.GreaterThan => ">",
        BinaryOperatorKind.GreaterThanOrEqual => ">=",
        BinaryOperatorKind.LessThan => "<",
        BinaryOperatorKind.LessThanOrEqual => "<=",
        BinaryOperatorKind.Or => "or",
        BinaryOperatorKind.And => "and",
        BinaryOperatorKind.Add => "+",
        BinaryOperatorKind.Subtract => "-",
        BinaryOperatorKind.Multiply => "*",
        BinaryOperatorKind.Divide => "/",
        BinaryOperatorKind.Modulo => "%",
        BinaryOperatorKind.Has => throw new NotImplementedException(),
        _ => string.Empty,
    };
}
