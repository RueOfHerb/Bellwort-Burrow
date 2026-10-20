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

        float PlaySeconds { get; }

        /// <summary>Fresh services, this profile, the first free slot and the first save. Loading the start scene is up to the caller.</summary>
        void StartNewGame(PlayerProfile profile, string startZone);

        /// <summary>
        /// Writes the game to its slot. The game saves only when the player goes to bed (not built yet, so that call is waiting
        /// for it), plus the first save from StartNewGame. The temporary debug button calls this too.
        /// </summary>
        bool Save();

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
        public string Zone { get; set; } = "";
        public float PlaySeconds { get; private set; }

        public GameSession(Func<GameServices> makeServices, SaveStore store)
        {
            this.makeServices = makeServices;
            this.store = store;
            Reset();
        }

        void Reset()
        {
            Services = makeServices();
            ServiceLocator.Clear();
            Services.Register();
            ServiceLocator.Register<IGameSession>(this);
        }

        public void Tick(float deltaTime)
        {
            Services.Time.Tick(deltaTime);
            if (!string.IsNullOrEmpty(Slot)) PlaySeconds += deltaTime;
        }

        public void StartNewGame(PlayerProfile profile, string startZone)
        {
            Reset();
            Services.Profile.Set(profile);
            Slot = store.FirstFreeSlot();
            Zone = startZone ?? "";
            PlaySeconds = 0f;
            Save();
        }

        public bool Save()
        {
            if (string.IsNullOrEmpty(Slot)) return false;

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
