// LogHelpers - small shared helpers for the clock and number formatting, so every log class does them the same way.

using System;
using System.Globalization;
using UnityEngine;

namespace DataLogging
{
    internal static class LogHelpers
    {
        /// <summary>Seconds since the game started (real time, not paused by Time.timeScale).</summary>
        public static double Now => Time.realtimeSinceStartupAsDouble;

        /// <summary>The current UTC time as text, e.g. 2026-11-17T22:07:13.481Z</summary>
        public static string IsoNow() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        /// <summary>Durations are stored with 3 decimals (milliseconds).</summary>
        public static double Round(double value) => Math.Round(value, 3);

        /// <summary>Seconds since a moment from Now, or null if that moment never happened (-1).</summary>
        public static object SecondsSince(double moment) => moment >= 0 ? Round(Now - moment) : (object)null;
    }
}
