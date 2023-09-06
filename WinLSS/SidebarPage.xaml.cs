using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Mapsui;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;

namespace WinLSS
{
    public sealed partial class SidebarPage : Page
    {
        SidebarDescriptor Descriptor { get; set; }
        readonly SettingsPageDescriptor Settings;
        readonly MissionListPageDescriptor MissionList;

        public SidebarPage()
        {
            this.InitializeComponent();

            Settings = new SettingsPageDescriptor
            {
                DisplayAllianceMissions = true,
                DisplayPrivateMissions = true,
                FilterMissions = () => {}
            };

            MissionList = new MissionListPageDescriptor
            {
                DisplayedMissions = new(),
                SelectionCallback = (_) => {}
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Descriptor = e.Parameter as SidebarDescriptor;
            MissionList.SelectionCallback = Descriptor.SelectMission;
            MissionList.DisplayedMissions = Descriptor.DisplayedMissions;
            MissionList.SelectionCallback = Descriptor.SelectMission;
            Settings.FilterMissions = FilterMissions;
            ContentFrame.Navigate(typeof(MissionListPage), MissionList);
        }

        private void FilterMissions()
        {
            Descriptor.DisplayedMissions.Clear();
            foreach (Mission mission in Descriptor.Missions.Values)
            {
                if (mission.ShouldDisplay(Settings, Descriptor.UserId))
                {
                    Descriptor.DisplayedMissions.Add(mission);
                }
            }
        }

        private void Navigate(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer.Tag.ToString() == "Missions")
            {
                ContentFrame.Navigate(typeof(MissionListPage), MissionList);
            }
            else
            {
                ContentFrame.Navigate(typeof(SettingsPage), Settings);
            }
        }
    }

    class SidebarDescriptor
    {
        public ObservableCollection<Mission> DisplayedMissions { get; set; }
        public Dictionary<long, Mission> Missions { get; set; }
        public Action<Mission> SelectMission { get; set; }
        public uint UserId { get; set; }
    }
}