namespace VnSQL.Protocols.Configuration;

/// <summary>
/// MySQL Protocol Configuration
/// </summary>
public class MySQLConfiguration
{
    /// <summary>
    /// Whether MySQL protocol is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Port for MySQL protocol
    /// </summary>
    public int Port { get; set; } = 3306;
    
    /// <summary>
    /// Authentication settings
    /// </summary>
    public MySQLAuthenticationConfiguration Authentication { get; set; } = new();
}

/// <summary>
/// MySQL Authentication Configuration
/// </summary>
public class MySQLAuthenticationConfiguration
{
    /// <summary>
    /// Root username for authentication
    /// </summary>
    public string RootUsername { get; set; } = "root";
    
    /// <summary>
    /// Root password for authentication
    /// </summary>
    public string RootPassword { get; set; } = "password";
}
