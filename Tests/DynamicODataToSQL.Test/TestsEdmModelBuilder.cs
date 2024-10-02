namespace DynamicODataToSQL.Test;

using System.Collections.Generic;

using Microsoft.OData.Edm;

/// <inheritdoc/>
/// <summary>
/// EdmModelBuilder with known test tables properties.
/// </summary>
public class TestsEdmModelBuilder : EdmModelBuilder
{
    private readonly Dictionary<string, Dictionary<string, EdmPrimitiveTypeKind>> _knownTableProperties;

    public TestsEdmModelBuilder() => _knownTableProperties = new Dictionary<string, Dictionary<string, EdmPrimitiveTypeKind>>
    {
        ["Products"] = new Dictionary<string, EdmPrimitiveTypeKind>
        {
            ["Id"] = EdmPrimitiveTypeKind.Int32,
            ["Name"] = EdmPrimitiveTypeKind.String,
            ["Type"] = EdmPrimitiveTypeKind.String,
            ["TotalInventory"] = EdmPrimitiveTypeKind.Int32,
            ["TimeCreated"] = EdmPrimitiveTypeKind.DateTimeOffset,
            ["Origin"] = EdmPrimitiveTypeKind.String,
            ["Spaced Column"] = EdmPrimitiveTypeKind.String
        },
        ["Orders"] = new Dictionary<string, EdmPrimitiveTypeKind>
        {
            ["OrderId"] = EdmPrimitiveTypeKind.Int32,
            ["TotalAmount"] = EdmPrimitiveTypeKind.Double,
            ["Country"] = EdmPrimitiveTypeKind.String,
            ["Amount"] = EdmPrimitiveTypeKind.Double,
            ["OrderDate"] = EdmPrimitiveTypeKind.DateTimeOffset,
            ["value"] = EdmPrimitiveTypeKind.Double
        }
    };

    protected override void AddProperties(EdmEntityType entityType)
    {
        var tableName = entityType.Name;

        if (_knownTableProperties.TryGetValue(tableName, out var value))
        {
            foreach (var column in value)
            {
                entityType.AddStructuralProperty(column.Key, column.Value);
            }
        }
    }
}
