using System.IO;
using System.Text.Json;

namespace ZKTecoManager.Services;

public enum DbAuthType { Windows, SqlServer }

public class ConnectionConfig
{
    public string     Server   { get; set; } = "(localdb)\\MSSQLLocalDB";
    public string     Database { get; set; } = "ZKTecoManager";
    public DbAuthType AuthType { get; set; } = DbAuthType.Windows;
    public string?    Username { get; set; }
    public string?    Password { get; set; }

    public string BuildConnectionString()
    {
        var auth = AuthType == DbAuthType.Windows
            ? "Trusted_Connection=True;"
            : $"User Id={Username};Password={Password};";
        return $"Server={Server};Database={Database};{auth}TrustServerCertificate=True;";
    }

    // ── persistence ──────────────────────────────────────────────────────────

    private static readonly string _configDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZKTecoManager");

    private static readonly string _configFile =
        Path.Combine(_configDir, "connection.json");

    private static readonly JsonSerializerOptions _json =
        new() { WriteIndented = true };

    public static ConnectionConfig Load()
    {
        try
        {
            if (File.Exists(_configFile))
            {
                var json = File.ReadAllText(_configFile);
                return JsonSerializer.Deserialize<ConnectionConfig>(json, _json) ?? new();
            }
        }
        catch { /* fall through to default */ }
        return new ConnectionConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(_configDir);
        File.WriteAllText(_configFile, JsonSerializer.Serialize(this, _json));
    }
}
