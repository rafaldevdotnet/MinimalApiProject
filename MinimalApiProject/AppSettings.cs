using System.Text.Json;
using System.Text.Json.Nodes;
using MinimalApiProject.Helper;

namespace MinimalApiProject
{
    /// <summary>
    /// Klasa do zarządzania ustawieniami aplikacji z pliku appsettings.json
    /// Klasa ta umożliwia odczyt wartości konfiguracyjnych, szyfrowanie wrażliwych danych.
    /// Aby zaszyfrować dany parametr wystarczy jego wartość poprzedzić znakiem '*', np.
    /// "Password": "*mojehasło"
    /// </summary>
    public static class AppSettings
    {
        private static IConfigurationRoot? _configuration;
        private static string _configPath = "appsettings.json";

        public static void Initialize(IConfiguration configuration)
        {
            _configuration = (IConfigurationRoot)configuration;
            CheckAndEncryptSensitiveValues();
        }

        private static string? Get(string key)
        {
            var value = _configuration?[key];
            if (value == null) return null;
            return EncryptionHelper.IsEncrypted(value) ? EncryptionHelper.Decrypt(value) : value;
        }

        public static string ConnectionString =>
            Get("ConnectionStrings:Default") ?? throw new InvalidOperationException("No connection string found.");

        public static string GetCsvUrl(string key)
        {
            return Get($"CsvDownloadUrls:{key}") ?? throw new InvalidOperationException($"Missing CsvDownloadUrls:{key} in appsettings.json");
        }

        /// <summary>
        /// Sprawdza plik appsettings.json i szyfruje wszystkie wartości zaczynające się od '*'.
        /// </summary>
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

        /// <summary>
        /// Rekurencyjnie przeszukuje obiekt JsonObject i szyfruje wartości zaczynające się od '*'.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
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
