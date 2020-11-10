namespace DynamicODataSampleService.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Dapper;
    using DynamicODataSampleService.Models;
    using DynamicODataToSQL;
    using Flurl;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Data.SqlClient;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    [ApiController]
    [Route("[controller]")]
    public class TablesController : ControllerBase
    {

        private readonly ILogger<TablesController> _logger;
        private readonly IODataToSqlConverter _oDataToSqlConverter;
        private readonly string _connectionString;

        public TablesController(ILogger<TablesController> logger,
            IODataToSqlConverter oDataToSqlConverter,
            IConfiguration configuration)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _oDataToSqlConverter = oDataToSqlConverter ?? throw new ArgumentNullException(nameof(oDataToSqlConverter));
            _connectionString = configuration.GetConnectionString("Sql");
        }

        [HttpGet("{tableName}", Name = "QueryRecords")]
        public async Task<IActionResult> QueryAsync(string tableName,
            [FromQuery] string select,
            [FromQuery] string filter,
            [FromQuery] string orderby,
            [FromQuery] int top = 10,
            [FromQuery] int skip = 0)
        {
            var query = _oDataToSqlConverter.ConvertToSQL(tableName,
                    new Dictionary<string, string>
                    {
                        { "select", select },
                        { "filter", filter },
                        { "orderby", orderby },
                        { "top", (top + 1).ToString() },
                        { "skip", skip.ToString() }
                    }
                );
            IEnumerable<dynamic> rows = null;
            using(var conn = new SqlConnection(_connectionString))
            {
                rows = await conn.QueryAsync(query.Item1, query.Item2).ConfigureAwait(false);                
            }

            ODataQueryResult result = null;
            if (rows != null)
            {
                var isLastPage = rows.Count() <= top;
                result = new ODataQueryResult
                {
                    Count = isLastPage ? rows.Count() : rows.Count() - 1,
                    Value = rows.Take(top),
                    NextLink = isLastPage ? null : BuildNextLink(tableName, select, filter, orderby, top, skip)
                };                    
            }

            return new JsonResult(result);
        }

        private string BuildNextLink(string tableName,
            string select,
            string filter,
            string orderby,
            int top,
            int skip
            )
        {
            var nextLink = Url.Link("QueryRecords", new { tableName });
            nextLink = nextLink
                .SetQueryParam("select", select)
                .SetQueryParam("filter", filter)
                .SetQueryParam("orderBy", orderby)                
                .SetQueryParam("top", top)
                .SetQueryParam("skip", skip + top);

            return nextLink;
        }
    }
}
