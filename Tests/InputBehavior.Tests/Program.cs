using System.Runtime.InteropServices;
using WTVRSettingsAssistant;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
// A native peer uses byte flags and withholds telemetry until centering.
string testMapping = "NeckTest-" + Guid.NewGuid();
using (var backend = new OpenXrNeckBackend(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), true, testMapping))
using (var mapping = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(testMapping))
using (var peer = mapping.CreateViewAccessor())
{
    var settings = new NeckAssistSettings { Enabled = true };
    peer.Write(56, false);
    peer.Write(61, false);
    backend.UpdateMotion(settings, true);
    Check(peer.ReadBoolean(56), "Fresh native session receives initial center request");
    peer.Write(56, false);
    peer.Write(61, true);
    peer.Write(0, 25f);
    peer.Write(4, 10f);
    backend.Apply(settings);
    backend.SetHeld(false, true);
    backend.UpdateMotion(settings, true);
    Check(!peer.ReadBoolean(56), "Acknowledged session is not continuously recentered");
    Check(peer.ReadBoolean(61) && peer.ReadSingle(0) == 25f && peer.ReadSingle(4) == 10f,
        "Settings and hold writes preserve native telemetry and acknowledgement");
    backend.ReadTelemetry();
    Check(backend.HasLiveTelemetry, "Native pose is detected after centering");
    peer.Write(61, false);
    backend.UpdateMotion(settings, true);
    Check(peer.ReadBoolean(56), "Restarted native session receives a new center request");
    peer.Write(56, false);
    settings.Enabled = false;
    backend.UpdateMotion(settings, false);
    Check(!peer.ReadBoolean(56), "Disabled assistance does not request centering");
}
NativeProbe.DriverMatch(out ushort dllVersion, out ushort driverVersion);
Check(dllVersion == 0x222, "Official x86 vJoy SDK loads and reports version 2.2.2");
string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
var output = new FakeOutput();
DateTime time = DateTime.UtcNow;
PhysicalJoystick? device = new(0, "HOTAS", 0, true);
using var service = new AdvancedSwitchService(path, output, _ => device, () => time);
service.Settings.Enabled = true;
var definition = new SwitchDefinition { Name = "Gear", States = new()
{
    new() { Name = "ON", Conditions = new() { new() { ButtonId = 1, RequiredState = true } }, OutputButton = 4 },
    new() { Name = "OFF", Conditions = new() { new() { ButtonId = 1, RequiredState = false } }, OutputButton = 4 }
} };
service.Settings.Switches.Add(definition);
void Poll(uint mask, int advance = 40)
{ time = time.AddMilliseconds(advance); device = new(0, "HOTAS", mask, true); service.Poll(); }
Poll(0); Check(output.Pulses.Count == 0, "Startup does not change gear");
Poll(1, 5); Poll(0, 5); Poll(0); Check(output.Pulses.Count == 0, "Debounce suppresses contact bounce");
Poll(1); Poll(1); Check(output.Pulses.SequenceEqual(new[] { 4 }), "ON emits one pulse");
Poll(1); Check(output.Pulses.Count == 1, "Held switch does not repeat");
Poll(0); Poll(0); Check(output.Pulses.SequenceEqual(new[] { 4, 4 }), "OFF repeats same command exactly once");
device = null; service.Poll(); Poll(1); Check(output.Pulses.Count == 2, "Reconnect uses initial-position guard");
definition.States[0].Behavior = SwitchOutputBehavior.TogglePulseSameCommand;
definition.States[1].Behavior = SwitchOutputBehavior.None;
service.ResetRuntime(); Poll(0); Poll(1); Poll(1); Poll(0); Poll(0);
Check(output.Pulses.Count == 4, "Single toggle mapping automatically handles release");
definition.States[1].Behavior = SwitchOutputBehavior.TogglePulseSameCommand;
service.ResetRuntime(); Poll(0); Poll(1); Poll(1); Poll(0); Poll(0);
Check(output.Pulses.Count == 6, "Two toggle states do not double-fire a transition");
definition.States[0].Behavior = SwitchOutputBehavior.Hold;
definition.States[1].Behavior = SwitchOutputBehavior.None;
service.ResetRuntime(); Poll(0); Poll(1); Poll(1);
Check(output.Held.Contains(4), "Hold sets button"); device = null; service.Poll();
Check(!output.Held.Contains(4), "Disconnect releases held output");
var transitions = new List<bool>();
var delays = new Queue<TaskCompletionSource>();
var queue = new ButtonPulseQueue((_, down) => transitions.Add(down), _ =>
{ var completion = new TaskCompletionSource(); delays.Enqueue(completion); return completion.Task; });
queue.Pulse(1, 75); queue.Pulse(1, 75);
Check(transitions.SequenceEqual(new[] { true }), "Fast transition queued behind first pulse");
delays.Dequeue().SetResult();
Check(transitions.SequenceEqual(new[] { true, false }), "First pulse released");
delays.Dequeue().SetResult();
Check(transitions.SequenceEqual(new[] { true, false, true }), "Second pulse begins after release gap");
delays.Dequeue().SetResult(); delays.Dequeue().SetResult();
Check(transitions.SequenceEqual(new[] { true, false, true, false }), "Both pulses finish distinctly");
queue.Pulse(1, 75); queue.Pulse(1, 75); queue.Cancel();
int count = transitions.Count; delays.Dequeue().SetResult();
Check(transitions.Count == count && !transitions.Last(), "Cancel prevents queued press after shutdown");
Console.WriteLine($"PASS: {checks} switch engine and pulse sequencing checks; no real device accessed.");
checks += FlapGestureChecks.Run();
checks += AdvancedFlapsChecks.Run();
checks += FlapServiceGestureChecks.Run();
if (args.Contains("--flaps-ui")) checks += AdvancedFlapsUiChecks.Run();
if (args.Contains("--native-output")) NativeOutputChecks.Run();

sealed class FakeOutput : IInputOutput
{
    public string Name => "Fake"; public bool IsConnected => true;
    public List<int> Pulses { get; } = new(); public HashSet<int> Held { get; } = new();
    public void Pulse(int id, int durationMs) => Pulses.Add(id);
    public void ButtonDown(int id) => Held.Add(id);
    public void ButtonUp(int id) => Held.Remove(id);
    public void ReleaseAll() => Held.Clear(); public void Dispose() { }
}

static class NativeProbe
{
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool DriverMatch(out ushort dllVersion, out ushort driverVersion);
}

