using MinimalApiProject.Enums;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using MinimalApiProject.Helper;

namespace MinimalApiProject
{
    public static class AppSettings
    {
        private static IConfigurationRoot? _configuration;
        private static string _configPath = "appsettings.json";

        public static void Initialize(IConfiguration configuration)
        {
            _configuration = (IConfigurationRoot)configuration;
            CheckAndEncryptSensitiveValues();
        }

        public static string? Get(string key)
        {
            var value = _configuration?[key];
            if (value == null) return null;
            return EncryptionHelper.IsEncrypted(value) ? EncryptionHelper.Decrypt(value) : value;
        }

        public static string ConnectionString =>
            Get("ConnectionStrings:Default") ?? throw new InvalidOperationException("No connection string found.");

        public static DbTypeEnum DatabaseType
        {
            get
            {
                var typeStr = Get("TypeDB");
                if (!Enum.TryParse<DbTypeEnum>(typeStr, ignoreCase: true, out var dbType))
                    throw new InvalidOperationException($"Invalid DB type: {typeStr}.\nOnly: MSSQL; Sqlite; Postgres");
                return dbType;
            }
        }

        public static string GetCsvUrl(string key)
        {
            return Get($"CsvDownloadUrls:{key}") ?? throw new InvalidOperationException($"Missing CsvDownloadUrls:{key} in appsettings.json");
        }

        private static void CheckAndEncryptSensitiveValues()
        {
            if (!File.Exists(_configPath)) return;

            var root = JsonNode.Parse(File.ReadAllText(_configPath))?.AsObject();
            if (root == null) return;

            bool modified = EncryptRecursive(root);

            if (modified)
            {
                File.WriteAllText(_configPath, root.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

                Console.WriteLine("🔐 Wrażliwe dane zostały zaszyfrowane w appsettings.json");
            }
        }

        private static bool EncryptRecursive(JsonObject obj)
        {
            bool modified = false;

            foreach (var (key, value) in obj.ToList())
            {
                if (value is JsonObject nestedObj)
                {
                    if (EncryptRecursive(nestedObj)) modified = true;
                }
                else if (value is JsonValue val)
                {
                    var str = val.ToString();
                    if (str.StartsWith("*"))
                    {
                        var plain = str.TrimStart('*');
                        var encrypted = EncryptionHelper.Encrypt(plain);
                        obj[key] = encrypted;
                        modified = true;
                    }
                }
            }

            return modified;
        }
    }


}
