using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Calendar;
using BellwortBurrow.Systems.Professions;
using BellwortBurrow.Systems.Professions.Botany;
using BellwortBurrow.Systems.Inventory;
using BellwortBurrow.Systems.Npcs;
using BellwortBurrow.Systems.Dialogue;
using BellwortBurrow.Systems.Relationships;
using BellwortBurrow.Systems.Save;

namespace BellwortBurrow.Systems
{
    public class Bootstrapper : MonoBehaviour
    {
        [SerializeField] CalendarConfig calendarConfig;
        [SerializeField] VoidEventChannel dayChangedChannel;

        void Awake()
        {
            ServiceLocator.Clear();

            var timeService = new TimeService(calendarConfig, dayChangedChannel);
            var professionService = new ProfessionService();
            var botanyPlotService = new BotanyPlotService(professionService, timeService);
            var inventoryService = new InventoryService();
            var npcScheduleService = new NpcScheduleService();
            var dialogueService = new DialogueService();
            var relationshipService = new RelationshipService();
            var saveService = new SaveService();

            saveService.Register(inventoryService);
            saveService.Register(relationshipService);

            ServiceLocator.Register<ITimeService>(timeService);
            ServiceLocator.Register<IProfessionService>(professionService);
            ServiceLocator.Register(botanyPlotService);
            ServiceLocator.Register<IInventoryService>(inventoryService);
            ServiceLocator.Register<INpcScheduleService>(npcScheduleService);
            ServiceLocator.Register<IDialogueService>(dialogueService);
            ServiceLocator.Register<IRelationshipService>(relationshipService);
            ServiceLocator.Register<ISaveService>(saveService);

            Debug.Log("[Bootstrapper] All services registered.");
        }

        void Update()
        {
            ServiceLocator.Get<ITimeService>().Tick(UnityEngine.Time.deltaTime);
        }
    }
}
