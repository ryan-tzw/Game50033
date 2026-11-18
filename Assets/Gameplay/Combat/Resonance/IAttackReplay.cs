namespace Game50033
{
    // a captured damaging attack. replays do not run ability state or participate in resonance.
    internal interface IAttackReplay
    {
        void Replay(float damageMultiplier);
    }
}
