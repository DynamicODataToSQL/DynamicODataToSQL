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
            bool count = false,
            bool tryToParseDates = true);

        /// <summary>
        /// ConvertToSQL.
        /// </summary>
        /// <param name="rawSql">rawSql.</param>
        /// <param name="odataQuery">odataQuery.</param>
        /// <param name="count">count.</param>
        /// <param name="tryToParseDates">True by default. In true it will try to convert values defined in filter as dates and if value can be parsed as date it will do so. Otherwise it will use provided value as is.</param>
        /// <returns>Tuple.</returns>
        (string, IDictionary<string, object>) ConvertToSqlFromRawSql(
            string rawSql,
            IDictionary<string, string> odataQuery,
            bool count = false,
            bool tryToParseDates = true);
    }
}
