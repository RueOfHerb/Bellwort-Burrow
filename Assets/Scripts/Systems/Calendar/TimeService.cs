using System;
using BellwortBurrow.Core;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Calendar
{
    public class TimeService : ITimeService
    {
        readonly CalendarConfig calendar;
        readonly VoidEventChannel dayChangedChannel;

        float minuteAccumulator;
        int lastHour;

        public int MinutesSinceMidnight { get; private set; }
        public int CurrentDay { get; private set; } = 1;
        public Season CurrentSeason { get; private set; } = Season.Spring;

        public event Action OnHourChanged;
        public event Action OnDayChanged;
        public event Action<Season> OnSeasonChanged;

        public TimeService(CalendarConfig calendar, VoidEventChannel dayChangedChannel = null)
        {
            this.calendar = calendar;
            this.dayChangedChannel = dayChangedChannel;
        }

        public void Tick(float deltaTime)
        {
            minuteAccumulator += deltaTime / calendar.RealSecondsPerInGameMinute;

            while (minuteAccumulator >= 1f)
            {
                minuteAccumulator -= 1f;
                AdvanceMinute();
            }
        }

        void AdvanceMinute()
        {
            MinutesSinceMidnight++;

            int currentHour = MinutesSinceMidnight / calendar.MinutesPerHour;
            if (currentHour != lastHour)
            {
                lastHour = currentHour;
                OnHourChanged?.Invoke();
            }

            if (MinutesSinceMidnight >= calendar.MinutesPerDay)
            {
                MinutesSinceMidnight = 0;
                lastHour = 0;
                AdvanceDay();
            }
        }

        void AdvanceDay()
        {
            CurrentDay++;
            OnDayChanged?.Invoke();
            dayChangedChannel?.Raise();

            int dayOfSeason = (CurrentDay - 1) % calendar.DaysPerSeason;
            if (dayOfSeason == 0 && CurrentDay > 1)
            {
                int seasonCount = Enum.GetValues(typeof(Season)).Length;
                CurrentSeason = (Season)(((int)CurrentSeason + 1) % seasonCount);
                OnSeasonChanged?.Invoke(CurrentSeason);
            }
        }
    }
}
