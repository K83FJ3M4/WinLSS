using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Mapsui.Animations;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using System.Net.Http;
using Mapsui.Extensions;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinLSS
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const string ADDRESS = "https://www.leitstellenspiel.de/";
        const string CLIENT_ID = "BAhJIilkNWYxZjUzMi01MjE0LTQxYjctYWI2NS0yYTY0MmEyZGNhMmEGOgZFRg%3D%3D--72cc052194965999a2fe5def658c52dd025b9c0b";
        const string SESSION_ID = "73df60a7d51b3778e5c0751b9edf5f7c";

        [GeneratedRegex("missionMarkerAdd\\(([^)]+)\\);")]
        private static partial Regex MissionRegex();

        Mission SelectedMission { get; set; }
        string CsrfToken;
        readonly HttpClient HttpClient = new();

        readonly ObservableCollection<Vehicle> Vehicles = new();
        readonly ObservableCollection<Mission> DisplayedMissions = new();
        readonly Collection<Mission> Missions = new();

        readonly SettingsPageDescriptor Settings;
        readonly MissionListPageDescriptor MissionList;
        readonly MapDescriptor Map;

        protected override void OnNavigatedTo(NavigationEventArgs args)
        {
            Initialize initialize = args.Parameter as Initialize;
            UserId.Text = initialize.UserId.ToString();
            Username.Text = initialize.Username;
            CsrfToken = initialize.CsrfToken;
        }

        public MainPage()
        {
            this.InitializeComponent();

            Settings = new SettingsPageDescriptor
            {
                DisplayAllianceMissions = true,
                DisplayPrivateMissions = true,
                FilterMissions = () => FilterMissions()
            };

            MissionList = new MissionListPageDescriptor
            {
                DisplayedMissions = DisplayedMissions,
                SelectionCallback = (mission) => SelectMission(mission)
            };

            Map = new MapDescriptor
            {
                DisplayedMissions = DisplayedMissions,
                SelectionCallback = (mission) => SelectMission(mission)
            };

            MainContent.Navigate(typeof(MapPage), Map);
            ContentFrame.Navigate(typeof(MissionListPage), MissionList);

            //_ = DispatcherQueue.TryEnqueue(AsyncSetup);
        }

        private void SelectMission(Mission mission)
        {
            if (MainContent.Content.GetType() != typeof(MapPage))
            {
                InspectMission(null, null);
            }
            else
            {
                Map.Map.Navigator.CenterOnAndZoomTo(
                    SphericalMercator.FromLonLat(mission.Longitude, mission.Latitude).ToMPoint(),
                    10.0,
                    500,
                    Easing.CubicInOut
                );
                MissionTip.IsOpen = true;
                MissionTip.PreferredPlacement = TeachingTipPlacementMode.BottomRight;
                MissionTip.Title = mission.Caption;
                MissionTip.Subtitle = mission.Address;
                SelectedMission = mission;
            }
        }

        private void InspectMission(object sender, object args)
        {
            BriefingDescriptor Briefing = new()
            {
                Mission = SelectedMission,
                Close = () => MainContent.Navigate(typeof(MapPage), Map),
                Vehicles = Vehicles,
                HttpClient = HttpClient,
                CsrfToken = CsrfToken
            };

            MissionTip.IsOpen = false;
            MainContent.Navigate(typeof(BriefingPage), Briefing);
        }

        private void FilterMissions()
        {
            DisplayedMissions.Clear();
            uint userId = uint.Parse(UserId.Text);
            foreach (Mission mission in Missions)
            {
                if (mission.ShouldDisplay(Settings, userId))
                {
                    DisplayedMissions.Add(mission);
                }
            }
            Map.Map.RefreshGraphics();
        }

        private async Task LoadVehicles(HttpClient client)
        {
            string json = await client.GetStringAsync(ADDRESS + "api/vehicles");
            Vehicle[] vehicles = JsonSerializer.Deserialize<Vehicle[]>(json);
            foreach (Vehicle vehicle in vehicles)
            {
                Vehicles.Add(vehicle);
            }

            await Task.CompletedTask;
        }

        private void ScanForMissionMarkers(string text)
        {
            MatchCollection mission_marker_match = MissionRegex().Matches(text);
            uint userId = uint.Parse(UserId.Text);

            foreach (Match mission_marker in mission_marker_match.Cast<Match>())
            {
                Mission mission = JsonSerializer.Deserialize<Mission>(mission_marker.Groups[1].Value);
                Missions.Add(mission);
                if (mission.ShouldDisplay(Settings, userId))
                {
                    DisplayedMissions.Add(mission);
                }
            }
            Map.Map.RefreshGraphics();
        }

        private void MissionSelectionChanged(NavigationView sender, NavigationViewItemInvokedEventArgs args)
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

    public class Vehicle
    {
        [JsonPropertyName("id")]
        public uint Id { get; set; }
        [JsonPropertyName("caption")]
        public string Caption { get; set; }
        [JsonPropertyName("building_id")]
        public uint BuildingId { get; set; }
    }

    public class Mission : PointFeature
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public bool ShouldDisplay(SettingsPageDescriptor settings, uint userId)
        {
            if (userId == UserId)
            {
                return settings.DisplayPrivateMissions;
            }
            else
            {
                return settings.DisplayAllianceMissions;
            }
        }

        public Mission(PointFeature pointFeature) : base(pointFeature) { }
        public Mission(MPoint point) : base(point) { }
        public Mission(double x, double y) : base(x, y) { }
        public Mission() : base(0.0, 0.0) { }

        [JsonPropertyName("address")]
        public string Address { get; set; }
        [JsonPropertyName("caption")]
        public string Caption { get; set; }
        [JsonPropertyName("user_id")]
        public uint? UserId { get; set; }
        [JsonPropertyName("alliance_id")]
        public uint? AllianceId { get; set; }
        [JsonPropertyName("id")]
        public long Id { get; set; }
        [JsonPropertyName("longitude")]
        public double Longitude
        {
            get
            {
                return SphericalMercator.ToLonLat(Point.X, 0.0).lon;
            }
            set
            {
                Point.X = SphericalMercator.FromLonLat(value, 0.0).x;
            }
        }
        [JsonPropertyName("latitude")]
        public double Latitude
        {
            get
            {
                return SphericalMercator.ToLonLat(0.0, Point.Y).lat;
            }
            set
            {
                Point.Y = SphericalMercator.FromLonLat(0.0, value).y;
            }
        }
    }
}
