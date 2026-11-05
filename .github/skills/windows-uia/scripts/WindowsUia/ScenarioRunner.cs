using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace WindowsUia;

internal static class ScenarioRunner
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static int Run(string scenarioPath, string evidence, bool includeText, bool screenshots, int? attachedId)
    {
        Scenario scenario = JsonSerializer.Deserialize<Scenario>(File.ReadAllText(scenarioPath), Options)
            ?? throw new InvalidDataException("Scenario is empty.");
        if (scenario.Steps.Length is < 1 or > 100) throw new InvalidDataException("Scenarios require 1 to 100 steps.");
        if (scenario.TimeoutSeconds is < 1 or > 600) throw new InvalidDataException("Overall timeout must be 1 to 600 seconds.");
        if (attachedId is null && string.IsNullOrWhiteSpace(scenario.Application.Executable))
            throw new InvalidDataException("Supply an explicit PID or an application executable.");
        foreach (Step step in scenario.Steps)
        {
            if (step.Action is not ("invoke" or "setValue" or "select" or "toggle" or "wait" or "assert" or "capture"))
                throw new InvalidDataException("Unknown scenario action.");
            if (step.TimeoutSeconds is < 1 or > 120) throw new InvalidDataException("Step timeout must be 1 to 120 seconds.");
            if (step.Action != "capture" && string.IsNullOrWhiteSpace(step.AutomationId) && string.IsNullOrWhiteSpace(step.Name))
                throw new InvalidDataException("Every action requires an automation ID or exact name selector.");
        }

        using UIA3Automation automation = new();
        using Application application = attachedId is { } processId ? Application.Attach(processId)
            : Application.Launch(CreateStartInfo(scenario.Application, scenarioPath));
        using Process process = Process.GetProcessById(application.ProcessId);
        using Timer watchdog = Program.CreateWatchdog(evidence, scenario.TimeoutSeconds + 15, () =>
        {
            if (attachedId is null && scenario.Application.StopOwnedProcess)
                Program.WriteJson(evidence, "cleanup.json", new { owned = true, terminated = StopOwned(process) });
        });
        List<object> results = [];
        List<Task> modalInvocations = [];
        Stopwatch elapsed = Stopwatch.StartNew();
        Window? window = null;
        bool cleanupSucceeded = true;
        Program.WriteJson(evidence, "process.json", new { processId = process.Id, owned = attachedId is null });
        try
        {
            window = Wait(() => ResolveWindow(application, automation, scenario.Application.WindowAutomationId),
                TimeSpan.FromSeconds(Math.Min(15, scenario.TimeoutSeconds)));
            Program.Snapshot(window, evidence, "before", includeText, screenshots);
            for (int index = 0; index < scenario.Steps.Length; index++)
            {
                Step step = scenario.Steps[index];
                TimeSpan remaining = TimeSpan.FromSeconds(scenario.TimeoutSeconds) - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero) throw new TimeoutException("Scenario exceeded its overall timeout.");
                TimeSpan timeout = remaining < TimeSpan.FromSeconds(step.TimeoutSeconds) ? remaining : TimeSpan.FromSeconds(step.TimeoutSeconds);
                string? windowId = step.WindowAutomationId ?? scenario.Application.WindowAutomationId;
                window = Wait(() => ResolveWindow(application, automation, windowId), timeout);
                if (step.Action == "capture")
                {
                    Program.Snapshot(window, evidence, $"step-{index + 1:D3}", includeText, screenshots);
                }
                else if (step.Action is "wait" or "assert")
                {
                    Wait(() => Matches(window, step, process.Id) ? true : (bool?)null, timeout);
                }
                else
                {
                    AutomationElement element = Wait(() => ResolveElement(window, step, process.Id), timeout);
                    if (!element.IsEnabled) throw new InvalidOperationException("Selected control is disabled.");
                    if (step.Action == "invoke" && step.OpensWindowAutomationId is not null)
                    {
                        modalInvocations.Add(Task.Run(() => element.Patterns.Invoke.Pattern.Invoke()));
                        Wait(() => ResolveWindow(application, automation, step.OpensWindowAutomationId), timeout);
                    }
                    else
                    {
                        Task action = Task.Run(() => Perform(element, step));
                        if (!action.Wait(timeout)) throw new TimeoutException("UIA action did not complete before its timeout.");
                    }
                }
                results.Add(new { step = index + 1, step.Action, result = "PASS", elapsedSeconds = elapsed.Elapsed.TotalSeconds });
                Program.WriteJson(evidence, "steps.json", results);
            }
            if (!Task.WaitAll(modalInvocations.ToArray(), TimeSpan.FromSeconds(3)))
                throw new TimeoutException("A modal invocation is still running; add an explicit dialog close step.");
            window = ResolveWindow(application, automation, scenario.Application.WindowAutomationId) ?? window;
            Program.Snapshot(window, evidence, "after", includeText, screenshots);
        }
        catch (Exception exception)
        {
            Program.WriteJson(evidence, "result.json", new { result = "FAIL", processId = process.Id, completedSteps = results.Count, exceptionType = exception.GetType().Name });
            if (window is not null)
                try { Program.Snapshot(window, evidence, "failure", includeText, screenshots); }
                catch (Exception captureException) { Program.WriteJson(evidence, "capture-error.json", new { exceptionType = captureException.GetType().Name }); }
            throw;
        }
        finally
        {
            if (attachedId is null && scenario.Application.StopOwnedProcess && !process.HasExited)
            {
                cleanupSucceeded = StopOwned(process);
                Program.WriteJson(evidence, "cleanup.json", new { owned = true, terminated = cleanupSucceeded });
            }
        }
        Program.WriteJson(evidence, "result.json", new { result = cleanupSucceeded ? "PASS" : "FAIL", processId = process.Id, steps = results.Count });
        Console.WriteLine($"{(cleanupSucceeded ? "PASS" : "FAIL")} scenario pid={process.Id} steps={results.Count}");
        return cleanupSucceeded ? 0 : 1;
    }

    private static bool StopOwned(Process process)
    {
        try { process.Kill(entireProcessTree: true); return process.WaitForExit(5000); }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception) { return process.HasExited; }
    }

    private static ProcessStartInfo CreateStartInfo(AppSpec application, string scenarioPath)
    {
        string path = Environment.ExpandEnvironmentVariables(application.Executable!);
        if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path, Path.GetDirectoryName(Path.GetFullPath(scenarioPath))!);
        if (!File.Exists(path)) throw new FileNotFoundException("Application executable does not exist.");
        ProcessStartInfo info = new(path) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(path) };
        foreach (string argument in application.Arguments) info.ArgumentList.Add(Environment.ExpandEnvironmentVariables(argument));
        return info;
    }

    private static Window? ResolveWindow(Application application, UIA3Automation automation, string? id)
    {
        Window[] top = application.GetAllTopLevelWindows(automation);
        IEnumerable<AutomationElement> candidates = top;
        if (!string.IsNullOrWhiteSpace(id))
            candidates = candidates.Concat(top.SelectMany(window => window.FindAllDescendants(condition => condition.ByAutomationId(id))));
        AutomationElement[] matching = candidates.Where(window => window.Properties.ProcessId.Value == application.ProcessId &&
            (string.IsNullOrEmpty(id) || HasId(window, id))).GroupBy(window => window.Properties.NativeWindowHandle.Value)
            .Select(group => group.First()).ToArray();
        if (matching.Length > 1) throw new InvalidOperationException("Window selector is ambiguous.");
        return matching.SingleOrDefault()?.AsWindow();
    }

    private static AutomationElement? ResolveElement(Window window, Step step, int processId)
    {
        IEnumerable<AutomationElement> candidates = window.FindAllDescendants();
        if (HasId(window, step.AutomationId) || HasName(window, step.Name)) candidates = candidates.Prepend(window);
        AutomationElement[] matching = candidates.Where(element =>
            (string.IsNullOrEmpty(step.AutomationId) || HasId(element, step.AutomationId)) &&
            (step.Name is null || HasName(element, step.Name))).ToArray();
        if (matching.Length > 1) throw new InvalidOperationException("Control selector is ambiguous; use a narrower window or unique automation ID.");
        AutomationElement? selected = matching.SingleOrDefault();
        if (selected is not null && selected.Properties.ProcessId.Value != processId)
            throw new InvalidOperationException("Selected control belongs to another process.");
        return selected;
    }

    private static bool HasId(AutomationElement element, string? id) => id is not null &&
        element.Properties.AutomationId.TryGetValue(out string? value) && value == id;

    private static bool HasName(AutomationElement element, string? name) => name is not null &&
        element.Properties.Name.TryGetValue(out string? value) && value == name;

    private static bool Matches(Window window, Step step, int processId)
    {
        AutomationElement? element = ResolveElement(window, step, processId);
        if (step.Exists is false) return element is null;
        if (element is null) return false;
        if (step.Enabled is { } enabled && element.IsEnabled != enabled) return false;
        if (step.NameEquals is not null && element.Name != step.NameEquals) return false;
        if (step.NameContains is not null && !element.Name.Contains(step.NameContains, StringComparison.Ordinal)) return false;
        if (step.ValueEquals is not null && element.Patterns.Value.Pattern.Value.Value != step.ValueEquals) return false;
        if (step.Checked is { } state && element.AsCheckBox().IsChecked != state) return false;
        return true;
    }

    private static void Perform(AutomationElement element, Step step)
    {
        switch (step.Action)
        {
            case "invoke": element.Patterns.Invoke.Pattern.Invoke(); break;
            case "setValue":
                if (!element.Properties.IsPassword.TryGetValue(out bool password) || password)
                    throw new InvalidOperationException("Do not put credentials into scenario files or automate password fields.");
                element.Patterns.Value.Pattern.SetValue(step.Value ?? throw new InvalidDataException("setValue requires value."));
                break;
            case "select":
                if (step.Value is null) element.Patterns.SelectionItem.Pattern.Select();
                else element.AsComboBox().Select(step.Value);
                break;
            case "toggle": element.AsCheckBox().IsChecked = step.Checked ?? throw new InvalidDataException("toggle requires checked."); break;
        }
    }

    private static T Wait<T>(Func<T?> read, TimeSpan timeout) where T : class
    {
        Stopwatch wait = Stopwatch.StartNew();
        while (wait.Elapsed < timeout)
        {
            T? result = read();
            if (result is not null) return result;
            Thread.Sleep(100);
        }
        throw new TimeoutException("UIA condition was not satisfied before its timeout.");
    }

    private static bool Wait(Func<bool?> read, TimeSpan timeout)
    {
        Stopwatch wait = Stopwatch.StartNew();
        while (wait.Elapsed < timeout)
        {
            if (read() is true) return true;
            Thread.Sleep(100);
        }
        throw new TimeoutException("UIA assertion was not satisfied before its timeout.");
    }

    private sealed record Scenario
    {
        public AppSpec Application { get; init; } = new();
        public Step[] Steps { get; init; } = [];
        public int TimeoutSeconds { get; init; } = 60;
        public bool RequiresPaidService { get; init; }
    }
    private sealed record AppSpec
    {
        public string? Executable { get; init; }
        public string[] Arguments { get; init; } = [];
        public string? WindowAutomationId { get; init; }
        public bool StopOwnedProcess { get; init; } = true;
    }
    private sealed record Step
    {
        public string Action { get; init; } = "assert";
        public string? AutomationId { get; init; }
        public string? Name { get; init; }
        public string? WindowAutomationId { get; init; }
        public string? OpensWindowAutomationId { get; init; }
        public string? Value { get; init; }
        public string? ValueEquals { get; init; }
        public string? NameEquals { get; init; }
        public string? NameContains { get; init; }
        public bool? Enabled { get; init; }
        public bool? Checked { get; init; }
        public bool? Exists { get; init; }
        public int TimeoutSeconds { get; init; } = 10;
    }
}