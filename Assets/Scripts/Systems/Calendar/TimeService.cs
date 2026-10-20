using System;
using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Calendar
{
    public class TimeService : ITimeService, ISaveable
    {
        [Serializable]
        class CalendarState
        {
            public int day;
            public string season;
            public int minutesSinceMidnight;
        }

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

        public string SaveKey => "Calendar";

        public string CaptureState() => JsonUtility.ToJson(new CalendarState
        {
            day = CurrentDay,
            season = CurrentSeason.ToString(),
            minutesSinceMidnight = MinutesSinceMidnight
        });

        public void RestoreState(string json)
        {
            var state = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<CalendarState>(json);
            if (state == null) return;

            CurrentDay = Math.Max(1, state.day);
            CurrentSeason = Enum.TryParse(state.season, out Season season) ? season : Season.Spring;
            MinutesSinceMidnight = Math.Max(0, state.minutesSinceMidnight);
            lastHour = calendar != null ? MinutesSinceMidnight / calendar.MinutesPerHour : 0;
            minuteAccumulator = 0f;
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
