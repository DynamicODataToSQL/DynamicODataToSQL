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
- Supports basic OData syntax for `select`, `filter`, `skip`, `top`, `orderby`
- Currently does NOT support `expand`, lambda operators.

### filter support
- All logical operators except `has` and `in` are supported. 
  - Spec: http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_LogicalOperators
  - Examples: http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_LogicalOperatorExamples

- Grouping filters using `()` is supported. 
  - Spec: http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_Grouping

- String functions `startswith`, `endswith` and `contains` are supported.
  - Spec: http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_StringandCollectionFunctions.

## Roadmap
- Add support for OData datetime functions. http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#_Toc31360996
- Add support for OData aggregate syntax. See http://docs.oasis-open.org/odata/odata-data-aggregation-ext/v4.0/odata-data-aggregation-ext-v4.0.html
- Build a sample OData service with docker Image using NorthWind or AdventureWorks DB. 

## Contributing

We are always looking for people to contribute! To find out how to help out, have a look at 
our [Contributing Guide](.github/CONTRIBUTING.md).


## Code of Conduct

Please note that this project is released with a [Contributor Code of Conduct](.github/CODE_OF_CONDUCT.md). By participating in this project you agree to abide by its terms.

## Copyright

Copyright MIT © 2020 Vaibhav Goyal. See [LICENSE](LICENSE) for details.


