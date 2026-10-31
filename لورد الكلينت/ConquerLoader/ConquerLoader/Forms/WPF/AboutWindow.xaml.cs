using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace ConquerLoader.Forms.WPF
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            Loaded += AboutWindow_Loaded;
            StateChanged += AboutWindow_StateChanged;
            UpdateChromeButtons();
        }

        private void AboutWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            lblAboutVersionN.Text = "v" + fvi.ProductVersion;
            ApplyTranslations();
        }

        private void BtnChangelog_Click(object sender, RoutedEventArgs e)
        {
            // External URL disabled for MoonQueen
        }

        private void BtnWebsite_Click(object sender, RoutedEventArgs e)
        {
            // External URL disabled for MoonQueen
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void BrandCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // External URL disabled for MoonQueen
        }

        private static void OpenUrl(string url)
        {
            Process.Start(url);
        }

        private void AboutWindow_StateChanged(object sender, System.EventArgs e)
        {
            UpdateChromeButtons();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is Button)
                {
                    return;
                }
                source = VisualTreeHelper.GetParent(source);
            }

            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
                return;
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void BtnMinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
            UpdateChromeButtons();
        }

        private void BtnMaxRestoreWindow_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void BtnCloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleMaximizeRestore()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            UpdateChromeButtons();
        }

        private void UpdateChromeButtons()
        {
            if (btnMaxRestoreWindow == null)
            {
                return;
            }

            btnMinimizeWindow.Content = "\uE921";
            btnMinimizeWindow.ToolTip = T("chromeMinimize", "Minimize");
            btnCloseWindow.Content = "\uE8BB";
            btnCloseWindow.Tag = "Close";
            btnCloseWindow.ToolTip = T("chromeClose", "Close");
            btnMaxRestoreWindow.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            btnMaxRestoreWindow.ToolTip = WindowState == WindowState.Maximized
                ? T("chromeRestore", "Restore")
                : T("chromeMaximize", "Maximize");
        }

        private void ApplyTranslations()
        {
            Title = T("aboutWindowTitle", "About");
            txtWindowTitleBar.Text = T("aboutWindowTitle", "About");
            txtAboutHeading.Text = T("aboutHeading", "About MoonQueen");
            txtAboutDescription.Text = T("aboutDescription", "Official MoonQueen Game Launcher (V6192).");
            txtAboutCreatedBy.Text = T("aboutCreatedBy", "Developed by ahmedzahran");
            txtAboutPortuguese.Text = T("aboutPortuguese", "Server & Client Edition: MoonQueen Classic+ V6192");
            txtAboutTesters.Text = T("aboutTesters", "All Rights Reserved - ahmedzahran");
            txtAboutVersionLabel.Text = T("aboutVersionLabel", "Version:");
            txtAboutLinks.Text = T("aboutLinks", "Details");
            runWebsiteLabel.Text = T("aboutWebsiteLabel", "Server: ");
            runChangelogLabel.Text = T("aboutChangelogLabel", "Developer: ");
        }

        private string T(string key, string fallback)
        {
            return Core.TranslateText(key, fallback);
        }
    }
}
