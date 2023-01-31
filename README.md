# DynamicODataToSQL
Dotnet NuGet package to convert OData query to SQL query when the data model is dynamic and 
hence entity framework or any other ORM with IQuerable support cannot be used.
In a multi-tenant enterprise or Saas applications, the data model is usually not fixed (dynamic).

[![License](https://img.shields.io/github/license/DynamicODataToSQL/DynamicODataToSQL)](https://github.com/DynamicODataToSQL/DynamicODataToSQL/blob/master/LICENSE)
[![GitHub Actions Status](https://github.com/DynamicODataToSQL/DynamicODataToSQL/workflows/Build/badge.svg?branch=master)](https://github.com/DynamicODataToSQL/DynamicODataToSQL/actions)
[![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/DynamicOdataToSQL/DynamicOdataToSQL?sort=semver)]()
[![Nuget](https://img.shields.io/nuget/v/DynamicODataToSQL)](https://www.nuget.org/packages/DynamicODataToSQL/)


## Table of Contents
- [Example Scenario](#example-scenario)
- [Getting Started](#getting-started)
- [Features](#features)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [Code of Conduct](#code-of-conduct)
- [Copyright](#copyright)

## Example Scenario
Let's consider you are building a Saas application for project and issue tracking, similar to Jira. 
Development teams and organizations track issues differently, to personalize and measure their agile process.  
Your Saas application provides this by allowing tenants to add properties(columns) to existing 
object types(tables) or create completely new object types, it further allows querying new 
object types and filtering using new properties through an OData service. 

Contoso Inc, one of your tenant
- Adds a boolean property `Internal` to `Issue` object. It is used to track internal vs 
customer reported issues.
- Adds another object type called `Customer` to track which customer reported the issue or was affected by it. `Customer` object contains standard properties like `Name`, `Email` etc. 

It is not trivial to expose a multi-tenant OData service in such a scenario using Entity Framework 
since DB schema/EF's DBContext can be different for each tenant and can be modified on the fly.

```
GET https://api.trackerOne.com/contoso/odata/Issues?$filter=Internal eq true
```

```
GET https://api.trackerOne.com/contoso/odata/Customers?$filter=contains(Email,'outlook.com')
```

This project aims to solve this issue by providing a simple API to convert an OData query to an SQL query when the data model is dynamic. 

## Getting Started
- Install Nuget Package  
  `Install-Package DynamicODataToSQL`
```c#
var converter = new ODataToSqlConverter(new EdmModelBuilder(), new SqlServerCompiler() { UseLegacyPagination = false });
var tableName = "Customers"; 
var odataQueryParams = new Dictionary<string, string>
                {
                    {"select", "Name, Email" },
                    {"filter", "contains(Email,'outlook.com')" },
                    {"orderby", "Name" },
                    {"top", "20" },
                    {"skip", "5" },
                };
 var result = converter.ConvertToSQL(
                tableName,
                odataQueryParams,
                false);

string sql = result.Item1;
// SELECT [Name], [Email] FROM [Customers] WHERE [Email] like @p0 ORDER BY [Name] ASC OFFSET @p1 ROWS FETCH NEXT @p2 ROWS ONLY

IDictionary<string, object> sqlParams = result.Item2; 
// {"@p0", "%outlook.com%"},{"@p1", 5}, {"@p2", 20}
```

See Unit tests for more examples

## Example OData Service
See [DynamicODataSampleService](Samples/DynamicODataSampleService) for and example OData service. 

1. Download [AdventureWorks2019 Sample Database](https://github.com/Microsoft/sql-server-samples/releases/download/adventureworks/AdventureWorks2019.bak)
2. Restore AdventureWorks2019 database. 
3. Setup database user and permissions

    ```sql
    CREATE LOGIN odata_service WITH PASSWORD = 'Password123';   
    use AdventureWorks2019
    CREATE USER odata_service FOR LOGIN odata_service;
    GRANT SELECT ON DATABASE::AdventureWorks2019 TO odata_service;
    GO
    ```

4. Run `dotnet run --project .\Samples\DynamicODataSampleService\DynamicODataSampleService.csproj`

5. Use Powershell to query the service, Top 10 Persons by ModifiedDate 
   
    ```Powershell
    Invoke-RestMethod 'https://localhost:5001/tables/Person.Person?orderby=ModifiedDate desc&skip=0&top=10&select=FirstName,LastName,ModifiedDate' | ConvertTo-Json
    ```   

    Products with StockLevel less than 100
    
    ```Powershell
    Invoke-RestMethod 'https://localhost:5001/tables/Production.Product?filter=SafetyStockLevel lt 100' | ConvertTo-Json
    ```

## Features
- Supports basic OData syntax for `select`, `filter`, `skip`, `top`, `orderby` and now `apply`
- Currently does NOT support `expand` and `lambda` operators.

### filter support
- All logical operators except `has` and `in` are supported. 
  - Spec: http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_LogicalOperators
  - Examples: http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_LogicalOperatorExamples

- Grouping filters using `()` is supported. 
  - Spec: http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_Grouping

- String functions `startswith`, `endswith` and `contains` are supported.
  - Spec: http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_StringandCollectionFunctions.

- DateTime functions `date`, `time`, `year`, `month`, `day`, `hour` and `minute` are supported.
  - Spec: http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#_Toc31360996

### apply support (aggregations)
Aggregations using Odata `apply` query option is supported. Spec: http://docs.oasis-open.org/odata/odata-data-aggregation-ext/v4.0/odata-data-aggregation-ext-v4.0.html

- Transformations: `filter`, `groupby` and `aggregate` are supported. `expand`, `concat`, `search`, `top`, `bottom` are NOT supported.
- `sum`, `min`, `max`, `avg`, `countdistinct` and `count` are supported.

Example

  ```
  \orders?$apply=groupby((Country),aggregate(Amount with sum as Total,Amount with average as AvgAmt))
  ```

is converted to 

  ```sql
  SELECT [Country], Sum(Amount) AS Total, Avg(Amount) AS AvgAmt FROM [Orders] GROUP BY [Country]
  ```

For more advanced aggreagate scenarios supported, see unit tests. 


### Handling Dates on filter

By default filter values are checked if they can be converted to dates. Sometimes this is not expected. You can disable date parsing by setting up tryToParseDate to false on ConvertToSqlFromRawSql function.

Example

```c#
var converter = new ODataToSqlConverter(new EdmModelBuilder(), new SqlServerCompiler() { UseLegacyPagination = false });
var tableName = "Customers"; 
converter.ConvertToSqlFromRawSql(customSqlQuery, oDataParams, false, false);

var odataQueryParams = new Dictionary<string, string>
                {
                    {"select", "Name, Type" },
                    {"filter", "Name eq '2022-11-30'" },
                };

// Default conversion with Date Parsing
 var result = converter.ConvertToSQL(
                tableName,
                odataQueryParams,
                false);
                
string sql = result.Item1;
// SELECT [Name], [Type] FROM [Products] WHERE [Name] = @p0
IDictionary<string, object> sqlParams = resultWithoutParsedDates.Item2; 
// {"@p0", "11/30/2022 12:00:00 AM"}

// Conversion without Date Parsing
var resultWithoutParsedDates = converter.ConvertToSQL(
                tableName,
                odataQueryParams,
                false,
                false);
string sql = resultWithoutParsedDates.Item1;
// SELECT [Name], [Type] FROM [Products] WHERE [Name] = @p0
IDictionary<string, object> sqlParams = resultWithoutParsedDates.Item2; 
// {"@p0", "2022-11-30"}
```

### Custom SQL

You can now use custom query as source (instead of table) and be able to build query in response. WITH clause is used to wrap custom query. 

Example

```http request
$apply=aggregate(TotalAmount with sum as TotalAmount, TotalAmount with average as AverageAmount,$count as OrderCount)
```
```c#
var rawSql = "SELECT * FROM [Orders]";
converter.ConvertToSqlFromRawSql(customSqlQuery, oDataParams, count);
```
is converted  to
```sql
WITH [RawSql] AS (SELECT * FROM [Orders])
SELECT Sum(TotalAmount) AS TotalAmount, AVG(TotalAmount) AS AverageAmount, COUNT(1) AS OrderCount FROM [RawSql] 
```

Check unit tests for more usage examples.


## Roadmap
- [] Support for validating column names and column data types.

## Contributing
We are always looking for people to contribute! To find out how to help out, have a look at 
our [Contributing Guide](.github/CONTRIBUTING.md).

## Contributors
* [Maciej Pieprzyk](https://github.com/maciejpieprzyk)

## Code of Conduct
Please note that this project is released with a [Contributor Code of Conduct](.github/CODE_OF_CONDUCT.md). By participating in this project you agree to abide by its terms.

## Copyright
Copyright MIT © 2020 Vaibhav Goyal. See [LICENSE](LICENSE) for details.
