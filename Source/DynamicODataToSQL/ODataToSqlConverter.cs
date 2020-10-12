namespace DynamicODataToSQL
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using DynamicODataToSQL.Interfaces;
    using Microsoft.OData.UriParser;
    using SqlKata;
    using SqlKata.Compilers;

    /// <inheritdoc/>
    public class ODataToSqlConverter : IODataToSqlConverter
    {
        private const DateTimeStyles DATETIMESTYLES = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces;
        private readonly IEdmModelBuilder edmModelBuilder;
        private readonly Compiler sqlCompiler;

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataToSqlConverter"/> class.
        /// </summary>
        /// <param name="edmModelBuilder">edmModelBuilder.</param>
        /// <param name="sqlCompiler">sqlCompiler.</param>
        public ODataToSqlConverter(IEdmModelBuilder edmModelBuilder, Compiler sqlCompiler)
        {
            this.edmModelBuilder = edmModelBuilder ?? throw new ArgumentNullException(nameof(edmModelBuilder));
            this.sqlCompiler = sqlCompiler ?? throw new ArgumentNullException(nameof(sqlCompiler));
        }

        /// <inheritdoc/>
        public (string, IDictionary<string, object>) ConvertToSQL(
            string tableName,
            IDictionary<string, string> odataQuery,
            bool count = false)
        {
            var query = this.BuildSqlKataQuery(tableName, odataQuery);
            return this.CompileSqlKataQuery(query);
        }

        private static Query BuildSelectClause(Query query, SelectExpandClause selectClause)
        {
            if (!selectClause.AllSelected)
            {
                foreach (var selectItem in selectClause.SelectedItems)
                {
                    if (selectItem is PathSelectItem path)
                    {
                        query = query.Select(path.SelectedPath.FirstSegment.Identifier.Trim());
                    }
                }
            }

            return query;
        }

        private static Query BuildOrderByClause(Query query, OrderByClause orderbyClause)
        {
            if (orderbyClause != null)
            {
                var direction = orderbyClause.Direction;
                if (orderbyClause.Expression is SingleValueOpenPropertyAccessNode expression)
                {
                    if (direction == OrderByDirection.Ascending)
                    {
                        query = query.OrderBy(expression.Name.Trim());
                    }
                    else
                    {
                        query = query.OrderByDesc(expression.Name.Trim());
                    }
                }

                orderbyClause = orderbyClause.ThenBy;
                query = BuildOrderByClause(query, orderbyClause);
            }

            return query;
        }

        private static Query BuildFilterClause(Query query, FilterClause filterClause)
        {
            var node = filterClause.Expression;
            switch (node.Kind)
            {
                case QueryNodeKind.BinaryOperator:
                    return BuildFromBinaryOperatorNode(query, node as BinaryOperatorNode);

                case QueryNodeKind.SingleValueFunctionCall:
                    return BuildFromFunctionCallNode(query, node as SingleValueFunctionCallNode);

                case QueryNodeKind.UnaryOperator:
                    return BuildFromUnaryOperatorNode(query, node as UnaryOperatorNode);
            }

            return query;
        }

        private static Query BuildFromUnaryOperatorNode(Query query, UnaryOperatorNode node)
        {
            switch (node.OperatorKind)
            {
                case UnaryOperatorKind.Not:
                    if (node.Operand.Kind == QueryNodeKind.SingleValueFunctionCall)
                    {
                        return BuildFromFunctionCallNode(query.Not(), node.Operand as SingleValueFunctionCallNode);
                    }
                    else if (node.Operand.Kind == QueryNodeKind.BinaryOperator)
                    {
                        return BuildFromBinaryOperatorNode(query.Not(), node.Operand as BinaryOperatorNode);
                    }

                    return query.Not();

                default:
                    return query;
            }
        }

        private static Query BuildFromFunctionCallNode(Query query, SingleValueFunctionCallNode node)
        {
            var nodes = node.Parameters.ToArray();

            switch (node.Name.ToUpperInvariant())
            {
                case "CONTAINS":
                    return query.WhereContains(GetColumnName(nodes[0]), (string)GetConstantValue(nodes[1]), true);

                case "ENDSWITH":
                    return query.WhereEnds(GetColumnName(nodes[0]), (string)GetConstantValue(nodes[1]), true);

                case "STARTSWITH":
                    return query.WhereStarts(GetColumnName(nodes[0]), (string)GetConstantValue(nodes[1]), true);
                default:
                    break;
            }

            return query;
        }

        private static Query BuildFromBinaryOperatorNode(Query query, BinaryOperatorNode node)
        {
            var left = node.Left;
            if (left.Kind == QueryNodeKind.Convert)
            {
                left = (left as ConvertNode).Source;
            }

            var right = node.Right;
            if (right.Kind == QueryNodeKind.Convert)
            {
                right = (right as ConvertNode).Source;
            }

            switch (node.OperatorKind)
            {
                case BinaryOperatorKind.Or:
                case BinaryOperatorKind.And:
                    Query lq = null;
                    switch (left.Kind)
                    {
                        case QueryNodeKind.BinaryOperator:
                            lq = query.Where(q => BuildFromBinaryOperatorNode(q, left as BinaryOperatorNode));
                            break;

                        case QueryNodeKind.SingleValueFunctionCall:
                            lq = query.Where(q => BuildFromFunctionCallNode(q, left as SingleValueFunctionCallNode));
                            break;

                        case QueryNodeKind.UnaryOperator:
                            lq = query.Where(q => BuildFromUnaryOperatorNode(q, left as UnaryOperatorNode));
                            break;
                    }

                    lq = lq != null && node.OperatorKind == BinaryOperatorKind.Or ? lq.Or() : lq;
                    switch (right.Kind)
                    {
                        case QueryNodeKind.BinaryOperator:
                            return lq.Where(q => BuildFromBinaryOperatorNode(q, right as BinaryOperatorNode));

                        case QueryNodeKind.SingleValueFunctionCall:
                            return lq.Where(q => BuildFromFunctionCallNode(q, right as SingleValueFunctionCallNode));

                        case QueryNodeKind.UnaryOperator:
                            return lq.Where(q => BuildFromUnaryOperatorNode(q, right as UnaryOperatorNode));
                    }

                    return query;

                case BinaryOperatorKind.Equal:
                case BinaryOperatorKind.NotEqual:
                case BinaryOperatorKind.GreaterThan:
                case BinaryOperatorKind.GreaterThanOrEqual:
                case BinaryOperatorKind.LessThan:
                case BinaryOperatorKind.LessThanOrEqual:
                    var op = GetOperatorString(node.OperatorKind);
                    if (left.Kind == QueryNodeKind.UnaryOperator)
                    {
                        query = BuildFromUnaryOperatorNode(query, left as UnaryOperatorNode);
                        left = (left as UnaryOperatorNode).Operand;
                    }

                    if (right.Kind == QueryNodeKind.Constant)
                    {
                        var value = GetConstantValue(right);
                        var column = GetColumnName(left);
                        return query.Where(column, op, value);
                    }

                    return query;

                default:
                    return query;
            }
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
                return (node as CollectionConstantNode).Collection.ToList().Select(cn => GetConstantValue(cn));
            }

            return null;
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

        private Query BuildSqlKataQuery(
            string tableName,
            IDictionary<string, string> odataQuery,
            bool count = false)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentNullException(nameof(tableName));
            }

            var result = this.edmModelBuilder.BuildTableModel(tableName);
            var model = result.Item1;
            var entityType = result.Item2;
            var entitySet = result.Item3;

            var parser = new ODataQueryOptionParser(model, entityType, entitySet, odataQuery);
            parser.Resolver.EnableCaseInsensitive = true;
            parser.Resolver.EnableNoDollarQueryOptions = true;

            var query = new Query(tableName);

            var filterClause = parser.ParseFilter();
            query = BuildFilterClause(query, filterClause);

            if (count)
            {
                query = query.AsCount();
            }
            else
            {
                var top = parser.ParseTop();
                if (top.HasValue)
                {
                    query = query.Take(Convert.ToInt32(top.Value));
                }

                var skip = parser.ParseSkip();
                if (skip.HasValue)
                {
                    query = query.Skip(Convert.ToInt32(skip.Value));
                }

                var orderbyClause = parser.ParseOrderBy();
                query = BuildOrderByClause(query, orderbyClause);

                var selectClause = parser.ParseSelectAndExpand();
                query = BuildSelectClause(query, selectClause);
            }

            return query;
        }

        private (string, IDictionary<string, object>) CompileSqlKataQuery(Query query)
        {
            var sqlResult = this.sqlCompiler.Compile(query);
            return (sqlResult.Sql, sqlResult.NamedBindings);
        }
    }
}
