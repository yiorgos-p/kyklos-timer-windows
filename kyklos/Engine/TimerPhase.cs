namespace Kyklos.Engine;

/// <summary>The rhythm: focus, then a break, with a long break after every few focus sessions.</summary>
public enum TimerPhase
{
    Focus,
    ShortBreak,
    LongBreak,
}

public static class TimerPhaseExtensions
{
    extension(TimerPhase phase)
    {
        /// <summary>The phase's name, in sentence case as Windows writes it.</summary>
        public string Title => phase switch
        {
            TimerPhase.ShortBreak => "Short break",
            TimerPhase.LongBreak => "Long break",
            _ => "Focus",
        };

        /// <summary>The length a new install starts with.</summary>
        public int DefaultMinutes => phase switch
        {
            TimerPhase.ShortBreak => 5,
            TimerPhase.LongBreak => 10,
            _ => 25,
        };

        /// <summary>The longest the preset can be: the whole dial, or half of it for a short break.</summary>
        public int MaximumMinutes => phase == TimerPhase.ShortBreak ? 30 : TimerViewModel.MaximumMinutes;
    }
}
