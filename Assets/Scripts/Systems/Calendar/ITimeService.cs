using System;
using BellwortBurrow.Core;

namespace BellwortBurrow.Systems.Calendar
{
    /// <summary>
    /// The clock. A day runs from when the player wakes (7:00 PM) to when they pass out (9:00 AM): the day number turns
    /// when the next one starts, not at midnight.
    /// </summary>
    public interface ITimeService
    {
        /// <summary>The time of day, in minutes from midnight.</summary>
        int MinutesSinceMidnight { get; }

        /// <summary>Minutes since the player woke today.</summary>
        int MinutesAwake { get; }

        int CurrentDay { get; }
        Season CurrentSeason { get; }

        /// <summary>True from the moment the player passes out until the next day starts. The clock holds still.</summary>
        bool DayOver { get; }

        event Action OnHourChanged;

        /// <summary>The player passed out: the clock reached the end of the day.</summary>
        event Action OnDayEnded;

        /// <summary>A new day started: the player woke on the next day.</summary>
        event Action OnDayChanged;

        event Action<Season> OnSeasonChanged;

        void Tick(float deltaTime);

        /// <summary>Wakes the player on the next day: the next day number, the clock at the wake time.</summary>
        void StartNextDay();
    }
}
