using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinLSS
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        SettingsPageDescriptor Descriptor { get; set; }

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Descriptor = (SettingsPageDescriptor)e.Parameter;
            PrivateMissionSwitch.IsOn = Descriptor.DisplayPrivateMissions;
            AllianceMissionSwitch.IsOn = Descriptor.DisplayAllianceMissions;
        }

        void PrivateMissionsToggled(object sender, RoutedEventArgs args)
        {
            Descriptor.DisplayPrivateMissions = PrivateMissionSwitch.IsOn;
            Descriptor.FilterMissions();
        }

        void AllianceMissionsToggled(object sender, RoutedEventArgs args)
        {
            Descriptor.DisplayAllianceMissions = AllianceMissionSwitch.IsOn;
            Descriptor.FilterMissions();
        }
    }

    public class SettingsPageDescriptor
    {
        public bool DisplayPrivateMissions { get; set; }
        public bool DisplayAllianceMissions { get; set; }
        public Action FilterMissions { get; set; }
    }
}
