using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Threading;
using Pomodoro.Locales;
using Pomodoro.Models;

namespace Pomodoro.ViewModels;

public class PomodoroViewModel : INotifyPropertyChanged
{
    private PomodoroModel model;
    private PomodoroPhase phase = PomodoroPhase.Focus;
    private PomodoroState state = PomodoroState.Idle;
    private int remainingSeconds;
    private int totalSeconds;
    private int completedSessions;
    private readonly DispatcherTimer timer;

    public PomodoroViewModel(PomodoroModel? initialModel = null)
    {
        model = initialModel ?? new PomodoroModel();
        totalSeconds = model.FocusMinutes * 60;
        remainingSeconds = totalSeconds;

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += OnTimerTick;
    }

    public PomodoroModel Model => model;

    public PomodoroPhase Phase
    {
        get => phase;
        private set
        {
            if (phase != value)
            {
                phase = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PhaseTitle));
                OnPropertyChanged(nameof(PhaseBrush));
                OnPropertyChanged(nameof(IsFocusPhase));
                OnPropertyChanged(nameof(IsShortBreakPhase));
                OnPropertyChanged(nameof(IsLongBreakPhase));
            }
        }
    }

    public PomodoroState State
    {
        get => state;
        private set
        {
            if (state != value)
            {
                state = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(PlayPauseIconData));
                OnPropertyChanged(nameof(PlayPauseTooltip));
            }
        }
    }

    public int RemainingSeconds
    {
        get => remainingSeconds;
        private set
        {
            if (remainingSeconds != value)
            {
                remainingSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeText));
                OnPropertyChanged(nameof(StrokeDashOffset));
                OnPropertyChanged(nameof(ProgressFraction));
            }
        }
    }

    public int TotalSeconds
    {
        get => totalSeconds;
        private set
        {
            if (totalSeconds != value)
            {
                totalSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StrokeDashOffset));
                OnPropertyChanged(nameof(ProgressFraction));
            }
        }
    }

    public int CompletedSessions
    {
        get => completedSessions;
        private set
        {
            if (completedSessions != value)
            {
                completedSessions = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SessionCounterText));
                OnPropertyChanged(nameof(CycleProgressText));
            }
        }
    }

    public bool IsRunning => State == PomodoroState.Running;
    public bool IsFocusPhase => Phase == PomodoroPhase.Focus;
    public bool IsShortBreakPhase => Phase == PomodoroPhase.ShortBreak;
    public bool IsLongBreakPhase => Phase == PomodoroPhase.LongBreak;

    public string TimeText => $"{(RemainingSeconds / 60):D2}:{(RemainingSeconds % 60):D2}";

    public string PhaseTitle => Phase switch
    {
        PomodoroPhase.Focus => Locale.Pomodoro_Phase_Focus,
        PomodoroPhase.ShortBreak => Locale.Pomodoro_Phase_ShortBreak,
        PomodoroPhase.LongBreak => Locale.Pomodoro_Phase_LongBreak,
        _ => Locale.Pomodoro_Phase_Focus
    };

    public IBrush PhaseBrush => Phase switch
    {
        PomodoroPhase.Focus => new SolidColorBrush(Color.Parse("#FF5A5F")),
        PomodoroPhase.ShortBreak => new SolidColorBrush(Color.Parse("#34C759")),
        PomodoroPhase.LongBreak => new SolidColorBrush(Color.Parse("#007AFF")),
        _ => new SolidColorBrush(Color.Parse("#FF5A5F"))
    };

    public double ProgressFraction => TotalSeconds > 0 ? (double)RemainingSeconds / TotalSeconds : 0.0;

    /// <summary>
    /// For EllipseGeometry circumference ~31.4 (radius 50, viewbox scaled).
    /// Starts at 0 (full circle), increases to 31.4 as time expires.
    /// </summary>
    public double StrokeDashOffset => 31.4 * (1.0 - ProgressFraction);

    public string SessionCounterText => $"🍅 × {CompletedSessions}";

    public string CycleProgressText
    {
        get
        {
            int interval = Math.Max(1, model.LongBreakInterval);
            int currentInCycle = (CompletedSessions % interval) + 1;
            return $"周期 {currentInCycle}/{interval}";
        }
    }

    // Play icon: triangle, Pause icon: two bars
    public string PlayPauseIconData => IsRunning
        ? "M6 19h4V5H6v14zm8-14v14h4V5h-4z"
        : "M8 5v14l11-7z";

    public string PlayPauseTooltip => IsRunning ? Locale.Pomodoro_Pause : Locale.Pomodoro_Start;

    private int GetPhaseMinutes(PomodoroPhase p) => p switch
    {
        PomodoroPhase.Focus => Math.Max(1, model.FocusMinutes),
        PomodoroPhase.ShortBreak => Math.Max(1, model.ShortBreakMinutes),
        PomodoroPhase.LongBreak => Math.Max(1, model.LongBreakMinutes),
        _ => 25
    };

    public void UpdateModel(PomodoroModel newModel)
    {
        model = newModel;
        if (State == PomodoroState.Idle)
        {
            TotalSeconds = GetPhaseMinutes(Phase) * 60;
            RemainingSeconds = TotalSeconds;
        }
        OnPropertyChanged(nameof(CycleProgressText));
    }

    public void StartOrPause()
    {
        if (IsRunning)
        {
            timer.Stop();
            State = PomodoroState.Paused;
        }
        else
        {
            if (RemainingSeconds <= 0)
            {
                TotalSeconds = GetPhaseMinutes(Phase) * 60;
                RemainingSeconds = TotalSeconds;
            }
            timer.Start();
            State = PomodoroState.Running;
        }
    }

    public void Reset()
    {
        timer.Stop();
        State = PomodoroState.Idle;
        TotalSeconds = GetPhaseMinutes(Phase) * 60;
        RemainingSeconds = TotalSeconds;
    }

    public void Skip()
    {
        AdvanceToNextPhase(autoStart: false);
    }

    public void SwitchPhase(PomodoroPhase next)
    {
        timer.Stop();
        State = PomodoroState.Idle;
        Phase = next;
        TotalSeconds = GetPhaseMinutes(Phase) * 60;
        RemainingSeconds = TotalSeconds;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (RemainingSeconds > 0)
        {
            RemainingSeconds--;
        }
        else
        {
            // Phase complete!
            bool autoStart;
            if (Phase == PomodoroPhase.Focus)
            {
                CompletedSessions++;
                autoStart = model.AutoStartBreaks;
            }
            else
            {
                autoStart = model.AutoStartFocus;
            }

            AdvanceToNextPhase(autoStart);
        }
    }

    private void AdvanceToNextPhase(bool autoStart)
    {
        timer.Stop();

        if (Phase == PomodoroPhase.Focus)
        {
            int interval = Math.Max(1, model.LongBreakInterval);
            if (CompletedSessions > 0 && CompletedSessions % interval == 0)
            {
                Phase = PomodoroPhase.LongBreak;
            }
            else
            {
                Phase = PomodoroPhase.ShortBreak;
            }
        }
        else
        {
            Phase = PomodoroPhase.Focus;
        }

        TotalSeconds = GetPhaseMinutes(Phase) * 60;
        RemainingSeconds = TotalSeconds;

        if (autoStart)
        {
            timer.Start();
            State = PomodoroState.Running;
        }
        else
        {
            State = PomodoroState.Idle;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
