using System;
using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Calendar
{
    /// <summary>
    /// The clock (see <see cref="ITimeService"/>). New games start on day 1 at the wake time. When the clock reaches
    /// the pass out time the day is over and the clock holds still until something starts the next day (going to bed,
    /// or passing out; for now the game session does it straight away).
    /// </summary>
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
        public bool DayOver { get; private set; }

        public int MinutesAwake => calendar == null ? 0
            : (MinutesSinceMidnight - calendar.WakeMinute + calendar.MinutesPerDay) % calendar.MinutesPerDay;

        public event Action OnHourChanged;
        public event Action OnDayEnded;
        public event Action OnDayChanged;
        public event Action<Season> OnSeasonChanged;

        public TimeService(CalendarConfig calendar, VoidEventChannel dayChangedChannel = null)
        {
            this.calendar = calendar;
            this.dayChangedChannel = dayChangedChannel;
            if (calendar != null) SetClock(calendar.WakeMinute);
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
            SetClock(Math.Max(0, state.minutesSinceMidnight));
            DayOver = false;
        }

        public void Tick(float deltaTime)
        {
            if (DayOver) return;

            // A save made outside the waking hours (before days ran 7 PM to 9 AM) ends its day at once.
            if (MinutesAwake >= calendar.AwakeMinutes)
            {
                EndDay();
                return;
            }

            minuteAccumulator += deltaTime / calendar.RealSecondsPerInGameMinute;

            while (minuteAccumulator >= 1f && !DayOver)
            {
                minuteAccumulator -= 1f;
                AdvanceMinute();
            }
        }

        public void StartNextDay()
        {
            DayOver = false;
            SetClock(calendar.WakeMinute);
            OnHourChanged?.Invoke();
            AdvanceDay();
        }

        void SetClock(int minutesSinceMidnight)
        {
            MinutesSinceMidnight = calendar != null ? minutesSinceMidnight % calendar.MinutesPerDay : minutesSinceMidnight;
            lastHour = calendar != null ? MinutesSinceMidnight / calendar.MinutesPerHour : 0;
            minuteAccumulator = 0f;
        }

        void AdvanceMinute()
        {
            MinutesSinceMidnight = (MinutesSinceMidnight + 1) % calendar.MinutesPerDay;

            int currentHour = MinutesSinceMidnight / calendar.MinutesPerHour;
            if (currentHour != lastHour)
            {
                lastHour = currentHour;
                OnHourChanged?.Invoke();
            }

            if (MinutesAwake == calendar.AwakeMinutes % calendar.MinutesPerDay)
                EndDay();
        }

        void EndDay()
        {
            DayOver = true;
            minuteAccumulator = 0f;
            OnDayEnded?.Invoke();
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
