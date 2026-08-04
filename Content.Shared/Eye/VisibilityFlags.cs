using Robust.Shared.Serialization;

namespace Content.Shared.Eye
{
    [Flags]
    [FlagsFor(typeof(VisibilityMaskLayer))]
    public enum VisibilityFlags : int
    {
        None = 0,
        Normal = 1 << 0,
        Ghost  = 1 << 1,
        Subfloor = 1 << 2,
        PhantomVessel = 1 << 3, // ADT Phantom
        Narcotic = 1 << 4, // ADT-Changeling-Tweak
        Schizo = 1 << 5, // ADT-Changeling-Tweak
        LingToxin = 1 << 6, // ADT-Changeling-Tweak
        Eldritch = 1 << 7, // ADT-Tweak Heretic
        Bubblegum = 1 << 8, // ADT-Tweak Bubblegum
        // ADT RTS: по биту на команду матча. Вражеская сущность получает бит команды,
        // которая её видит, и теряет его, когда уходит в туман, — так враг не попадает
        // в PVS вовсе. См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §6.3.
        RtsTeamA = 1 << 9,
        RtsTeamB = 1 << 10,
    }
}
