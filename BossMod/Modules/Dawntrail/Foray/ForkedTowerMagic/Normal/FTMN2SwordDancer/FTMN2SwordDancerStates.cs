namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN2SwordDancer;

[SkipLocalsInit]
sealed class SwordDancerStates : StateMachineBuilder
{
    public SwordDancerStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<SwordDancerAOEs>()
            .ActivateOnEnter<SwordRush>()
            .ActivateOnEnter<SwordBladeRects>()
            .ActivateOnEnter<ElectricBoundary>();
    }
}
