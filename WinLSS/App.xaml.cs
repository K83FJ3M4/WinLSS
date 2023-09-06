using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BruTile.Wmts.Generated;
using Mapsui;
using Microsoft.UI.Xaml;
using SkiaSharp;
using Windows.System;

namespace WinLSS
{
    public partial class App : Application
    {
        const string ADDRESS = "https://www.leitstellenspiel.de/";
        const string CLIENT_ID = "BAhJIilkNWYxZjUzMi01MjE0LTQxYjctYWI2NS0yYTY0MmEyZGNhMmEGOgZFRg%3D%3D--72cc052194965999a2fe5def658c52dd025b9c0b";
        const string SESSION_ID = "2e25ea6a19c67e910a5836eb3b96f8bf";

        private MainWindow Window;
        private readonly AutoResetEvent AutoReset = new(false);
        private readonly HttpClient HttpClient = new();
        private readonly BackgroundWorker BackgroundWorker = new()
        {
            WorkerReportsProgress = true,
            WorkerSupportsCancellation = true
        };
        
        public App()
        {
            InitializeComponent();
            StringBuilder Cookie = new();

            Cookie.Append($"mc_unique_client_id={CLIENT_ID};");
            Cookie.Append($"_session_id={SESSION_ID};");
            Cookie.Append($"cookie_eu_consented={true};");
            Cookie.Append($"deactive_selection=[]");
            HttpClient.BaseAddress = new(ADDRESS);
            HttpClient.DefaultRequestHeaders.Add("Cookie", Cookie.ToString());

            BackgroundWorker.DoWork += new DoWorkEventHandler(AsyncSetup);
            BackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(AsyncCompleted);
            BackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(AsyncProgress);
            BackgroundWorker.RunWorkerAsync((AutoReset, HttpClient));
        }

        private static void AsyncSetup(object sender, DoWorkEventArgs args)
        {
            bool launched = false;
            BackgroundWorker worker = sender as BackgroundWorker;
            (AutoResetEvent autoReset, HttpClient httpClient) = ((AutoResetEvent, HttpClient))args.Argument;
            BackgroundTask task = new(httpClient, (value) =>
            {
                if (!launched)
                {
                    autoReset.WaitOne();
                    autoReset.Reset();
                    launched = true;
                }
                worker.ReportProgress(0, value);
            });
            task.Run(() => worker.CancellationPending).Wait();
            autoReset.Set();
        }

        private void AsyncProgress(object sender, ProgressChangedEventArgs args)
        {
            Type type = args.UserState.GetType();

            if (type == typeof(Initialize))
            {
                Window.Initialize((Initialize)args.UserState);
            }
            else if (type == typeof(Mission[]))
            {
                Window.AddMissions((Mission[])args.UserState);
            }
            else if (type == typeof(Vehicle[]))
            {
                Window.AddVehicles((Vehicle[])args.UserState);
            }
            else if (type == typeof(VehicleDrive[]))
            {
                Window.AddVehicleDrives((VehicleDrive[])args.UserState);
            }
            else
            {
                Debug.WriteLine($"Received unhandled event {type} from background thread");
            }
        }

        private void AsyncCompleted(object sender, RunWorkerCompletedEventArgs args)
        {
            if (args.Error != null)
            {
                Debug.WriteLine("--------------- ERROR! ------------------");
                Debug.WriteLine($"Background thread chrashed: {args.Error.Message}");
            }
        }
        
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Window = new();
            Window.Activate();
            AutoReset.Set();
            DispatcherQueue.GetForCurrentThread().ShutdownStarting += (_, _) =>
            {
                BackgroundWorker.CancelAsync();
                AutoReset.WaitOne();
            };
        }
    }

    public class Initialize
    {
        public string Username { get; set; }
        public uint UserId { get; set; }
        public string CsrfToken { get; set; }
        public HttpClient HttpClient { get; set; }
    }

    partial class BackgroundTask
    {
        [GeneratedRegex("var user_id = (\\d+)")]
        private static partial Regex UserIdRegex();
        [GeneratedRegex("var username = \"([^\"]+)\"")]
        private static partial Regex UsernameRegex();
        [GeneratedRegex("<meta[^>]*content=\"([^\"]*)\"[^>]*name=\"csrf-token\"")]
        private static partial Regex CsrfTokenRegex();
        [GeneratedRegex("missionMarkerAdd\\(([^)]+)\\);")]
        private static partial Regex MissionRegex();
        [GeneratedRegex("message\\.ext\\[\"([^\"]+)\"\\] = \"([^\"]+)\"")]
        private static partial Regex MessageExtRegex();
        [GeneratedRegex("vehicleDrive\\(([^)]+)\\);")]
        private static partial Regex VehicleDriveRegex();
        //[GeneratedRegex("missionDelete\\(([^)]+)\\);")]
        //private static partial Regex MissionDeleteRegex();

        //[GeneratedRegex("(\\w+)\\(\\s*({?[^{}]*}?)\\);")]
        //private static partial Regex FunctionRegex();

        string ClientId;
        uint MessageId = 0;
        readonly uint UserId;
        readonly Action<object> EventCallback;
        readonly HttpClient HttpClient;
        readonly ClientWebSocket Websocket = new();
        readonly Dictionary<string, string> MessageExtensions = new();

        public BackgroundTask(HttpClient httpClient, Action<object> eventCallback)
        {
            HttpClient = httpClient;
            EventCallback = eventCallback;

            HttpResponseMessage response = HttpClient.Send(new());
            response.EnsureSuccessStatusCode();
            StreamReader stream = new(response.Content.ReadAsStream());
            string body = stream.ReadToEnd();

            Match userIdMatch = UserIdRegex().Match(body);
            Match usernameMatch = UsernameRegex().Match(body);
            Match csrfToken = CsrfTokenRegex().Match(body);

            UserId = uint.Parse(userIdMatch.Groups[1].Value);

            EventCallback(new Initialize()
            {
                UserId = UserId,
                Username = usernameMatch.Groups[1].Value,
                CsrfToken = csrfToken.Groups[1].Value,
                HttpClient = HttpClient
            });

            ScanForMissionMarkers(body);
            LoadVehicles();
            ScanForVehicleDrives(body);

            foreach (Match match in MessageExtRegex().Matches(body))
            {
                MessageExtensions.Add(match.Groups[1].Value, match.Groups[2].Value);
            }
        }

        private string GenMessageId()
        {
            if(MessageId == uint.MaxValue)
            {
                MessageId = 0;
                return MessageId.ToString();
            } else
            {
                return MessageId++.ToString();
            }
        }

        private void ScanForMissionMarkers(string text)
        {
            EventCallback(MissionRegex().Matches(text).Select((match) =>
            {
                return JsonSerializer.Deserialize<Mission>(match.Groups[1].Value);
            }).ToArray());
        }

        private void ScanForMissionDelete(string text)
        {

        }

        private void ScanForVehicleDrives(string text)
        {
            EventCallback(MissionRegex().Matches(text).Select((match) =>
            {
                return JsonSerializer.Deserialize<VehicleDrive>(match.Groups[1].Value);
            }).ToArray());
        }

        private void LoadVehicles()
        {
            HttpRequestMessage message = new()
            {
                RequestUri = new Uri("api/vehicles", UriKind.Relative)
            };
            HttpResponseMessage response = HttpClient.Send(message);
            response.EnsureSuccessStatusCode();
            StreamReader stream = new(response.Content.ReadAsStream());
            string json = stream.ReadToEnd();
            Vehicle[] vehicles = JsonSerializer.Deserialize<Vehicle[]>(json);
            EventCallback(vehicles);
        }
        private void HandleMessageData(string data)
        {
            if (data == null) { return; }

            foreach (Match match in VehicleDriveRegex().Matches(data))
            {
                ScanForMissionMarkers(match.Groups[1].Value);
                ScanForVehicleDrives(match.Groups[1].Value);
            }
        }

        private void HandleMessage(BayeuxMessage message)
        {
            switch (message.Channel)
            {
                case "/meta/handshake":
                    if (message.Successful)
                    {
                        ClientId = message.ClientId;
                        SendMessage(new BayeuxMessage
                        {
                            Channel = "/meta/connect",
                            ClientId = ClientId,
                            ConnectionType = "websocket"
                        });
                        foreach (KeyValuePair<string, string> ext in MessageExtensions)
                        {
                            SendMessage(new BayeuxMessage
                            {
                                Channel = "/meta/subscribe",
                                ClientId = ClientId,
                                Ext = MessageExtensions,
                                Subscription = ext.Key
                            });
                        }
                    } else
                    {
                        throw new Exception($"Bayeux connection failed: {message.Error}");
                    }
                    break;
                case "/meta/connect":
                    ClientId = message.ClientId;
                    SendMessage(new BayeuxMessage
                    {
                        Channel = "/meta/connect",
                        ClientId = ClientId,
                        ConnectionType = "websocket"
                    });
                    break;
                default:
                    HandleMessageData(message.Data);
                    break;
            }
        }

        private async void SendMessage(BayeuxMessage message)
        {
            message.Id = GenMessageId();
            byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new BayeuxMessage[] { message }));
            await Websocket.SendAsync(json, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task Run(Func<bool> cancel)
        {
            Websocket.Options.SetRequestHeader("Cookie", HttpClient.DefaultRequestHeaders.GetValues("Cookie").First());
            Uri uri = new($"wss://{HttpClient.BaseAddress.Host}/faye");
            await Websocket.ConnectAsync(uri, CancellationToken.None);

            SendMessage(new() {
                Channel = "/meta/handshake",
                SupportedConnectionTypes = new[] { "websocket" },
                Version = "1.0"
            });

            using MemoryStream memoryStream = new();

            while (Websocket.State == WebSocketState.Open && !cancel())
            {
                long streamPos = memoryStream.Position;
                ArraySegment<byte> messageBuffer = WebSocket.CreateClientBuffer(1024, 16);
                WebSocketReceiveResult result;
                do
                {
                    result = await Websocket.ReceiveAsync(messageBuffer, CancellationToken.None);
                    memoryStream.Write(messageBuffer.Array, messageBuffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    memoryStream.Position = streamPos;
                    foreach (BayeuxMessage message in JsonSerializer.Deserialize<BayeuxMessage[]>(memoryStream))
                    {
                        HandleMessage(message);
                    }
                }
            }
        }
    }

    public class VehicleDrive
    {
        [JsonPropertyName("b")]
        public uint BuildingId { get; set; }
        [JsonPropertyName("rh")]
        public string RouteHandle { get; set; }
        [JsonPropertyName("vom")]
        public bool VehicleOnMove { get; set; }
        [JsonPropertyName("vtid")]
        public uint VehiclyTypeId { get; set; }
        [JsonPropertyName("mid")]
        public string MissionId { get; set; }
        [JsonPropertyName("dd")]
        public long DriveDuration { get; set; }
        [JsonPropertyName("s")]
        public string Steps { get; set; }
        [JsonPropertyName("fms")]
        public uint Fms { get; set; }
        [JsonPropertyName("fms_real")]
        public uint FmsReal { get; set; }
        [JsonPropertyName("user_id")]
        public uint UserId { get; set; }
        [JsonPropertyName("isr")]
        public string ImageSiren { get; set; }
        [JsonPropertyName("in")]
        public string ImageNormal { get; set; }
        [JsonPropertyName("apng_sonderrechte")]
        public string ExclusivePngSiren { get; set; }
        [JsonPropertyName("ioverwrite")]
        public string IdentityOverwrite { get; set; }
        [JsonPropertyName("caption")]
        public string Caption { get; set; }
        [JsonPropertyName("id")]
        public uint Id { get; set; }
        [JsonPropertyName("sr")]
        public string Siren { get; set; }
    }

    public class BayeuxMessage
    {
        [JsonPropertyName("channel")]
        public string Channel { get; set; }
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }
        [JsonPropertyName("successful")]
        public bool Successful { get; set; }
        [JsonPropertyName("error")]
        public string Error { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("supportedConnectionTypes")]
        public string[] SupportedConnectionTypes { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }
        [JsonPropertyName("connectionType")]
        public string ConnectionType { get; set; }
        [JsonPropertyName("subscription")]
        public string Subscription { get; set; }
        [JsonPropertyName("ext")]
        public Dictionary<string, string> Ext { get; set; }
        [JsonPropertyName("data")]
        public string Data { get; set; }
    }
}
