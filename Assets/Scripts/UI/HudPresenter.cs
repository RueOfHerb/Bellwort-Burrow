using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Systems.Calendar;

namespace BellwortBurrow.UI
{
    public class HudPresenter : MonoBehaviour
    {
        ITimeService timeService;

        void Start()
        {
            timeService = ServiceLocator.Get<ITimeService>();
            timeService.OnHourChanged += Refresh;
        }

        void OnDestroy()
        {
            if (timeService != null)
                timeService.OnHourChanged -= Refresh;
        }

        void Refresh()
        {
        }
    }
}
