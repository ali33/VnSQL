using VnSQL.Core.Models;
using VnSQL.Core.Interfaces;

namespace VnSQL.Core.Sql;

/// <summary>
/// Interface for protocol-specific response formatting
/// </summary>
public interface IProtocolResponseFormatter
{
    /// <summary>
    /// Format the response for a specific protocol
    /// </summary>
    /// <param name="result">Query result</param>
    /// <param name="protocol">Protocol name (MySQL, PostgreSQL, SQLite)</param>
    /// <returns>Formatted response</returns>
    QueryResult FormatResponse(QueryResult result, string protocol);
    
    /// <summary>
    /// Get protocol-specific column types
    /// </summary>
    /// <param name="columnType">Generic column type</param>
    /// <param name="protocol">Protocol name</param>
    /// <returns>Protocol-specific column type</returns>
    string GetProtocolColumnType(string columnType, string protocol);
    
    /// <summary>
    /// Get protocol-specific error message format
    /// </summary>
    /// <param name="errorCode">Error code</param>
    /// <param name="message">Error message</param>
    /// <param name="protocol">Protocol name</param>
    /// <returns>Formatted error message</returns>
    string FormatErrorMessage(int errorCode, string message, string protocol);
}
