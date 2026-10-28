// Red Moon machine-local INI settings. Secrets may be environment references.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace ServerCore.Common
{
    public sealed class RuntimeSettings
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static RuntimeSettings Load(string file)
        {
            if (!File.Exists(file)) throw new FileNotFoundException("Run Red Moon setup before starting a server.", file);
            var result = new RuntimeSettings();
            string section = string.Empty;
            foreach (string raw in File.ReadAllLines(file))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2); continue; }
                int equals = line.IndexOf('=');
                if (equals <= 0) throw new FormatException("Invalid configuration entry in " + Path.GetFileName(file));
                string key = section + "/" + line.Substring(0, equals).Trim();
                if (result._values.ContainsKey(key)) throw new FormatException("Duplicate configuration key: " + key);
                result._values.Add(key, line.Substring(equals + 1).Trim());
            }
            return result;
        }
        public string Get(string section, string key, string fallback = null)
        {
            if (!_values.TryGetValue(section + "/" + key, out string value))
            {
                if (fallback != null) return fallback;
                throw new FormatException("Missing configuration key: " + section + "/" + key);
            }
            if (value.StartsWith("env:", StringComparison.Ordinal))
            {
                string name = value.Substring(4);
                value = Environment.GetEnvironmentVariable(name);
                if (value == null) throw new InvalidOperationException("Required local secret is not loaded: " + name + ". Use Start-RedMoon.cmd.");
            }
            return value;
        }
        public int Port(string section, string key, int fallback)
        {
            if (!int.TryParse(Get(section, key, fallback.ToString()), out int value) || value < 1 || value > 65535)
                throw new FormatException("Invalid TCP port in " + section + "/" + key);
            return value;
        }
        public string Address(string section, string key, string fallback)
        {
            string value = Get(section, key, fallback);
            if (!IPAddress.TryParse(value, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                throw new FormatException("An IPv4 address is required in " + section + "/" + key);
            return value;
        }
        public static void UseConfigurationDirectory(string[] args)
        {
            int index = Array.IndexOf(args, "--config-root");
            string path = index < 0 ? AppContext.BaseDirectory : index + 1 < args.Length ? args[index + 1] : throw new ArgumentException("Missing configuration directory.");
            Directory.SetCurrentDirectory(Path.GetFullPath(path));
        }
    }
}
