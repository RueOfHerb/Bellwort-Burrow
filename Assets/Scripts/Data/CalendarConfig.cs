using UnityEngine;

namespace BellwortBurrow.Data
{
    [CreateAssetMenu(menuName = "Bellwort Burrow/Time/Calendar Config", fileName = "CalendarConfig")]
    public class CalendarConfig : ScriptableObject
    {
        [SerializeField, Min(1)] int daysPerSeason = 28;
        [SerializeField, Min(0.01f)] float realSecondsPerInGameMinute = 0.7f;
        [SerializeField, Min(1)] int minutesPerHour = 60;
        [SerializeField, Min(1)] int hoursPerDay = 24;

        public int DaysPerSeason => daysPerSeason;
        public float RealSecondsPerInGameMinute => realSecondsPerInGameMinute;
        public int MinutesPerHour => minutesPerHour;
        public int HoursPerDay => hoursPerDay;
        public int MinutesPerDay => minutesPerHour * hoursPerDay;

        internal void ConfigureForTesting(int daysPerSeason, float realSecondsPerInGameMinute, int minutesPerHour, int hoursPerDay)
        {
            this.daysPerSeason = daysPerSeason;
            this.realSecondsPerInGameMinute = realSecondsPerInGameMinute;
            this.minutesPerHour = minutesPerHour;
            this.hoursPerDay = hoursPerDay;
        }
    }
}
