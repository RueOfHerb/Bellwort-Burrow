using System;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Data.Professions.Botany;
using BellwortBurrow.Systems.Calendar;
using BellwortBurrow.Systems.Dialogue;
using BellwortBurrow.Systems.Inventory;
using BellwortBurrow.Systems.Npcs;
using BellwortBurrow.Systems.Professions;
using BellwortBurrow.Systems.Professions.Botany;
using BellwortBurrow.Systems.Profile;
using BellwortBurrow.Systems.Relationships;
using BellwortBurrow.Systems.Save;

namespace BellwortBurrow.Systems
{
    /// <summary>
    /// One full set of gameplay services (arc-9). A new game or a loaded save gets a fresh set,
    /// so nothing from the last game leaks into the next.
    /// </summary>
    public sealed class GameServices
    {
        public TimeService Time { get; }
        public ProfessionService Professions { get; }
        public BotanyPlotService Botany { get; }
        public InventoryService Inventory { get; }
        public NpcScheduleService Npcs { get; }
        public DialogueService Dialogue { get; }
        public RelationshipService Relationships { get; }
        public PlayerProfileService Profile { get; }
        public SaveService Save { get; }

        /// <param name="findItem">Finds an item by id when a save is loaded (the Item Catalog's Find).</param>
        /// <param name="findCrop">Finds a crop by id when a save is loaded (the Crop Catalog's Find).</param>
        public GameServices(CalendarConfig calendar, VoidEventChannel dayChangedChannel = null, Func<string, ItemDefinition> findItem = null,
            Func<string, BotanyCropDefinition> findCrop = null)
        {
            Time = new TimeService(calendar, dayChangedChannel);
            Professions = new ProfessionService();
            Botany = new BotanyPlotService(Professions, Time, findCrop);
            Inventory = new InventoryService(findItem: findItem);
            Npcs = new NpcScheduleService();
            Dialogue = new DialogueService();
            Relationships = new RelationshipService();
            Profile = new PlayerProfileService();
            Save = new SaveService();

            // Each one becomes a section of the save file, holding ids rather than assets.
            Save.Register(Profile);
            Save.Register(Time);
            Save.Register(Inventory);
            Save.Register(Relationships);
            Save.Register(Professions);
            Save.Register(Botany);
        }

        /// <summary>Puts this set in the service locator, replacing whatever set was there.</summary>
        public void Register()
        {
            ServiceLocator.Register<ITimeService>(Time);
            ServiceLocator.Register<IProfessionService>(Professions);
            ServiceLocator.Register(Botany);
            ServiceLocator.Register<IInventoryService>(Inventory);
            ServiceLocator.Register<INpcScheduleService>(Npcs);
            ServiceLocator.Register<IDialogueService>(Dialogue);
            ServiceLocator.Register<IRelationshipService>(Relationships);
            ServiceLocator.Register<IPlayerProfileService>(Profile);
            ServiceLocator.Register<ISaveService>(Save);
        }
    }
}
