using System.Collections.ObjectModel;
using System.Text;
using SampleLog.Generation;
using SampleLog.Generation.Scenarios;
using SampleLog.Models;
using Terminal.Gui;

namespace SampleLog.UI;

public sealed class MainWindow : Window
{
    private readonly LogGenerator _generator;
    private readonly ScenarioRunner _runner;
    private readonly DefaultsConfig _defaults;

    private readonly TextView _logView;
    private readonly Label _statusLabel;

    private readonly List<string> _logLines = [];
    private const int MaxLogLines = 500;

    private long _lastEmittedCount;
    private DateTime _lastRateCheck = DateTime.UtcNow;
    private double _currentRate;

    private BaselineScenario? _baselineScenario;
    private bool _baselineRunning;
    private object? _timerToken;

    public MainWindow(LogGenerator generator, ScenarioRunner runner, DefaultsConfig defaults)
    {
        _generator = generator;
        _runner = runner;
        _defaults = defaults;

        Title = "SampleLog Generator (Q to quit)";
        ColorScheme = Colors.ColorSchemes["Base"];

        // --- Log output frame (top ~70%) ---
        var logFrame = new FrameView
        {
            Title = "Log Output",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(70)
        };

        _logView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true
        };
        logFrame.Add(_logView);

        // --- Status bar (1 line) ---
        _statusLabel = new Label
        {
            Text = "Baseline: OFF | Active: none | Total: 0 | Rate: 0/sec",
            X = 0,
            Y = Pos.Bottom(logFrame),
            Width = Dim.Fill(),
            Height = 1
        };

        // --- Command frame (bottom) ---
        var cmdFrame = new FrameView
        {
            Title = "Commands",
            X = 0,
            Y = Pos.Bottom(_statusLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        var cmdText = new Label
        {
            Text = "[1] Toggle Baseline  [2] Spike  [3] Degradation  [4] Correlated  [5] Rate  [6] Volume  [7] Stop All  [Q] Quit",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1
        };
        cmdFrame.Add(cmdText);

        Add(logFrame, _statusLabel, cmdFrame);

        // Wire log events
        _generator.OnLogEmitted += OnLogEmitted;

        // Start timer for status updates
        _timerToken = Application.AddTimeout(TimeSpan.FromMilliseconds(500), UpdateStatus);

        // Auto-start baseline if configured
        if (_defaults.BaselineEnabled)
        {
            StartBaseline();
        }
    }

    /// <summary>
    /// Handle keyboard shortcuts at the window level.
    /// </summary>
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
                ShowRateDialog();
                return true;
            case KeyCode.D6:
                ShowVolumeDialog();
                return true;
            case KeyCode.D7:
                StopAllScenarios();
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
            _logLines.Add(line);
            while (_logLines.Count > MaxLogLines)
            {
                _logLines.RemoveAt(0);
            }

            var sb = new StringBuilder();
            foreach (var l in _logLines)
            {
                sb.AppendLine(l);
            }

            _logView.Text = sb.ToString();
            _logView.MoveEnd();
        });
    }

    private bool UpdateStatus()
    {
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

        var baselineStatus = _baselineRunning
            ? $"ON ({_baselineScenario?.RatePerSecond ?? 0}/sec)"
            : "OFF";

        var activeNames = _runner.ActiveScenarios.Count > 0
            ? string.Join(", ", _runner.ActiveScenarios.Keys)
            : "none";

        _statusLabel.Text =
            $"Baseline: {baselineStatus} | Active: {activeNames} | Total: {currentCount} | Rate: {_currentRate:F1}/sec";

        return true; // keep timer running
    }

    private void StartBaseline()
    {
        _baselineScenario = new BaselineScenario(_generator, _defaults.BaselineRatePerSecond);
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

    private void ShowSpikeDialog()
    {
        var dialog = new Dialog
        {
            Title = "Trigger Spike",
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
            var templateId = templateField.Text?.Trim() ?? "";
            if (int.TryParse(countField.Text?.Trim(), out var count)
                && int.TryParse(durationField.Text?.Trim(), out var duration)
                && !string.IsNullOrEmpty(templateId))
            {
                var scenario = new SpikeScenario(_generator, templateId, count, duration);
                _runner.Start(scenario);
            }

            e.Cancel = true;
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
            Title = "Trigger Degradation",
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
            if (int.TryParse(startField.Text?.Trim(), out var startRate)
                && int.TryParse(endField.Text?.Trim(), out var endRate)
                && int.TryParse(durationField.Text?.Trim(), out var duration))
            {
                var scenario = new DegradationScenario(_generator, startRate, endRate, duration);
                _runner.Start(scenario);
            }

            e.Cancel = true;
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
            Title = "Trigger Correlated",
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
            Source = new ListWrapper<string>(new ObservableCollection<string>(groupNames))
        };
        groupList.SelectedItem = 0;

        var burstLabel = new Label { Text = "Burst count:", X = 1, Y = Pos.Bottom(groupList) + 1 };
        var burstField = new TextField { X = 15, Y = Pos.Bottom(groupList) + 1, Width = 10, Text = "5" };
        var durationLabel = new Label { Text = "Duration (s):", X = 1, Y = Pos.Bottom(groupList) + 3 };
        var durationField = new TextField { X = 15, Y = Pos.Bottom(groupList) + 3, Width = 10, Text = "30" };

        var okButton = new Button { Text = "OK", IsDefault = true };
        okButton.Accepting += (s, e) =>
        {
            var selectedIndex = groupList.SelectedItem;
            if (selectedIndex >= 0 && selectedIndex < groups.Count
                && int.TryParse(burstField.Text?.Trim(), out var burstCount)
                && int.TryParse(durationField.Text?.Trim(), out var duration))
            {
                var group = groups[selectedIndex];
                var scenario = new CorrelatedScenario(_generator, group, burstCount, duration);
                _runner.Start(scenario);
            }

            e.Cancel = true;
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

    private void ShowRateDialog()
    {
        if (!_baselineRunning || _baselineScenario is null)
        {
            MessageBox.ErrorQuery("No Baseline", "Baseline is not running. Press 1 to start it first.", "OK");
            return;
        }

        var dialog = new Dialog
        {
            Title = "Change Baseline Rate",
            Width = 45,
            Height = 8
        };

        var rateLabel = new Label { Text = "New rate/sec:", X = 1, Y = 1 };
        var rateField = new TextField { X = 16, Y = 1, Width = 10, Text = _baselineScenario.RatePerSecond.ToString() };

        var okButton = new Button { Text = "OK", IsDefault = true };
        okButton.Accepting += (s, e) =>
        {
            if (int.TryParse(rateField.Text?.Trim(), out var newRate) && newRate > 0)
            {
                _baselineScenario!.RatePerSecond = newRate;
            }

            e.Cancel = true;
            Application.RequestStop();
        };

        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (s, e) =>
        {
            e.Cancel = true;
            Application.RequestStop();
        };

        dialog.Add(rateLabel, rateField);
        dialog.AddButton(okButton);
        dialog.AddButton(cancelButton);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void ShowVolumeDialog()
    {
        var dialog = new Dialog
        {
            Title = "Trigger Volume",
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
            if (int.TryParse(rateField.Text?.Trim(), out var rate)
                && int.TryParse(durationField.Text?.Trim(), out var duration))
            {
                var scenario = new VolumeScenario(_generator, rate, duration);
                _runner.Start(scenario);
            }

            e.Cancel = true;
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

            if (_timerToken is not null)
            {
                Application.RemoveTimeout(_timerToken);
                _timerToken = null;
            }

            _runner.StopAll();
        }

        base.Dispose(disposing);
    }
}
