namespace BossMod;

// A continuous mechanic/AI request selects once; manual changes remain available.
public sealed class TargetSelectionOnce
{
    private ulong _last;
    public bool Update(ulong requested, bool selectable = true)
    {
        if (requested != 0 && !selectable) return false;
        var changed = requested != 0 && requested != _last;
        _last = requested;
        return changed;
    }
}
