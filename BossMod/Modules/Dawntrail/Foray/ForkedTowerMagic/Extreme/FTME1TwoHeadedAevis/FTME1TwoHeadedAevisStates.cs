namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Extreme.FTME1TwoHeadedAevis;

sealed class TwoHeadedAevisStates : StateMachineBuilder
{
    public TwoHeadedAevisStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<TwoHeadedAevisAOEs>()
            .ActivateOnEnter<IceFlameCross>()
            .ActivateOnEnter<MahjongMechanics>();
    }
}
