namespace TakeInitiative.Api.Tests.Integration;

/// <summary>
/// The real clock plus an offset. Unlike <c>FakeTimeProvider</c> it keeps moving and can
/// be moved back, so one fixture can serve tests that shift it and tests that do not.
/// </summary>
public class ShiftableTimeProvider : TimeProvider
{
    public TimeSpan Offset { get; set; } = TimeSpan.Zero;

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + Offset;

    /// <summary>Moves the clock forward until the returned scope is disposed.</summary>
    public IDisposable Advance(TimeSpan by)
    {
        Offset += by;
        return new Reset(this, by);
    }

    private sealed class Reset(ShiftableTimeProvider clock, TimeSpan by) : IDisposable
    {
        public void Dispose() => clock.Offset -= by;
    }
}
