using System;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Mapsui.Projections;
using Mapsui.Extensions;
using Mapsui.Limiting;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinLSS
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    public sealed partial class MapPage : Page
    {
        readonly ObservableMemoryLayer<Mission> PinLayer;
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

            MapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
            MapControl.Map.Layers.Add(PinLayer);
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
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {

            Descriptor.Viewport = MapControl.Map.Navigator.Viewport;
        }
    }

    class MapDescriptor
    {
        public ObservableCollection<Mission> DisplayedMissions { get; set; }
        public Action<Mission> SelectionCallback { get; set; }
        public Map Map { get; set; }
        public Viewport Viewport { get; set; } = new() { Resolution = 100000.0 };
        public bool Initialized { get; set; } = false;
    }
}
