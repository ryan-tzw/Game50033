namespace Game50033
{
    using System;

    /// <summary>
    /// The short list of things an encounter may know about any boss, whichever boss script it is:
    /// whether it is beaten, what each hit actually did, and which phase it is in.
    /// The EncounterManager listens to these to decide victory, credit damage to players and pick
    /// the Discordance fill rate. Everything else about the boss stays inside the boss.
    /// </summary>
    public interface IEncounterBoss
    {
        bool IsDefeated { get; }

        /// <summary>Current phase, starting at 1.</summary>
        int Phase { get; }

        /// <summary>Raised once, when the boss is beaten (before it hides or plays a death).</summary>
        event Action<IEncounterBoss> Defeated;

        /// <summary>Raised for every accepted hit with the damage the boss actually lost.</summary>
        event Action<HitInfo, float> Damaged;

        /// <summary>Raised with the new phase number when the boss changes phase.</summary>
        event Action<int> PhaseChanged;
    }
}
