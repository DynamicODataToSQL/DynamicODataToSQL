namespace DynamicODataToSQL.Interfaces;

using Microsoft.OData.Edm;

/// <summary>
/// IEdmModelBuilder.
/// </summary>
public interface IEdmModelBuilder
{

    /// <summary>
    /// BuildTableModel.
    /// </summary>
    /// <param name="tableName">tableName.</param>
    /// <returns>Tuple.</returns>
    (IEdmModel, IEdmEntityType, IEdmEntitySet) BuildTableModel(string tableName);
}
