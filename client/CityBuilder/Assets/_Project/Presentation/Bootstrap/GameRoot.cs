#nullable enable

using System;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Build;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Client.Presentation.Connection;
using CityBuilder.Client.Presentation.Feedback;
using CityBuilder.Client.Presentation.Hud;
using CityBuilder.Client.Presentation.Inspector;
using CityBuilder.Client.Presentation.Research;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Client.Presentation.WelcomeBack;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.Bootstrap
{
    /// <summary>
    /// The composition root: the one place that builds the client's services by hand and hands them to the views
    /// (core-instructions/09, "Runtime structure"). <see cref="GameBootstrap"/> creates exactly one when the Bootstrap scene loads.
    /// It builds the API client from the settings asset, runs <see cref="StartupFlow"/> (login, content, city) and has the <see cref="ConnectionStatusView"/> show how it ended:
    /// "Connected" with the era and the coins, a message when the server cannot be used, or the fatal error screen when the server's content is invalid. Once connected it owns the city store, keeps the display city up to date for the views and builds the command sender they use.
    /// It creates the city's camera first (<see cref="CityWorld"/>, so the screen is cleared at once) and, once connected, shows the ground for the grid size of the rules and the buildings of the displayed city (<see cref="CityView"/>).
    /// It shows the top HUD (<see cref="HudView"/>) over the displayed city and the connection state.
    /// It also shows the build menu (<see cref="BuildMenu"/>), whose cards follow the displayed city, and tells the ground that a press on the menu or on the placement bar is not its own.
    /// It joins the menu, the ground, the placement bar and the command sender into the placement flow (<see cref="PlacementFlow"/>).
    /// The menus the player opens and closes (the build menu's sheet, the inspector, the research screen, the welcome-back report) are one <see cref="MenuGroup"/>: opening one closes the others.
    /// It also shows the building inspector (<see cref="BuildingInspector"/>) for the building on the tapped tile and joins it to the command sender (<see cref="InspectorFlow"/>).
    /// It also shows the research screen (<see cref="ResearchScreen"/>) for the city's era and joins it to the command sender (<see cref="ResearchFlow"/>); it is one more member of the menu group.
    /// It also shows the welcome-back report (<see cref="WelcomeBackPanel"/>, <see cref="WelcomeBackFlow"/>) for any snapshot that settled five minutes or more, the first one included; the panel is one more member of the menu group.
    /// It also drives the connection monitor: it polls the city, goes quiet while the app is paused, and the status view says how the connection stands.
    /// While the connection is not good it shows a banner under the HUD with the age of the last sync and an action, or, for a game that does not match the server, a blocking screen (<see cref="ConnectionBanner"/>, <see cref="ConnectionFlow"/>).
    /// Whenever the connection changes it tells the placement flow, the inspector and the research screen whether a command may be sent (<see cref="CommandAvailability"/>), so their buttons go off with the reason.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        /// <summary>The API client settings asset, loaded by name from <c>Assets/_Project/Resources/Config</c>.</summary>
        private const string SettingsPath = "Config/ApiClientSettings";

        /// <summary>The connection status on the screen; null when an asset is missing and nothing is shown.</summary>
        private ConnectionStatusView? _status;

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

        /// <summary>The city's camera, light and ground; made in <c>Awake</c>, its ground once the content's grid size is known.</summary>
        private CityWorld? _world;

        /// <summary>The top HUD; null until the start has connected, or when its assets are missing.</summary>
        private HudView? _hud;

        /// <summary>The menus the player opens and closes as one group: opening one closes the others (owner directive OD-16).</summary>
        private readonly MenuGroup _menus = new MenuGroup();

        /// <summary>The build menu; null until the start has connected, or when its assets are missing.</summary>
        private BuildMenu? _buildMenu;

        /// <summary>The placement bar; null until the start has connected, or when its assets are missing.</summary>
        private PlacementBar? _placementBar;

        /// <summary>The placement flow (pick a card, a tile, confirm); null until the start has connected, or when the menu or the bar is missing.</summary>
        private PlacementFlow? _placement;

        /// <summary>The building inspector; null until the start has connected, or when its assets are missing.</summary>
        private BuildingInspector? _inspector;

        /// <summary>Joins the tile selection, the inspector and the command sender; null until the start has connected, or when the inspector is missing.</summary>
        private InspectorFlow? _inspectorFlow;

        /// <summary>The research screen; null until the start has connected, or when its assets are missing.</summary>
        private ResearchScreen? _researchScreen;

        /// <summary>Joins the displayed city, the command sender and the research screen; null until the start has connected, or when the screen is missing.</summary>
        private ResearchFlow? _researchFlow;

        /// <summary>The welcome-back report; null until the start has connected, or when its assets are missing.</summary>
        private WelcomeBackPanel? _welcomePanel;

        /// <summary>Shows the welcome-back report for the snapshots the store takes; null until the start has connected, or when the panel is missing.</summary>
        private WelcomeBackFlow? _welcomeFlow;

        /// <summary>The connection banner and the blocking screen; null until the start has connected, or when its assets are missing.</summary>
        private ConnectionBanner? _connectionBanner;

        /// <summary>Joins the monitor, the city store and the banner; null until the start has connected, or when the banner is missing.</summary>
        private ConnectionFlow? _connectionFlow;

        /// <summary>The toasts on the screen; null until the start has connected.</summary>
        private ToastQueue? _toasts;

        /// <summary>Shows the toasts; null until the start has connected, or when its assets are missing.</summary>
        private ToastHost? _toastHost;

        /// <summary>Turns every command result into a toast; null until the start has connected.</summary>
        private CommandFeedback? _feedback;

        /// <summary>The text the screen shows now, for tests and logs.</summary>
        public string CurrentStatusText { get; private set; } = string.Empty;

        /// <summary>The command sender the views use (<c>09</c> Commands); null until the start has connected.</summary>
        internal CommandSender? CommandSender => _commandSender;

        /// <summary>The connection monitor the views read the state from (<c>09</c> Polling); null until the start has connected.</summary>
        internal ConnectionMonitor? Connection => _monitor;

        /// <summary>The toasts (<c>09</c> Feedback); null until the start has connected.</summary>
        internal ToastQueue? Toasts => _toasts;

        /// <summary>The toast screen (<c>09</c> Feedback); null until the start has connected, or when its assets are missing.</summary>
        internal ToastHost? ToastHost => _toastHost;

        /// <summary>The top HUD (<c>09</c> HUD); null until the start has connected, or when its assets are missing.</summary>
        internal HudView? Hud => _hud;

        /// <summary>The build menu (<c>09</c> Build menu); null until the start has connected, or when its assets are missing.</summary>
        internal BuildMenu? BuildMenu => _buildMenu;

        /// <summary>The placement flow (<c>09</c> Placement); null until the start has connected, or when the menu or the bar is missing.</summary>
        internal PlacementFlow? Placement => _placement;

        /// <summary>The placement bar (<c>09</c> Placement); null until the start has connected, or when its assets are missing.</summary>
        internal PlacementBar? PlacementBar => _placementBar;

        /// <summary>The building inspector (<c>09</c> Building inspector); null until the start has connected, or when its assets are missing.</summary>
        internal BuildingInspector? Inspector => _inspector;

        /// <summary>The inspector's flow (<c>09</c> Building inspector); null until the start has connected, or when the inspector is missing.</summary>
        internal InspectorFlow? InspectorFlow => _inspectorFlow;

        /// <summary>The research screen (<c>09</c> Research screen); null until the start has connected, or when its assets are missing.</summary>
        internal ResearchScreen? ResearchScreen => _researchScreen;

        /// <summary>The research screen's flow (<c>09</c> Research screen); null until the start has connected, or when the screen is missing.</summary>
        internal ResearchFlow? ResearchFlow => _researchFlow;

        /// <summary>The welcome-back panel (<c>09</c> Welcome-back report); null until the start has connected, or when its assets are missing.</summary>
        internal WelcomeBackPanel? WelcomePanel => _welcomePanel;

        /// <summary>The welcome-back flow (<c>09</c> Welcome-back report); null until the start has connected, or when the panel is missing.</summary>
        internal WelcomeBackFlow? WelcomeFlow => _welcomeFlow;

        /// <summary>The connection banner (<c>09</c> Connection states); null until the start has connected, or when its assets are missing.</summary>
        internal ConnectionBanner? ConnectionBanner => _connectionBanner;

        /// <summary>The city's camera and ground (<c>09</c> City view); null only before <c>Awake</c> has run.</summary>
        internal CityWorld? World => _world;

        /// <summary>Whether the fatal error screen is up.</summary>
        public bool IsFatalErrorShown { get; private set; }

        /// <summary>Unity calls it once, right after the component is created: it creates the city's camera and the connection status.</summary>
        private void Awake()
        {
            // The camera comes first: it clears the screen, so an earlier text never shows through the status label.
            var worldObject = new GameObject("World");
            worldObject.transform.SetParent(transform, false);
            _world = worldObject.AddComponent<CityWorld>();
            _status = ConnectionStatusView.Create(transform);
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
                    if (Debug.isDebugBuild)
                    {
                        CommandSender sender = _commandSender;
                        _ = new PredictionWatch(_cityStore, _displayCity, outcome.Content!, () => sender.Pending.Count > 0, Debug.LogWarning);
                    }

                    _content = outcome.Content;
                    CreateFeedback();
                    _world!.ShowGrid(new CityGrid(outcome.Content!.Rules.GridWidth, outcome.Content.Rules.GridHeight));
                    _displayCity.Changed += ShowBuildings;
                    CreateHud(outcome.Content!);
                    CreateBuildMenu(outcome.Content!);
                    CreatePlacement(outcome.Content!);
                    CreateInspector(outcome.Content!);
                    CreateResearchScreen(outcome.Content!);
                    CreateWelcomeBack(outcome.Content!, outcome.TimeScale);
                    ShowBuildings();
                    _monitor = new ConnectionMonitor(
                        _api!,
                        _auth!,
                        _cityStore,
                        outcome.Content!.ContentVersion,
                        _displayName,
                        new PollScheduler(() => Time.realtimeSinceStartupAsDouble, PollScheduler.DefaultIntervalSeconds),
                        () => Application.internetReachability != NetworkReachability.NotReachable);
                    CreateConnectionBanner();
                    _monitor.StateChanged += OnConnectionChanged;
                    _commandSender.Completed += OnCommandCompleted;
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

        /// <summary>Unity calls it when the object is destroyed: the placement flow and the monitor no longer report to a screen that is gone.</summary>
        private void OnDestroy()
        {
            _placement?.Dispose();
            _inspectorFlow?.Dispose();
            _researchFlow?.Dispose();
            _welcomeFlow?.Dispose();
            _connectionFlow?.Dispose();
            _feedback?.Dispose();
            if (_displayCity != null)
            {
                _displayCity.Changed -= ShowBuildings;
            }

            if (_monitor != null)
            {
                _monitor.StateChanged -= OnConnectionChanged;
                if (_commandSender != null)
                {
                    _commandSender.Completed -= OnCommandCompleted;
                }

                _cityStore.SnapshotChanged -= OnConnectionChanged;
            }
        }

        /// <summary>The displayed city changed (a new snapshot, or a whole second of prediction): the building views follow it.</summary>
        private void ShowBuildings()
        {
            CityState? city = _displayCity?.Current;
            if (city != null)
            {
                _world!.ShowCity(city, _content);
                _hud?.Model?.Update(city, _monitor?.State ?? ConnectionState.Connected);
                _buildMenu?.Model?.Update(city);
                _placement?.Update(city);
                _inspectorFlow?.Update(city);
                _researchFlow?.Update(city);
                _connectionFlow?.Update();
            }
        }

        /// <summary>
        /// Makes the toast queue and its screen, and the feedback that fills it from the command sender, so every command result is shown
        /// (<c>09</c> UX rules). The feedback works without the screen: a missing asset logs an error and the queue still fills.
        /// </summary>
        private void CreateFeedback()
        {
            _toasts = new ToastQueue();
            _toastHost = ToastHost.Create(transform, _toasts);
            _feedback = new CommandFeedback(_commandSender!, _toasts, ToastHost.NowMilliseconds);
        }

        /// <summary>
        /// Shows the top HUD for the displayed city. The start has just connected, so the indicator begins at connected; the monitor's
        /// state replaces it from then on. Does nothing when there is no displayed city yet; the HUD logs an error and stays null when
        /// its assets are missing.
        /// </summary>
        private void CreateHud(GameContent content)
        {
            CityState? city = _displayCity?.Current;
            if (city == null)
            {
                return;
            }

            _hud = HudView.Create(transform, new HudModel(content, city, ConnectionState.Connected));
        }

        /// <summary>
        /// Shows the build menu for the displayed city and makes the ground leave a press on the menu to it. Does nothing when there is no
        /// displayed city yet; the menu logs an error and stays null when its assets are missing.
        /// </summary>
        private void CreateBuildMenu(GameContent content)
        {
            CityState? city = _displayCity?.Current;
            if (city == null)
            {
                return;
            }

            BuildMenu? menu = _buildMenu = BuildMenu.Create(transform, new BuildMenuModel(content, city));
            if (menu != null)
            {
                _world!.IsPointerOverUi = IsOverUi;
                menu.Opened += _menus.Join(() => menu.SetOpen(false)).NotifyOpened;
            }
        }

        /// <summary>
        /// Makes the placement flow over the menu, the ground and the command sender, with its bar. Does nothing when there is no build menu
        /// (its assets are missing: it logged an error) or the bar cannot be shown (it logged one too).
        /// </summary>
        private void CreatePlacement(GameContent content)
        {
            CityState? city = _displayCity?.Current;
            if (_buildMenu == null || _buildMenu.Model == null || city == null || _commandSender == null || _world!.Grid == null)
            {
                return;
            }

            var model = new PlacementFlowModel(content, _world.Grid);
            model.Update(city);
            PlacementBar? bar = PlacementBar.Create(transform, model);
            if (bar == null)
            {
                return;
            }

            _placementBar = bar;
            BuildMenu menu = _buildMenu;
            _placement = new PlacementFlow(
                model,
                menu.Model!,
                () => menu.SetOpen(false),
                _world,
                _commandSender,
                bar,
                () => _displayCity!.Current!.Era,
                destroyCancellationToken);
        }

        /// <summary>
        /// Shows the building inspector and joins it to the ground's tile selection, the placement flow's state and the command sender, and
        /// makes the ground leave a press on it to it. Does nothing when there is no command sender yet; the inspector logs an error and
        /// stays null when its assets are missing.
        /// </summary>
        private void CreateInspector(GameContent content)
        {
            if (_commandSender == null || _world!.Grid == null)
            {
                return;
            }

            _inspector = BuildingInspector.Create(transform);
            if (_inspector == null)
            {
                return;
            }

            var flow = _inspectorFlow = new InspectorFlow(content, _world, _commandSender, _inspector, _placement?.Model, destroyCancellationToken);
            _inspector.Opened += _menus.Join(flow.Close).NotifyOpened;
            _world.IsPointerOverUi = IsOverUi;
        }

        /// <summary>
        /// Shows the research screen, joins it to the command sender and to the menu group, and makes the ground leave a press on it to it. Does
        /// nothing when there is no command sender yet; the screen logs an error and stays null when its assets are missing.
        /// </summary>
        private void CreateResearchScreen(GameContent content)
        {
            if (_commandSender == null)
            {
                return;
            }

            _researchScreen = ResearchScreen.Create(transform);
            if (_researchScreen == null)
            {
                return;
            }

            var flow = _researchFlow = new ResearchFlow(content, _commandSender, _researchScreen, destroyCancellationToken);
            _researchScreen.Opened += _menus.Join(flow.Close).NotifyOpened;
            _world!.IsPointerOverUi = IsOverUi;
        }

        /// <summary>
        /// Shows the welcome-back panel, joins it to the menu group and then makes the flow over the city store, so a report for the snapshot the
        /// start stored (the answer after a long absence) opens the panel through the group. Does nothing when its assets are missing: the panel
        /// logs an error and stays null.
        /// </summary>
        private void CreateWelcomeBack(GameContent content, int timeScale)
        {
            WelcomeBackPanel? panel = _welcomePanel = WelcomeBackPanel.Create(transform);
            if (panel == null)
            {
                return;
            }

            panel.Opened += _menus.Join(panel.Dismiss).NotifyOpened;
            _welcomeFlow = new WelcomeBackFlow(content, _cityStore, panel, timeScale);
            _world!.IsPointerOverUi = IsOverUi;
        }

        /// <summary>
        /// Shows the connection banner and the blocking screen and joins them to the monitor and the city store. Does nothing when the monitor
        /// does not exist yet; the banner logs an error and stays null when its assets are missing.
        /// </summary>
        private void CreateConnectionBanner()
        {
            if (_monitor == null)
            {
                return;
            }

            _connectionBanner = ConnectionBanner.Create(transform);
            if (_connectionBanner != null)
            {
                _connectionFlow = new ConnectionFlow(_monitor, _cityStore, _connectionBanner);
                _world!.IsPointerOverUi = IsOverUi;
            }
        }

        /// <summary>Whether a screen position (pixels, origin bottom left) is over the build menu, the placement bar, the building inspector, the research screen, the welcome-back report or the connection banner (the whole screen while the blocking screen is up): a press there is the UI's, not the ground's.</summary>
        private bool IsOverUi(Vector2 screenPosition)
        {
            return (_buildMenu != null && _buildMenu.IsOverUi(screenPosition))
                || (_placementBar != null && _placementBar.IsOverUi(screenPosition))
                || (_inspector != null && _inspector.IsOverUi(screenPosition))
                || (_researchScreen != null && _researchScreen.IsOverUi(screenPosition))
                || (_welcomePanel != null && _welcomePanel.IsOverUi(screenPosition))
                || (_connectionBanner != null && _connectionBanner.IsOverUi(screenPosition));
        }

        /// <summary>A command ended: the monitor hears its error, because a refused token needs a new login.</summary>
        private void OnCommandCompleted(CommandOutcome outcome)
        {
            if (outcome.Error != null)
            {
                _monitor?.ReportCommandError(outcome.Error);
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
            CommandAvailability availability = CommandAvailability.For(_monitor.State);
            _placement?.Model.SetCommandAvailability(availability);
            _inspectorFlow?.SetCommandAvailability(availability);
            _researchFlow?.SetCommandAvailability(availability);
            CityState? city = _displayCity?.Current;
            if (city != null)
            {
                _hud?.Model?.Update(city, _monitor.State);
            }
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

        /// <summary>Remembers what <paramref name="status"/> says, for tests and logs, and has the status view show it.</summary>
        private void Show(ConnectionStatusModel status)
        {
            CurrentStatusText = status.Headline;
            IsFatalErrorShown = status.IsFatal;
            if (_status != null)
            {
                _status.Show(status);
            }
        }
    }
}
