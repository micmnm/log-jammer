using System.Text;
using SampleLog.Generation;
using SampleLog.Generation.Scenarios;
using SampleLog.Models;
using Terminal.Gui;

namespace SampleLog.UI;

public sealed class MainWindow : Toplevel
{
    private readonly LogGenerator _generator;
    private readonly ScenarioRunner _runner;
    private readonly DefaultsConfig _defaults;

    private readonly TextView _logView;
    private readonly Label _statusLabel;

    private readonly List<string> _logLines = [];
    private readonly List<string> _pendingLines = [];
    private const int MaxLogLines = 500;
    private bool _logDirty;

    private long _lastEmittedCount;
    private DateTime _lastRateCheck = DateTime.UtcNow;
    private double _currentRate;

    private BaselineScenario? _baselineScenario;
    private bool _baselineRunning;
    private object? _statusTimerToken;
    private object? _logFlushTimerToken;

    public MainWindow(LogGenerator generator, ScenarioRunner runner, DefaultsConfig defaults)
    {
        _generator = generator;
        _runner = runner;
        _defaults = defaults;

        // Use terminal-native colors (white on black)
        var attr = new Terminal.Gui.Attribute(Color.White, Color.Black);
        var dimAttr = new Terminal.Gui.Attribute(Color.Gray, Color.Black);
        var terminalScheme = new ColorScheme(
            normal: attr, focus: attr, hotNormal: attr, hotFocus: attr, disabled: dimAttr);
        ColorScheme = terminalScheme;

        // Log output (top, fills most of screen)
        _logView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(10),
            ReadOnly = true,
            CanFocus = false
        };

        // Status line
        _statusLabel = new Label
        {
            Text = "Baseline: OFF | Total: 0 | Rate: 0/sec",
            X = 0,
            Y = Pos.Bottom(_logView),
            Width = Dim.Fill(),
            Height = 1
        };

        // Menu area
        var menuView = new View
        {
            X = 0,
            Y = Pos.Bottom(_statusLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = false
        };

        var logPathLabel = new Label
        {
            Text = $"  Log: {_generator.JsonFilePath}",
            X = 0, Y = 0, Width = Dim.Fill()
        };
        var sep = new Label { Text = new string('=', 120), X = 0, Y = 1, Width = Dim.Fill() };
        var row1 = new Label { Text = "  [1] Toggle baseline     [5] Rates (inf/wrn/err)", X = 0, Y = 2, Width = Dim.Fill() };
        var row2 = new Label { Text = "  [2] Spike burst         [6] Volume/load test", X = 0, Y = 3, Width = Dim.Fill() };
        var row3 = new Label { Text = "  [3] Gradual degradation [7] Stop all", X = 0, Y = 4, Width = Dim.Fill() };
        var row4 = new Label { Text = "  [4] Correlated failures [C] Copy log path", X = 0, Y = 5, Width = Dim.Fill() };
        var row5 = new Label { Text = "  [Q] Quit", X = 0, Y = 6, Width = Dim.Fill() };
        var sep2 = new Label { Text = new string('=', 120), X = 0, Y = 7, Width = Dim.Fill() };

        menuView.Add(logPathLabel, sep, row1, row2, row3, row4, row5, sep2);

        Add(_logView, _statusLabel, menuView);

        // Wire events
        _generator.OnLogEmitted += OnLogEmitted;
        _runner.OnScenarioError += OnScenarioError;

        // Timers
        _statusTimerToken = Application.AddTimeout(TimeSpan.FromMilliseconds(500), UpdateStatus);
        _logFlushTimerToken = Application.AddTimeout(TimeSpan.FromMilliseconds(100), FlushLogBuffer);

        // Auto-start baseline if configured
        if (_defaults.BaselineEnabled)
            StartBaseline();
    }

    protected override bool OnKeyDown(Key key)
    {
        switch (key.KeyCode)
        {
            case KeyCode.D1:
                ToggleBaseline();
                return true;
            case KeyCode.D2:
                ShowSpikeDialog();
                return true;
            case KeyCode.D3:
                ShowDegradationDialog();
                return true;
            case KeyCode.D4:
                ShowCorrelatedDialog();
                return true;
            case KeyCode.D5:
                ShowRatesDialog();
                return true;
            case KeyCode.D6:
                ShowVolumeDialog();
                return true;
            case KeyCode.D7:
                StopAllScenarios();
                return true;
            case KeyCode.C:
            case KeyCode.C | KeyCode.ShiftMask:
                Clipboard.TrySetClipboardData(_generator.JsonFilePath);
                _pendingLines.Add($"{DateTime.Now:HH:mm:ss} INF  [ui] Log path copied to clipboard");
                _logDirty = true;
                return true;
            case KeyCode.Q:
            case KeyCode.Q | KeyCode.ShiftMask:
                _runner.StopAll();
                Application.RequestStop();
                return true;
        }

        return base.OnKeyDown(key);
    }

    private void OnLogEmitted(string line)
    {
        Application.Invoke(() =>
        {
            _pendingLines.Add(line);
            _logDirty = true;
        });
    }

    private void OnScenarioError(string errorMessage)
    {
        Application.Invoke(() =>
        {
            _pendingLines.Add($"{DateTime.Now:HH:mm:ss} ERR  [scenario] {errorMessage}");
            _logDirty = true;
        });
    }

    private bool FlushLogBuffer()
    {
        if (!_logDirty)
            return true;

        _logDirty = false;

        _logLines.AddRange(_pendingLines);
        _pendingLines.Clear();

        if (_logLines.Count > MaxLogLines)
            _logLines.RemoveRange(0, _logLines.Count - MaxLogLines);

        var sb = new StringBuilder();
        foreach (var l in _logLines)
            sb.AppendLine(l);

        _logView.Text = sb.ToString();
        _logView.MoveEnd();

        return true;
    }

    private bool UpdateStatus()
    {
        _runner.ClearCompleted();

        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRateCheck).TotalSeconds;
        var currentCount = _generator.EmittedCount;

        if (elapsed >= 2.0)
        {
            var delta = currentCount - _lastEmittedCount;
            _currentRate = delta / elapsed;
            _lastEmittedCount = currentCount;
            _lastRateCheck = now;
        }

        var parts = new List<string>();

        if (_baselineRunning && _baselineScenario is not null)
            parts.Add($"Baseline: INF:{_baselineScenario.InfoRate}/s WRN:{_baselineScenario.WarnRate}/s ERR:{_baselineScenario.ErrorRate}/s");
        else
            parts.Add("Baseline: OFF");

        var active = _runner.ActiveScenarios;
        var others = active.Keys.Where(k => k != "Baseline").ToList();
        if (others.Count > 0)
            parts.Add($"Active: {string.Join(", ", others)}");

        parts.Add($"Total: {currentCount}");
        parts.Add($"Rate: {_currentRate:F1}/sec");

        _statusLabel.Text = string.Join(" | ", parts);

        return true;
    }

    private void StartBaseline()
    {
        _baselineScenario = new BaselineScenario(
            _generator,
            _defaults.InfoRatePerSecond,
            _defaults.WarnRatePerSecond,
            _defaults.ErrorRatePerSecond);
        _runner.Start(_baselineScenario);
        _baselineRunning = true;
    }

    private void ToggleBaseline()
    {
        if (_baselineRunning)
        {
            _runner.Stop("Baseline");
            _baselineRunning = false;
            _baselineScenario = null;
        }
        else
        {
            StartBaseline();
        }
    }

    private void StopAllScenarios()
    {
        _runner.StopAll();
        _baselineRunning = false;
        _baselineScenario = null;
    }

    private void ShowRatesDialog()
    {
        var dialog = new Dialog
        {
            Title = "Baseline Rates",
            Width = 40,
            Height = 12
        };

        var currentInfo = _baselineScenario?.InfoRate ?? _defaults.InfoRatePerSecond;
        var currentWarn = _baselineScenario?.WarnRate ?? _defaults.WarnRatePerSecond;
        var currentErr = _baselineScenario?.ErrorRate ?? _defaults.ErrorRatePerSecond;

        var infoLabel = new Label { Text = "Info /sec:", X = 1, Y = 1 };
        var infoField = new TextField { X = 14, Y = 1, Width = 8, Text = currentInfo.ToString() };
        var warnLabel = new Label { Text = "Warn /sec:", X = 1, Y = 3 };
        var warnField = new TextField { X = 14, Y = 3, Width = 8, Text = currentWarn.ToString() };
        var errLabel = new Label { Text = "Error /sec:", X = 1, Y = 5 };
        var errField = new TextField { X = 14, Y = 5, Width = 8, Text = currentErr.ToString() };

        var okButton = new Button { Text = "OK", IsDefault = true };
        okButton.Accepting += (s, e) =>
        {
            e.Cancel = true;

            if (!int.TryParse(infoField.Text?.Trim(), out var info)
                || !int.TryParse(warnField.Text?.Trim(), out var warn)
                || !int.TryParse(errField.Text?.Trim(), out var err))
            {
                MessageBox.ErrorQuery("Invalid Input", "All rates must be valid integers.", "OK");
                return;
            }

            if (info < 0 || warn < 0 || err < 0)
            {
                MessageBox.ErrorQuery("Invalid Input", "Rates cannot be negative.", "OK");
                return;
            }

            if (_baselineRunning && _baselineScenario is not null)
            {
                _baselineScenario.InfoRate = info;
                _baselineScenario.WarnRate = warn;
                _baselineScenario.ErrorRate = err;
            }
            else
            {
                _defaults.InfoRatePerSecond = info;
                _defaults.WarnRatePerSecond = warn;
                _defaults.ErrorRatePerSecond = err;
            }

            Application.RequestStop();
        };

        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (s, e) =>
        {
            e.Cancel = true;
            Application.RequestStop();
        };

        dialog.Add(infoLabel, infoField, warnLabel, warnField, errLabel, errField);
        dialog.AddButton(okButton);
        dialog.AddButton(cancelButton);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void ShowSpikeDialog()
    {
        var dialog = new Dialog
        {
            Title = "Spike Burst",
            Width = 50,
            Height = 12
        };

        var templateLabel = new Label { Text = "Template ID:", X = 1, Y = 1 };
        var templateField = new TextField { X = 15, Y = 1, Width = 30, Text = "db-timeout" };
        var countLabel = new Label { Text = "Count:", X = 1, Y = 3 };
        var countField = new TextField { X = 15, Y = 3, Width = 10, Text = _defaults.SpikeCount.ToString() };
        var durationLabel = new Label { Text = "Duration (s):", X = 1, Y = 5 };
        var durationField = new TextField { X = 15, Y = 5, Width = 10, Text = _defaults.SpikeDurationSeconds.ToString() };

        var okButton = new Button { Text = "OK", IsDefault = true };
        okButton.Accepting += (s, e) =>
        {
            e.Cancel = true;

            var templateId = templateField.Text?.Trim() ?? "";
            if (!int.TryParse(countField.Text?.Trim(), out var count)
                || !int.TryParse(durationField.Text?.Trim(), out var duration))
            {
                MessageBox.ErrorQuery("Invalid Input", "Count and duration must be valid integers.", "OK");
                return;
            }

            if (count <= 0 || duration <= 0)
            {
                MessageBox.ErrorQuery("Invalid Input", "Count and duration must be greater than 0.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(templateId))
            {
                MessageBox.ErrorQuery("Invalid Input", "Template ID is required.", "OK");
                return;
            }

            var scenario = new SpikeScenario(_generator, templateId, count, duration);
            _runner.Start(scenario);
            Application.RequestStop();
        };

        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (s, e) =>
        {
            e.Cancel = true;
            Application.RequestStop();
        };

        dialog.Add(templateLabel, templateField, countLabel, countField, durationLabel, durationField);
        dialog.AddButton(okButton);
        dialog.AddButton(cancelButton);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void ShowDegradationDialog()
    {
        var dialog = new Dialog
        {
            Title = "Gradual Degradation",
            Width = 50,
            Height = 12
        };

        var startLabel = new Label { Text = "Start rate:", X = 1, Y = 1 };
        var startField = new TextField { X = 15, Y = 1, Width = 10, Text = "2" };
        var endLabel = new Label { Text = "End rate:", X = 1, Y = 3 };
        var endField = new TextField { X = 15, Y = 3, Width = 10, Text = "20" };
        var durationLabel = new Label { Text = "Duration (s):", X = 1, Y = 5 };
        var durationField = new TextField { X = 15, Y = 5, Width = 10, Text = _defaults.DegradationDurationSeconds.ToString() };

        var okButton = new Button { Text = "OK", IsDefault = true };
        okButton.Accepting += (s, e) =>
        {
            e.Cancel = true;

            if (!int.TryParse(startField.Text?.Trim(), out var startRate)
                || !int.TryParse(endField.Text?.Trim(), out var endRate)
                || !int.TryParse(durationField.Text?.Trim(), out var duration))
            {
                MessageBox.ErrorQuery("Invalid Input", "All fields must be valid integers.", "OK");
                return;
            }

            if (startRate <= 0 || endRate <= 0 || duration <= 0)
            {
                MessageBox.ErrorQuery("Invalid Input", "All values must be greater than 0.", "OK");
                return;
            }

            var scenario = new DegradationScenario(_generator, startRate, endRate, duration);
            _runner.Start(scenario);
            Application.RequestStop();
        };

        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (s, e) =>
        {
            e.Cancel = true;
            Application.RequestStop();
        };

        dialog.Add(startLabel, startField, endLabel, endField, durationLabel, durationField);
        dialog.AddButton(okButton);
        dialog.AddButton(cancelButton);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void ShowCorrelatedDialog()
    {
        var groups = _generator.Library.CorrelationGroups;
        if (groups.Count == 0)
        {
            MessageBox.ErrorQuery("No Groups", "No correlation groups defined in log library.", "OK");
            return;
        }

        var dialog = new Dialog
        {
            Title = "Correlated Failures",
            Width = 55,
            Height = 14
        };

        var groupLabel = new Label { Text = "Group:", X = 1, Y = 1 };
        var groupNames = groups.Select(g => g.Name).ToList();
        var groupList = new ListView
        {
            X = 15,
            Y = 1,
            Width = 35,
            Height = Math.Min(groups.Count, 5),
            Source = new ListWrapper<string>(new System.Collections.ObjectModel.ObservableCollection<string>(groupNames))
        };
        groupList.SelectedItem = 0;

        var burstLabel = new Label { Text = "Burst count:", X = 1, Y = Pos.Bottom(groupList) + 1 };
        var burstField = new TextField { X = 15, Y = Pos.Bottom(groupList) + 1, Width = 10, Text = "5" };
        var durationLabel = new Label { Text = "Duration (s):", X = 1, Y = Pos.Bottom(groupList) + 3 };
        var durationField = new TextField { X = 15, Y = Pos.Bottom(groupList) + 3, Width = 10, Text = "30" };

        var okButton = new Button { Text = "OK", IsDefault = true };
        okButton.Accepting += (s, e) =>
        {
            e.Cancel = true;

            var selectedIndex = groupList.SelectedItem;
            if (selectedIndex < 0 || selectedIndex >= groups.Count)
            {
                MessageBox.ErrorQuery("Invalid Input", "Please select a correlation group.", "OK");
                return;
            }

            if (!int.TryParse(burstField.Text?.Trim(), out var burstCount)
                || !int.TryParse(durationField.Text?.Trim(), out var duration))
            {
                MessageBox.ErrorQuery("Invalid Input", "Burst count and duration must be valid integers.", "OK");
                return;
            }

            if (burstCount <= 0 || duration <= 0)
            {
                MessageBox.ErrorQuery("Invalid Input", "Burst count and duration must be greater than 0.", "OK");
                return;
            }

            var group = groups[selectedIndex];
            var scenario = new CorrelatedScenario(_generator, group, burstCount, duration);
            _runner.Start(scenario);
            Application.RequestStop();
        };

        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (s, e) =>
        {
            e.Cancel = true;
            Application.RequestStop();
        };

        dialog.Add(groupLabel, groupList, burstLabel, burstField, durationLabel, durationField);
        dialog.AddButton(okButton);
        dialog.AddButton(cancelButton);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void ShowVolumeDialog()
    {
        var dialog = new Dialog
        {
            Title = "Volume/Load Test",
            Width = 45,
            Height = 10
        };

        var rateLabel = new Label { Text = "Rate/sec:", X = 1, Y = 1 };
        var rateField = new TextField { X = 15, Y = 1, Width = 10, Text = "100" };
        var durationLabel = new Label { Text = "Duration (s):", X = 1, Y = 3 };
        var durationField = new TextField { X = 15, Y = 3, Width = 10, Text = "30" };

        var okButton = new Button { Text = "OK", IsDefault = true };
        okButton.Accepting += (s, e) =>
        {
            e.Cancel = true;

            if (!int.TryParse(rateField.Text?.Trim(), out var rate)
                || !int.TryParse(durationField.Text?.Trim(), out var duration))
            {
                MessageBox.ErrorQuery("Invalid Input", "Rate and duration must be valid integers.", "OK");
                return;
            }

            if (rate <= 0 || duration <= 0)
            {
                MessageBox.ErrorQuery("Invalid Input", "Rate and duration must be greater than 0.", "OK");
                return;
            }

            var scenario = new VolumeScenario(_generator, rate, duration);
            _runner.Start(scenario);
            Application.RequestStop();
        };

        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (s, e) =>
        {
            e.Cancel = true;
            Application.RequestStop();
        };

        dialog.Add(rateLabel, rateField, durationLabel, durationField);
        dialog.AddButton(okButton);
        dialog.AddButton(cancelButton);
        Application.Run(dialog);
        dialog.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _generator.OnLogEmitted -= OnLogEmitted;
            _runner.OnScenarioError -= OnScenarioError;

            if (_statusTimerToken is not null)
            {
                Application.RemoveTimeout(_statusTimerToken);
                _statusTimerToken = null;
            }

            if (_logFlushTimerToken is not null)
            {
                Application.RemoveTimeout(_logFlushTimerToken);
                _logFlushTimerToken = null;
            }

            _runner.StopAll();
        }

        base.Dispose(disposing);
    }
}
