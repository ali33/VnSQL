namespace VnSQL.Protocols.Configuration;

/// <summary>
/// Configuration for SQLite protocol
/// </summary>
public class SQLiteConfiguration
{
    public int Port { get; set; } = 5433;
    public string Host { get; set; } = "localhost";
    public int MaxConnections { get; set; } = 50;
    public string DatabasePath { get; set; } = "./data/sqlite.db";
    public AuthenticationConfig Authentication { get; set; } = new();
    
    public class AuthenticationConfig
    {
        public string Username { get; set; } = "sqlite";
        public string Password { get; set; } = "password";
    }
}
