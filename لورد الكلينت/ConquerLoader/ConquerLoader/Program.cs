using System;
using System.IO;
using System.Windows;
using CLCore;
using CLCore.Models;
using ConquerLoader.Forms.WPF;

namespace ConquerLoader
{
    static class Program
    {
        /// <summary>
        /// Main entry point for MoonQueen Launcher.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Core.EnforceDeveloperRightsOrExit();
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0 && string.Equals(args[0], "--lock-config", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(Core.ConfigJsonPath))
                {
                    MessageBox.Show("config.json was not found in the current folder.", DeveloperRightsGuard.BrandName, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                LoaderConfig cfg = Newtonsoft.Json.JsonConvert.DeserializeObject<LoaderConfig>(File.ReadAllText(Core.ConfigJsonPath));
                DeveloperRightsGuard.SanitizeConfig(cfg);
                string encrypted = ConfigFilesEncryption.AESEncription.EncryptString(
                    Constants.LockConfigurationKey,
                    Newtonsoft.Json.JsonConvert.SerializeObject(cfg, Newtonsoft.Json.Formatting.Indented));
                File.WriteAllText(Core.ConfigJsonPath + ".lock", encrypted);
                MessageBox.Show(
                    "Encrypted config.json.lock has been generated and bound to developer " + DeveloperRightsGuard.DeveloperId + ".\nRemove plain config.json before distributing the client to players.",
                    DeveloperRightsGuard.BrandName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(new MainLite());
        }
    }
}
