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
        public (IEdmModel, IEdmEntityType, IEdmEntitySet) BuildTableModel(string tableName, string[] expands)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentNullException(nameof(tableName));
            }

            var model = new EdmModel();
            var entityType = new EdmEntityType(DEFAULTNAMESPACE, tableName, null, false, true);
            model.AddElement(entityType);

            if (expands != null)
            {
                foreach (var expand in expands)
                {
                    // Create a new EntityType for each expanded entity
                    var relatedEntityType = new EdmEntityType(DEFAULTNAMESPACE, expand, null, false, true);
                    model.AddElement(relatedEntityType);

                    // Define the navigation property for the expanded entity
                    var navigationPropertyInfo = new EdmNavigationPropertyInfo()
                    {
                        Name = expand,
                        Target = relatedEntityType,
                        // TODO: how can we now which type of Multiplicity it is just by looking at the query ?!
                        // Perhaps an additional configuration needs to be provided?
                        TargetMultiplicity = EdmMultiplicity.One
                    };

                    // TODO: same here ... is it Uni- or Bidirectional?
                    entityType.AddUnidirectionalNavigation(navigationPropertyInfo);
                }
            }

            var defaultContainer = new EdmEntityContainer(DEFAULTNAMESPACE, "DefaultContainer");
            model.AddElement(defaultContainer);
            var entitySet = defaultContainer.AddEntitySet(tableName, entityType);

            return (model, entityType, entitySet);
        }
    }
}
