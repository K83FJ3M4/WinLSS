using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Mapsui.Animations;
using Mapsui.Extensions;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Collections.Generic;
using Windows.Devices.Bluetooth.Advertisement;
using System;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;

namespace WinLSS
{

    public sealed partial class MainWindow : Window
    {

        string CsrfToken;
        Mission SelectedMission { get; set; }
        HttpClient HttpClient { get; set; }

        readonly Dictionary<uint, Vehicle> Vehicles = new();
        readonly ObservableCollection<Mission> DisplayedMissions = new();
        readonly Dictionary<long, Mission> Missions = new();
        readonly ObservableCollection<VehicleMovement> VehicleMovements = new();
        
        readonly MapDescriptor Map;
        readonly SidebarDescriptor Sidebar;

        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);

            Map = new MapDescriptor
            {
                DisplayedMissions = DisplayedMissions,
                SelectionCallback = SelectMission,
                VehicleMovement = VehicleMovements
            };

            Sidebar = new SidebarDescriptor
            {
                DisplayedMissions = DisplayedMissions,
                Missions = Missions,
                SelectMission = SelectMission
            };

            DisplayedMissions.CollectionChanged += (_, _) => Map.Refresh();
            MainContent.Navigate(typeof(MapPage), Map);
        }

        public void Initialize(Initialize initialize)
        {
            HttpClient = initialize.HttpClient;
            Sidebar.UserId = initialize.UserId;
            CsrfToken = initialize.CsrfToken;
            SidebarContent.Navigate(typeof(SidebarPage), Sidebar);
        }

        public void AddMissions(Mission[] missions)
        {
            foreach (Mission mission in missions)
            {
                Missions[mission.Id] = mission;
                DisplayedMissions.Add(mission);
            }
        }

        public void AddVehicles(Vehicle[] vehicles)
        {
            foreach (Vehicle vehicle in vehicles)
            {
                Vehicles.Add(vehicle.Id, vehicle);
            }
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

        public void AddVehicleDrives(VehicleDrive[] vehicleDrives)
        {
            foreach (VehicleDrive vehicleDrive in vehicleDrives)
            {
                if (vehicleDrive.Steps == "-1" || vehicleDrive.UserId != Sidebar.UserId) { continue; }

                Debug.WriteLine("Added drive animation");

                VehicleMovement movement = new()
                {
                    CurrentStep = 0,
                    CurrentStepTime = DateTime.UtcNow.AddSeconds(vehicleDrive.DriveDuration),
                    Steps = JsonSerializer.Deserialize<double[][]>(vehicleDrive.Steps)
                };

                Vehicle vehicle = Vehicles[vehicleDrive.Id];
                movement.Interpolate();
                if (vehicle.Movement != null)
                {
                    VehicleMovements.Remove(vehicle.Movement);

                }
                vehicle.Movement = movement;
                VehicleMovements.Add(movement);
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
    }

    public class VehicleMovement : PointFeature
    {

        public VehicleMovement(PointFeature pointFeature) : base(pointFeature) { }
        public VehicleMovement(MPoint point) : base(point) { }
        public VehicleMovement(double x, double y) : base(x, y) { }
        public VehicleMovement() : base(0.0, 0.0) { }

        public double Longditude
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

        public double[][] Steps { get; set; }
        public DateTime CurrentStepTime { get; set; }
        public int CurrentStep { get; set; }

        public bool Interpolate()
        {
            DateTime CurrentTime = DateTime.UtcNow;

            while (true)
            {
                if (!(CurrentStep < Steps.Length - 1)) { return false; }
                double offsetStep = (CurrentTime - CurrentStepTime).TotalSeconds / Steps[CurrentStep][2];

                if (offsetStep >= 1)
                {
                    CurrentStepTime = CurrentStepTime.AddSeconds(Steps[CurrentStep][2]);
                    CurrentStep++;
                    continue;
                }

                double LatitudeDiff = Steps[CurrentStep + 1][0] - Steps[CurrentStep][0];
                double LongditudeDiff = Steps[CurrentStep + 1][1] - Steps[CurrentStep][1];
                Latitude = Steps[CurrentStep][0] + LatitudeDiff * (offsetStep);
                Longditude = Steps[CurrentStep][1] + LongditudeDiff * (offsetStep);
                return true;
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

        [JsonIgnore]
        public VehicleMovement Movement { get; set; }
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
