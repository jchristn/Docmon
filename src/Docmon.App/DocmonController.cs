namespace Docmon.App
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.App.Monitoring;
    using Docmon.App.Screens;
    using Docmon.App.Theming;
    using Docmon.App.Widgets;
    using Docmon.Core.Enums;
    using Docmon.Core.Helpers;
    using Docmon.Core.Models;
    using Docmon.Core.Services.Interfaces;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;

    /// <summary>
    /// The Docmon application controller: it builds the layout and theme, wires navigation and key
    /// handling, runs the background refresh and event loops, and implements every user action. All UI
    /// mutations are marshaled onto the loop thread with <see cref="TuiApplication.Post"/>.
    /// </summary>
    public sealed class DocmonController
    {
        #region Private-Members

        private static readonly string[] _TabNames = { "Containers", "Stacks", "Metrics", "Images", "Events", "Tools" };

        private readonly IDockerService _Docker;
        private readonly IStatsStreamer _Stats;
        private readonly IEventsMonitor _Events;
        private readonly IExecService _Exec;
        private readonly ITransferService _Transfer;
        private readonly IShellLauncher _Shell;
        private readonly IComposeService _Compose;
        private readonly IRegistryService _Registry;
        private readonly string _Endpoint;
        private readonly string _Version;
        private readonly bool _ShowSplash;

        private readonly StatsHistory _History = new StatsHistory(120);
        private readonly Dictionary<string, CancellationTokenSource> _StatsStreams = new Dictionary<string, CancellationTokenSource>(StringComparer.Ordinal);

        private readonly ContainersScreen _ContainersScreen = new ContainersScreen();
        private readonly StacksScreen _StacksScreen = new StacksScreen();
        private readonly MetricsScreen _MetricsScreen = new MetricsScreen();
        private readonly ImagesScreen _ImagesScreen = new ImagesScreen();
        private readonly EventsScreen _EventsScreen = new EventsScreen(500);
        private readonly ToolsScreen _ToolsScreen = new ToolsScreen();

        private TuiApplication? _App;
        private HeaderBanner? _Header;
        private TabBar? _Tabs;
        private ScreenHost? _Content;
        private StatusBar? _Status;

        private CancellationTokenSource _Lifetime = new CancellationTokenSource();
        private ScreenId _Active = ScreenId.Containers;
        private IReadOnlyList<ImageInfo> _LastImages = new List<ImageInfo>();
        private Func<Task>? _PendingSuspend;
        private bool _Quitting;
        private int _RefreshIntervalMs = 2000;
        private int _RefreshTimeoutMs = 20000;
        private int _StalledProbeIntervalMs = 300000;
        private int _MaxOutstandingRefreshes = 4;
        private int _OutstandingRefreshes;
        private int _EventsRetryMinMs = 1000;
        private int _EventsRetryMaxMs = 30000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="DocmonController"/> class.
        /// </summary>
        /// <param name="docker">The Docker service. Must not be null.</param>
        /// <param name="stats">The stats streamer. Must not be null.</param>
        /// <param name="events">The events monitor. Must not be null.</param>
        /// <param name="exec">The exec service. Must not be null.</param>
        /// <param name="transfer">The transfer service. Must not be null.</param>
        /// <param name="shell">The shell launcher. Must not be null.</param>
        /// <param name="compose">The compose service. Must not be null.</param>
        /// <param name="registry">The registry service. Must not be null.</param>
        /// <param name="endpoint">The Docker endpoint in use.</param>
        /// <param name="version">The product version string.</param>
        /// <param name="showSplash">Whether to show the startup splash.</param>
        public DocmonController(
            IDockerService docker,
            IStatsStreamer stats,
            IEventsMonitor events,
            IExecService exec,
            ITransferService transfer,
            IShellLauncher shell,
            IComposeService compose,
            IRegistryService registry,
            string endpoint,
            string version,
            bool showSplash)
        {
            _Docker = docker ?? throw new ArgumentNullException(nameof(docker));
            _Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _Events = events ?? throw new ArgumentNullException(nameof(events));
            _Exec = exec ?? throw new ArgumentNullException(nameof(exec));
            _Transfer = transfer ?? throw new ArgumentNullException(nameof(transfer));
            _Shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _Compose = compose ?? throw new ArgumentNullException(nameof(compose));
            _Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _Endpoint = endpoint ?? string.Empty;
            _Version = version ?? "0.1.0";
            _ShowSplash = showSplash;
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets a value indicating whether the user has asked to quit (as opposed to suspending for a
        /// shell session).
        /// </summary>
        public bool IsQuitting
        {
            get { return _Quitting; }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Configures the application: theme, layout, widgets, key handling, and background loops. Pass
        /// this as the configure callback to the hosting loop.
        /// </summary>
        /// <param name="app">The application to configure. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is null.</exception>
        public void Configure(TuiApplication app)
        {
            _App = app ?? throw new ArgumentNullException(nameof(app));
            app.Theme = DocmonTheme.Create();

            string[] logoRows = DocmonBanner.WordmarkLines();
            _Header = new HeaderBanner(logoRows);
            _Header.HostSummary = "connecting…";
            _Tabs = new TabBar(_TabNames);
            _Content = new ScreenHost();
            _Content.Current = _ContainersScreen;
            _Status = new StatusBar();
            _Status.Hints = _ContainersScreen.KeyHints;

            // One extra row below the wordmark: HeaderBanner only draws its logo and text rows, so this
            // trailing row stays blank and separates the header from the tab bar beneath it.
            int headerHeight = Math.Max(3, logoRows.Length) + 1;
            app.Layout = Layout.Create()
                .DockTop("header", headerHeight)
                .DockTop("tabs", 1)
                .DockBottom("status", 1)
                .Fill("content")
                .Build();

            app.Bind("header", _Header);
            app.Bind("tabs", _Tabs);
            app.Bind("status", _Status);
            app.Bind("content", _Content);

            app.KeyFilter = OnKeyFilter;
            app.Bind("ctrl+q", () => { _Quitting = true; app.RequestStop(); });
            app.Bind("f1", () => Launch(ShowHelpAsync));

            app.Focus("content");

            if (_ShowSplash)
                app.Modals.Push(new SplashModal("docmon", DocmonBanner.SplashLines(_Version), "press any key to start", true));

            _ = RefreshLoopAsync(_Lifetime.Token);
            _ = EventsLoopAsync(_Lifetime.Token);
        }

        /// <summary>
        /// Returns and clears any pending suspend action (a shell or interactive exec). The hosting loop
        /// calls this after <see cref="TuiApplication.RunAsync"/> returns; a non-null result means the
        /// TUI should be stopped, the action run against the real terminal, and the TUI resumed.
        /// </summary>
        /// <returns>The pending action, or null when the loop should end.</returns>
        public Func<Task>? TakePendingSuspend()
        {
            Func<Task>? pending = _PendingSuspend;
            _PendingSuspend = null;
            return pending;
        }

        /// <summary>
        /// Cancels background work. Call once the application has exited for good.
        /// </summary>
        public void Shutdown()
        {
            _Lifetime.Cancel();
            foreach (CancellationTokenSource cts in _StatsStreams.Values)
                cts.Cancel();
            _StatsStreams.Clear();
        }

        #endregion

        #region Private-Methods-Navigation

        private bool OnKeyFilter(KeyEvent key)
        {
            if (key.Code == KeyCode.Tab)
            {
                bool back = (key.Modifiers & KeyModifiers.Shift) != 0;
                SwitchTo((ScreenId)(((int)_Active + (back ? _TabNames.Length - 1 : 1)) % _TabNames.Length));
                return true;
            }

            if (key.Code == KeyCode.Character)
            {
                char c = (char)key.Rune;
                if (c >= '1' && c <= '6')
                {
                    SwitchTo((ScreenId)(c - '1'));
                    return true;
                }

                return DispatchAction(c);
            }

            if (key.Code == KeyCode.Enter)
                return DispatchEnter();

            return false;
        }

        private void SwitchTo(ScreenId id)
        {
            _Active = id;
            if (_Tabs != null)
                _Tabs.ActiveIndex = (int)id;

            DocmonScreen screen = ScreenFor(id);
            if (_Content != null)
                _Content.Current = screen;
            if (_Status != null)
                _Status.Hints = screen.KeyHints;
        }

        private DocmonScreen ScreenFor(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.Stacks: return _StacksScreen;
                case ScreenId.Metrics: return _MetricsScreen;
                case ScreenId.Images: return _ImagesScreen;
                case ScreenId.Events: return _EventsScreen;
                case ScreenId.Tools: return _ToolsScreen;
                default: return _ContainersScreen;
            }
        }

        private bool DispatchEnter()
        {
            if (_Active == ScreenId.Containers && _ContainersScreen.SelectedTag is ContainerInfo container)
            {
                Launch(() => InspectAsync(container));
                return true;
            }

            return false;
        }

        private bool DispatchAction(char c)
        {
            switch (_Active)
            {
                case ScreenId.Containers:
                    return DispatchContainerAction(c);
                case ScreenId.Images:
                    return DispatchImageAction(c);
                case ScreenId.Stacks:
                    return DispatchStackAction(c);
                case ScreenId.Tools:
                    if (c == 'x')
                    {
                        Launch(PruneImagesAsync);
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private bool DispatchContainerAction(char c)
        {
            ContainerInfo? container = _ContainersScreen.SelectedTag as ContainerInfo;
            if (container == null)
                return false;

            switch (c)
            {
                case 's': Launch(() => ShellAsync(container)); return true;
                case 'x': Launch(() => ExecAsync(container)); return true;
                case 'l': Launch(() => LogsAsync(container)); return true;
                case 'r': Launch(() => LifecycleAsync(container, "restart")); return true;
                case 'S': Launch(() => LifecycleAsync(container, "stop")); return true;
                case 'K': Launch(() => KillAsync(container)); return true;
                case 'p': Launch(() => PauseToggleAsync(container)); return true;
                case 't': Launch(() => TransferAsync(container)); return true;
                case 'u': Launch(() => CheckContainerUpdateAsync(container)); return true;
                case 'U': Launch(() => PullApplyAsync(container)); return true;
                case 'd': Launch(() => RemoveAsync(container)); return true;
                default: return false;
            }
        }

        private bool DispatchImageAction(char c)
        {
            switch (c)
            {
                case 'u': Launch(RecheckAllImagesAsync); return true;
                case 'p':
                    if (_ImagesScreen.SelectedTag is ImageInfo selected && !selected.IsDangling)
                        Launch(() => PullImageWithProgressAsync(selected.Repository + ":" + selected.Tag));
                    return true;
                case 'P': Launch(PullNewImageAsync); return true;
                case 'd':
                    if (_ImagesScreen.SelectedTag is ImageInfo target)
                        Launch(() => DeleteImageAsync(target));
                    return true;
                case 'x': Launch(PruneImagesAsync); return true;
                default: return false;
            }
        }

        private bool DispatchStackAction(char c)
        {
            ComposeStack? stack = _StacksScreen.SelectedTag as ComposeStack;
            if (stack == null)
                return false;

            switch (c)
            {
                case 'u': Launch(() => ComposeAsync(stack, "up")); return true;
                case 'd': Launch(() => ComposeAsync(stack, "down")); return true;
                case 'r': Launch(() => ComposeAsync(stack, "restart")); return true;
                case 'P': Launch(() => ComposeAsync(stack, "pull")); return true;
                default: return false;
            }
        }

        #endregion

        #region Private-Methods-Loops

        private async Task RefreshLoopAsync(CancellationToken token)
        {
            long lastAttemptTicks = 0;

            while (!token.IsCancellationRequested)
            {
                // While too many abandoned attempts are still wedged, only probe occasionally so a dead
                // daemon connection cannot accumulate an unbounded number of stuck requests.
                bool stalled = Volatile.Read(ref _OutstandingRefreshes) >= _MaxOutstandingRefreshes;
                bool probeDue = (Environment.TickCount64 - lastAttemptTicks) >= _StalledProbeIntervalMs;

                if (!stalled || probeDue)
                {
                    lastAttemptTicks = Environment.TickCount64;

                    try
                    {
                        await AttemptRefreshAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        // Nothing here may kill the loop; the next tick retries.
                    }
                }
                else
                {
                    Post(() => SetStatus("Docker is not responding; retrying every " + (_StalledProbeIntervalMs / 60000) + "m until it returns."));
                }

                try
                {
                    await Task.Delay(_RefreshIntervalMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task AttemptRefreshAsync(CancellationToken token)
        {
            // A wedged named-pipe request can ignore cancellation entirely (the daemon-side hang that
            // froze the container list for days), so the refresh is raced against a deadline and
            // abandoned if it loses. The continuation deregisters the attempt whenever the abandoned
            // task eventually completes.
            CancellationTokenSource attemptCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            Interlocked.Increment(ref _OutstandingRefreshes);

            Task refresh = RefreshAllAsync(attemptCts.Token);
            _ = refresh.ContinueWith(completed =>
            {
                _ = completed.Exception;
                Interlocked.Decrement(ref _OutstandingRefreshes);
                attemptCts.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            Task deadline = Task.Delay(_RefreshTimeoutMs, token);
            Task first = await Task.WhenAny(refresh, deadline).ConfigureAwait(false);

            if (first == refresh)
            {
                try
                {
                    await refresh.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Shutting down.
                }
                catch (Exception ex)
                {
                    Post(() => SetStatus("Refresh error: " + ex.Message));
                }

                return;
            }

            try
            {
                attemptCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The refresh completed and its continuation disposed the source between WhenAny and here.
            }

            if (!token.IsCancellationRequested)
                Post(() => SetStatus("Docker did not respond within " + (_RefreshTimeoutMs / 1000) + "s; still retrying."));
        }

        private async Task RefreshAllAsync(CancellationToken token)
        {
            IReadOnlyList<ContainerInfo> containers = await _Docker.ListContainersAsync(true, token).ConfigureAwait(false);
            IReadOnlyList<ImageInfo> images = await _Docker.ListImagesAsync(token).ConfigureAwait(false);
            IReadOnlyList<ComposeStack> stacks = await _Compose.DiscoverAsync(token).ConfigureAwait(false);
            SystemUsage usage = await _Docker.GetSystemUsageAsync(token).ConfigureAwait(false);

            Post(() =>
            {
                _LastImages = images;
                SyncStatsStreams(containers);
                _History.TickOverall();

                _ContainersScreen.SetContainers(containers, _History);
                _StacksScreen.SetStacks(stacks);
                _ImagesScreen.SetImages(images);
                _ToolsScreen.SetUsage(usage);
                UpdateMetricsTarget();

                if (_Content != null)
                    _Content.Initializing = false;

                if (_Header != null)
                    _Header.HostSummary = _Endpoint + "   " + usage.RunningCount + "/" + usage.ContainerCount + " running";
            });
        }

        private async Task EventsLoopAsync(CancellationToken token)
        {
            DelegateProgress<DockerEventInfo> progress = new DelegateProgress<DockerEventInfo>(info =>
            {
                Post(() => _EventsScreen.AddEvent(info));
            });

            int retryDelayMs = _EventsRetryMinMs;

            // The stream ends whenever the daemon restarts or the connection drops; reconnect with
            // exponential backoff instead of leaving the events pane dead for the rest of the session.
            while (!token.IsCancellationRequested)
            {
                long connectedTicks = Environment.TickCount64;

                try
                {
                    await _Events.MonitorAsync(progress, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Post(() => SetStatus("Events stream interrupted: " + ex.Message));
                }

                if (token.IsCancellationRequested)
                    break;

                // A connection that survived a while earns a fresh backoff; rapid failures double it.
                if (Environment.TickCount64 - connectedTicks >= _EventsRetryMaxMs)
                    retryDelayMs = _EventsRetryMinMs;

                try
                {
                    await Task.Delay(retryDelayMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                retryDelayMs = Math.Min(retryDelayMs * 2, _EventsRetryMaxMs);
            }
        }

        private void SyncStatsStreams(IReadOnlyList<ContainerInfo> containers)
        {
            HashSet<string> running = new HashSet<string>(StringComparer.Ordinal);
            foreach (ContainerInfo container in containers)
            {
                if (!container.IsRunning)
                    continue;

                running.Add(container.Id);
                if (!_StatsStreams.ContainsKey(container.Id))
                    StartStatsStream(container.Id);
            }

            List<string> stopped = new List<string>();
            foreach (string id in _StatsStreams.Keys)
            {
                if (!running.Contains(id))
                    stopped.Add(id);
            }

            foreach (string id in stopped)
            {
                _StatsStreams[id].Cancel();
                _StatsStreams.Remove(id);
            }

            _History.Prune(running);
        }

        private void StartStatsStream(string containerId)
        {
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_Lifetime.Token);
            _StatsStreams[containerId] = cts;

            DelegateProgress<ContainerStatsSample> progress = new DelegateProgress<ContainerStatsSample>(sample =>
            {
                Post(() => _History.AddSample(sample));
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await _Stats.StreamAsync(containerId, progress, cts.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Streaming stops when the container stops or the app shuts down; ignore.
                }
                finally
                {
                    // Deregister on the loop thread so the next refresh restarts the stream if the
                    // container is still running. Guard on identity: the slot may already belong to a
                    // newer stream for the same container.
                    if (!_Lifetime.IsCancellationRequested)
                    {
                        Post(() =>
                        {
                            if (_StatsStreams.TryGetValue(containerId, out CancellationTokenSource? current) && ReferenceEquals(current, cts))
                                _StatsStreams.Remove(containerId);
                        });
                    }
                }
            }, cts.Token);
        }

        private void UpdateMetricsTarget()
        {
            string id = string.Empty;
            string name = string.Empty;
            if (_ContainersScreen.SelectedTag is ContainerInfo container)
            {
                id = container.Id;
                name = container.Name;
            }

            _MetricsScreen.SetData(_History, id, name);
        }

        #endregion

        #region Private-Methods-Actions

        private async Task InspectAsync(ContainerInfo container)
        {
            ContainerDetail detail = await _Docker.InspectAsync(container.Id, _Lifetime.Token).ConfigureAwait(false);

            List<string> lines = new List<string>();
            lines.Add("ID        " + detail.Id);
            lines.Add("Name      " + detail.Name);
            lines.Add("Image     " + detail.Image);
            lines.Add("State     " + detail.State + (detail.Health.Length > 0 ? " (" + detail.Health + ")" : string.Empty));
            lines.Add("Created   " + detail.CreatedUtc.ToLocalTime());
            lines.Add("Restart   " + detail.RestartPolicy);
            lines.Add("Command   " + detail.Command);
            AddSection(lines, "Ports", FormatPorts(detail.Ports));
            AddSection(lines, "Networks", detail.Networks);
            AddSection(lines, "Mounts", detail.Mounts);
            AddSection(lines, "Environment", detail.Environment);

            await ShowInfoAsync("Inspect: " + detail.Name, lines).ConfigureAwait(false);
        }

        private async Task LogsAsync(ContainerInfo container)
        {
            IReadOnlyList<string> logs = await _Docker.GetLogsAsync(container.Id, 1000, _Lifetime.Token).ConfigureAwait(false);
            List<string> lines = new List<string>(logs);
            if (lines.Count == 0)
                lines.Add("(no output)");

            if (_App != null)
                await _App.ShowAsync(new LogViewerModal("Logs: " + container.Name, lines)).ConfigureAwait(false);
        }

        private async Task ExecAsync(ContainerInfo container)
        {
            string? command = await PromptAsync("Command to run in " + container.Name, string.Empty).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(command))
                return;

            List<string> args = SplitCommand(command!);
            List<string> output = new List<string>();
            DelegateProgress<string> progress = new DelegateProgress<string>(line => output.Add(line));

            ExecResult result = await _Exec.RunAsync(container.Id, args, progress, _Lifetime.Token).ConfigureAwait(false);
            output.Add(string.Empty);
            output.Add("exit code: " + result.ExitCode);
            if (output.Count == 2)
                output.Insert(0, "(no output)");

            await ShowInfoAsync("exec: " + command, output).ConfigureAwait(false);
        }

        private Task ShellAsync(ContainerInfo container)
        {
            if (!_Shell.IsCliAvailable)
                return NotifyAsync("Shell unavailable", "The docker CLI was not found on PATH.");

            _PendingSuspend = async () =>
            {
                Console.WriteLine();
                Console.WriteLine("Opening a shell in '" + container.Name + "'. Type 'exit' to return to Docmon.");
                Console.WriteLine();
                await _Shell.OpenShellAsync(container.Id, _Lifetime.Token).ConfigureAwait(false);
            };

            _App?.RequestStop();
            return Task.CompletedTask;
        }

        private async Task LifecycleAsync(ContainerInfo container, string action)
        {
            switch (action)
            {
                case "restart":
                    await _Docker.RestartAsync(container.Id, _Lifetime.Token).ConfigureAwait(false);
                    SetStatus("Restarted " + container.Name);
                    break;
                case "stop":
                    await _Docker.StopAsync(container.Id, _Lifetime.Token).ConfigureAwait(false);
                    SetStatus("Stopped " + container.Name);
                    break;
            }
        }

        private async Task KillAsync(ContainerInfo container)
        {
            bool confirmed = await ConfirmAsync("Kill '" + container.Name + "' immediately?", "Kill", "Cancel").ConfigureAwait(false);
            if (!confirmed)
                return;

            await _Docker.KillAsync(container.Id, _Lifetime.Token).ConfigureAwait(false);
            SetStatus("Killed " + container.Name);
        }

        private async Task PauseToggleAsync(ContainerInfo container)
        {
            if (container.State == ContainerStateEnum.Paused)
            {
                await _Docker.UnpauseAsync(container.Id, _Lifetime.Token).ConfigureAwait(false);
                SetStatus("Unpaused " + container.Name);
            }
            else
            {
                await _Docker.PauseAsync(container.Id, _Lifetime.Token).ConfigureAwait(false);
                SetStatus("Paused " + container.Name);
            }
        }

        private async Task RemoveAsync(ContainerInfo container)
        {
            bool confirmed = await ConfirmAsync("Remove '" + container.Name + "'? This cannot be undone.", "Remove", "Cancel").ConfigureAwait(false);
            if (!confirmed)
                return;

            await _Docker.RemoveAsync(container.Id, true, _Lifetime.Token).ConfigureAwait(false);
            SetStatus("Removed " + container.Name);
        }

        private async Task TransferAsync(ContainerInfo container)
        {
            int direction = await SelectAsync("Transfer files", "Copy OUT (container → host)", "Copy IN (host → container)").ConfigureAwait(false);
            if (direction < 0)
                return;

            if (direction == 0)
            {
                string? containerPath = await PromptAsync("Path inside the container to copy out", "/").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(containerPath))
                    return;
                string? hostDir = await PromptAsync("Destination directory on the host", Environment.CurrentDirectory).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(hostDir))
                    return;

                await _Transfer.CopyOutAsync(container.Id, containerPath!, hostDir!, _Lifetime.Token).ConfigureAwait(false);
                SetStatus("Copied " + containerPath + " to " + hostDir);
            }
            else
            {
                string? hostPath = await PromptAsync("File or directory on the host to copy in", string.Empty).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(hostPath))
                    return;
                string? containerDir = await PromptAsync("Destination directory inside the container", "/").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(containerDir))
                    return;

                await _Transfer.CopyInAsync(container.Id, hostPath!, containerDir!, _Lifetime.Token).ConfigureAwait(false);
                SetStatus("Copied " + hostPath + " into " + container.Name + ":" + containerDir);
            }
        }

        private async Task CheckContainerUpdateAsync(ContainerInfo container)
        {
            ImageInfo? image = FindImageFor(container.Image);
            if (image == null)
            {
                await NotifyAsync("Update check", "Could not find a local image record for " + container.Image + ".").ConfigureAwait(false);
                return;
            }

            SetStatus("Checking " + container.Image + " for updates…");
            UpdateStatusEnum status = await _Registry.CheckAsync(image, _Lifetime.Token).ConfigureAwait(false);
            Post(() => _ImagesScreen.SetImages(_LastImages));
            SetStatus(container.Image + ": " + DescribeStatus(status));
        }

        private async Task PullApplyAsync(ContainerInfo container)
        {
            await PullImageAsync(container.Image).ConfigureAwait(false);

            if (container.ComposeProject.Length > 0)
            {
                string configFile = await ResolveComposeFileAsync(container.ComposeProject).ConfigureAwait(false);
                DelegateProgress<string> progress = new DelegateProgress<string>(line => Post(() => SetStatus("compose: " + line)));
                await _Compose.UpAsync(container.ComposeProject, configFile, container.ComposeService, progress, _Lifetime.Token).ConfigureAwait(false);
                SetStatus("Recreated " + container.ComposeService + " with the new image.");
            }
            else
            {
                await NotifyAsync("Image pulled", "Recreate the container to apply the update (compose recommended for standalone containers).").ConfigureAwait(false);
            }
        }

        private async Task PullImageAsync(string image)
        {
            SetStatus("Pulling " + image + "…");
            await foreach (string line in _Docker.PullAsync(image, _Lifetime.Token).ConfigureAwait(false))
                Post(() => SetStatus("pull " + image + ": " + line));

            SetStatus("Pulled " + image);
        }

        private async Task RecheckAllImagesAsync()
        {
            SetStatus("Checking all images for updates…");
            foreach (ImageInfo image in _LastImages)
            {
                if (image.IsDangling)
                    continue;

                image.Status = UpdateStatusEnum.Checking;
                Post(() => _ImagesScreen.SetImages(_LastImages));
                UpdateStatusEnum status = await _Registry.CheckAsync(image, _Lifetime.Token).ConfigureAwait(false);
                image.Status = status;
                Post(() => _ImagesScreen.SetImages(_LastImages));
            }

            SetStatus("Update check complete.");
        }

        private async Task PullNewImageAsync()
        {
            string? reference = await PromptAsync("Image to pull (name and optional :tag, defaults to latest)", string.Empty).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(reference))
                return;

            await PullImageWithProgressAsync(reference!.Trim()).ConfigureAwait(false);
        }

        private async Task PullImageWithProgressAsync(string image)
        {
            if (_App == null)
                return;

            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_Lifetime.Token);
            PullProgressModal modal = new PullProgressModal("Pulling " + image, () => cts.Cancel());
            System.Threading.Tasks.Task<object?> shown = _App.ShowAsync(modal);

            try
            {
                await foreach (string line in _Docker.PullAsync(image, cts.Token).ConfigureAwait(false))
                    Post(() => modal.Append(line));

                Post(() => modal.MarkDone("Pulled " + image));
                SetStatus("Pulled " + image);
            }
            catch (OperationCanceledException)
            {
                Post(() => modal.MarkDone("Pull cancelled."));
                SetStatus("Pull of " + image + " cancelled.");
            }
            catch (Exception ex)
            {
                Post(() => modal.MarkDone("Pull failed: " + ex.Message));
                SetStatus("Pull of " + image + " failed: " + ex.Message);
            }
            finally
            {
                cts.Dispose();
            }

            await shown.ConfigureAwait(false);
        }

        private async Task DeleteImageAsync(ImageInfo image)
        {
            string reference = image.IsDangling ? image.Id : image.Repository + ":" + image.Tag;
            bool confirmed = await ConfirmAsync("Delete image '" + reference + "'? The daemon refuses if a container depends on it.", "Delete", "Cancel").ConfigureAwait(false);
            if (!confirmed)
                return;

            try
            {
                await _Docker.RemoveImageAsync(image.Id, false, _Lifetime.Token).ConfigureAwait(false);
                SetStatus("Deleted image " + reference + ".");
            }
            catch (Exception ex)
            {
                SetStatus("Could not delete " + reference + ": " + ex.Message);
            }
        }

        private async Task PruneImagesAsync()
        {
            bool confirmed = await ConfirmAsync(
                "Prune dangling images? Only untagged images not used by any container are removed; tagged images and your running deployment are left untouched.",
                "Prune",
                "Cancel").ConfigureAwait(false);
            if (!confirmed)
                return;

            long reclaimed = await _Docker.PruneImagesAsync(_Lifetime.Token).ConfigureAwait(false);
            SetStatus("Pruned dangling images, reclaimed " + ByteFormatter.Format(reclaimed) + ".");
        }

        private async Task ComposeAsync(ComposeStack stack, string action)
        {
            if (action == "down")
            {
                bool confirmed = await ConfirmAsync("Bring down stack '" + stack.Project + "'?", "Down", "Cancel").ConfigureAwait(false);
                if (!confirmed)
                    return;
            }

            DelegateProgress<string> progress = new DelegateProgress<string>(line => Post(() => SetStatus(stack.Project + ": " + line)));
            SetStatus(action + " " + stack.Project + "…");

            switch (action)
            {
                case "up": await _Compose.UpAsync(stack.Project, stack.ConfigFilePath, null, progress, _Lifetime.Token).ConfigureAwait(false); break;
                case "down": await _Compose.DownAsync(stack.Project, stack.ConfigFilePath, progress, _Lifetime.Token).ConfigureAwait(false); break;
                case "restart": await _Compose.RestartAsync(stack.Project, stack.ConfigFilePath, progress, _Lifetime.Token).ConfigureAwait(false); break;
                case "pull": await _Compose.PullAsync(stack.Project, stack.ConfigFilePath, progress, _Lifetime.Token).ConfigureAwait(false); break;
            }

            SetStatus(action + " " + stack.Project + " complete.");
        }

        private Task ShowHelpAsync()
        {
            List<string> lines = new List<string>
            {
                "Navigation",
                "  Tab / Shift+Tab   move between screens",
                "  1..6              jump to a screen",
                "  ↑ ↓ PgUp PgDn     move the selection",
                string.Empty,
                "Containers",
                "  Enter inspect   s shell    x exec    l logs",
                "  r restart  S stop  K kill  p pause  t transfer",
                "  u check update   U pull+apply   d remove",
                string.Empty,
                "Images:  u recheck   p pull selected   P pull new   d delete   x prune",
                "Stacks:  u up   d down   r restart   P pull",
                "Metrics: o toggle overall / container",
                "Tools:   x prune",
                string.Empty,
                "F1 help    Ctrl+Q quit"
            };

            return ShowInfoAsync("Keyboard shortcuts", lines);
        }

        #endregion

        #region Private-Methods-Helpers

        private ImageInfo? FindImageFor(string imageReference)
        {
            foreach (ImageInfo image in _LastImages)
            {
                if (string.Equals(image.Repository + ":" + image.Tag, imageReference, StringComparison.Ordinal))
                    return image;
            }

            return null;
        }

        private async Task<string> ResolveComposeFileAsync(string project)
        {
            IReadOnlyList<ComposeStack> stacks = await _Compose.DiscoverAsync(_Lifetime.Token).ConfigureAwait(false);
            foreach (ComposeStack stack in stacks)
            {
                if (string.Equals(stack.Project, project, StringComparison.Ordinal))
                    return stack.ConfigFilePath;
            }

            return string.Empty;
        }

        private void Launch(Func<Task> action)
        {
            _ = RunGuardedAsync(action);
        }

        private async Task RunGuardedAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
            catch (Exception ex)
            {
                Post(() => SetStatus("Error: " + ex.Message));
            }
        }

        private void Post(Action action)
        {
            _App?.Post(action);
        }

        private void SetStatus(string message)
        {
            if (_Status != null)
                _Status.Message = message;
        }

        private Task ShowInfoAsync(string title, IReadOnlyList<string> lines)
        {
            if (_App == null)
                return Task.CompletedTask;

            return _App.ShowAsync(new SplashModal(title, lines, "press any key to continue", false));
        }

        private Task NotifyAsync(string title, string message)
        {
            SetStatus(title + " — " + message);
            return ShowInfoAsync(title, new List<string> { message });
        }

        private Task<bool> ConfirmAsync(string message, string confirmLabel, string cancelLabel)
        {
            return _App != null ? _App.ConfirmAsync(message, confirmLabel, cancelLabel) : Task.FromResult(false);
        }

        private Task<string?> PromptAsync(string title, string initial)
        {
            return _App != null ? _App.PromptAsync(title, initial) : Task.FromResult<string?>(null);
        }

        private Task<int> SelectAsync(string title, params string[] options)
        {
            return _App != null ? _App.SelectAsync(title, options) : Task.FromResult(-1);
        }

        private static void AddSection(List<string> lines, string title, IReadOnlyList<string> values)
        {
            if (values.Count == 0)
                return;

            lines.Add(string.Empty);
            lines.Add(title + ":");
            foreach (string value in values)
                lines.Add("  " + value);
        }

        private static IReadOnlyList<string> FormatPorts(IReadOnlyList<PortMap> ports)
        {
            List<string> result = new List<string>();
            foreach (PortMap port in ports)
                result.Add(port.ToString());

            return result;
        }

        private static string DescribeStatus(UpdateStatusEnum status)
        {
            switch (status)
            {
                case UpdateStatusEnum.Current: return "up to date";
                case UpdateStatusEnum.UpdateAvailable: return "update available";
                case UpdateStatusEnum.Error: return "check failed";
                default: return "unknown";
            }
        }

        private static List<string> SplitCommand(string command)
        {
            List<string> parts = new List<string>();
            System.Text.StringBuilder current = new System.Text.StringBuilder();
            bool inQuotes = false;

            foreach (char c in command)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                parts.Add(current.ToString());

            return parts;
        }

        #endregion
    }
}
