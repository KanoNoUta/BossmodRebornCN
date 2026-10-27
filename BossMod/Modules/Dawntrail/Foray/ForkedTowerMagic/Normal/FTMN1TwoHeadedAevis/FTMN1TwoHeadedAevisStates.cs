namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN1TwoHeadedAevis;

sealed class TwoHeadedAevisStates : StateMachineBuilder
{
    public TwoHeadedAevisStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<TwoHeadedAevisAOEs>()
            .ActivateOnEnter<OrbExplosions>()
            .ActivateOnEnter<StormsBreathKnockback>()
            .ActivateOnEnter<TimedCurseKnockback>();
    }
}
