using System;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using Mapsui.Limiting;
using System.Threading;
using System.Timers;
using System.Linq;

namespace WinLSS
{
    public sealed partial class MapPage : Page
    {
        readonly ObservableMemoryLayer<Mission> PinLayer;
        readonly ObservableMemoryLayer<VehicleMovement> VehicleLayer;
        readonly System.Timers.Timer Timer = new(40);

        MapDescriptor Descriptor { get; set; }

        public MapPage()
        {
            this.InitializeComponent();
            PinLayer = new((mission) => mission)
            {
                Name = "PinLayer",
                IsMapInfoLayer = true,
                Style = SymbolStyles.CreatePinStyle(symbolScale: 0.7),
            };

            VehicleLayer = new((vehicle) => vehicle)
            {
                Name = "VehicleDriveLayer",
                IsMapInfoLayer = true,
                Style = SymbolStyles.CreatePinStyle(Color.Red, 0.7)
            };

            MapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
            MapControl.Map.Layers.Add(PinLayer);
            MapControl.Map.Layers.Add(VehicleLayer);

            Timer.AutoReset = true;
            Timer.Enabled = false;
            Timer.Elapsed += (_, _) =>
            {
                for (int i = Descriptor.VehicleMovement.Count - 1; i >= 0; i--) {
                    VehicleMovement movement = Descriptor.VehicleMovement[i];
                    if (!movement.Interpolate())
                    {
                        Descriptor.VehicleMovement.Remove(movement);
                    }
                }
                VehicleLayer.DataHasChanged();
            };
        }

        private void MapInfo(object sender, MapInfoEventArgs args)
        {
            var mapInfo = args.MapInfo;
            if (mapInfo.Feature is Mission)
            {
                Descriptor.SelectionCallback(mapInfo.Feature as Mission);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {


            Descriptor = (MapDescriptor)e.Parameter;
            PinLayer.ObservableCollection = Descriptor.DisplayedMissions;
            Descriptor.Map = MapControl.Map;

            MPoint position = new(Descriptor.Viewport.CenterX, Descriptor.Viewport.CenterY);
            Double resolution = Descriptor.Viewport.Resolution;
            MapControl.Map.Navigator.CenterOnAndZoomTo(position, resolution, -1);
            MapControl.Map.Home = n => n.CenterOnAndZoomTo(position, resolution, -1);
            MapControl.CallHomeIfNeeded();

            ViewportLimiterKeepWithinExtent Limiter = new();
            Limiter.Limit(MapControl.Map.Navigator.Viewport, null, null);
            MapControl.Map.Navigator.Limiter = Limiter;

            VehicleLayer.ObservableCollection = Descriptor.VehicleMovement;
            Timer.Enabled = true;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {

            Descriptor.Viewport = MapControl.Map.Navigator.Viewport;
            Descriptor.Map = null;
            Timer.Enabled = false;
        }
    }

    class MapDescriptor
    {
        public ObservableCollection<VehicleMovement> VehicleMovement { get; set; }
        public ObservableCollection<Mission> DisplayedMissions { get; set; }
        public Action<Mission> SelectionCallback { get; set; }
        public Map Map { get; set; }
        public Viewport Viewport { get; set; } = new() { Resolution = 100000.0 };
        public bool Initialized { get; set; } = false;

        public void Refresh()
        {
            if (Map != null)
            {
                Map.RefreshGraphics();
            }
        }
    }
}
