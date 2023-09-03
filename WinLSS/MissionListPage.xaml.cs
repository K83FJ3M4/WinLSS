using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinLSS
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MissionListPage : Page
    {
        MissionListPageDescriptor Descriptor { get; set; }

        public MissionListPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Descriptor = (MissionListPageDescriptor)e.Parameter;
            MissionList.ItemsSource = Descriptor.DisplayedMissions;
        }

        private void MissionViewSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (args.AddedItems.Count == 0)
            {
                return;
            }

            Mission mission = args.AddedItems[0] as Mission;
            Descriptor.SelectionCallback(mission);
        }
    }

    class MissionListPageDescriptor
    {
        public ObservableCollection<Mission> DisplayedMissions { get; set; }
        public Action<Mission> SelectionCallback { get; set; }
    }
}
