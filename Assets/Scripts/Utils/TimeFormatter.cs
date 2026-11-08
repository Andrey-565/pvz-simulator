using System;
using UnityEngine;

namespace PVZ.Utils
{
    /// <summary>
    /// Utility class for formatting seconds into human-readable minutes and seconds (e.g. "1 мин. 40 сек.").
    /// Used across all game timers: delivery arrival countdowns, cooldowns, truck departure, and customer service.
    /// </summary>
    public static class TimeFormatter
    {
        /// <summary>
        /// Formats total seconds into "X мин. Y сек." format.
        /// Examples:
        /// - 100 sec -> "1 мин. 40 сек."
        /// - 120 sec -> "2 мин. 00 сек."
        /// - 45 sec  -> "0 мин. 45 сек." (or "45 сек." if omitZeroMinutes is true)
        /// - 5 sec   -> "0 мин. 05 сек." (or "5 сек." if omitZeroMinutes is true)
        /// </summary>
        /// <param name="totalSeconds">Total time in seconds.</param>
        /// <param name="omitZeroMinutes">When true, times under 60s omit the "0 мин." prefix.</param>
        public static string FormatMinutesSeconds(float totalSeconds, bool omitZeroMinutes = false)
        {
            if (totalSeconds < 0f)
            {
                totalSeconds = 0f;
            }

            int total = Mathf.CeilToInt(totalSeconds);
            int minutes = total / 60;
            int seconds = total % 60;

            if (minutes > 0 || !omitZeroMinutes)
            {
                return $"{minutes} мин. {seconds:D2} сек.";
            }

            return $"{seconds} сек.";
        }

        /// <summary>
        /// Formats elapsed time with fractional seconds (e.g. for customer speed tags: "1 мин. 15.2 сек.").
        /// </summary>
        public static string FormatMinutesSecondsFractional(float totalSeconds)
        {
            if (totalSeconds < 0f)
            {
                totalSeconds = 0f;
            }

            int minutes = Mathf.FloorToInt(totalSeconds / 60f);
            float remainingSeconds = totalSeconds % 60f;

            if (minutes > 0)
            {
                return $"{minutes} мин. {remainingSeconds:F1} сек.";
            }

            return $"{remainingSeconds:F1} сек.";
        }
    }
}
