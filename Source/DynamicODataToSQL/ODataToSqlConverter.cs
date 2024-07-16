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
        public const string SPACE_SIGN_REPLACEMENT = "_x0020_";

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
            bool count = false,
            bool tryToParseDates = true)
        {
            var query = BuildSqlKataQuery(tableName, odataQuery, count, tryToParseDates);
            return CompileSqlKataQuery(query);
        }
        public Query ConvertToSQLKataQuery(
            string tableName,
            IDictionary<string, string> odataQuery,
            bool count = false,
            bool tryToParseDates = true) => BuildSqlKataQuery(tableName, odataQuery, count, tryToParseDates);



        public (string, IDictionary<string, object>) ConvertToSqlFromRawSql(
            string rawSql,
            IDictionary<string, string> odataQuery,
            bool count = false,
            bool tryToParseDates = true)
        {
            var query = BuildSqlKataQueryFromRawSql(rawSql, odataQuery, count, tryToParseDates);
            return CompileSqlKataQuery(query);
        }

        public Query ConvertToSQLKataQueryFromRawSql(
            string rawSql,
            IDictionary<string, string> odataQuery,
            bool count = false,
            bool tryToParseDates = true) => BuildSqlKataQueryFromRawSql(rawSql, odataQuery, count, tryToParseDates);


        private Query BuildSqlKataQueryFromRawSql(
            string rawSql,
            IDictionary<string, string> odataQuery,
            bool count,
            bool tryToParseDates)
        {
            if (string.IsNullOrWhiteSpace(rawSql))
            {
                throw new ArgumentNullException(nameof(rawSql));
            }

            var tableName = "RawSql";
            var query = new Query(tableName);
            query = this.BuildSqlKataQueryFromOdataParameters(query, tableName, odataQuery, count, tryToParseDates);

            query.WithRaw(tableName, rawSql);

            return query;
        }

        private Query BuildSqlKataQuery(
            string tableName,
            IDictionary<string, string> odataQuery,
            bool count,
            bool tryToParseDates)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentNullException(nameof(tableName));
            }

            var query = new Query(tableName);

            return this.BuildSqlKataQueryFromOdataParameters(query, tableName, odataQuery, count, tryToParseDates);
        }

        private Query BuildSqlKataQueryFromOdataParameters(Query query, string modelName, IDictionary<string, string> odataQuery, bool count, bool tryToParseDates)
        {
            var parser = GetParser(modelName, odataQuery);

            var applyClause = parser.ParseApply();
            var filterClause = parser.ParseFilter();
            var top = parser.ParseTop();
            var skip = parser.ParseSkip();
            var orderbyClause = parser.ParseOrderBy();
            var selectClause = parser.ParseSelectAndExpand();

            if (applyClause != null)
            {
                query = new ApplyClauseBuilder(this._sqlCompiler).BuildApplyClause(query, applyClause, tryToParseDates);
                if(filterClause != null || selectClause != null)
                {
                    query = new Query().From(query, "apply");
                }
            }

            if (filterClause != null)
            {
                query = filterClause.Expression.Accept(new FilterClauseBuilder(query, tryToParseDates));
            }

            if (count)
            {
                query = query.AsCount();
            }
            else
            {
                if (top.HasValue)
                {
                    query = query.Take(Convert.ToInt32(top.Value));
                }

                if (skip.HasValue)
                {
                    query = query.Skip(Convert.ToInt32(skip.Value));
                }

                if (orderbyClause != null)
                {
                    query = BuildOrderByClause(query, orderbyClause);
                }

                if (selectClause != null)
                {
                    query = BuildSelectClause(query, selectClause, modelName);
                }
            }

            return query;
        }
        private ODataQueryOptionParser GetParser(string name, IDictionary<string, string> odataQuery)
        {
            var expands = (odataQuery.Keys.Contains("expand") && !string.IsNullOrWhiteSpace(odataQuery["expand"]))
                ? odataQuery["expand"].Split(',')
                : null;
			var result = _edmModelBuilder.BuildTableModel(name, expands);
            var model = result.Item1;
            var entityType = result.Item2;
            var entitySet = result.Item3;
			var parser = new ODataQueryOptionParser(model, entityType, entitySet, odataQuery);
            parser.Resolver.EnableCaseInsensitive = true;
            parser.Resolver.EnableNoDollarQueryOptions = true;
            return parser;
        }

        private (string, IDictionary<string, object>) CompileSqlKataQuery(Query query)
        {
            var sqlResult = _sqlCompiler.Compile(query);
            return (sqlResult.Sql, sqlResult.NamedBindings);
        }

        private static Query BuildOrderByClause(Query query, OrderByClause orderbyClause)
        {
            while (orderbyClause != null)
            {
                var direction = orderbyClause.Direction;
                if (orderbyClause.Expression is SingleValueOpenPropertyAccessNode expression)
                {
                    if (direction == OrderByDirection.Ascending)
                    {
                        query = query.OrderBy(expression.Name.Trim().Replace(ODataToSqlConverter.SPACE_SIGN_REPLACEMENT, " "));
                    }
                    else
                    {
                        query = query.OrderByDesc(expression.Name.Trim().Replace(ODataToSqlConverter.SPACE_SIGN_REPLACEMENT, " "));
                    }
                }

                orderbyClause = orderbyClause.ThenBy;
            }

            return query;
        }

        private static Query BuildSelectClause(Query query, SelectExpandClause selectClause, string modelName)
        {
            if (!selectClause.AllSelected)
            {
                foreach (var selectItem in selectClause.SelectedItems)
                {
                    if (selectItem is PathSelectItem path)
                    {
                        query = query.Select(path.SelectedPath.FirstSegment.Identifier.Trim().Replace(SPACE_SIGN_REPLACEMENT, " "));
                    }
                }
            }
            else
            {
                query = query.Select(modelName + ".*");
            }

            foreach(var expandItem in selectClause.SelectedItems.OfType<ExpandedNavigationSelectItem>())
            {
				if (expandItem.SelectAndExpand.AllSelected)
				{
					query = query.Select(expandItem.PathToNavigationProperty[0].Identifier + ".*");
                    var source = ((expandItem.PathToNavigationProperty[0] as NavigationPropertySegment).NavigationProperty.DeclaringType as Microsoft.OData.Edm.EdmEntityType).Name;
                    var target = expandItem.PathToNavigationProperty[0].Identifier;

                    // TODO: if the model for the navigation properties is done right, we don't need to second guess here
                    // meanwhile this expects that 
					query = query.Join(target, target + "." + "id", source + "." + target + "_id");
				}			
			}

            return query;
        }
    }
}
