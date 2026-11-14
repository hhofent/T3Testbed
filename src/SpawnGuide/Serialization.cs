using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace SpawnGuide
{
    /// <summary>
    /// Handles configuration serialization: JSON (primary), binary cache, hot-reload, and schema migration.
    /// </summary>
    public static class ConfigSerialization
    {
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
        };

        private static readonly JsonSerializerOptions JsonReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() },
        };

        /// <summary>
        /// Current schema version. Increment when making breaking changes.
        /// </summary>
        public const int CurrentSchemaVersion = 3;

        /// <summary>
        /// Load configuration from JSON file with automatic migration and binary caching.
        /// </summary>
        public static SpawnConfig Load(string path, bool useCache = true)
        {
            if (!File.Exists(path)) return SpawnConfig.Default;

            string binPath = Path.ChangeExtension(path, ".config.bin");
            bool cacheValid = useCache && File.Exists(binPath) && File.GetLastWriteTimeUtc(binPath) >= File.GetLastWriteTimeUtc(path);

            if (cacheValid)
            {
                try
                {
                    var cached = LoadBinary(binPath);
                    if (cached.SchemaVersion == CurrentSchemaVersion)
                    {
                        cached.Validate();
                        return cached;
                    }
                    // Version mismatch - fall through to JSON load + migrate
                }
                catch { /* fall through to JSON */ }
            }

            // Load from JSON
            string json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<SpawnConfig>(json, JsonReadOptions) ?? SpawnConfig.Default;

            // Migrate if needed
            if (config.SchemaVersion < CurrentSchemaVersion)
            {
                config = Migrate(config, config.SchemaVersion);
                config.SchemaVersion = CurrentSchemaVersion;
            }

            config.Validate();

            // Write binary cache
            if (useCache)
            {
                try { SaveBinary(config, binPath); } catch { /* ignore cache errors */ }
            }

            return config;
        }

        /// <summary>
        /// Save configuration to JSON file (and update binary cache).
        /// </summary>
        public static void Save(SpawnConfig config, string path, bool updateCache = true)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            config.SchemaVersion = CurrentSchemaVersion;
            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(path, json);

            if (updateCache)
            {
                string binPath = Path.ChangeExtension(path, ".config.bin");
                try { SaveBinary(config, binPath); } catch { /* ignore cache errors */ }
            }
        }

        /// <summary>
        /// Save configuration to binary format for fast loading.
        /// </summary>
        public static void SaveBinary(SpawnConfig config, string path)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            bw.Write(CurrentSchemaVersion);
            bw.Write(config.GetType().AssemblyQualifiedName ?? "");

            bw.Write("SpawnGuide.FullConfig.v1");
            bw.Write(JsonSerializer.Serialize(config, JsonOptions));
        }

        /// <summary>Load all sections; obsolete partial caches are rebuilt from JSON.</summary>
        public static SpawnConfig LoadBinary(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            int version = br.ReadInt32();
            br.ReadString(); // assembly identity retained for format diagnostics
            if (br.ReadString() != "SpawnGuide.FullConfig.v1")
                throw new InvalidDataException("Obsolete partial config cache; reload the source JSON.");
            var config = JsonSerializer.Deserialize<SpawnConfig>(br.ReadString(), JsonReadOptions)
                ?? throw new InvalidDataException("Empty config cache.");
            if (config.SchemaVersion != version) throw new InvalidDataException("Config cache version mismatch.");
            return config;
        }

        /// <summary>
        /// Migrate a configuration from an older schema version to current.
        /// </summary>
        public static SpawnConfig Migrate(SpawnConfig config, int fromVersion)
        {
            var migrated = config; // JSON deserializer already created the object

            // Version 1 -> 2: Add new parameter types, enums, distributions
            if (fromVersion < 2)
            {
                migrated = MigrateV1ToV2(migrated);
            }

            if (fromVersion < 3)
                migrated.BuilderGuy ??= new BuilderGuyConfig();

            migrated.SchemaVersion = CurrentSchemaVersion;

            return migrated;
        }

        /// <summary>
        /// Migration from schema v1 (FloatRange/IntRange, strings) to v2 (Distributions, Enums, etc.)
        /// </summary>
        private static SpawnConfig MigrateV1ToV2(SpawnConfig config)
        {
            // The JSON deserializer with new types will handle most conversions automatically
            // because we kept the same property names. But we need to handle:
            // 1. String enums -> Enum types (handled by JsonStringEnumConverter)
            // 2. FloatRange -> FloatDistribution (handled by custom converters if we add them)
            // 3. New fields get default values

            // For now, the main migration is ensuring new fields have sensible defaults
            // which happens automatically via parameterless constructors

            // Add rationale comments to JSON on next save (handled by Save method)

            return config;
        }

        /// <summary>
        /// Create a backup of the config file before migration.
        /// </summary>
        public static void BackupConfig(string path)
        {
            if (!File.Exists(path)) return;
            string backupPath = $"{path}.backup.v{CurrentSchemaVersion - 1}.{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Copy(path, backupPath, overwrite: true);
        }

        /// <summary>
        /// Generate a diff report between two configurations.
        /// </summary>
        public static string Diff(SpawnConfig oldConfig, SpawnConfig newConfig, string prefix = "")
        {
            var diffs = new List<string>();
            DiffObject(oldConfig, newConfig, prefix, diffs);
            return string.Join("\n", diffs);
        }

        private static void DiffObject(object oldObj, object newObj, string path, List<string> diffs)
        {
            if (oldObj == null && newObj == null) return;
            if (oldObj == null || newObj == null)
            {
                diffs.Add($"{path}: {(oldObj == null ? "null" : "set")} -> {(newObj == null ? "null" : "set")}");
                return;
            }

            var type = oldObj.GetType();
            if (type != newObj.GetType())
            {
                diffs.Add($"{path}: type {type.Name} -> {newObj.GetType().Name}");
                return;
            }

            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            {
                if (!Equals(oldObj, newObj))
                    diffs.Add($"{path}: {oldObj} -> {newObj}");
                return;
            }

            if (type.IsEnum)
            {
                if (!Equals(oldObj, newObj))
                    diffs.Add($"{path}: {oldObj} -> {newObj}");
                return;
            }

            // Handle known config types
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                var propPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                var oldVal = prop.GetValue(oldObj);
                var newVal = prop.GetValue(newObj);
                DiffObject(oldVal, newVal, propPath, diffs);
            }
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var fieldPath = string.IsNullOrEmpty(path) ? field.Name : $"{path}.{field.Name}";
                var oldVal = field.GetValue(oldObj);
                var newVal = field.GetValue(newObj);
                DiffObject(oldVal, newVal, fieldPath, diffs);
            }
        }
    }

    /// <summary>
    /// File system watcher for hot-reloading configuration changes.
    /// </summary>
    public sealed class ConfigHotReloader : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly string _configPath;
        private readonly Timer _debounceTimer;
        private readonly Action<SpawnConfig> _onReload;
        private SpawnConfig _currentConfig;
        private bool _disposed;

        public ConfigHotReloader(string configPath, Action<SpawnConfig> onReload, int debounceMs = 500)
        {
            _configPath = Path.GetFullPath(configPath);
            _onReload = onReload;
            _currentConfig = ConfigSerialization.Load(configPath);

            _watcher = new FileSystemWatcher(Path.GetDirectoryName(_configPath), Path.GetFileName(_configPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnFileChanged;

            _debounceTimer = new Timer(DebouncedReload, null, Timeout.Infinite, Timeout.Infinite);
        }

        public SpawnConfig CurrentConfig => _currentConfig;

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            _debounceTimer.Change(500, Timeout.Infinite); // 500ms debounce
        }

        private void DebouncedReload(object state)
        {
            try
            {
                var newConfig = ConfigSerialization.Load(_configPath, useCache: false);
                if (!ConfigsEqual(_currentConfig, newConfig))
                {
                    _currentConfig = newConfig;
                    _onReload?.Invoke(newConfig);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hot-reload error: {ex.Message}");
            }
        }

        private bool ConfigsEqual(SpawnConfig a, SpawnConfig b)
        {
            // Quick comparison - in practice you'd want a proper equality check
            var jsonA = JsonSerializer.Serialize(a, ConfigSerialization.JsonOptions);
            var jsonB = JsonSerializer.Serialize(b, ConfigSerialization.JsonOptions);
            return jsonA == jsonB;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _watcher?.Dispose();
            _debounceTimer?.Dispose();
        }
    }
}
