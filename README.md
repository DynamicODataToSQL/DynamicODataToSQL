# DynamicODataToSQL

[![License](https://img.shields.io/github/license/DynamicODataToSQL/DynamicODataToSQL)](https://github.com/DynamicODataToSQL/DynamicODataToSQL/blob/master/LICENSE)
[![GitHub Actions Status](https://github.com/DynamicODataToSQL/DynamicODataToSQL/workflows/Build/badge.svg?branch=master)](https://github.com/DynamicODataToSQL/DynamicODataToSQL/actions)
[![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/DynamicOdataToSQL/DynamicOdataToSQL?sort=semver)]()
[![Nuget](https://img.shields.io/nuget/v/DynamicODataToSQL)](https://www.nuget.org/packages/DynamicODataToSQL/)

Dotnet standard package to convert OData query to SQL query when db schema is dynamic and hence using entity framework or IQueryable is NOT trivial. 


## Table of Contents

- [Introduction](#introduction)
- [Getting Started](#getting-started)
- [Features](#features)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [Code of Conduct](#code-of-conduct)
- [Copyright](#copyright)

## Introduction

In most multi-tenant enterprise systems data model is not fixed (dynamic). 
Dynamic nature of data model rules out effectively using Entity Framework or IQueryable. 

In such case, this project helps build SQL query from OData query without using Entity Framework or IQuerable. 
You can use this package to expose an OData service to query this dynamic data model (Coming soon: Sample service as Docker image). 


## Getting Started
- Install Nuget Package  
  `Install-Package DynamicODataToSQL`
```c#
var converter = new ODataToSqlConverter(new EdmModelBuilder(), new SqlServerCompiler() { UseLegacyPagination = false });
var tableName = "Products"; 
var odataQueryParams = new Dictionary<string, string>
                {
                    {"select", "Name, Type" },
                    {"filter", "contains(Name,'Tea')" },
                    {"orderby", "Id desc" },
                    {"top", "20" },
                    {"skip", "5" },
                };
 var result = converter.ConvertToSQL(
                tableName,
                odataQueryParams,
                false);

string sql = result.Item1;
// SELECT [Name], [Type] FROM [Products] WHERE [Name] like @p0 ORDER BY [Id] DESC OFFSET @p1 ROWS FETCH NEXT @p2 ROWS ONLY

IDictionary<string, object> sqlParams = result.Item2; 
// {"@p0", "%Tea%"},{"@p1", 5}, {"@p2", 20}
```

See Unit tests for more examples

## Features
- Supports basic OData syntax for `select`, `filter`, `skip`, `top`, `orderby`
- Currently does NOT support `expand`, lambda operators.

### filter support
- All logical operators except `has` and `in` are supported. 
  - Spec : http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_LogicalOperators
  - Examples : http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_LogicalOperatorExamples

- Grouping filters using `()` is supported. 
  - Spec: http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_Grouping

- String functions `startswith`, `endswith` and `contains` are supported.
  - Spec : http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#sec_StringandCollectionFunctions

## Roadmap
- Add support for OData datetime functions. http://docs.oasis-open.org/odata/odata/v4.01/odata-v4.01-part2-url-conventions.html#_Toc31360996
- Add support for OData aggregate syntax. See http://docs.oasis-open.org/odata/odata-data-aggregation-ext/v4.0/odata-data-aggregation-ext-v4.0.html

## Contributing

We are always looking for people to contribute! To find out how to help out, have a look at our [Contributing Guide](.github/CONTRIBUTING.md).


## Code of Conduct

Please note that this project is released with a [Contributor Code of Conduct](.github/CODE_OF_CONDUCT.md). By participating in this project you agree to abide by its terms.

## Copyright

Copyright MIT © 2020 Vaibhav Goyal. See [LICENSE](LICENSE) for details.


