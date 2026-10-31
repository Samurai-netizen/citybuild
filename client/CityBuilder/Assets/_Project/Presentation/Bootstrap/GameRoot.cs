#nullable enable

using System;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Simulation.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Bootstrap
{
    /// <summary>
    /// The composition root: the one place that builds the client's services by hand and hands them to the views
    /// (core-instructions/09, "Runtime structure"). <see cref="GameBootstrap"/> creates exactly one when the Bootstrap scene loads.
    /// It shows the connection status, builds the API client from the settings asset, runs <see cref="StartupFlow"/> (login, content, city)
    /// and shows how it ended: "Connected" with the era and the coins, a message when the server cannot be used, or the fatal error screen
    /// when the server's content is invalid. Once connected it owns the city store, keeps the display city up to date for the views and builds the command sender they use.
    /// It also drives the connection monitor: it polls the city, goes quiet while the app is paused, and the screen says how the connection stands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        /// <summary>
        /// The UI's panel settings, loaded by name from <c>Assets/_Project/Resources/UI</c>: this object is created from code, so there is no
        /// scene field to assign.
        /// </summary>
        private const string PanelSettingsPath = "UI/PanelSettings";
        /// <summary>The connection status layout (UXML), loaded by name from the same folder.</summary>
        private const string ConnectionStatusPath = "UI/ConnectionStatus";
        /// <summary>The fatal error layout (UXML), loaded by name from the same folder.</summary>
        private const string FatalErrorPath = "UI/FatalError";
        /// <summary>The API client settings asset, loaded by name from <c>Assets/_Project/Resources/Config</c>.</summary>
        private const string SettingsPath = "Config/ApiClientSettings";

        /// <summary>The name of the status label in <c>ConnectionStatus.uxml</c>.</summary>
        internal const string StatusLabelName = "connection-status";
        /// <summary>The name of the title label in <c>FatalError.uxml</c>.</summary>
        internal const string FatalTitleName = "fatal-error-title";
        /// <summary>The name of the details label in <c>FatalError.uxml</c>.</summary>
        internal const string FatalDetailsName = "fatal-error-details";

        /// <summary>The UI object's document; null when an asset is missing and nothing is shown.</summary>
        private UIDocument? _document;

        /// <summary>The fatal error layout, kept to swap in; null when it is missing.</summary>
        private VisualTreeAsset? _fatalErrorLayout;

        /// <summary>Holds the last snapshot; the flow and, later, the command sender and the polling give it to this one store.</summary>
        private readonly ClientCityStore _cityStore = new ClientCityStore();

        /// <summary>The predicted city; null until the start has connected.</summary>
        private DisplayCity? _displayCity;

        /// <summary>The API client the start flow and the command sender share; null until the settings were read.</summary>
        private ApiClient? _api;

        /// <summary>The way every view gives the server a command; null until the start has connected.</summary>
        private CommandSender? _commandSender;

        /// <summary>The login service the start flow and the connection monitor share; null until the settings were read.</summary>
        private AuthService? _auth;

        /// <summary>The content the start loaded; the status words and the monitor's content version come from it. Null until connected.</summary>
        private GameContent? _content;

        /// <summary>The dev player's name from the settings, for a new login; empty until the settings were read.</summary>
        private string _displayName = string.Empty;

        /// <summary>Polls the city and tracks the connection state; null until the start has connected.</summary>
        private ConnectionMonitor? _monitor;

        /// <summary>The text the screen shows now, for tests and logs.</summary>
        public string CurrentStatusText { get; private set; } = string.Empty;

        /// <summary>The command sender the views use (<c>09</c> Commands); null until the start has connected.</summary>
        internal CommandSender? CommandSender => _commandSender;

        /// <summary>The connection monitor the views read the state from (<c>09</c> Polling); null until the start has connected.</summary>
        internal ConnectionMonitor? Connection => _monitor;

        /// <summary>Whether the fatal error screen is up.</summary>
        public bool IsFatalErrorShown { get; private set; }

        /// <summary>Unity calls it once, right after the component is created: it creates the UI.</summary>
        private void Awake()
        {
            CreateConnectionStatus();
        }

        /// <summary>
        /// Unity calls it before the first frame: builds the services and runs the startup flow. <c>async void</c> because this is a Unity
        /// event method; nothing escapes it: the flow's problems are outcomes, a cancel (the object was destroyed) is the normal end, and
        /// anything else is logged.
        /// </summary>
        private async void Start()
        {
            try
            {
                // The document has built its tree by now (it does so when enabled), so the label can be set.
                Show(ConnectionStatusModel.Connecting());
                StartupFlow? flow = BuildStartupFlow();
                if (flow == null)
                {
                    return;
                }

                StartupOutcome outcome = await flow.RunAsync(destroyCancellationToken);
                Show(ConnectionStatusModel.From(outcome));
                if (outcome.Kind == StartupResultKind.Connected)
                {
                    _displayCity = new DisplayCity(_cityStore, new DisplayClock(_cityStore, outcome.TimeScale), outcome.Content!);
                    _commandSender = new CommandSender(_api!, _cityStore, CommandPolicy.Default);
                    _content = outcome.Content;
                    _monitor = new ConnectionMonitor(
                        _api!,
                        _auth!,
                        _cityStore,
                        outcome.Content!.ContentVersion,
                        _displayName,
                        new PollScheduler(() => Time.realtimeSinceStartupAsDouble, PollScheduler.DefaultIntervalSeconds),
                        () => Application.internetReachability != NetworkReachability.NotReachable);
                    _monitor.StateChanged += OnConnectionChanged;
                    // The label names the coins, which change with every snapshot, not only when the connection state does.
                    _cityStore.SnapshotChanged += OnConnectionChanged;
                }
                else
                {
                    // A failed connection is expected when no server runs: a warning, so a scene test or a player's log is not an error.
                    string reason = outcome.Error != null ? outcome.Error.ToString() : outcome.ContentErrors.Count + " content error(s)";
                    Debug.LogWarning("GameRoot: startup did not connect: " + reason);
                }
            }
            catch (OperationCanceledException)
            {
                // The object was destroyed while a request was open (the scene reloaded or the app closed): nothing to show.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Show(ConnectionStatusModel.BadSettings("an unexpected error, see the log"));
            }
        }

        /// <summary>
        /// Unity calls it every frame: the display city predicts again when a whole second has passed, and the connection monitor polls the
        /// city when its schedule says so. Both do nothing on most frames.
        /// </summary>
        private void Update()
        {
            _displayCity?.Refresh();
            _ = _monitor?.TickAsync(destroyCancellationToken);
        }

        /// <summary>Unity calls it when the app goes to the background (<paramref name="pauseStatus"/> true) or comes back: no polling in the background, a poll at once on return.</summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            _monitor?.SetPaused(pauseStatus);
        }

        /// <summary>Unity calls it when the object is destroyed: the monitor no longer reports to a screen that is gone.</summary>
        private void OnDestroy()
        {
            if (_monitor != null)
            {
                _monitor.StateChanged -= OnConnectionChanged;
                _cityStore.SnapshotChanged -= OnConnectionChanged;
            }
        }

        /// <summary>The monitor's state or error changed: the screen says how the connection stands now.</summary>
        private void OnConnectionChanged()
        {
            if (_monitor == null || _content == null)
            {
                return;
            }

            Show(ConnectionStatusModel.ForConnection(_monitor.State, _monitor.LastError, _content, _cityStore.Snapshot));
        }

        /// <summary>
        /// Builds the Net services from the settings asset and the flow over them. A missing asset or a setting the client refuses is shown
        /// as a fatal screen and logged as an error; the method then returns null.
        /// </summary>
        private StartupFlow? BuildStartupFlow()
        {
            var settings = Resources.Load<ApiClientSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError($"GameRoot: the settings asset is missing: Assets/_Project/Resources/{SettingsPath}.asset.");
                Show(ConnectionStatusModel.BadSettings("the asset " + SettingsPath + " is missing"));
                return null;
            }

            ApiClient api;
            try
            {
                api = _api = new ApiClient(settings.BaseUrl, settings.RequestTimeoutSeconds, new UnityWebRequestTransport());
            }
            catch (ArgumentException exception)
            {
                Debug.LogError("GameRoot: " + exception.Message);
                Show(ConnectionStatusModel.BadSettings(exception.Message));
                return null;
            }

            // Only the Editor and development players keep the token on the device (M0-38).
            _displayName = settings.DevDisplayName;
            var auth = _auth = new AuthService(api, AccessTokenStores.For(Debug.isDebugBuild));
            return new StartupFlow(auth, new ContentService(api), api, _displayName, _cityStore);
        }

        /// <summary>
        /// Creates the UI: a child object <c>UI</c> with a <see cref="UIDocument"/> of the panel settings and the status layout. When an
        /// asset is missing it logs an error that names them and shows nothing.
        /// </summary>
        private void CreateConnectionStatus()
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var connectionStatus = Resources.Load<VisualTreeAsset>(ConnectionStatusPath);
            _fatalErrorLayout = Resources.Load<VisualTreeAsset>(FatalErrorPath);
            if (panelSettings == null || connectionStatus == null || _fatalErrorLayout == null)
            {
                Debug.LogError(
                    "GameRoot: cannot show the connection status, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{ConnectionStatusPath} {(connectionStatus == null ? "missing" : "found")}, " +
                    $"{FatalErrorPath} {(_fatalErrorLayout == null ? "missing" : "found")}.");
                return;
            }

            var uiObject = new GameObject("UI");
            uiObject.transform.SetParent(transform, false);

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            uiObject.SetActive(false);
            _document = uiObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.visualTreeAsset = connectionStatus;
            uiObject.SetActive(true);
        }

        /// <summary>Puts <paramref name="status"/> on the screen: the label's text, or the fatal error layout with its lines.</summary>
        private void Show(ConnectionStatusModel status)
        {
            CurrentStatusText = status.Headline;
            IsFatalErrorShown = status.IsFatal;
            VisualElement? root = _document == null ? null : _document.rootVisualElement;
            if (_document == null || root == null)
            {
                return;
            }

            if (status.IsFatal)
            {
                // Setting the layout rebuilds the document's tree, so the labels are looked up after it.
                _document.visualTreeAsset = _fatalErrorLayout;
                VisualElement? fatalRoot = _document.rootVisualElement;
                if (fatalRoot == null)
                {
                    return;
                }

                SetText(fatalRoot, FatalTitleName, status.Headline);
                SetText(fatalRoot, FatalDetailsName, string.Join("\n", status.Details));
                return;
            }

            SetText(root, StatusLabelName, status.Headline);
        }

        /// <summary>Sets the text of the label called <paramref name="labelName"/> under <paramref name="root"/>; logs an error when the layout lacks it.</summary>
        private static void SetText(VisualElement root, string labelName, string text)
        {
            var label = root.Q<Label>(labelName);
            if (label == null)
            {
                Debug.LogError($"GameRoot: the layout has no label named '{labelName}'.");
                return;
            }

            label.text = text;
        }
    }
}
