namespace DynamicODataToSQL
{
    using System;
    using DynamicODataToSQL.Interfaces;
    using Microsoft.OData.Edm;

    /// <inheritdoc/>
    public class EdmModelBuilder : IEdmModelBuilder
    {
        private const string DEFAULTNAMESPACE = "ODataToSqlConverter";

        /// <inheritdoc/>
        public (IEdmModel, IEdmEntityType, IEdmEntitySet) BuildTableModel(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentNullException(nameof(tableName));
            }

            var model = new EdmModel();
            var entityType = new EdmEntityType(DEFAULTNAMESPACE, tableName, null, false, true);
            AddProperties(entityType);
            model.AddElement(entityType);

            var defaultContainer = new EdmEntityContainer(DEFAULTNAMESPACE, "DefaultContainer");
            model.AddElement(defaultContainer);
            var entitySet = defaultContainer.AddEntitySet(tableName, entityType);

            return (model, entityType, entitySet);
        }

        protected virtual void AddProperties(EdmEntityType entityType)
        {
        }
    }
}
