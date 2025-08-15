namespace VnSQL.Protocols.Configuration;

/// <summary>
/// Configuration for SQL Server protocol
/// </summary>
public class SQLServerConfiguration
{
    public int Port { get; set; } = 1433;
    public string Host { get; set; } = "localhost";
    public int MaxConnections { get; set; } = 100;
    public string InstanceName { get; set; } = "SQLEXPRESS";
    public AuthenticationConfig Authentication { get; set; } = new();
    public SslConfig Ssl { get; set; } = new();
    
    public class AuthenticationConfig
    {
        public string Username { get; set; } = "sa";
        public string Password { get; set; } = "password";
        public string Database { get; set; } = "master";
        public bool WindowsAuthentication { get; set; } = false;
    }
    
    public class SslConfig
    {
        public bool Enabled { get; set; } = false;
        public string CertificatePath { get; set; } = "";
        public string KeyPath { get; set; } = "";
    }
}
