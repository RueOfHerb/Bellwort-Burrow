using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>
    /// How the clock runs. The player is up through the night: a day starts when they wake (7:00 PM) and ends when
    /// they pass out (9:00 AM). The clock itself still counts minutes from midnight.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Time/Calendar Config", fileName = "CalendarConfig")]
    public class CalendarConfig : ScriptableObject
    {
        [SerializeField, Min(1)] int daysPerSeason = 28;
        [SerializeField, Min(0.01f)] float realSecondsPerInGameMinute = 0.7f;
        [SerializeField, Min(1)] int minutesPerHour = 60;
        [SerializeField, Min(1)] int hoursPerDay = 24;
        [Tooltip("The hour the player wakes and a day starts (19 is 7:00 PM). New games start here.")]
        [SerializeField, Min(0)] int wakeHour = 19;
        [Tooltip("The hour the player passes out and the day ends, if they haven't gone to bed (9 is 9:00 AM).")]
        [SerializeField, Min(0)] int passOutHour = 9;

        public int DaysPerSeason => daysPerSeason;
        public float RealSecondsPerInGameMinute => realSecondsPerInGameMinute;
        public int MinutesPerHour => minutesPerHour;
        public int HoursPerDay => hoursPerDay;
        public int MinutesPerDay => minutesPerHour * hoursPerDay;

        /// <summary>Minutes from midnight when a day starts.</summary>
        public int WakeMinute => (wakeHour % hoursPerDay) * minutesPerHour;

        /// <summary>Minutes from midnight when the day ends.</summary>
        public int PassOutMinute => (passOutHour % hoursPerDay) * minutesPerHour;

        /// <summary>How long a day is, from waking to passing out (a whole day when the two hours are the same).</summary>
        public int AwakeMinutes
        {
            get
            {
                int minutes = (PassOutMinute - WakeMinute + MinutesPerDay) % MinutesPerDay;
                return minutes == 0 ? MinutesPerDay : minutes;
            }
        }

        internal void ConfigureForTesting(int daysPerSeason, float realSecondsPerInGameMinute, int minutesPerHour, int hoursPerDay,
            int wakeHour = 19, int passOutHour = 9)
        {
            this.daysPerSeason = daysPerSeason;
            this.realSecondsPerInGameMinute = realSecondsPerInGameMinute;
            this.minutesPerHour = minutesPerHour;
            this.hoursPerDay = hoursPerDay;
            this.wakeHour = wakeHour;
            this.passOutHour = passOutHour;
        }
    }
}
