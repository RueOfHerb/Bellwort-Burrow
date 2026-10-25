using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Save;

namespace BellwortBurrow.Systems
{
    /// <summary>The game being played: which save slot it writes to, and starting, saving and loading it (arc-9, sys-3).</summary>
    public interface IGameSession
    {
        /// <summary>The save slot this game writes to. Empty until a game starts or loads.</summary>
        string Slot { get; }

        /// <summary>The zone the player is in, as the save header records it.</summary>
        string Zone { get; }

        /// <summary>True while the player is in the world (a zone scene). The clock and play time only run then.</summary>
        bool InWorld { get; }

        float PlaySeconds { get; }

        /// <summary>Fresh services, this profile, the first free slot and the first save. Loading the start scene is up to the caller.</summary>
        void StartNewGame(PlayerProfile profile, string startZone);

        /// <summary>
        /// Writes the game to its slot. The game saves when the player goes to bed (Sleep) and whenever a scene loads
        /// during a game (EnterScene), plus the first save from StartNewGame. The temporary debug button calls this too.
        /// </summary>
        bool Save();

        /// <summary>Going to bed (sys-9): the next day starts and the game saves. Passing out starts the next day without saving.</summary>
        void Sleep();

        /// <summary>Fresh services filled from the slot's file. False, and nothing changes, when the slot can't be read.</summary>
        bool Load(string slot);

        IReadOnlyList<SaveHeader> ListSlots();
    }

    public class GameSession : IGameSession
    {
        readonly Func<GameServices> makeServices;
        readonly SaveStore store;

        public GameServices Services { get; private set; }
        public string Slot { get; private set; } = "";
        public string Zone { get; private set; } = "";
        public bool InWorld { get; private set; }
        public float PlaySeconds { get; private set; }

        /// <summary>
        /// When set, every new game goes in this slot instead of the next free one. The Game Root sets it to the dev slot
        /// in the editor and development builds, so test saves don't pile up.
        /// </summary>
        public string NewGameSlot { get; set; } = "";

        bool HasGame => !string.IsNullOrEmpty(Slot);

        public GameSession(Func<GameServices> makeServices, SaveStore store)
        {
            this.makeServices = makeServices;
            this.store = store;
            Reset();
        }

        void Reset()
        {
            Services = makeServices();
            Services.Time.OnDayEnded += PassOut;
            ServiceLocator.Clear();
            Services.Register();
            ServiceLocator.Register<IGameSession>(this);
        }

        /// <summary>
        /// The clock reached the end of the day (9:00 AM): the player passed out and wakes on the next day straight away.
        /// Nothing is saved, so a day that ends badly can be loaded back from the last bed (Jocelyn's loop plan, Oct 25).
        /// </summary>
        void PassOut()
        {
            Services.Time.StartNextDay();
        }

        public void Sleep()
        {
            Services.Time.StartNextDay();
            Save();
        }

        /// <summary>Moves the clock and play time along, but only while the player is in the world.</summary>
        public void Tick(float deltaTime)
        {
            if (!InWorld) return;

            Services.Time.Tick(deltaTime);
            if (HasGame) PlaySeconds += deltaTime;
        }

        /// <summary>
        /// The Game Root calls this whenever a scene loads. Zones are the world: the clock runs there, and the zone goes in
        /// the save. Any other scene (the character creator, menus) pauses the clock. A game in progress saves.
        /// </summary>
        public void EnterScene(string sceneName, bool isZone)
        {
            InWorld = isZone;
            if (isZone) Zone = sceneName ?? "";
            if (HasGame) Save();
        }

        public void StartNewGame(PlayerProfile profile, string startZone)
        {
            Reset();
            Services.Profile.Set(profile);
            Slot = string.IsNullOrEmpty(NewGameSlot) ? store.FirstFreeSlot() : NewGameSlot;
            Zone = startZone ?? "";
            PlaySeconds = 0f;
            Save();
        }

        public bool Save()
        {
            if (!HasGame) return false;

            var profile = Services.Profile.Profile;
            var header = new SaveHeader
            {
                slot = Slot,
                playerName = profile?.name ?? "",
                critterKind = profile?.critterKind ?? "",
                day = Services.Time.CurrentDay,
                season = Services.Time.CurrentSeason.ToString(),
                zone = Zone,
                playSeconds = PlaySeconds,
                savedAt = DateTime.UtcNow.ToString("o")
            };

            try
            {
                store.Write(Slot, Services.Save.Capture(header));
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[Save] Couldn't write {store.PathFor(Slot)}: {e.Message}");
                return false;
            }
        }

        public bool Load(string slot)
        {
            if (!store.TryRead(slot, out var file)) return false;
            if (file.header.version > SaveService.CurrentVersion)
            {
                Debug.LogWarning($"[Save] {slot} was saved by a newer version of the game (save version {file.header.version}), so it wasn't loaded.");
                return false;
            }

            Reset();
            Services.Save.Restore(file);
            Slot = slot;
            Zone = file.header.zone ?? "";
            PlaySeconds = file.header.playSeconds;
            return true;
        }

        public IReadOnlyList<SaveHeader> ListSlots() => store.ListSlots();
    }
}
