using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Vortice.DirectInput;
using DirectInputState = Vortice.DirectInput.JoystickState;

namespace HOTASTrimUtility;

public partial class Form1
{
    private sealed record FlightRoute(Guid DeviceGuid, int? XInputIndex, string AxisKey,
        int CenterRaw, bool SignedRange, bool Inverted, AxisResponse Response);

    private sealed record FlightConfiguration(FlightRoute? Roll, FlightRoute? Pitch,
        FlightRoute? Rudder, double RollTrim, double PitchTrim, double RudderTrim);

    private sealed record FlightSample(AxisVector Physical, AxisVector Output,
        bool RollAvailable, bool PitchAvailable, bool RudderAvailable, long ReadAt);

    private sealed record StoreCommand(bool Cancel, AxisVector Physical, AxisVector Threshold);

    // DirectInput sampling and vJoy delivery stay on this thread. The WinForms
    // message pump can paint, load profiles or process other assistants without
    // delaying the next flight-axis sample.
    private sealed class FlightAxisWorker : IDisposable
    {
        private const int PeriodMilliseconds = 8;
        private const int ReadFailureGraceMilliseconds = 100;
        private readonly IntPtr _owner;
        private readonly Thread _thread;
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentQueue<StoreCommand> _storeCommands = new();
        private readonly StoreTrimController _storeReturn = new();
        private readonly Dictionary<Guid, InputDevice> _devices = new();
        private readonly Dictionary<Guid, (DirectInputState State, long At)> _lastStates = new();
        private readonly Dictionary<Guid, long> _lastOpenAttempt = new();
        private readonly Func<FlightRoute, DirectInputState?>? _testInput;
        private readonly Func<AxisVector, bool>? _testOutput;
        private readonly FlightTimingTrace? _trace;
        private IDirectInput8? _directInput;
        private FlightConfiguration? _configuration;
        private FlightSample? _latest;
        private UiInputRoute[] _uiRoutes = [];
        private UiInputSnapshot? _uiLatest;
        private string? _outputError;
        private int _outputActive;
        private volatile bool _outputFaulted;
        private int _failedReads;
        private double _inputMilliseconds;
        private double _outputMilliseconds;
        private long _lastUiTick;
        private int _maximumUiGap;

        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint period);

        public FlightAxisWorker(IntPtr owner,
            Func<FlightRoute, DirectInputState?>? testInput = null,
            Func<AxisVector, bool>? testOutput = null)
        {
            _owner = owner;
            _testInput = testInput;
            _testOutput = testOutput;
            if (testInput is null && File.Exists(Path.Combine(AppContext.BaseDirectory, "LOCAL-TEST-BUILD.txt")))
                _trace = new FlightTimingTrace();
            _thread = new Thread(Run) { IsBackground = true, Name = "VTrim flight axes" };
            _thread.Start();
        }

        public FlightSample? Latest => Volatile.Read(ref _latest);
        public UiInputSnapshot? UiLatest => Volatile.Read(ref _uiLatest);
        public string? TakeOutputError() => Interlocked.Exchange(ref _outputError, null);
        public void Configure(FlightConfiguration? configuration) =>
            Volatile.Write(ref _configuration, configuration);
        public void ConfigureUiRoutes(UiInputRoute[] routes) =>
            Volatile.Write(ref _uiRoutes, routes);
        public void ObserveUiTick()
        {
            long now = Environment.TickCount64;
            long previous = Interlocked.Exchange(ref _lastUiTick, now);
            if (previous != 0)
            {
                int gap = (int)Math.Min(int.MaxValue, now - previous);
                int maximum;
                do { maximum = Volatile.Read(ref _maximumUiGap); }
                while (gap > maximum && Interlocked.CompareExchange(ref _maximumUiGap, gap, maximum) != maximum);
            }
        }

        public void SetOutputActive(bool active)
        {
            Volatile.Write(ref _outputActive, active ? 1 : 0);
            if (active)
            {
                _outputFaulted = false;
                Interlocked.Exchange(ref _outputError, null);
            }
        }

        public void BeginStore(AxisVector physical, AxisVector threshold) =>
            _storeCommands.Enqueue(new(false, physical, threshold));
        public void CancelStore() => _storeCommands.Enqueue(new(true, default, default));

        private void Run()
        {
            bool preciseTimer = timeBeginPeriod(1) == 0;
            long period = Stopwatch.Frequency * PeriodMilliseconds / 1000;
            long next = Stopwatch.GetTimestamp();
            long lastTick = next;
            long lastReport = next;
            double maxGap = 0, maxTick = 0, maxInput = 0, maxOutput = 0;
            int readFailures = 0;
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    long started = Stopwatch.GetTimestamp();
                    maxGap = Math.Max(maxGap, Stopwatch.GetElapsedTime(lastTick, started).TotalMilliseconds);
                    lastTick = started;
                    try { Tick(); }
                    catch (Exception exception)
                    {
                        // A driver fault must not kill the flight thread silently.
                        Interlocked.CompareExchange(ref _outputError, exception.Message, null);
                        _outputFaulted = true;
                    }
                    long completed = Stopwatch.GetTimestamp();
                    maxTick = Math.Max(maxTick, Stopwatch.GetElapsedTime(started, completed).TotalMilliseconds);
                    maxInput = Math.Max(maxInput, _inputMilliseconds);
                    maxOutput = Math.Max(maxOutput, _outputMilliseconds);
                    readFailures += _failedReads;
                    if (_trace is not null && Stopwatch.GetElapsedTime(lastReport, completed).TotalSeconds >= 1)
                    {
                        FlightConfiguration? reported = Volatile.Read(ref _configuration);
                        int flightDevices = new Guid?[] { reported?.Roll?.DeviceGuid,
                            reported?.Pitch?.DeviceGuid, reported?.Rudder?.DeviceGuid }
                            .Where(guid => guid.HasValue).Distinct().Count();
                        _trace.Write($"gapMax={maxGap:F1}ms tickMax={maxTick:F1}ms " +
                            $"inputMax={maxInput:F1}ms outputMax={maxOutput:F1}ms " +
                            $"readFailures={readFailures} uiGapMax={Interlocked.Exchange(ref _maximumUiGap, 0)}ms " +
                            $"flightDevices={flightDevices} " +
                            $"outputActive={Volatile.Read(ref _outputActive)}");
                        lastReport = completed;
                        maxGap = maxTick = maxInput = maxOutput = 0;
                        readFailures = 0;
                    }
                    next += period;
                    long now = Stopwatch.GetTimestamp();
                    if (next < now) next = now;
                    while (!_stop.IsCancellationRequested && Stopwatch.GetTimestamp() < next)
                        Thread.Sleep(1);
                }
            }
            finally
            {
                foreach (InputDevice device in _devices.Values) device.Dispose();
                _devices.Clear();
                _directInput?.Dispose();
                if (preciseTimer) timeEndPeriod(1);
            }
        }

        private void Tick()
        {
            _inputMilliseconds = _outputMilliseconds = 0;
            _failedReads = 0;
            long inputStarted = Stopwatch.GetTimestamp();
            while (_storeCommands.TryDequeue(out StoreCommand? command))
            {
                if (command.Cancel) _storeReturn.Cancel();
                else _storeReturn.Begin(command.Physical, command.Threshold);
            }

            FlightConfiguration? configuration = Volatile.Read(ref _configuration);
            long now = Environment.TickCount64;
            var states = new Dictionary<Guid, DirectInputState>(3);
            // Production uses one DirectInput device instance and one read per
            // tick for flight output, trim buttons and the on-screen preview.
            foreach (UiInputRoute route in Volatile.Read(ref _uiRoutes))
            {
                if (_testInput is null && ReadDevice(route, now, out DirectInputState state))
                    states[route.Guid] = state;
            }
            if (configuration is not null)
                foreach (FlightRoute? route in new[] { configuration.Roll, configuration.Pitch, configuration.Rudder })
                {
                    if (route is null || states.ContainsKey(route.DeviceGuid)) continue;
                    if (_testInput is not null)
                    {
                        DirectInputState? synthetic = _testInput(route);
                        if (synthetic is not null) states[route.DeviceGuid] = synthetic;
                    }
                    else if (ReadDevice(new UiInputRoute(route.DeviceGuid, route.XInputIndex), now,
                        out DirectInputState state)) states[route.DeviceGuid] = state;
                }
            _inputMilliseconds = Stopwatch.GetElapsedTime(inputStarted).TotalMilliseconds;
            Volatile.Write(ref _uiLatest, new UiInputSnapshot(states, Environment.TickCount64));
            if (configuration is null) return;

            double roll = ReadAxis(configuration.Roll, states, out bool rollAvailable);
            double pitch = ReadAxis(configuration.Pitch, states, out bool pitchAvailable);
            double rudder = ReadAxis(configuration.Rudder, states, out bool rudderAvailable);
            var physical = new AxisVector(roll, pitch, rudder);
            var neutral = new AxisVector(configuration.RollTrim / 100.0,
                configuration.PitchTrim / 100.0, configuration.RudderTrim / 100.0);
            var normal = new AxisVector(
                ApplyIndependentTrimBias(configuration.Roll?.Response.Apply(roll) ?? 0, neutral.Roll),
                ApplyIndependentTrimBias(configuration.Pitch?.Response.Apply(pitch) ?? 0, neutral.Pitch),
                ApplyIndependentTrimBias(configuration.Rudder?.Response.Apply(rudder) ?? 0, neutral.Rudder));
            AxisVector output = _storeReturn.Apply(physical, normal, neutral,
                rollAvailable, pitchAvailable, rudderAvailable);
            Volatile.Write(ref _latest, new FlightSample(physical, output,
                rollAvailable, pitchAvailable, rudderAvailable, now));

            if (Volatile.Read(ref _outputActive) == 0 || _outputFaulted) return;
            long outputStarted = Stopwatch.GetTimestamp();
            try
            {
                bool sent = _testOutput?.Invoke(output) ??
                    SharedVJoyOutputCoordinator.TrySendFlightAxesForAxisClient(
                        output.Roll, output.Pitch, output.Rudder);
                if (!sent)
                    throw new IOException("vJoy axis client disconnected.");
            }
            catch (Exception exception)
            {
                _outputFaulted = true;
                Interlocked.CompareExchange(ref _outputError, exception.Message, null);
            }
            finally { _outputMilliseconds = Stopwatch.GetElapsedTime(outputStarted).TotalMilliseconds; }
        }

        private bool ReadDevice(UiInputRoute route, long now, out DirectInputState state)
        {
            state = new DirectInputState();
            if (!_devices.TryGetValue(route.Guid, out InputDevice? device))
            {
                if (_lastOpenAttempt.TryGetValue(route.Guid, out long previous) && now - previous < 1000)
                    return RecentState(route.Guid, now, out state);
                _lastOpenAttempt[route.Guid] = now;
                IDirectInputDevice8? openedInput = null;
                try
                {
                    if (route.XInputIndex is int xinputIndex)
                        device = new InputDevice { Guid = route.Guid, FriendlyName = "XInput", XInputIndex = xinputIndex };
                    else
                    {
                        _directInput ??= DInput.DirectInput8Create();
                        openedInput = _directInput.CreateDevice(route.Guid);
                        openedInput.SetCooperativeLevel(_owner, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                        if (openedInput.SetDataFormat<RawJoystickState>().Failure)
                        { _failedReads++; return RecentState(route.Guid, now, out state); }
                        openedInput.Acquire();
                        device = new InputDevice { Guid = route.Guid, FriendlyName = "Flight axis", Device = openedInput };
                        openedInput = null;
                    }
                    _devices[route.Guid] = device;
                }
                catch { _failedReads++; return RecentState(route.Guid, now, out state); }
                finally { openedInput?.Dispose(); }
            }

            if (TryReadState(device, out state))
            {
                _lastStates[route.Guid] = (state, now);
                return true;
            }
            _failedReads++;
            if ((_lastStates.TryGetValue(route.Guid, out var last) && now - last.At > 1000) ||
                (!_lastStates.ContainsKey(route.Guid) && now - _lastOpenAttempt[route.Guid] > 1000))
            {
                device.Dispose();
                _devices.Remove(route.Guid);
            }
            return RecentState(route.Guid, now, out state);
        }

        private bool RecentState(Guid guid, long now, out DirectInputState state)
        {
            if (_lastStates.TryGetValue(guid, out var last) &&
                now - last.At <= ReadFailureGraceMilliseconds)
            { state = last.State; return true; }
            state = new DirectInputState();
            return false;
        }

        private static double ReadAxis(FlightRoute? route,
            IReadOnlyDictionary<Guid, DirectInputState> states, out bool available)
        {
            available = route is not null && states.TryGetValue(route.DeviceGuid, out DirectInputState? state) &&
                TryReadAxisRaw(state, route.AxisKey, out _);
            if (!available || route is null) return 0;
            TryReadAxisRaw(states[route.DeviceGuid], route.AxisKey, out int raw);
            double value = NormalizeAxis(raw, route.CenterRaw, route.SignedRange);
            return route.Inverted ? -value : value;
        }

        public void Dispose()
        {
            _stop.Cancel();
            if (_thread.Join(2000)) _stop.Dispose();
            _trace?.Dispose();
        }
    }

    // The local test build writes timing summaries from a separate thread so
    // file I/O cannot delay HOTAS sampling or vJoy output.
    private sealed class FlightTimingTrace : IDisposable
    {
        private readonly ConcurrentQueue<string> _lines = new();
        private readonly AutoResetEvent _ready = new(false);
        private readonly Thread _writer;
        private volatile bool _stopped;

        public FlightTimingTrace()
        {
            _writer = new Thread(Run) { IsBackground = true, Name = "VTrim timing log" };
            _writer.Start();
        }

        public void Write(string line)
        {
            _lines.Enqueue($"{DateTimeOffset.Now:O} {line}");
            _ready.Set();
        }

        private void Run()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "VTrim-timing.log");
                using var writer = new StreamWriter(path, append: true) { AutoFlush = true };
                while (!_stopped || !_lines.IsEmpty)
                {
                    while (_lines.TryDequeue(out string? line)) writer.WriteLine(line);
                    if (!_stopped) _ready.WaitOne(1000);
                }
            }
            catch { /* Diagnostics must never affect flight controls. */ }
        }

        public void Dispose()
        {
            _stopped = true;
            _ready.Set();
            _writer.Join(1000);
            _ready.Dispose();
        }
    }

    private sealed record UiInputRoute(Guid Guid, int? XInputIndex);
    private sealed record UiInputSnapshot(IReadOnlyDictionary<Guid, DirectInputState> States, long ReadAt);

    // Button capture, trim bindings and live previews consume a snapshot. A slow
    // DirectInput driver can no longer block the WinForms message pump.
    private sealed class UiInputSnapshotWorker : IDisposable
    {
        private readonly IntPtr _owner;
        private readonly Func<UiInputRoute, DirectInputState?>? _testInput;
        private readonly Thread _thread;
        private readonly CancellationTokenSource _stop = new();
        private readonly Dictionary<Guid, InputDevice> _devices = new();
        private readonly Dictionary<Guid, long> _lastOpenAttempt = new();
        private readonly Dictionary<Guid, long> _lastReadSuccess = new();
        private IDirectInput8? _directInput;
        private UiInputRoute[]? _routes;
        private UiInputSnapshot? _latest;

        public UiInputSnapshotWorker(IntPtr owner, Func<UiInputRoute, DirectInputState?>? testInput = null)
        {
            _owner = owner;
            _testInput = testInput;
            _thread = new Thread(Run) { IsBackground = true, Name = "VTrim UI inputs" };
            _thread.Start();
        }

        public UiInputSnapshot? Latest => Volatile.Read(ref _latest);
        public void Configure(UiInputRoute[]? routes)
        {
            Volatile.Write(ref _latest, null);
            Volatile.Write(ref _routes, routes);
        }

        private void Run()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    try { Tick(); }
                    catch { Volatile.Write(ref _latest, null); }
                    if (_stop.Token.WaitHandle.WaitOne(16)) break;
                }
            }
            finally
            {
                foreach (InputDevice device in _devices.Values) device.Dispose();
                _directInput?.Dispose();
            }
        }

        private void Tick()
        {
            UiInputRoute[]? routes = Volatile.Read(ref _routes);
            if (routes is null) return;
            var states = new Dictionary<Guid, DirectInputState>(routes.Length);
            long now = Environment.TickCount64;
            foreach (UiInputRoute route in routes)
            {
                if (_testInput is not null)
                {
                    DirectInputState? synthetic = _testInput(route);
                    if (synthetic is not null) states[route.Guid] = synthetic;
                    continue;
                }
                if (!_devices.TryGetValue(route.Guid, out InputDevice? device))
                {
                    if (_lastOpenAttempt.TryGetValue(route.Guid, out long previous) && now - previous < 1000)
                        continue;
                    _lastOpenAttempt[route.Guid] = now;
                    IDirectInputDevice8? openedInput = null;
                    try
                    {
                        if (route.XInputIndex is int xinputIndex)
                            device = new InputDevice { Guid = route.Guid, FriendlyName = "XInput", XInputIndex = xinputIndex };
                        else
                        {
                            _directInput ??= DInput.DirectInput8Create();
                            openedInput = _directInput.CreateDevice(route.Guid);
                            openedInput.SetCooperativeLevel(_owner, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                            if (openedInput.SetDataFormat<RawJoystickState>().Failure) continue;
                            openedInput.Acquire();
                            device = new InputDevice { Guid = route.Guid, FriendlyName = "UI input", Device = openedInput };
                            openedInput = null;
                        }
                        _devices[route.Guid] = device;
                    }
                    catch { continue; }
                    finally { openedInput?.Dispose(); }
                }
                if (TryReadState(device, out DirectInputState state))
                {
                    states[route.Guid] = state;
                    _lastReadSuccess[route.Guid] = now;
                }
                else if (now - (_lastReadSuccess.TryGetValue(route.Guid, out long lastSuccess)
                    ? lastSuccess : _lastOpenAttempt[route.Guid]) > 1000)
                {
                    device.Dispose();
                    _devices.Remove(route.Guid);
                    _lastReadSuccess.Remove(route.Guid);
                }
            }
            foreach (Guid guid in _devices.Keys.Where(guid => !routes.Any(route => route.Guid == guid)).ToArray())
            {
                _devices[guid].Dispose();
                _devices.Remove(guid);
                _lastOpenAttempt.Remove(guid);
                _lastReadSuccess.Remove(guid);
            }
            Volatile.Write(ref _latest, new UiInputSnapshot(states, Environment.TickCount64));
        }

        public void Dispose()
        {
            _stop.Cancel();
            if (_thread.Join(2000)) _stop.Dispose();
        }
    }

    private FlightAxisWorker? _flightAxisWorker;
    private UiInputSnapshotWorker? _uiInputWorker;
    private FlightConfiguration? _publishedFlightConfiguration;

    private void StartFlightAxisWorker()
    {
        if (_offlinePreview || !FlightAssistantLockedPendingGaijinLegalReview || _flightAxisWorker is not null) return;
        _flightAxisWorker = new FlightAxisWorker(Handle);
        _uiInputWorker = new UiInputSnapshotWorker(Handle);
        _publishedFlightConfiguration = null;
        PublishFlightConfiguration();
        PublishUiInputRoutes();
        _flightAxisWorker.SetOutputActive(_vJoyConnected);
    }

    private void StopFlightAxisWorker()
    {
        _flightAxisWorker?.SetOutputActive(false);
        _flightAxisWorker?.Dispose();
        _flightAxisWorker = null;
        _uiInputWorker?.Dispose();
        _uiInputWorker = null;
        _publishedFlightConfiguration = null;
    }

    private void PublishUiInputRoutes()
    {
        if (_flightAxisWorker is null) return;
        var flightGuids = new[] { _publishedFlightConfiguration?.Roll?.DeviceGuid,
            _publishedFlightConfiguration?.Pitch?.DeviceGuid,
            _publishedFlightConfiguration?.Rudder?.DeviceGuid }
            .Where(guid => guid.HasValue).Select(guid => guid!.Value).ToHashSet();
        _flightAxisWorker.ConfigureUiRoutes([]);
        _uiInputWorker?.Configure(_devices.Values
            .Where(device => !flightGuids.Contains(device.Guid))
            .Select(device => new UiInputRoute(device.Guid, device.XInputIndex)).ToArray());
    }

    private void PublishFlightConfiguration()
    {
        if (_flightAxisWorker is null) return;
        bool SameRoute(AxisSource? source, bool inverted, AxisResponse response, FlightRoute? previous)
        {
            if (source is null) return previous is null;
            if (previous is null) return false;
            _devices.TryGetValue(source.DeviceGuid, out InputDevice? device);
            AxisResponse old = previous.Response;
            return source.DeviceGuid == previous.DeviceGuid && device?.XInputIndex == previous.XInputIndex &&
                source.AxisKey == previous.AxisKey && source.CenterRaw == previous.CenterRaw &&
                source.SignedRange == previous.SignedRange && inverted == previous.Inverted &&
                response.Curve == old.Curve && response.Deadzone == old.Deadzone &&
                response.InputRange == old.InputRange && response.OutputRange == old.OutputRange &&
                response.CustomPoints.Count == old.CustomPoints.Count &&
                response.CustomPoints.Zip(old.CustomPoints).All(pair => pair.First.X == pair.Second.X && pair.First.Y == pair.Second.Y);
        }
        FlightConfiguration? previousConfig = _publishedFlightConfiguration;
        if (previousConfig is not null && previousConfig.RollTrim == _rollTrim &&
            previousConfig.PitchTrim == _pitchTrim && previousConfig.RudderTrim == _rudderTrim &&
            SameRoute(_rollAxis, _invertRollBox.Checked, _rollResponse, previousConfig.Roll) &&
            SameRoute(_pitchAxis, _invertPitchBox.Checked, _pitchResponse, previousConfig.Pitch) &&
            SameRoute(_rudderAxis, _invertRudderBox.Checked, _rudderResponse, previousConfig.Rudder))
            return;
        FlightRoute? Route(AxisSource? source, bool inverted, AxisResponse response)
        {
            if (source is null) return null;
            _devices.TryGetValue(source.DeviceGuid, out InputDevice? device);
            return new FlightRoute(source.DeviceGuid, device?.XInputIndex, source.AxisKey,
                source.CenterRaw, source.SignedRange, inverted, new AxisResponse
                {
                    Curve = response.Curve, Deadzone = response.Deadzone,
                    InputRange = response.InputRange, OutputRange = response.OutputRange,
                    CustomPoints = response.CustomPoints.Select(p => new AxisCurvePoint(p.X, p.Y)).ToList()
                });
        }
        _publishedFlightConfiguration = new FlightConfiguration(
            Route(_rollAxis, _invertRollBox.Checked, _rollResponse),
            Route(_pitchAxis, _invertPitchBox.Checked, _pitchResponse),
            Route(_rudderAxis, _invertRudderBox.Checked, _rudderResponse),
            _rollTrim, _pitchTrim, _rudderTrim);
        _flightAxisWorker.Configure(_publishedFlightConfiguration);
        PublishUiInputRoutes();
    }

    internal static bool RunFlightWorkerSelfTest()
    {
        var writes = new ConcurrentQueue<(long At, AxisVector Output)>();
        Guid guid = Guid.NewGuid();
        var route = new FlightRoute(guid, null, "X", 0, true, false,
            new AxisResponse { Deadzone = 0 });
        using var worker = new FlightAxisWorker(IntPtr.Zero,
            _ => new DirectInputState { X = 16384 },
            output => { writes.Enqueue((Environment.TickCount64, output)); return true; });
        worker.Configure(new FlightConfiguration(route, null, null, 0, 0, 0));
        worker.SetOutputActive(true);
        // Mimic a blocked UI thread. Flight samples must keep reaching output.
        Thread.Sleep(180);
        var samples = writes.ToArray();
        bool steady = samples.Length >= 10 &&
            samples.Zip(samples.Skip(1)).All(pair => pair.Second.At - pair.First.At < 50) &&
            samples.All(sample => sample.Output.Roll is > 0.45 and < 0.55);
        bool sharedSample = worker.UiLatest?.States.TryGetValue(guid, out DirectInputState? sampled) == true &&
            sampled.X == 16384 && worker.Latest?.Physical.Roll is > 0.45 and < 0.55;
        worker.Configure(new FlightConfiguration(route, null, null, 10, 0, 0));
        Thread.Sleep(40);
        bool trimUpdated = writes.LastOrDefault().Output.Roll is > 0.55 and < 0.65;
        worker.SetOutputActive(false);
        Thread.Sleep(10);
        int count = writes.Count;
        Thread.Sleep(30);
        return steady && sharedSample && trimUpdated && writes.Count == count;
    }

    internal static bool RunUiInputWorkerSelfTest()
    {
        Guid guid = Guid.NewGuid();
        int reads = 0;
        using var worker = new UiInputSnapshotWorker(IntPtr.Zero, _ =>
        {
            Interlocked.Increment(ref reads);
            return new DirectInputState { X = 12345 };
        });
        worker.Configure([new UiInputRoute(guid, null)]);
        // A blocked UI must not postpone controller reads for trim bindings.
        Thread.Sleep(160);
        UiInputSnapshot? snapshot = worker.Latest;
        return Volatile.Read(ref reads) >= 5 && snapshot is not null &&
            snapshot.States.TryGetValue(guid, out DirectInputState? state) && state.X == 12345 &&
            Environment.TickCount64 - snapshot.ReadAt < 100;
    }
}
