using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UniGetUI.Core.Language;
using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.PackageClasses;
using UniGetUI.PackageEngine.Serializable;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UniGetUI.Interface.Dialogs
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class InstallOptionsPage : Page
    {
        public SerializableInstallationOptions Options;
        public IPackage Package;
        public event EventHandler? Close;
        private readonly OperationType Operation;
        private readonly string packageInstallLocation;
        private bool _uiLoaded;

        public InstallOptionsPage(IPackage package, SerializableInstallationOptions options) : this(package, OperationType.None, options) { }
        public InstallOptionsPage(IPackage package, OperationType operation, SerializableInstallationOptions options)
        {
            Package = package;
            InitializeComponent();
            Operation = operation;
            Options = options;

            packageInstallLocation = Package.Manager.DetailsHelper.GetInstallLocation(package) ?? CoreTools.Translate("Unset or unknown");

            AdminCheckBox.IsChecked = Options.RunAsAdministrator;
            AdminCheckBox.IsEnabled = Package.Manager.Capabilities.CanRunAsAdmin;

            InteractiveCheckBox.IsChecked = Options.InteractiveInstallation;
            InteractiveCheckBox.IsEnabled = Package.Manager.Capabilities.CanRunInteractively;

            HashCheckbox.IsChecked = Options.SkipHashCheck;
            HashCheckbox.IsEnabled = operation != OperationType.Uninstall && Package.Manager.Capabilities.CanSkipIntegrityChecks;

            ArchitectureComboBox.IsEnabled = operation != OperationType.Uninstall && Package.Manager.Capabilities.SupportsCustomArchitectures;
            ArchitectureComboBox.Items.Add(CoreTools.Translate("Default"));
            ArchitectureComboBox.SelectedIndex = 0;

            if (Package.Manager.Capabilities.SupportsCustomArchitectures)
            {
                foreach (Architecture arch in Package.Manager.Capabilities.SupportedCustomArchitectures)
                {
                    ArchitectureComboBox.Items.Add(CommonTranslations.ArchNames[arch]);
                    if (Options.Architecture == CommonTranslations.ArchNames[arch])
                    {
                        ArchitectureComboBox.SelectedValue = CommonTranslations.ArchNames[arch];
                    }
                }
            }

            VersionComboBox.IsEnabled =
                (operation == OperationType.Install
                    || operation == OperationType.None)
                && (Package.Manager.Capabilities.SupportsCustomVersions
                    || Package.Manager.Capabilities.SupportsPreRelease);

            VersionComboBox.SelectionChanged += (_, _) =>
            {
                IgnoreUpdatesCheckbox.IsChecked =
                    !(new []
                    {
                        CoreTools.Translate("Latest"),
                        CoreTools.Translate("PreRelease"),
                        ""
                    }.Contains(VersionComboBox.SelectedValue.ToString()));
            };

            VersionComboBox.Items.Add(CoreTools.Translate("Latest"));
            VersionComboBox.SelectedIndex = 0;
            if (package.Manager.Capabilities.SupportsPreRelease)
            {
                VersionComboBox.Items.Add(CoreTools.Translate("PreRelease"));
                if (Options.PreRelease)
                {
                    VersionComboBox.SelectedValue = CoreTools.Translate("PreRelease");
                }
            }

            SkipMinorUpdatesCheckbox.IsChecked = Options.SkipMinorUpdates;

            if (Package.Manager.Capabilities.SupportsCustomVersions)
            {
                _ = LoadVersions();
            }
            else
            {
                VersionProgress.Visibility = Visibility.Collapsed;
            }

            ScopeCombo.IsEnabled = Package.Manager.Capabilities.SupportsCustomScopes;
            ScopeCombo.Items.Add(CoreTools.Translate("Default"));
            ScopeCombo.SelectedIndex = 0;
            if (package.Manager.Capabilities.SupportsCustomScopes)
            {
                ScopeCombo.Items.Add(CoreTools.Translate(CommonTranslations.ScopeNames[PackageScope.Local]));
                if (Options.InstallationScope == CommonTranslations.ScopeNames_NonLang[PackageScope.Local])
                {
                    ScopeCombo.SelectedValue = CommonTranslations.ScopeNames[PackageScope.Local];
                }

                ScopeCombo.Items.Add(CoreTools.Translate(CommonTranslations.ScopeNames[PackageScope.Global]));
                if (Options.InstallationScope == CommonTranslations.ScopeNames_NonLang[PackageScope.Global])
                {
                    ScopeCombo.SelectedValue = CommonTranslations.ScopeNames[PackageScope.Global];
                }
            }

            ResetDir.IsEnabled = Package.Manager.Capabilities.SupportsCustomLocations;
            SelectDir.IsEnabled = Package.Manager.Capabilities.SupportsCustomLocations;

            if (Options.CustomInstallLocation == "") CustomInstallLocation.Text = packageInstallLocation;
            else CustomInstallLocation.Text = Options.CustomInstallLocation;

            if (Options.CustomParameters.Count != 0)
            {
                CustomParameters.Text = string.Join(' ', Options.CustomParameters);
            }

            _uiLoaded = true;
            GenerateCommand();
            LoadIgnoredUpdates();
        }

        private async void LoadIgnoredUpdates()
        {
            IgnoreUpdatesCheckbox.IsChecked = await Package.GetIgnoredUpdatesVersionAsync() == "*";
        }

        private async Task LoadVersions()
        {
            IgnoreUpdatesCheckbox.IsChecked = await Package.HasUpdatesIgnoredAsync();
            VersionComboBox.IsEnabled = false;

            IReadOnlyList<string> versions = await Task.Run(() => Package.Manager.DetailsHelper.GetVersions(Package));
            foreach (string ver in versions)
            {
                VersionComboBox.Items.Add(ver);
                if (Options.Version == ver)
                {
                    VersionComboBox.SelectedValue = ver;
                }
            }

            VersionComboBox.IsEnabled =
                (Operation == OperationType.Install
                 || Operation == OperationType.None)
                && (Package.Manager.Capabilities.SupportsCustomVersions
                    || Package.Manager.Capabilities.SupportsPreRelease);
            VersionProgress.Visibility = Visibility.Collapsed;
        }

        public async Task<SerializableInstallationOptions> GetUpdatedOptions(bool updateIgnoredUpdates = true)
        {
            Options.RunAsAdministrator = AdminCheckBox?.IsChecked ?? false;
            Options.InteractiveInstallation = InteractiveCheckBox?.IsChecked ?? false;
            Options.SkipHashCheck = HashCheckbox?.IsChecked ?? false;

            if (CommonTranslations.InvertedArchNames.ContainsKey(ArchitectureComboBox.SelectedValue.ToString() ?? ""))
            {
                Options.Architecture = ArchitectureComboBox.SelectedValue.ToString() ?? "";
            }
            else
            {
                Options.Architecture = "";
            }

            if (CommonTranslations.InvertedScopeNames.ContainsKey(ScopeCombo.SelectedValue.ToString() ?? ""))
            {
                Options.InstallationScope = CommonTranslations.ScopeNames_NonLang[CommonTranslations.InvertedScopeNames[ScopeCombo.SelectedValue.ToString() ?? ""]];
            }
            else
            {
                Options.InstallationScope = "";
            }

            if (CustomInstallLocation.Text == packageInstallLocation) Options.CustomInstallLocation = "";
            else Options.CustomInstallLocation = CustomInstallLocation.Text;

            Options.CustomParameters = CustomParameters.Text.Split(' ').ToList();
            Options.PreRelease = VersionComboBox.SelectedValue.ToString() == CoreTools.Translate("PreRelease");

            if (VersionComboBox.SelectedValue.ToString() != CoreTools.Translate("PreRelease") && VersionComboBox.SelectedValue.ToString() != CoreTools.Translate("Latest"))
            {
                Options.Version = VersionComboBox.SelectedValue.ToString() ?? "";
            }
            else
            {
                Options.Version = "";
            }
            Options.SkipMinorUpdates = SkipMinorUpdatesCheckbox?.IsChecked ?? false;

            if (updateIgnoredUpdates)
            {
                if (IgnoreUpdatesCheckbox?.IsChecked ?? false)
                {
                    await Package.AddToIgnoredUpdatesAsync(version: "*");
                }
                else
                {
                    if (await Package.GetIgnoredUpdatesVersionAsync() == "*")
                    {
                        await Package.RemoveFromIgnoredUpdatesAsync();
                    }
                }
            }
            return Options;
        }

        private void SelectDir_Click(object sender, RoutedEventArgs e)
        {
            ExternalLibraries.Pickers.FolderPicker openPicker = new(MainApp.Instance.MainWindow.GetWindowHandle());
            string folder = openPicker.Show();
            if (folder != string.Empty)
            {
                CustomInstallLocation.Text = folder;
            }
            GenerateCommand();
        }

        private void ResetDir_Click(object sender, RoutedEventArgs e)
        {
            CustomInstallLocation.Text = packageInstallLocation;
            GenerateCommand();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close?.Invoke(this, EventArgs.Empty);
        }

        private void CustomParameters_TextChanged(object sender, TextChangedEventArgs e) => GenerateCommand();
        private void ScopeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => GenerateCommand();
        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => GenerateCommand();
        private void AdminCheckBox_Click(object sender, RoutedEventArgs e) => GenerateCommand();
        private void InteractiveCheckBox_Click(object sender, RoutedEventArgs e) => GenerateCommand();
        private void HashCheckbox_Click(object sender, RoutedEventArgs e) => GenerateCommand();
        private void ArchitectureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => GenerateCommand();


        private async void GenerateCommand()
        {
            if (!_uiLoaded) return;
            InstallationOptions io = InstallationOptions.FromSerialized(await GetUpdatedOptions(updateIgnoredUpdates: false), Package);
            var op = Operation;
            if (op is OperationType.None) op = OperationType.Install;
            var commandline = await Task.Run(() => Package.Manager.OperationHelper.GetParameters(Package, io, op));
            CommandBox.Text = Package.Manager.Properties.ExecutableFriendlyName + " " + string.Join(" ", commandline);
        }

        private void LayoutGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(LayoutGrid.ActualSize.Y > 1 && LayoutGrid.ActualSize.Y < double.PositiveInfinity) MaxHeight = LayoutGrid.ActualSize.Y;
        }
    }
}
