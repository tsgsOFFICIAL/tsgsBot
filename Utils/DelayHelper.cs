namespace tsgsBot_C_.Utils;

public static class DelayHelper
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(int.MaxValue - 1);

    public static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
            return;

        TimeSpan remaining = delay;

        while (remaining > MaxDelay)
        {
            await Task.Delay(MaxDelay, cancellationToken);
            remaining -= MaxDelay;
        }

        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining, cancellationToken);
    }
}