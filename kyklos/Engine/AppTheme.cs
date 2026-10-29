namespace Kyklos.Engine;

/// <summary>"App theme" in Settings: follow Windows, or always light or dark.</summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>A window's top-left corner, in screen pixels.</summary>
public readonly record struct WindowPosition(int X, int Y);
