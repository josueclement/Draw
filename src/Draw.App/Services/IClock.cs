using System;

namespace Draw.App.Services;

/// <summary>Abstraction over the system clock so time-dependent code is testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
