using System;

namespace Draw.App.Services;

/// <summary>Default <see cref="IClock"/> backed by the operating system clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
