namespace DynamicODataToSQL
{
    using System.Collections.Generic;

    /// <summary>
    /// IODataToSqlConverter.
    /// </summary>
    public interface IODataToSqlConverter
    {       

        /// <summary>
        /// ConvertToSQL.
        /// </summary>
        /// <param name="tableName">tableName.</param>
        /// <param name="odataQuery">odataQuery.</param>
        /// <param name="count">count.</param>
        /// <returns>Tuple.</returns>
        (string, IDictionary<string, object>) ConvertToSQL(
            string tableName,
            IDictionary<string, string> odataQuery,
            bool count = false);

        /// <summary>
        /// ConvertToSQL.
        /// </summary>
        /// <param name="rawSql">rawSql.</param>
        /// <param name="odataQuery">odataQuery.</param>
        /// <param name="count">count.</param>
        /// <returns>Tuple.</returns>
        (string, IDictionary<string, object>) ConvertToSqlFromRawSql(
            string rawSql,
            IDictionary<string, string> odataQuery,
            bool count = false);
    }
}
