using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace WinLSS
{
    public partial class App : Application
    {
        const string ADDRESS = "https://www.leitstellenspiel.de/";
        const string CLIENT_ID = "BAhJIilkNWYxZjUzMi01MjE0LTQxYjctYWI2NS0yYTY0MmEyZGNhMmEGOgZFRg%3D%3D--72cc052194965999a2fe5def658c52dd025b9c0b";
        const string SESSION_ID = "73df60a7d51b3778e5c0751b9edf5f7c";

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
            Cookie.Append($"deactive_selection=[];");
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
                if(!launched)
                {
                    autoReset.WaitOne();
                    autoReset.Reset();
                    launched = true;
                }
                worker.ReportProgress(0, value);
            });
            task.Run(() => worker.CancellationPending);
            autoReset.Set();
        }

        private void AsyncProgress(object sender, ProgressChangedEventArgs args)
        {
            Type type = args.UserState.GetType();

            if (type == typeof(Initialize))
            {
                Window.Initialize((Initialize)args.UserState);
            } else
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
    }

    partial class BackgroundTask
    {
        [GeneratedRegex("var user_id = (\\d+)")]
        private static partial Regex UserIdRegex();
        [GeneratedRegex("var username = \"([^\"]+)\"")]
        private static partial Regex UsernameRegex();
        [GeneratedRegex("<meta[^>]*content=\"([^\"]*)\"[^>]*name=\"csrf-token\"")]
        private static partial Regex CsrfTokenRegex();

        readonly Action<object> EventCallback;
        readonly HttpClient HttpClient;

        public BackgroundTask(HttpClient httpClient, Action<object> eventCallback)
        {
            HttpClient = httpClient;
            EventCallback = eventCallback;
            
            Stopwatch stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response = HttpClient.Send(new());
            response.EnsureSuccessStatusCode();
            StreamReader stream = new(response.Content.ReadAsStream());
            string body = stream.ReadToEnd();

            Match userIdMatch = UserIdRegex().Match(body);
            Match usernameMatch = UsernameRegex().Match(body);
            Match csrfToken = CsrfTokenRegex().Match(body);

            EventCallback(new Initialize()
            {
                UserId = uint.Parse(userIdMatch.Groups[1].Value),
                Username = usernameMatch.Groups[1].Value,
                CsrfToken = csrfToken.Groups[1].Value
            });
        }

        public void Run(Func<bool> Continue)
        {
            while (Continue())
            {
                break;
            }
        }
    }
}
