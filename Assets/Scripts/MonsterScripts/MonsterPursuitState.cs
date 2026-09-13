using System;
using RunawayChimps.Travel;

namespace RunawayChimps.Monsters
{
    /// <summary>
    /// Atomic player-pursuit identity. A pursuit without a positive Photon ActorNumber
    /// is deliberately normalized to no pursuit so local presentation fails closed.
    /// </summary>
    public readonly struct MonsterPursuitState : IEquatable<MonsterPursuitState>
    {
        public bool IsPursuing { get; }
        public int TargetActorNumber { get; }

        public MonsterPursuitState(bool isPursuing, int targetActorNumber)
        {
            IsPursuing = isPursuing && targetActorNumber > 0;
            TargetActorNumber = IsPursuing ? targetActorNumber : 0;
        }

        public bool Equals(MonsterPursuitState other) =>
            IsPursuing == other.IsPursuing && TargetActorNumber == other.TargetActorNumber;

        public override bool Equals(object obj) => obj is MonsterPursuitState other && Equals(other);
        public override int GetHashCode() => (IsPursuing, TargetActorNumber).GetHashCode();
        public static bool operator ==(MonsterPursuitState left, MonsterPursuitState right) => left.Equals(right);
        public static bool operator !=(MonsterPursuitState left, MonsterPursuitState right) => !left.Equals(right);
    }

    /// <summary>
    /// Implemented by any monster brain that can pursue one Photon player at a time.
    /// Threat feedback consumes this read-only contract without knowing monster type.
    /// </summary>
    public interface IMonsterPursuitProvider
    {
        MonsterPursuitState PursuitState { get; }
        SectorId ThreatSector { get; }
        event Action<MonsterPursuitState> PursuitChanged;
    }

    /// <summary>
    /// Network-replicated monster brains additionally accept authoritative remote state.
    /// </summary>
    public interface IMonsterPursuitSyncTarget : IMonsterPursuitProvider
    {
        void ApplyRemotePursuit(bool isPursuing, int targetActorNumber);
    }
}
