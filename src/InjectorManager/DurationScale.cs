using UnityEngine;

namespace InjectorManager;

internal static class DurationScale
{
    internal const float MaximumSeconds = 24f * 60f * 60f;

    private const float FirstBoundary = 1f / 3f;
    private const float SecondBoundary = 2f / 3f;
    private static readonly float[] LongDurations =
    {
        15f * 60f,
        20f * 60f,
        30f * 60f,
        45f * 60f,
        60f * 60f,
        90f * 60f,
        2f * 60f * 60f,
        3f * 60f * 60f,
        4f * 60f * 60f,
        6f * 60f * 60f,
        8f * 60f * 60f,
        12f * 60f * 60f,
        18f * 60f * 60f,
        MaximumSeconds
    };

    internal static float FromPosition(float position)
    {
        position = Mathf.Clamp01(position);
        if (position <= FirstBoundary)
        {
            return Mathf.Round(position / FirstBoundary * 300f);
        }

        if (position <= SecondBoundary)
        {
            var minuteStep = Mathf.Round((position - FirstBoundary) / FirstBoundary * 10f);
            return 300f + minuteStep * 60f;
        }

        var index = Mathf.RoundToInt(
            (position - SecondBoundary) / FirstBoundary * (LongDurations.Length - 1));
        index = Mathf.Clamp(index, 0, LongDurations.Length - 1);
        return LongDurations[index];
    }

    internal static float ToPosition(float seconds)
    {
        seconds = Mathf.Clamp(seconds, 0f, MaximumSeconds);
        if (seconds <= 300f)
        {
            return seconds / 300f * FirstBoundary;
        }

        if (seconds <= 900f)
        {
            return FirstBoundary + (seconds - 300f) / 600f * FirstBoundary;
        }

        var nearestIndex = 0;
        var nearestDistance = float.MaxValue;
        for (var index = 0; index < LongDurations.Length; index++)
        {
            var distance = Mathf.Abs(LongDurations[index] - seconds);
            if (distance < nearestDistance)
            {
                nearestIndex = index;
                nearestDistance = distance;
            }
        }

        return SecondBoundary + nearestIndex / (float)(LongDurations.Length - 1) * FirstBoundary;
    }
}
