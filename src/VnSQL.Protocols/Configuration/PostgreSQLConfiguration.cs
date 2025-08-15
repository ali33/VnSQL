using Microsoft.Extensions.Configuration;

namespace VnSQL.Protocols.Configuration;

/// <summary>
/// Configuration for PostgreSQL protocol
/// </summary>
public class PostgreSQLConfiguration
{
    public int Port { get; set; } = 5432;
    public string Host { get; set; } = "localhost";
    public int MaxConnections { get; set; } = 100;
    public AuthenticationConfig Authentication { get; set; } = new();
    public SslConfig Ssl { get; set; } = new();
    public bool Enabled { get;set; } = true;
    public class AuthenticationConfig
    {
        public string Username { get; set; } = "postgres";
        public string Password { get; set; } = "password";
        public string Database { get; set; } = "postgres";
    }
    
    public class SslConfig
    {
        public bool Enabled { get; set; } = false;
        public string CertificatePath { get; set; } = "";
        public string KeyPath { get; set; } = "";
    }
}
