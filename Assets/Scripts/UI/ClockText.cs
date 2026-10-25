using BellwortBurrow.Core;

namespace BellwortBurrow.UI
{
    /// <summary>The words on the HUD's clock (ui-3): "Day 3 · Spring" and "7:00 PM", from the time service's numbers.</summary>
    public static class ClockText
    {
        public static string Day(int day, Season season) => $"Day {day} · {season}";

        /// <summary>A 12 hour clock with AM and PM, the minutes rounded down to ten ("7:10 PM"), since one minute ticks by too fast to read.</summary>
        public static string Time(int minutesSinceMidnight, int minutesPerDay = 1440)
        {
            if (minutesPerDay <= 0) minutesPerDay = 1440;
            int minutes = ((minutesSinceMidnight % minutesPerDay) + minutesPerDay) % minutesPerDay;
            int hour24 = minutes / 60;
            int minute = minutes % 60 / 10 * 10;
            int hour12 = hour24 % 12 == 0 ? 12 : hour24 % 12;
            string half = hour24 < 12 ? "AM" : "PM";
            return $"{hour12}:{minute:00} {half}";
        }
    }
}
