namespace DynamicODataToSQL.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SqlKata.Compilers;
    using Xunit;
    using Xunit.Abstractions;

    public class ODataToSqlConverterTests
    {
        private readonly ITestOutputHelper output;

        public ODataToSqlConverterTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        [Theory]
        [MemberData(nameof(GetTestData))]
        public void ConvertToSQL_ExpectedBehavior(
            string testName,
            string tableName,
            IDictionary<string,string> odataQuery,
            bool count,
            string expectedSQL,
            IDictionary<string,object> expectedSQLParams)
        {
            // Arrange
            var oDataToSqlConverter = CreateODataToSqlConverter();

            // Act
            output.WriteLine($"Test: {testName}");
            output.WriteLine($"ODataQuery : {string.Join("&",odataQuery.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            output.WriteLine("Expected SQL: \n{0} \nParams: {1}", expectedSQL, string.Join(",", expectedSQLParams.ToArray().Select(kvp => $"{kvp.Key}={kvp.Value}")));
            var result = oDataToSqlConverter.ConvertToSQL(
                tableName,
                odataQuery,
                count);

            // Assert
            var actualSQL = result.Item1;
            var actualSQLParams = result.Item2;            
            output.WriteLine("Actual SQL: \n{0} \nParams: {1}", actualSQL, string.Join(",", actualSQLParams.ToArray().Select(kvp => $"{kvp.Key}={kvp.Value}")));

            Assert.Equal(expectedSQL, actualSQL, ignoreCase: true, ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
            foreach(var kvp in actualSQLParams)
            {
                Assert.Equal(expectedSQLParams[kvp.Key], kvp.Value);
            }
        }

        public static IEnumerable<object[]> GetTestData()
        {           

            // Test 1 
            {
                var testName = "Select+Filter+Sort+Pagination";
                var tableName = "Products";
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"select", "Name, Type" },
                    {"filter", "contains(Name,'Tea')" },
                    {"orderby", "Id desc" },
                    {"top", "20" },
                    {"skip", "5" },
                };
                var expectedSQL = @"SELECT [Name], [Type] FROM [Products] WHERE [Name] like @p0 ORDER BY [Id] DESC OFFSET @p1 ROWS FETCH NEXT @p2 ROWS ONLY";
                var expectedSQLParams = new Dictionary<string, object>
                {                    
                    {"@p0", "%Tea%"},
                    {"@p1", 5},
                    {"@p2", 20},
                };
                yield return new object[] { testName, tableName, odataQueryParams,false, expectedSQL, expectedSQLParams };
            }

            // Test 2 
            {
                var testName = "Select+Sort+Pagination";
                var tableName = "Products";
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"select", "Name, Type" },
                    {"orderby", "Id desc" },
                    {"top", "20" },
                    {"skip", "5"},
                };
                var expectedSQL = @"SELECT [Name], [Type] FROM [Products] ORDER BY [Id] DESC OFFSET @p0 ROWS FETCH NEXT @p1 ROWS ONLY";
                var expectedSQLParams = new Dictionary<string, object>
                {
                    {"@p0", 5 },                   
                    {"@p1", 20},                    
                };
                yield return new object[] { testName, tableName, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

            // Test 3
            {
                var testName = "SelectAll+Filter+Sort+Pagination";
                var tableName = "Products";
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"filter", "contains(Name,'Tea')" },
                    {"orderby", "Id desc" },
                    {"top", "20" },
                    {"skip", "0" },
                };
                var expectedSQL = @"SELECT * FROM [Products] WHERE [Name] like @p0 ORDER BY [Id] DESC OFFSET @p1 ROWS FETCH NEXT @p2 ROWS ONLY";
                var expectedSQLParams = new Dictionary<string, object>
                {                    
                    {"@p0", "%Tea%"},
                    {"@p1", 0 },
                    {"@p2", 20 },
                };
                yield return new object[] { testName, tableName, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

            // Test 4
            {
                var testName = "Select+Filter";
                var tableName = "Products";
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"select", "Name, Type" },
                    {"filter", "contains(Name,'Tea')" },
                };
                var expectedSQL = @"SELECT [Name], [Type] FROM [Products] WHERE [Name] like @p0";
                var expectedSQLParams = new Dictionary<string, object>
                {                    
                    {"@p0", "%Tea%"}
                };
                yield return new object[] { testName, tableName, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }


            // Test 5
            {
                var testName = "AdvancedFilters";
                var tableName = "Products";
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"filter", "contains(Name,'Tea') or (TotalInventory ge 100 and TotalInventory le 1000) or (TimeCreated gt '2020-06-01T00:00-04:00' and TimeCreated lt '2020-07-01T00:00-04:00') or not (Origin eq 'Canada' or Origin eq 'USA')" },
                    {"orderby", "Id desc" }
                };
                var expectedSQL = @"SELECT * FROM [Products] WHERE ((([Name] like @p0 OR ([TotalInventory] >= @p1 AND [TotalInventory] <= @p2)) OR ([TimeCreated] > @p3 AND [TimeCreated] < @p4)) OR NOT ([Origin] = @p5 OR [Origin] = @p6)) ORDER BY [Id] DESC";
                var expectedSQLParams = new Dictionary<string, object>
                {                   
                    {"@p0", "%Tea%"},
                    {"@p1", 100},
                    {"@p2", 1000},
                    {"@p3", new DateTime(2020,6,1,4,0,0,DateTimeKind.Utc)},
                    {"@p4", new DateTime(2020,7,1,4,0,0,DateTimeKind.Utc)},
                    {"@p5", "Canada"},
                    {"@p6", "USA"},
                };
                yield return new object[] { testName, tableName, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

        }

        private static ODataToSqlConverter CreateODataToSqlConverter() => new ODataToSqlConverter(new EdmModelBuilder(), new SqlServerCompiler() { UseLegacyPagination = false });
    }
}
