namespace DynamicODataToSQL;

using System;
using System.Linq;

using Microsoft.OData.UriParser;
using Microsoft.OData.UriParser.Aggregation;

using SqlKata;
using SqlKata.Compilers;

/// <summary>
/// ApplyClauseBuilder
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ApplyClauseBuilder"/> class.
/// </remarks>
/// <param name="compiler">sqlCompiler.</param>
public class ApplyClauseBuilder(Compiler compiler)
{
    private readonly Compiler _compiler = compiler;

    /// <summary>
    ///
    /// </summary>
    /// <param name="queryIn"></param>
    /// <param name="applyClause"></param>
    /// <returns></returns>
    public Query BuildApplyClause(Query queryIn, ApplyClause applyClause, bool tryToParseDates)
    {
        if (queryIn is null)
        {
            throw new ArgumentNullException(nameof(queryIn));
        }

        if (applyClause is null)
        {
            throw new ArgumentNullException(nameof(applyClause));
        }

        var i = 0;
        foreach (var node in applyClause.Transformations)
        {
            // Supported nodes, Aggragate is always the end node
            // 1. Aggregate
            // 2. GroupBy
            // 3. GroupBy / Aggregate
            // 4. Filter / GroupBy
            // 5. Filter / Aggregate
            // 6. Filter / GroupBy / Filter
            // 7. Filter / GroupBy / Aggregate
            // 8. Filter / GroupBy / GroupBy .../ GroupBy / Filter
            if (i > 0 && applyClause.Transformations.ElementAt(i - 1).Kind != TransformationNodeKind.Filter)
            {
                // Use sub queriy if prev transformation is not filter
                queryIn = new Query().From(queryIn);
            }

            switch (node.Kind)
            {
                case TransformationNodeKind.Aggregate:
                    queryIn = Visit(queryIn, node as AggregateTransformationNode);
                    // Aggregate is end node so return here
                    return queryIn;
                case TransformationNodeKind.GroupBy:
                    queryIn = Visit(queryIn, node as GroupByTransformationNode);
                    break;
                case TransformationNodeKind.Filter:
                    queryIn = Visit(queryIn, node as FilterTransformationNode, tryToParseDates);
                    break;
                case TransformationNodeKind.Compute:
                    queryIn = Visit(queryIn, node as ComputeTransformationNode);
                    break;
                case TransformationNodeKind.Expand:
                default:
                    throw new NotSupportedException($"TransformationNode not {node.Kind:g} supported");
            }
            i++;
        }
        return queryIn;
    }

    private Query Visit(Query queryIn, AggregateTransformationNode nodeIn)
    {
        foreach (var expr in nodeIn.AggregateExpressions.OfType<AggregateExpression>())
        {
            if (expr.AggregateKind == AggregateExpressionKind.PropertyAggregate)
            {
                queryIn = expr.Method switch
                {
                    AggregationMethod.Sum or AggregationMethod.Min or AggregationMethod.Max => queryIn.SelectRaw($"{expr.Method:g}({GetColumnName(expr.Expression, true)}) AS {_compiler.WrapValue(expr.Alias)}"),
                    AggregationMethod.Average => queryIn.SelectRaw($"AVG({GetColumnName(expr.Expression, true)}) AS {_compiler.WrapValue(expr.Alias)}"),
                    AggregationMethod.CountDistinct => queryIn.SelectRaw($"COUNT(DISTINCT {GetColumnName(expr.Expression, true)}) AS {_compiler.WrapValue(expr.Alias)}"),
                    AggregationMethod.VirtualPropertyCount => queryIn.SelectRaw($"COUNT(1) AS {_compiler.WrapValue(expr.Alias)}"),
                    AggregationMethod.Custom => throw new NotImplementedException(),
                    _ => throw new NotSupportedException($"Aggregate method {expr.Method:g} not supported"),
                };
            }
        }

        return queryIn;
    }


    private Query Visit(Query queryIn, GroupByTransformationNode nodeIn)
    {
        foreach (var groupByProperty in nodeIn.GroupingProperties)
        {
            var columnName = GetColumnName(groupByProperty.Expression);
            queryIn = queryIn.Select(columnName).GroupBy(columnName);
        }

        if (nodeIn.ChildTransformations?.Kind == TransformationNodeKind.Aggregate)
        {
            queryIn = Visit(queryIn, nodeIn.ChildTransformations as AggregateTransformationNode);
        }

        return queryIn;
    }

    private Query Visit(Query queryIn, ComputeTransformationNode nodeIn)
    {
        queryIn = queryIn.SelectRaw("*");
        foreach (var computeExpression in nodeIn.Expressions)
        {
            if (computeExpression.Expression is SingleValueFunctionCallNode se)
            {
                switch (se.Name.ToUpperInvariant())
                {
                    case "YEAR":
                    case "MONTH":
                    case "DAY":
                    case "HOUR":
                    case "MINUTE":
                        var pr = se.Parameters.Single();
                        var columnName = GetColumnName(pr);
                        queryIn = queryIn.SelectRaw($"{se.Name}({columnName}) as {computeExpression.Alias}");
                        break;
                    default:
                        throw new NotSupportedException($"Aggregate method {se.Name} not supported");
                }
            }
            else
            {
                throw new NotSupportedException($"Compute expression {computeExpression.Expression.GetType().Name} not supported");
            }
        }


        return queryIn;
    }

    private static Query Visit(Query queryIn, FilterTransformationNode nodeIn, bool tryToParseDates)
    {
        var filterClause = nodeIn.FilterClause.Expression;
        var filterClauseBuilder = new FilterClauseBuilder(queryIn, tryToParseDates);
        return filterClause.Accept(filterClauseBuilder);
    }
    /// <summary>
    ///
    /// </summary>
    /// <param name="node"></param>
    /// <param name="wrap">if true returned value will be wrapped in opening and closing column identifier</param>
    /// <returns></returns>
    private string GetColumnName(QueryNode node, bool wrap = false)
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
        if (wrap)
        {
            return _compiler.WrapValue(column).Replace(ODataToSqlConverter.SPACESIGNREPLACEMENT, " ");
        }

        return column.Replace(ODataToSqlConverter.SPACESIGNREPLACEMENT, " ");
    }
}
