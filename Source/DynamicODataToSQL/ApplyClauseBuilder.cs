namespace DynamicODataToSQL
{
    using System;
    using System.Linq;
    using Microsoft.OData.UriParser;
    using Microsoft.OData.UriParser.Aggregation;
    using SqlKata;

    /// <summary>
    /// ApplyClauseBuilder
    /// </summary>
    public class ApplyClauseBuilder
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="queryIn"></param>
        /// <param name="applyClause"></param>
        /// <returns></returns>
        public Query BuildApplyClause(Query queryIn, ApplyClause applyClause)
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
                        queryIn = Visit(queryIn, node as FilterTransformationNode);
                        break;
                    case TransformationNodeKind.Compute:                        
                    case TransformationNodeKind.Expand:                        
                    default:
                        throw new NotSupportedException($"TransformationNode not {node.Kind:g} supported");                        
                }
                i++;
            }
            return queryIn;
        }

        private static Query Visit(Query queryIn, AggregateTransformationNode nodeIn)
        {
            foreach(var expr in nodeIn.AggregateExpressions.OfType<AggregateExpression>())
            {
                if(expr.AggregateKind == AggregateExpressionKind.PropertyAggregate)
                {
                    switch (expr.Method)
                    {
                        case AggregationMethod.Sum:                            
                        case AggregationMethod.Min:                            
                        case AggregationMethod.Max:
                            queryIn = queryIn.SelectRaw($"{expr.Method:g}({GetColumnName(expr.Expression)}) AS {expr.Alias}");
                            break;
                        case AggregationMethod.Average:
                            queryIn = queryIn.SelectRaw($"AVG({GetColumnName(expr.Expression)}) AS {expr.Alias}");
                            break;
                        case AggregationMethod.CountDistinct:
                            queryIn = queryIn.SelectRaw($"COUNT(DISTINCT {GetColumnName(expr.Expression)}) AS {expr.Alias}");
                            break;
                        case AggregationMethod.VirtualPropertyCount:                            
                            queryIn = queryIn.SelectRaw($"COUNT(1) AS {expr.Alias}");
                            break;
                        case AggregationMethod.Custom:                            
                        default:
                            throw new NotSupportedException($"Aggregate method {expr.Method:g} not supported");
                    }
                }
            }

            return queryIn;
        }

        private static Query Visit(Query queryIn, GroupByTransformationNode nodeIn)
        {
            foreach(var groupByProperty in nodeIn.GroupingProperties)
            {
                var columnName = GetColumnName(groupByProperty.Expression);
                queryIn = queryIn.Select(columnName).GroupBy(columnName);
            }

            if(nodeIn.ChildTransformations?.Kind == TransformationNodeKind.Aggregate)
            {
                queryIn = Visit(queryIn, nodeIn.ChildTransformations as AggregateTransformationNode);
            }

            return queryIn;
        }

        private static Query Visit(Query queryIn, FilterTransformationNode nodeIn)
        {
            var filterClause = nodeIn.FilterClause.Expression;            
            var filterClauseBuilder = new FilterClauseBuilder(queryIn);
            return filterClause.Accept(filterClauseBuilder);
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
    }
}
