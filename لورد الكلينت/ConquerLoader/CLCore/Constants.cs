using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CLCore.Models;

namespace CLCore
{
	public static class Constants
	{
		//The folder name which contains the plugin DLLs
		public const string PluginsFolderName = "Plugins";
		public static string LicenseKey = null;
		public static string ClientPath = null;
		public static System.ComponentModel.BackgroundWorker MainWorker = null;
		public static bool CloseOnFinish = false;
		public static bool HideInTrayOnFinish = false;
		public static int MinVersionCreateFlashFix = 5717;
        public static int MinVersionUseRAWServerDat = 5095;
        public static int MaxVersionUseRAWServerDat = 6736;
		public static string LockConfigurationKey { get { return DeveloperRightsGuard.ConfigLockKey; } }
		public const string LegacyLockConfigurationKey = "CONQUERLOADERDFX";
		public static int MinVersionUseDX8DX9Folders = 6371;
		public static bool EnableCLServerConnections = false; // standalone local/Hamachi mode
        public static bool ForceServerDat = false;
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

        private static readonly byte[] EncLoaderDesc =
        {
            0x62, 0xB3, 0xFF, 0x28, 0x7E, 0xBC, 0x00, 0xC5, 0xED, 0x48, 0x16, 0xDE,
            0x93, 0x74, 0x04, 0xAC, 0xE8, 0x3B, 0x39, 0x66, 0xBC, 0xF8, 0x24, 0x29,
            0x61, 0x04, 0xCC, 0x9F, 0x5E, 0x2D, 0xD4, 0x9B, 0x0D, 0x3D, 0x8F, 0xB7,
            0x6C, 0x2C, 0xD3, 0x80, 0xA3
        };

        private static readonly byte[] EncLoaderEdition =
        {
            0x80, 0xC2, 0x19, 0x59, 0x6E, 0xB5, 0x53, 0x99, 0xC3, 0x74, 0x27, 0xE6,
            0xAE, 0x79, 0x17, 0xFF, 0xD6, 0x23, 0x62, 0xBB, 0xC2, 0x08, 0x5D, 0xC5,
            0x63, 0x3A, 0xC4, 0x80, 0x45, 0x06, 0xF6, 0xBA, 0x76, 0x49, 0xCB, 0xEC,
            0x27, 0x66, 0x80, 0xCC, 0x0A, 0x54, 0x20, 0x6F, 0xDD, 0xE1, 0xD2, 0x26,
            0x61
        };

        private static readonly byte[] EncLoaderRights =
        {
            0x70, 0xA5, 0xE1, 0x6D, 0x7B, 0x88, 0x3A, 0xF5, 0xB5, 0x7A, 0x4D, 0xFF,
            0x8C, 0x42, 0x18, 0x4F, 0x87, 0xCC, 0x09, 0x0D, 0xE4, 0xA1, 0x3C, 0x75,
            0x4C, 0x0C, 0xE9, 0xB7, 0x68, 0x19, 0xCF, 0x9C, 0x5F
        };

        private static readonly byte[] EncLoaderTray =
        {
            0x4A, 0x94, 0xD0, 0x15, 0x76, 0x86, 0x2A, 0xE6, 0xB9, 0x3B, 0x50, 0x3D,
            0xF1, 0xAA, 0x6C, 0x5A, 0x86, 0xD7, 0x5F, 0x77, 0xA6, 0xC6, 0x01, 0x40,
            0x7F, 0x3E, 0xED, 0xFB, 0x3A, 0x43, 0xEB, 0x96, 0xB1, 0x1E, 0x53, 0x94,
            0xD7, 0x16, 0x6B, 0xE3, 0xF5, 0x22, 0xFF, 0x7A, 0x3F, 0xEE, 0xAA, 0x57,
            0x1D, 0xDA, 0x97, 0x49, 0x66, 0x5D
        };

        private static readonly byte[] EncConfigLockKey =
        {
            0x58, 0xBC, 0x82, 0x28, 0x5D, 0x90, 0x5C, 0x15, 0xFF, 0x8C, 0x79, 0x3B,
            0xE4, 0xB3, 0x3A, 0x13, 0xE5, 0xBF, 0x77, 0x2A, 0xB6, 0xDB, 0x1E, 0x7D,
            0x6A, 0x2E, 0xFA, 0xFF, 0xD3, 0x2C, 0x60, 0xB3
        };

        private const ulong CoreSeal0 = 0xBACC66A9F34593DCUL;
        private const ulong CoreSeal1 = 0xAA779F8BBEC9A54DUL;
        private const ulong CoreSeal2 = 0xBC882D2F8D837A20UL;
        private const ulong CoreSeal3 = 0x270E9519454CD928UL;

        private const ulong DevSeal0 = 0x8030375F9E3DFDE5UL;
        private const ulong DevSeal1 = 0xCA3FDA04F8E6EB6EUL;
        private const ulong DevSeal2 = 0x9A224BAB80B7D285UL;
        private const ulong DevSeal3 = 0xCDC14DAD671B2249UL;

        private static readonly Regex ForeignCreditRegex = new Regex(
            @"(?:Developed\s+by|Created\s+by|Coded\s+by|Programmed\s+by|Developer\s*:)\s*([^\r\n|!\.\-]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string DeveloperId { get; private set; }
        public static string BrandName { get; private set; }
        public static string OfficialCredit { get; private set; }
        public static string LoaderDescription { get; private set; }
        public static string LoaderEdition { get; private set; }
        public static string LoaderRights { get; private set; }
        public static string LoaderTray { get; private set; }
        internal static string ConfigLockKey { get; private set; }

        static DeveloperRightsGuard()
        {
            DeveloperId = DecodeVault(EncDeveloperId, 0x91);
            BrandName = DecodeVault(EncBrandName, 0xA7);
            OfficialCredit = DecodeVault(EncOfficialCredit, 0xB3);
            LoaderDescription = DecodeVault(EncLoaderDesc, 0x77);
            LoaderEdition = DecodeVault(EncLoaderEdition, 0x89);
            LoaderRights = DecodeVault(EncLoaderRights, 0x6B);
            LoaderTray = DecodeVault(EncLoaderTray, 0x5D);
            ConfigLockKey = DecodeVault(EncConfigLockKey, 0x4F);

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

        private static ulong ReadBigEndianUInt64(byte[] buf, int offset)
        {
            return ((ulong)buf[offset] << 56)
                 | ((ulong)buf[offset + 1] << 48)
                 | ((ulong)buf[offset + 2] << 40)
                 | ((ulong)buf[offset + 3] << 32)
                 | ((ulong)buf[offset + 4] << 24)
                 | ((ulong)buf[offset + 5] << 16)
                 | ((ulong)buf[offset + 6] << 8)
                 | buf[offset + 7];
        }

        private static bool VerifyDigest(byte[] payload, ulong e0, ulong e1, ulong e2, ulong e3)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(payload);
                ulong diff = (ReadBigEndianUInt64(hash, 0) ^ e0)
                           | (ReadBigEndianUInt64(hash, 8) ^ e1)
                           | (ReadBigEndianUInt64(hash, 16) ^ e2)
                           | (ReadBigEndianUInt64(hash, 24) ^ e3);
                return diff == 0UL;
            }
        }

        public static bool VerifyCryptographicVault()
        {
            if (!VerifyDigest(Encoding.UTF8.GetBytes(DeveloperId), DevSeal0, DevSeal1, DevSeal2, DevSeal3))
            {
                return false;
            }

            string composite = string.Concat(BrandName, "::", DeveloperId, "::", OfficialCredit, "::MQ-RIGHTS-SEAL-V6192");
            if (!VerifyDigest(Encoding.UTF8.GetBytes(composite), CoreSeal0, CoreSeal1, CoreSeal2, CoreSeal3))
            {
                return false;
            }

            return LoaderRights.IndexOf(DeveloperId, StringComparison.Ordinal) >= 0
                && LoaderTray.IndexOf(DeveloperId, StringComparison.Ordinal) >= 0
                && LoaderDescription.IndexOf(BrandName, StringComparison.Ordinal) >= 0
                && ConfigLockKey.Length == 32;
        }

        public static bool VerifyAssemblyMetadata(Assembly assembly)
        {
            if (assembly == null)
            {
                return true;
            }

            object[] compAttrs = assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
            object[] copyAttrs = assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
            object[] prodAttrs = assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false);

            string company = compAttrs.Length > 0 ? (((AssemblyCompanyAttribute)compAttrs[0]).Company ?? string.Empty) : string.Empty;
            string copyright = copyAttrs.Length > 0 ? (((AssemblyCopyrightAttribute)copyAttrs[0]).Copyright ?? string.Empty) : string.Empty;
            string product = prodAttrs.Length > 0 ? (((AssemblyProductAttribute)prodAttrs[0]).Product ?? string.Empty) : string.Empty;

            return string.Equals(company, DeveloperId, StringComparison.Ordinal)
                && copyright.IndexOf(DeveloperId, StringComparison.Ordinal) >= 0
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

        public static string SanitizeTitle(string title)
        {
            EnforceOrTerminate();
            if (string.IsNullOrWhiteSpace(title))
            {
                return BrandName;
            }
            string trimmed = title.Trim();
            if (trimmed.IndexOf(BrandName, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return BrandName + " - " + trimmed;
            }
            return trimmed;
        }

        public static string SanitizeDescription(string description)
        {
            EnforceOrTerminate();
            string defaultBanner = "Welcome to " + BrandName + " (V6192) - " + OfficialCredit;
            if (string.IsNullOrWhiteSpace(description))
            {
                return defaultBanner;
            }

            string clean = description.Trim();
            if (ForeignCreditRegex.IsMatch(clean))
            {
                clean = ForeignCreditRegex.Replace(clean, m =>
                {
                    string who = m.Groups[1].Value.Trim();
                    return who.IndexOf(DeveloperId, StringComparison.OrdinalIgnoreCase) >= 0
                        ? m.Value
                        : OfficialCredit;
                });
            }

            if (clean.IndexOf(DeveloperId, StringComparison.OrdinalIgnoreCase) < 0)
            {
                clean = clean + " | " + OfficialCredit;
            }

            return clean;
        }

        public static void SanitizeConfig(LoaderConfig config)
        {
            if (config == null)
            {
                return;
            }
            config.Title = SanitizeTitle(config.Title);
            config.Description = SanitizeDescription(config.Description);
        }

        private static void FailIntegrity()
        {
            try
            {
                MessageBox.Show(
                    "Critical Integrity Violation: Official developer rights metadata or cryptographic seal was modified. Launcher execution is blocked.",
                    "Security Integrity Protection",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
            Environment.Exit(77);
        }
    }

	public static class CLTheme
	{
        public static Control.ControlCollection MainControls = null;
        public static object MainForm = null;
    }
	public enum PluginType
	{
		FREE,
		PREMIUM
	}
}
