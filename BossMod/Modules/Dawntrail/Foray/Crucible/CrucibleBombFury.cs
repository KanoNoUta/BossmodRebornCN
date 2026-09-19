namespace BossMod.Dawntrail.Foray.Crucible;

// 48827 is the R6 impact; 48805 is the accompanying 15y knockback (row 112).
// Three helpers cast 2s apart. The latter's R40 reach is not a full-arena AOE.
sealed class CrucibleBombFuryAOEs(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override double RiskyActivationWindow => 0.5;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 48827 ? new(new AOEShapeCircle(6), true) : null;
    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            if (aoe.Risky)
                CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

sealed class CrucibleBombFuryKnockback(BossModule module) : HighCrucibleKnockback(module, 48805u, 15)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        Casters.Sort((a, b) => a.Activation.CompareTo(b.Activation));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        // The normal cast end can precede the effect packet; release on impact.
        if (!spell.EventHappened && spell.NPCRemainingTime > 0.5)
            base.OnCastFinished(caster, spell);
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InCircle(Module.Center, 19.5f)
        || (Module.FindComponent<CrucibleBombPools>()?.ActiveAOEs(slot, actor).ToArray().Any(a => a.Check(pos)) ?? false);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var sources = ActiveKnockbacks(slot, actor).ToArray();
        if (sources.Length == 0)
            return;
        var displacements = sources.Select(kb => IsImmune(slot, kb.Activation) ? 0 : kb.Distance).ToArray();
        var pools = Module.FindComponent<CrucibleBombPools>()?.ActiveAOEs(slot, actor).ToArray() ?? [];
        // Solve the sequence from each candidate's actual previous landing, not
        // three independent knockbacks from the player's initial position.
        hints.AddForbiddenZone(new Landings(Module.Center, sources, displacements, pools), sources[0].Activation.AddSeconds(-0.5));
    }

    private sealed class Landings(WPos center, Knockback[] sources, float[] distances, Components.GenericAOEs.AOEInstance[] pools) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var pos = p;
            var clearance = 19.5f - (pos - center).Length();
            for (var i = 0; i < sources.Length; ++i)
            {
                var offset = pos - sources[i].Origin;
                var length = offset.Length();
                clearance = Math.Min(clearance, length - 6.35f);
                if (length < 0.1f)
                    return -1;
                pos += offset * (distances[i] / length);
                clearance = Math.Min(clearance, 19.5f - (pos - center).Length());
                foreach (var pool in pools)
                    clearance = Math.Min(clearance, (pos - pool.Origin).Length() - 6.35f);
            }
            return clearance;
        }
    }
}
