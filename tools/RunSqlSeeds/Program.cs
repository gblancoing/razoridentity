using System.Text.Json;
using Npgsql;

static string? GetConnectionString(string[] args)
{
    var env = Environment.GetEnvironmentVariable("COMUNACLICK_DB")
        ?? Environment.GetEnvironmentVariable("PG_CONNECTION_STRING");
    if (!string.IsNullOrWhiteSpace(env))
        return env;

    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].Equals("--connection", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            return args[i + 1];
        if (args[i].StartsWith("--connection=", StringComparison.OrdinalIgnoreCase))
            return args[i]["--connection=".Length..];
    }

    var repo = FindRepoRoot(Directory.GetCurrentDirectory());
    var path = Path.Combine(repo, "src", "ComunaClick.Acl", "appsettings.Development.json");
    if (!File.Exists(path))
        return null;

    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var cs))
        return null;
    if (!cs.TryGetProperty("AclDb", out var acl))
        return null;
    return acl.GetString();
}

static string FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        var sln = Path.Combine(dir.FullName, "ComunaClick.sln");
        var acl = Path.Combine(dir.FullName, "src", "ComunaClick.Acl", "ComunaClick.Acl.csproj");
        if (File.Exists(sln) || File.Exists(acl))
            return dir.FullName;
        dir = dir.Parent;
    }

    return start;
}

static List<string> GetSqlFiles(string[] args, string repoRoot)
{
    var files = new List<string>();
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].StartsWith("--connection=", StringComparison.OrdinalIgnoreCase))
            continue;
        if (args[i].Equals("--connection", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 < args.Length)
                i++;
            continue;
        }

        if (args[i].StartsWith("--", StringComparison.Ordinal))
            continue;

        var p = args[i];
        if (!Path.IsPathRooted(p))
            p = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), p));
        files.Add(p);
    }

    if (files.Count > 0)
        return files;

    var seeds = Path.Combine(repoRoot, "infra", "seeds");
    return new List<string>
    {
        Path.Combine(seeds, "acl_seed.sql"),
        Path.Combine(seeds, "acl_seed_test_users_all_roles.sql")
    };
}

var connStr = GetConnectionString(args);
if (string.IsNullOrWhiteSpace(connStr))
{
    Console.Error.WriteLine(
        "No hay cadena de conexión. Opciones:\n" +
        "  Variable de entorno COMUNACLICK_DB o PG_CONNECTION_STRING\n" +
        "  --connection \"Host=localhost;Port=5432;Database=comunaclick_db;Username=postgres;Password=...\"\n" +
        "  Archivo src/ComunaClick.Acl/appsettings.Development.json → ConnectionStrings:AclDb");
    return 1;
}

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var sqlFiles = GetSqlFiles(args, repoRoot);

foreach (var file in sqlFiles)
{
    if (!File.Exists(file))
    {
        Console.Error.WriteLine($"No existe el archivo: {file}");
        return 1;
    }

    Console.WriteLine($"Ejecutando: {file}");
    var sql = await File.ReadAllTextAsync(file);
    await using var conn = new NpgsqlConnection(connStr);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 300 };
    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine("Listo.");
return 0;
