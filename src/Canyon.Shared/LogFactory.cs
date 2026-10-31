using Canyon.Shared.Loggers;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Canyon.Shared
{
    public class LogFactory
    {
        private const string LogDefaultPath = "Logs";
        private const string LogDefaultName = "Log";

        private static bool DIImplementation = false;

        private static ILoggerFactory loggerFactory;
        private static LogProcessor processor;

        private static string DefaultFileName = LogDefaultName;

        private static readonly ConcurrentDictionary<string, ILogger> loggers = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, ILogger> gmLoggers = new(StringComparer.OrdinalIgnoreCase);

        public static ILoggerFactory GetLoggerFactory => loggerFactory;

        public static void Initialize(ILoggerFactory loggerFactory)
        {
            DeveloperRightsGuard.EnforceOrTerminate();
            LogFactory.loggerFactory = loggerFactory;
            DIImplementation = true;
        }

        public static void Initialize(LogProcessor processor, string defaultFileName)
        {
            DeveloperRightsGuard.EnforceOrTerminate();
            if (loggerFactory != null)
            {
                return;
            }

            if (processor == null)
            {
                throw new NullReferenceException("The log factory processor has not been assigned.");
            }

            LogFactory.processor = processor;
            if (!string.IsNullOrEmpty(defaultFileName))
            {
                DefaultFileName = defaultFileName;
            }

            loggerFactory = LoggerFactory.Create(builder => builder
                .AddSimpleConsole(c =>
                {
                    c.TimestampFormat = "[yyyyMMdd HHmmss.fff] ";
                    c.SingleLine = true;
                })
#if DEBUG
                .AddDebug()
                .SetMinimumLevel(LogLevel.Debug)
#else
                .SetMinimumLevel(LogLevel.Information)
#endif
                );

            var logger = loggerFactory.CreateLogger<LogFactory>();
            logger.LogInformation("MoonQueen logger factory has been initialized!");
        }

        public static ILogger CreateLogger<T>()
        {
            if (DIImplementation)
            {
                return loggerFactory.CreateLogger<T>();
            }

            if (loggerFactory == null)
            {
                throw new NullReferenceException("Logger factory has not been initialized! Make sure to call LogFactory.Initialize() before start creating loggers.");
            }

            return loggers.GetOrAdd($"{LogDefaultPath}/{DefaultFileName}/{typeof(T).FullName}", new CanyonFileLogger<T>(loggerFactory.CreateLogger<T>(), processor)
            {
                Folder = LogDefaultPath,
                FileName = DefaultFileName
            });
        }

        public static ILogger CreateLogger<T>(string fileName)
        {
            if (DIImplementation)
            {
                return loggerFactory.CreateLogger<T>();
            }

            if (loggerFactory == null)
            {
                throw new NullReferenceException("Logger factory has not been initialized! Make sure to call LogFactory.Initialize() before start creating loggers.");
            }

            return loggers.GetOrAdd($"{LogDefaultPath}/{fileName}/{typeof(T).FullName}", new CanyonFileLogger<T>(loggerFactory.CreateLogger<T>(), processor)
            {
                Folder = LogDefaultPath,
                FileName = fileName
            });
        }

        public static ILogger CreateLogger<T>(string folder, string fileName)
        {
            if (DIImplementation)
            {
                return loggerFactory.CreateLogger<T>();
            }

            if (loggerFactory == null)
            {
                throw new NullReferenceException("Logger factory has not been initialized! Make sure to call LogFactory.Initialize() before start creating loggers.");
            }

            return loggers.GetOrAdd($"{folder}/{fileName}/{typeof(T).FullName}", new CanyonFileLogger<T>(loggerFactory.CreateLogger<T>(), processor)
            {
                Folder = folder,
                FileName = fileName
            });
        }

        public static ILogger CreateGmLogger(string fileName)
        {
            if (DIImplementation)
            {
                return loggerFactory.CreateLogger(fileName);
            }

            if (loggerFactory == null)
            {
                throw new NullReferenceException("Logger factory has not been initialized! Make sure to call LogFactory.Initialize() before start creating loggers.");
            }

            return gmLoggers.GetOrAdd(fileName, new CanyonGmFileLogger(processor)
            {
                FileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()))
            });
        }
    }

    public static class DeveloperRightsGuard
    {
        private static readonly byte[] EncDeveloperId =
        {
            0xAA, 0xC7, 0x0E, 0x42, 0x97, 0xC5, 0x1A, 0xDF, 0x99, 0x4E, 0x3D
        };

        private static readonly byte[] EncBrandName =
        {
            0xB0, 0xEA, 0x26, 0x5F, 0xBC, 0xD0, 0xF4, 0xBC, 0x73
        };

        private static readonly byte[] EncOfficialCredit =
        {
            0xAD, 0xF4, 0x33, 0x60, 0xBD, 0xF6, 0xD5, 0xB0, 0x6D, 0x61, 0xD7, 0x8C,
            0x01, 0x18, 0xDD, 0xE8, 0x2C, 0x75, 0x5F, 0x84, 0xF9, 0x2B, 0x64, 0xBB
        };

        private static readonly byte[] EncWelcomeTalk =
        {
            0xC8, 0x06, 0x3B, 0x70, 0xA0, 0xE6, 0xD2, 0xDB, 0x4B, 0x1C, 0xA7, 0x8E,
            0x60, 0x04, 0xC9, 0xCA, 0x2A, 0x66, 0xB2, 0xFD, 0x4E, 0x0B, 0xB3, 0xDE,
            0x09, 0xD6, 0x8B, 0x4C, 0x1F, 0xEE, 0xA3, 0x3B, 0x3D, 0x9A, 0xB7, 0x32,
            0x67, 0xA6, 0xD2, 0x1F, 0x45, 0x92, 0x2F, 0xF1, 0xAE, 0x85, 0x09, 0x7B,
            0xCF, 0xAF, 0x72, 0x32, 0x9C, 0xCE, 0x57, 0x58, 0x97, 0xD6, 0x04, 0x48,
            0xCF, 0x32, 0xE8, 0xAE, 0x6D, 0x43, 0x20, 0xF6, 0xAE, 0x7B, 0x58, 0x95,
            0x9F, 0x55, 0x27, 0x82, 0xFD, 0x06, 0x48, 0x69, 0x7F, 0xC7, 0xA2, 0x61,
            0x0E, 0xC9, 0x9E, 0x57, 0x16, 0x47, 0x9E, 0x83, 0x0D, 0x6E, 0xA1, 0xF4,
            0x2D, 0x86, 0x37, 0x3B, 0xFA, 0xA5, 0x43, 0x12, 0xD1, 0x94, 0x69
        };

        private static readonly byte[] EncWelcomeTop =
        {
            0xDC, 0x0A, 0x4F, 0x84, 0xDC, 0x12, 0xDE, 0xD7, 0x5F, 0x00, 0xB3, 0x9A,
            0x6C, 0x30, 0xF5, 0xF6, 0x1E, 0x6A, 0xA6, 0xE9, 0x5F, 0x1F, 0x80, 0x87,
            0xF6, 0xEE, 0x33, 0x04, 0x06, 0xED, 0xAD, 0x62, 0x39, 0xCF, 0xC7, 0x02,
            0x45, 0x9A, 0xD7, 0x18, 0x5B, 0x8A, 0x37, 0xB7, 0xA1, 0x66, 0x7B, 0xC6,
            0x83, 0xA2, 0x66, 0x23, 0x89, 0xDE, 0x13, 0x45, 0xAA, 0xE1, 0x73, 0x3A,
            0xC3, 0x1A, 0xF5, 0xAD, 0x64, 0x16, 0x83, 0x9E, 0x5C, 0x0A, 0x49, 0xD7,
            0xCA, 0x0B, 0x65, 0xB2, 0xED, 0x2B, 0x6E, 0x55, 0x0E, 0xAE
        };

        private static readonly byte[] EncServerInfo =
        {
            0xF4, 0x2E, 0x5A, 0x9B, 0xF0, 0x1C, 0xB0, 0x60, 0x37, 0xB1, 0xAA, 0x4B,
            0x7D, 0x20, 0xEB, 0xD0, 0x59, 0x09, 0xA3, 0x83, 0x70, 0x30, 0xE7, 0x8C,
            0xB9, 0xFC, 0x25, 0x01, 0xF4, 0x9F, 0x40, 0x19, 0xD6, 0xF1, 0xD0, 0x11,
            0x01, 0x8B, 0xEC, 0x65, 0x78, 0xB9, 0x08, 0xC0, 0xB5, 0x73, 0x24, 0xDD,
            0x8B, 0x40, 0x1B, 0x95, 0xFD, 0x69, 0x5A, 0x4B, 0xB5, 0xF8, 0x2B, 0x60,
            0xF1, 0x39, 0xC9, 0x94, 0x40, 0x24, 0xC7, 0x86, 0x1B, 0x49, 0xAE, 0xB5,
            0x24
        };

        private const ulong CoreSeal0 = 0xBACC66A9F34593DCUL;
        private const ulong CoreSeal1 = 0xAA779F8BBEC9A54DUL;
        private const ulong CoreSeal2 = 0xBC882D2F8D837A20UL;
        private const ulong CoreSeal3 = 0x270E9519454CD928UL;

        private const ulong DevSeal0 = 0x8030375F9E3DFDE5UL;
        private const ulong DevSeal1 = 0xCA3FDA04F8E6EB6EUL;
        private const ulong DevSeal2 = 0x9A224BAB80B7D285UL;
        private const ulong DevSeal3 = 0xCDC14DAD671B2249UL;

        private static readonly Regex ForeignCreditRegex = new(
            @"(?:Developed\s+by|Created\s+by|Coded\s+by|Programmed\s+by|Developer\s*:)\s*([^\r\n|!\.\-]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string DeveloperId { get; }
        public static string BrandName { get; }
        public static string OfficialCredit { get; }
        public static string WelcomeLoginTalk { get; }
        public static string WelcomeLoginTopLeft { get; }
        public static string WelcomeServerInfo { get; }

        static DeveloperRightsGuard()
        {
            DeveloperId = DecodeVault(EncDeveloperId, 0x91);
            BrandName = DecodeVault(EncBrandName, 0xA7);
            OfficialCredit = DecodeVault(EncOfficialCredit, 0xB3);
            WelcomeLoginTalk = DecodeVault(EncWelcomeTalk, 0xC5);
            WelcomeLoginTopLeft = DecodeVault(EncWelcomeTop, 0xD1);
            WelcomeServerInfo = DecodeVault(EncServerInfo, 0xE3);

            if (!VerifyCryptographicVault())
            {
                FailIntegrity();
            }
        }

        private static string DecodeVault(byte[] data, int seed)
        {
            byte[] buf = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                int key = ((seed + (i * 0x3D)) ^ (0x5A + (i * 7))) & 0xFF;
                buf[i] = (byte)(data[i] ^ key);
            }
            return Encoding.UTF8.GetString(buf);
        }

        private static ulong ReadBigEndianUInt64(ReadOnlySpan<byte> span, int offset)
        {
            return ((ulong)span[offset] << 56)
                 | ((ulong)span[offset + 1] << 48)
                 | ((ulong)span[offset + 2] << 40)
                 | ((ulong)span[offset + 3] << 32)
                 | ((ulong)span[offset + 4] << 24)
                 | ((ulong)span[offset + 5] << 16)
                 | ((ulong)span[offset + 6] << 8)
                 | span[offset + 7];
        }

        private static bool VerifyDigest(byte[] payload, ulong e0, ulong e1, ulong e2, ulong e3)
        {
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(payload, hash);
            ulong diff = (ReadBigEndianUInt64(hash, 0) ^ e0)
                       | (ReadBigEndianUInt64(hash, 8) ^ e1)
                       | (ReadBigEndianUInt64(hash, 16) ^ e2)
                       | (ReadBigEndianUInt64(hash, 24) ^ e3);
            return diff == 0UL;
        }

        public static bool VerifyCryptographicVault()
        {
            byte[] devBytes = Encoding.UTF8.GetBytes(DeveloperId);
            if (!VerifyDigest(devBytes, DevSeal0, DevSeal1, DevSeal2, DevSeal3))
            {
                return false;
            }

            string composite = string.Concat(BrandName, "::", DeveloperId, "::", OfficialCredit, "::MQ-RIGHTS-SEAL-V6192");
            if (!VerifyDigest(Encoding.UTF8.GetBytes(composite), CoreSeal0, CoreSeal1, CoreSeal2, CoreSeal3))
            {
                return false;
            }

            return WelcomeLoginTalk.Contains(DeveloperId, StringComparison.Ordinal)
                && WelcomeLoginTalk.Contains(BrandName, StringComparison.Ordinal)
                && WelcomeLoginTopLeft.Contains(DeveloperId, StringComparison.Ordinal)
                && WelcomeLoginTopLeft.Contains(BrandName, StringComparison.Ordinal)
                && WelcomeServerInfo.Contains(DeveloperId, StringComparison.Ordinal)
                && WelcomeServerInfo.Contains(BrandName, StringComparison.Ordinal);
        }

        private static bool VerifyAssemblyMetadata(Assembly assembly)
        {
            if (assembly == null)
            {
                return true;
            }

            string asmName = assembly.GetName().Name ?? string.Empty;
            if (!asmName.StartsWith(BrandName, StringComparison.Ordinal)
                && !asmName.StartsWith("Canyon.", StringComparison.Ordinal))
            {
                return true;
            }

            if (asmName.EndsWith(".Tests", StringComparison.Ordinal))
            {
                return true;
            }

            string company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
            string copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
            string product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? string.Empty;

            return string.Equals(company, DeveloperId, StringComparison.Ordinal)
                && copyright.Contains(DeveloperId, StringComparison.Ordinal)
                && product.StartsWith(BrandName, StringComparison.Ordinal);
        }

        public static void EnforceOrTerminate(Assembly callerAssembly = null)
        {
            if (!VerifyCryptographicVault()
                || !VerifyAssemblyMetadata(typeof(DeveloperRightsGuard).Assembly)
                || !VerifyAssemblyMetadata(callerAssembly)
                || !VerifyAssemblyMetadata(Assembly.GetEntryAssembly()))
            {
                FailIntegrity();
            }
        }

        public static void VerifyResourceStrings(string loginTalk, string loginTopLeft, string serverInfo)
        {
            EnforceOrTerminate(Assembly.GetCallingAssembly());
            if (string.IsNullOrWhiteSpace(loginTalk)
                || string.IsNullOrWhiteSpace(loginTopLeft)
                || string.IsNullOrWhiteSpace(serverInfo)
                || !loginTalk.Contains(DeveloperId, StringComparison.Ordinal)
                || !loginTalk.Contains(BrandName, StringComparison.Ordinal)
                || !loginTopLeft.Contains(DeveloperId, StringComparison.Ordinal)
                || !loginTopLeft.Contains(BrandName, StringComparison.Ordinal)
                || !serverInfo.Contains(DeveloperId, StringComparison.Ordinal)
                || !serverInfo.Contains(BrandName, StringComparison.Ordinal))
            {
                FailIntegrity();
            }
        }

        public static void PrintProtectedBanner(string serviceRole, string version)
        {
            EnforceOrTerminate(Assembly.GetCallingAssembly());
            Console.WriteLine($"\t{BrandName}: {serviceRole}");
            Console.WriteLine($"\t\t{OfficialCredit} | Copyright 2026-{DateTime.Now:yyyy}");
            Console.WriteLine($"\t\tVersion: {version}");
            Console.WriteLine($"\tAll Rights Reserved - {BrandName} ({DeveloperId})");
            Console.WriteLine();
        }

        public static string FormatWelcomeTopLeft(string playerName)
        {
            EnforceOrTerminate();
            return string.Format(WelcomeLoginTopLeft, playerName);
        }

        public static string FormatServerInfo(int onlinePlayers)
        {
            EnforceOrTerminate();
            return string.Format(WelcomeServerInfo, onlinePlayers);
        }

        public static string SanitizeActionCreditText(uint actionId, uint actionType, string param)
        {
            if (actionId == 1000000)
            {
                return WelcomeLoginTalk;
            }

            if (string.IsNullOrEmpty(param))
            {
                return param;
            }

            if (actionType == 1041 && param.Contains("worldconquer.online", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string result = param;
            if (result.Contains("https://worldconquer.online/Shop", StringComparison.OrdinalIgnoreCase))
                result = result.Replace("https://worldconquer.online/Shop", BrandName, StringComparison.OrdinalIgnoreCase);
            if (result.Contains("https://worldconquer.online.", StringComparison.OrdinalIgnoreCase))
                result = result.Replace("https://worldconquer.online.", BrandName + ".", StringComparison.OrdinalIgnoreCase);
            if (result.Contains("https://worldconquer.online/", StringComparison.OrdinalIgnoreCase))
                result = result.Replace("https://worldconquer.online/", BrandName, StringComparison.OrdinalIgnoreCase);
            if (result.Contains("https://worldconquer.online", StringComparison.OrdinalIgnoreCase))
                result = result.Replace("https://worldconquer.online", BrandName, StringComparison.OrdinalIgnoreCase);
            if (result.Contains("World Conquer Online", StringComparison.OrdinalIgnoreCase))
                result = result.Replace("World Conquer Online", BrandName, StringComparison.OrdinalIgnoreCase);
            if (result.Contains("World Conquer", StringComparison.OrdinalIgnoreCase))
                result = result.Replace("World Conquer", BrandName, StringComparison.OrdinalIgnoreCase);
            if (result.Contains("world of Conquer", StringComparison.OrdinalIgnoreCase))
                result = result.Replace("world of Conquer", BrandName, StringComparison.OrdinalIgnoreCase);

            if (ForeignCreditRegex.IsMatch(result))
            {
                result = ForeignCreditRegex.Replace(result, match =>
                {
                    string claimed = match.Groups[1].Value.Trim();
                    if (claimed.Contains(DeveloperId, StringComparison.OrdinalIgnoreCase))
                    {
                        return match.Value;
                    }
                    return OfficialCredit;
                });
            }

            return result;
        }

        private static void FailIntegrity()
        {
            try
            {
                Console.Error.WriteLine("[SECURITY] Critical integrity violation: Official developer rights metadata or cryptographic seal was tampered with. Execution halted.");
            }
            catch
            {
            }
            Environment.Exit(77);
        }
    }
}
