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
            bool tryToParseDates,
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
                count,
                tryToParseDates);

            // Assert
            var actualSQL = result.Item1;
            var actualSQLParams = result.Item2;
            output.WriteLine("Actual SQL: \n{0} \nParams: {1}", actualSQL, string.Join(",", actualSQLParams.ToArray().Select(kvp => $"{kvp.Key}={kvp.Value}")));

            Assert.Equal(expectedSQL, actualSQL, ignoreCase: true, ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
            Assert.Equal(expectedSQLParams, actualSQLParams);
        }

        public static IEnumerable<object[]> GetTestData()
        {

            // Test 1
            {
                var testName = "Select+Filter+Sort+Pagination";
                var tableName = "Products";
                var tryToParseDates = true;
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
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams,false, expectedSQL, expectedSQLParams };
            }

            // Test 2
            {
                var testName = "Select+Sort+Pagination";
                var tableName = "Products";
                var tryToParseDates = true;
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
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

            // Test 3
            {
                var testName = "SelectAll+Filter+Sort+Pagination";
                var tableName = "Products";
                var tryToParseDates = true;
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
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

            // Test 4
            {
                var testName = "Select+Filter";
                var tableName = "Products";
                var tryToParseDates = true;
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
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }


            // Test 5
            {
                var testName = "AdvancedFilters";
                var tableName = "Products";
                var tryToParseDates = true;
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
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }


            // Test 6
            {
                var testName = "Aggregate";
                var tableName = "Orders";
                var tryToParseDates = true;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"apply","aggregate(TotalAmount with sum as TotalAmount, TotalAmount with average as AverageAmount,$count as OrderCount)"}
                };
                var expectedSQL = @"SELECT SUM(TotalAmount) AS TotalAmount, AVG(TotalAmount) AS AverageAmount, Count(1) AS OrderCount FROM [Orders]";
                var expectedSQLParams = new Dictionary<string, object> { };
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }


            // Test 7
            {
                var testName = "GroupBy";
                var tableName = "Orders";
                var tryToParseDates = true;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"apply","groupby((Country),aggregate(Amount with sum as Total,Amount with average as AvgAmt))"}
                };
                var expectedSQL = @"SELECT [Country], Sum(Amount) AS Total, Avg(Amount) AS AvgAmt FROM [Orders] GROUP BY [Country]";
                var expectedSQLParams = new Dictionary<string, object> { };
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }


            // Test 8
            {
                var testName = "Filter+GroupBy";
                var tableName = "Orders";
                var tryToParseDates = true;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"apply","filter(Amount ge 100)/groupby((Country),aggregate(Amount with sum as Total,Amount with average as AvgAmt))"}
                };
                var expectedSQL = @"SELECT [Country], Sum(Amount) AS Total, AVG(Amount) AS AvgAmt FROM [Orders] WHERE [Amount] >= @p0 GROUP BY [Country]";
                var expectedSQLParams = new Dictionary<string, object> { {"@p0", 100} };
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

            // Test 9
            {
                var testName = "Filter+GroupBy+Filter";
                var tableName = "Orders";
                var tryToParseDates = true;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"apply","filter(Amount ge 100)/groupby((Country),aggregate(Amount with sum as Total,Amount with average as AvgAmt))/filter(AvgAmt ge 20)"}
                };
                var expectedSQL = @"SELECT * FROM (SELECT [Country], Sum(Amount) AS Total, AVG(Amount) AS AvgAmt FROM [Orders] WHERE [Amount] >= @p0 GROUP BY [Country]) WHERE [AvgAmt] >= @p1";
                var expectedSQLParams = new Dictionary<string, object> { { "@p0", 100 },{"@p1",20 } };
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

            // Test 10
            {
                var testName = "Filter+GroupBy++Filter";
                var tableName = "Orders";
                var tryToParseDates = true;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"apply","filter(Amount ge 100)/groupby((Country),aggregate(Amount with sum as Total,Amount with average as AvgAmt))"},
                    {"filter","AvgAmt ge 20" }
                };
                var expectedSQL = @"SELECT * FROM (SELECT [Country], Sum(Amount) AS Total, AVG(Amount) AS AvgAmt FROM [Orders] WHERE [Amount] >= @p0 GROUP BY [Country]) AS [apply] WHERE [AvgAmt] >= @p1";
                var expectedSQLParams = new Dictionary<string, object> { { "@p0", 100 }, { "@p1", 20 } };
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

            // Test 11
            {
                var testName = "DateTimeFunctions";
                var tableName = "Orders";
                var tryToParseDates = true;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"filter","year(OrderDate) eq 1971" }
                };
                var expectedSQL = @"SELECT * FROM [Orders] WHERE DATEPART(YEAR, [OrderDate]) = @p0";
                var expectedSQLParams = new Dictionary<string, object> { { "@p0", 1971 }};
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

            // Test 12
            {
                var testName = "DateTimeFunctions-Date";
                var tableName = "Orders";
                var tryToParseDates = true;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"filter","date(OrderDate) gt '2001-01-17'" }
                };
                var expectedSQL = @"SELECT * FROM [Orders] WHERE CAST([OrderDate] as date) > @p0";
                var expectedSQLParams = new Dictionary<string, object> { { "@p0", "2001-01-17" } };
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

            // Test 13
            {
                var testName = "DateTimeFunctions-Time";
                var tableName = "Orders";
                var tryToParseDates = true;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"filter","time(OrderDate) gt '16:30'" }
                };
                var expectedSQL = @"SELECT * FROM [Orders] WHERE CAST([OrderDate] as time) > @p0";
                var expectedSQLParams = new Dictionary<string, object> { { "@p0", "16:30" } };
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }
            // Test 14
            {
                var testName = "Filter+ToUpper";
                var tableName = "Products";
                var tryToParseDates = true;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"select", "Name, Type" },
                    {"filter", "toupper(Name) eq 'Tea'" },
                };
                var expectedSQL = @"SELECT [Name], [Type] FROM [Products] WHERE LOWER([Name]) LIKE @p0";
                var expectedSQLParams = new Dictionary<string, object>
                {
                    {"@p0", "tea"}
                };
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }
            // Test 15
            {
                var testName = "Filter+Contains+ToUpper";
                var tableName = "Products";
                var tryToParseDates = true;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"select", "Name, Type" },
                    {"filter", "contains(toupper(Name),'Tea')" },
                };
                var expectedSQL = @"SELECT [Name], [Type] FROM [Products] WHERE LOWER([Name]) like @p0";
                var expectedSQLParams = new Dictionary<string, object>
                {
                    {"@p0", "%tea%"}
                };
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

            // Test 16
            {
                var testName = "Compute+GroupBy";
                var tableName = "Orders";
                var tryToParseDates = true;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"apply","compute(year(OrderDate) as yr, month(OrderDate) as mn)/groupby((yr,mn),aggregate(value with average as AvgValue))" }
                };
                var expectedSQL = @"SELECT [yr], [mn], AVG(value) AS AvgValue FROM (SELECT *, year(OrderDate) as yr, month(OrderDate) as mn FROM [Orders]) GROUP BY [yr], [mn]";
                var expectedSQLParams = new Dictionary<string, object> {  };
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

            // Test 17
            {
                var testName = "FilterWithoutParsingValuesToDate";
                var tableName = "Products";
                var tryToParseDates = false;
                var odataQueryParams = new Dictionary<string, string>
                {
                    {"select", "Name, Type" },
                    {"filter", "Name eq '2022-11-30'" },
                };
                var expectedSQL = @"SELECT [Name], [Type] FROM [Products] WHERE [Name] = @p0";
                var expectedSQLParams = new Dictionary<string, object>
                {
                    {"@p0", "2022-11-30"}
                };
                yield return new object[] { testName, tableName, tryToParseDates, odataQueryParams, false, expectedSQL, expectedSQLParams };
            }

        }

        private static ODataToSqlConverter CreateODataToSqlConverter() => new ODataToSqlConverter(new EdmModelBuilder(), new SqlServerCompiler() { UseLegacyPagination = false });
    }
}
