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
        private readonly IEdmModelBuilder _edmModelBuilder;
        private readonly Compiler _sqlCompiler;       

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataToSqlConverter"/> class.
        /// </summary>
        /// <param name="edmModelBuilder">edmModelBuilder.</param>
        /// <param name="sqlCompiler">sqlCompiler.</param>
        public ODataToSqlConverter(IEdmModelBuilder edmModelBuilder, Compiler sqlCompiler)
        {
            _edmModelBuilder = edmModelBuilder ?? throw new ArgumentNullException(nameof(edmModelBuilder));
            _sqlCompiler = sqlCompiler ?? throw new ArgumentNullException(nameof(sqlCompiler));
        }       

        /// <inheritdoc/>
        public (string, IDictionary<string, object>) ConvertToSQL(
            string tableName,
            IDictionary<string, string> odataQuery,
            bool count = false)
        {
            var query = BuildSqlKataQuery(tableName, odataQuery, count);
            return CompileSqlKataQuery(query);
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

            var result = _edmModelBuilder.BuildTableModel(tableName);
            var model = result.Item1;
            var entityType = result.Item2;
            var entitySet = result.Item3;

            var parser = new ODataQueryOptionParser(model, entityType, entitySet, odataQuery);
            parser.Resolver.EnableCaseInsensitive = true;
            parser.Resolver.EnableNoDollarQueryOptions = true;

            var query = new Query(tableName);

            var filterClause = parser.ParseFilter();
            if (filterClause != null)
            {
                query = BuildFilterClause(query, filterClause);
            }

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
                if (orderbyClause != null)
                {
                    query = BuildOrderByClause(query, orderbyClause);
                }

                var selectClause = parser.ParseSelectAndExpand();
                if (selectClause != null)
                {
                    query = BuildSelectClause(query, selectClause);
                }
            }

            return query;
        }

        private (string, IDictionary<string, object>) CompileSqlKataQuery(Query query)
        {
            var sqlResult = _sqlCompiler.Compile(query);
            return (sqlResult.Sql, sqlResult.NamedBindings);
        }

        private static Query BuildFilterClause(Query query, FilterClause filterClause)
        {
            var node = filterClause.Expression;
            var filterClauseBuilder = new FilterClauseBuilder(query);
            return node.Accept(filterClauseBuilder);
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
    }
}
