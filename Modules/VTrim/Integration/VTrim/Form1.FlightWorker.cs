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
        private IDirectInput8? _directInput;
        private FlightConfiguration? _configuration;
        private FlightSample? _latest;
        private string? _outputError;
        private int _outputActive;
        private volatile bool _outputFaulted;

        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint period);

        public FlightAxisWorker(IntPtr owner,
            Func<FlightRoute, DirectInputState?>? testInput = null,
            Func<AxisVector, bool>? testOutput = null)
        {
            _owner = owner;
            _testInput = testInput;
            _testOutput = testOutput;
            _thread = new Thread(Run) { IsBackground = true, Name = "VTrim flight axes" };
            _thread.Start();
        }

        public FlightSample? Latest => Volatile.Read(ref _latest);
        public string? TakeOutputError() => Interlocked.Exchange(ref _outputError, null);
        public void Configure(FlightConfiguration? configuration) =>
            Volatile.Write(ref _configuration, configuration);

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
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    try { Tick(); }
                    catch (Exception exception)
                    {
                        // A driver fault must not kill the flight thread silently.
                        Interlocked.CompareExchange(ref _outputError, exception.Message, null);
                        _outputFaulted = true;
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
            while (_storeCommands.TryDequeue(out StoreCommand? command))
            {
                if (command.Cancel) _storeReturn.Cancel();
                else _storeReturn.Begin(command.Physical, command.Threshold);
            }

            FlightConfiguration? configuration = Volatile.Read(ref _configuration);
            if (configuration is null) return;
            long now = Environment.TickCount64;
            var states = new Dictionary<Guid, DirectInputState>(3);
            foreach (FlightRoute? route in new[] { configuration.Roll, configuration.Pitch, configuration.Rudder })
            {
                if (route is null || states.ContainsKey(route.DeviceGuid)) continue;
                if (_testInput is not null)
                {
                    DirectInputState? synthetic = _testInput(route);
                    if (synthetic is not null) states[route.DeviceGuid] = synthetic;
                }
                else if (ReadDevice(route, now, out DirectInputState state)) states[route.DeviceGuid] = state;
            }

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
        }

        private bool ReadDevice(FlightRoute route, long now, out DirectInputState state)
        {
            state = new DirectInputState();
            if (!_devices.TryGetValue(route.DeviceGuid, out InputDevice? device))
            {
                if (_lastOpenAttempt.TryGetValue(route.DeviceGuid, out long previous) && now - previous < 1000)
                    return RecentState(route.DeviceGuid, now, out state);
                _lastOpenAttempt[route.DeviceGuid] = now;
                IDirectInputDevice8? openedInput = null;
                try
                {
                    if (route.XInputIndex is int xinputIndex)
                        device = new InputDevice { Guid = route.DeviceGuid, FriendlyName = "XInput", XInputIndex = xinputIndex };
                    else
                    {
                        _directInput ??= DInput.DirectInput8Create();
                        openedInput = _directInput.CreateDevice(route.DeviceGuid);
                        openedInput.SetCooperativeLevel(_owner, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                        if (openedInput.SetDataFormat<RawJoystickState>().Failure)
                            return RecentState(route.DeviceGuid, now, out state);
                        openedInput.Acquire();
                        device = new InputDevice { Guid = route.DeviceGuid, FriendlyName = "Flight axis", Device = openedInput };
                        openedInput = null;
                    }
                    _devices[route.DeviceGuid] = device;
                }
                catch { return RecentState(route.DeviceGuid, now, out state); }
                finally { openedInput?.Dispose(); }
            }

            if (TryReadState(device, out state))
            {
                _lastStates[route.DeviceGuid] = (state, now);
                return true;
            }
            if ((_lastStates.TryGetValue(route.DeviceGuid, out var last) && now - last.At > 1000) ||
                (!_lastStates.ContainsKey(route.DeviceGuid) && now - _lastOpenAttempt[route.DeviceGuid] > 1000))
            {
                device.Dispose();
                _devices.Remove(route.DeviceGuid);
            }
            return RecentState(route.DeviceGuid, now, out state);
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
        }
    }

    private FlightAxisWorker? _flightAxisWorker;
    private FlightConfiguration? _publishedFlightConfiguration;

    private void StartFlightAxisWorker()
    {
        if (_offlinePreview || !FlightAssistantLockedPendingGaijinLegalReview || _flightAxisWorker is not null) return;
        _flightAxisWorker = new FlightAxisWorker(Handle);
        _publishedFlightConfiguration = null;
        PublishFlightConfiguration();
        _flightAxisWorker.SetOutputActive(_vJoyConnected);
    }

    private void StopFlightAxisWorker()
    {
        _flightAxisWorker?.SetOutputActive(false);
        _flightAxisWorker?.Dispose();
        _flightAxisWorker = null;
        _publishedFlightConfiguration = null;
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
        worker.Configure(new FlightConfiguration(route, null, null, 10, 0, 0));
        Thread.Sleep(40);
        bool trimUpdated = writes.LastOrDefault().Output.Roll is > 0.55 and < 0.65;
        worker.SetOutputActive(false);
        Thread.Sleep(10);
        int count = writes.Count;
        Thread.Sleep(30);
        return steady && trimUpdated && writes.Count == count;
    }
}
