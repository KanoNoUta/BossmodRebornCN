namespace BossMod.Dawntrail.Foray.Crucible;

// ARR 22:01:47: 49472 spawns two 20152/14937 magic-empowered blades at
// Z=-403/-437. Kill both before moving to the edge for 49477 -> 49480.
sealed class CrucibleLaudaEmpoweredBlades(BossModule module) : BossComponent(module)
{
    private readonly HashSet<ulong> _blades = [];
    private DateTime _expires;
    private WPos? _edge;

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == 20152)
        {
            if (_expires <= WorldState.CurrentTime)
                _blades.Clear();
            _blades.Add(actor.InstanceID);
            _expires = WorldState.FutureTime(45);
            _edge = null;
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;
        if (spell.Action.ID == 49472)
        {
            _blades.Clear();
            foreach (var blade in Module.Enemies(20152))
                if (!blade.IsDeadOrDestroyed)
                    _blades.Add(blade.InstanceID);
            _edge = null;
            _expires = WorldState.FutureTime(45);
        }
        else if (spell.Action.ID == 49477)
            _expires = Module.CastFinishAt(spell, 3.5);
        else if (spell.Action.ID == 49480)
            _expires = Module.CastFinishAt(spell, 0.5);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 49480)
            _expires = default;
    }

    private IEnumerable<Actor> Remaining => _blades.Select(id => WorldState.Actors.Find(id))
        .OfType<Actor>().Where(a => !a.IsDeadOrDestroyed);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_expires <= WorldState.CurrentTime)
        {
            RestoreBoss(actor, hints);
            return;
        }
        var remaining = Remaining.ToArray();
        if (remaining.Length > 0)
        {
            foreach (var blade in remaining)
                if (blade.IsTargetable && blade.HPMP.CurHP > 0)
                    hints.SetPriority(blade, 10);
            hints.ForcedTarget = remaining.FirstOrDefault(a => a.IsTargetable && a.HPMP.CurHP > 0 && a.InstanceID == actor.TargetID)
                ?? remaining.Where(a => a.IsTargetable && a.HPMP.CurHP > 0).MinBy(a => (a.Position - actor.Position).LengthSq());
        }
        else if (_blades.Count >= 2)
        {
            RestoreBoss(actor, hints);
            _edge ??= Module.Center + new WDir(0, actor.Position.Z < Module.Center.Z ? -22.5f : 22.5f);
            var edge = _edge.Value;
            hints.GoalZones.Add(p => Math.Max(0, 100 - (p - edge).Length() * 2));
            hints.AddForbiddenZone(new SDInvertedCircle(edge, 1), _expires.AddSeconds(-0.8));
            hints.MaxCastTime = 0;
        }
    }

    private void RestoreBoss(Actor actor, AIHints hints)
    {
        if (_blades.Contains(actor.TargetID) && Module.PrimaryActor is { IsTargetable: true, IsDeadOrDestroyed: false } boss)
            hints.ForcedTarget = boss;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_expires > WorldState.CurrentTime && _blades.Count > 0)
            hints.Add(Remaining.Any() ? "先击杀两把魔法强化魔刃！" : "魔刃已清：到场地最外侧躲避！", false);
    }
}
