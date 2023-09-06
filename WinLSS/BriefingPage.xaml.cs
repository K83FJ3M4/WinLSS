using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using Mapsui.Layers;
using Mapsui.UI.WinUI;
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
    public sealed partial class BriefingPage : Page
    {
        private Collection<uint> VehicleIds = new();
        public BriefingDescriptor Descriptor { get; set; }

        public BriefingPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Descriptor = (BriefingDescriptor)e.Parameter;
            Vehicles.ItemsSource = Descriptor.Vehicles.Values;
        }

        void Close(object sender, RoutedEventArgs e)
        {
            Descriptor.Close();
        }

        private void SelectVehicle(object sender, SelectionChangedEventArgs e)
        {
            foreach (Vehicle vehicle in e.AddedItems)
            {
                VehicleIds.Add(vehicle.Id);
            }

            foreach (Vehicle vehicle in e.RemovedItems)
            {
                VehicleIds.Remove(vehicle.Id);
            }
        }

        private async void Submit(object sender, RoutedEventArgs e)
        {
            ErrorMessage.Visibility = Visibility.Collapsed;
            SuccessMessage.Visibility = Visibility.Collapsed;

            List<KeyValuePair<string, string>> Keys = new(VehicleIds.Count)
            {
                new KeyValuePair<string, string>("utf8", "✓"),
                new KeyValuePair<string, string>("authenticity_token", Descriptor.CsrfToken),
                new KeyValuePair<string, string>("commit", "Alarmieren"),
                new KeyValuePair<string, string>("next_mission", "0"),
                new KeyValuePair<string, string>("alliance_mission_publish", "0"),
            };

            foreach (uint Id in VehicleIds)
            {
                Keys.Add(new KeyValuePair<string, string>("vehicle_ids[]", Id.ToString()));
            }

            FormUrlEncodedContent content = new(Keys);
            var response = await Descriptor.HttpClient.PostAsync($"missions/{Descriptor.Mission.Id}/alarm", content);

            if (response.IsSuccessStatusCode)
            {
                SuccessMessageText.Text = $"Successfully alarmed {VehicleIds.Count} vehicles";
                SuccessMessage.Visibility = Visibility.Visible;
            } else
            {
                ErrorMessageText.Text = $"Failed to alarm vehicles: {response.ReasonPhrase}";
                ErrorMessage.Visibility = Visibility.Visible;
            }
        }
    }

    public class BriefingDescriptor
    {
        public Dictionary<uint, Vehicle> Vehicles { get; set; }
        public HttpClient HttpClient { get; set; }
        public Mission Mission { get; set; }
        public Action Close { get; set; }
        public string CsrfToken { get; set; }
    }
}
