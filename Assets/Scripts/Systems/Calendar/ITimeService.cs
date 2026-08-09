using System;
using BellwortBurrow.Core;

namespace BellwortBurrow.Systems.Calendar
{
    public interface ITimeService
    {
        int MinutesSinceMidnight { get; }
        int CurrentDay { get; }
        Season CurrentSeason { get; }

        event Action OnHourChanged;
        event Action OnDayChanged;
        event Action<Season> OnSeasonChanged;

        void Tick(float deltaTime);
    }
}
