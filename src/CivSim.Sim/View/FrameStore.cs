using System;
using System.Threading;

namespace CivSim.Sim.View;

/// <summary>
/// Hands frames from the simulation thread to the render thread without locks (docs/MVP/18 §5). Four frames rotate: the
/// writer fills its back frame, the reader holds the previous and current frames it interpolates between, and one sits
/// in a pending slot. <see cref="Publish"/> swaps the back frame into the pending slot; <see cref="TryAcquire"/> swaps the
/// pending frame in when a fresh one is there. The swap is one <see cref="Interlocked.Exchange(ref int, int)"/> (a full
/// fence), so a frame's contents are complete before the other side can see it. If the writer publishes twice before
/// the reader looks, the older frame is reused and the reader simply interpolates over a longer interval: frames carry
/// their tick.
/// <para>Exactly one writer thread and one reader thread. Nothing here is simulation state, and nothing here touches the
/// model, so presentation timing can never reach the simulation (rule D7).</para>
/// </summary>
public sealed class FrameStore
{
    private const int Fresh = 4, IndexMask = 3;
    private readonly MatchFrame[] _frames = { new(), new(), new(), new() };
    private int _pending = 3;             // index | Fresh; shared
    private int _back = 0;                // writer-owned
    private int _previous = 1, _current = 2; // reader-owned
    private bool _hasCurrent, _hasPrevious;   // reader-owned

    /// <summary>Grows all four frames (and the id links) up front; see <see cref="MatchFrame.Reserve"/>. Call before handing the store to two threads.</summary>
    public void Reserve(int figures, int settlements, int herds, int parties = 64, int plots = 4096,
        int events = FrameBuilder.DefaultEventWindow, int aiWhy = 32)
    {
        foreach (MatchFrame frame in _frames) frame.Reserve(figures, settlements, herds, parties, plots, events, aiWhy);
        Figures.Reserve(figures);
        Herds.Reserve(herds);
    }

    /// <summary>Writer: the frame to fill next. Valid until <see cref="Publish"/>.</summary>
    public MatchFrame Back => _frames[_back];

    /// <summary>Writer: fills the back frame with <paramref name="builder"/> and publishes it.</summary>
    public void BuildAndPublish(FrameBuilder builder, Research.MatchModel match)
    {
        builder.Build(match, Back);
        Publish();
    }

    /// <summary>Writer: makes the back frame the newest one and takes a free frame as the new back frame.</summary>
    public void Publish()
    {
        int old = Interlocked.Exchange(ref _pending, _back | Fresh);
        _back = old & IndexMask;
    }

    /// <summary>
    /// Reader: when a newer frame was published, moves current to previous and the new frame to current, rebuilds
    /// <see cref="Figures"/> and <see cref="Herds"/>, and returns true. Returns false (and changes nothing) otherwise.
    /// </summary>
    public bool TryAcquire()
    {
        if ((Volatile.Read(ref _pending) & Fresh) == 0) return false;
        int old = Interlocked.Exchange(ref _pending, _previous);
        _previous = _current;
        _current = old & IndexMask;
        _hasPrevious = _hasCurrent;
        _hasCurrent = true;
        if (_hasPrevious)
        {
            Figures.Update(Previous!.FigureId, Previous.FigureCount, Current!.FigureId, Current.FigureCount);
            Herds.Update(Previous.HerdId, Previous.HerdCount, Current.HerdId, Current.HerdCount);
        }
        else
        {
            Figures.Update(Array.Empty<long>(), 0, Current!.FigureId, Current.FigureCount);
            Herds.Update(Array.Empty<int>(), 0, Current.HerdId, Current.HerdCount);
        }
        return true;
    }

    /// <summary>Reader: the newest frame acquired, or null before the first.</summary>
    public MatchFrame? Current => _hasCurrent ? _frames[_current] : null;

    /// <summary>Reader: the frame before <see cref="Current"/>, or null.</summary>
    public MatchFrame? Previous => _hasPrevious ? _frames[_previous] : null;

    /// <summary>Reader: figures of <see cref="Current"/> matched by id to <see cref="Previous"/>.</summary>
    public IdLink Figures { get; } = new();

    /// <summary>Reader: herds of <see cref="Current"/> matched by id to <see cref="Previous"/>.</summary>
    public IdLink Herds { get; } = new();

    /// <summary>
    /// Reader: the interpolation weight of <paramref name="renderTick"/> (fractional ticks, scaled by
    /// <paramref name="scale"/>) between the previous frame's tick and the current one's, in 0 to scale. The renderer
    /// usually runs one frame interval behind the newest frame so it always has two to blend.
    /// </summary>
    public long Alpha(long renderTick, long scale)
    {
        MatchFrame? cur = Current, prev = Previous;
        if (cur is null) return scale;
        if (prev is null || cur.Tick <= prev.Tick) return scale;
        long span = (cur.Tick - prev.Tick) * scale;
        long into = renderTick - prev.Tick * scale;
        return Math.Clamp(into * scale / span, 0, scale);
    }
}

/// <summary>
/// Matches the entities of two frames by id (both lists ascending): for each entity in the newer frame its index in the
/// older one, or -1 if it is new (born, came into sight); and the older-frame indices of entities that are gone (died,
/// left sight). A merge join, allocation-free once its arrays have grown. Renderers lerp
/// <c>X = prevX + (curX - prevX) × alpha</c> for matched entities and fade the others in or out.
/// </summary>
public sealed class IdLink
{
    private int[] _previousIndex = Array.Empty<int>();
    private int[] _removed = Array.Empty<int>();

    /// <summary>Entities in the newer frame.</summary>
    public int Count { get; private set; }
    /// <summary>Entities new in the newer frame.</summary>
    public int SpawnedCount { get; private set; }
    /// <summary>Entities of the older frame missing from the newer one.</summary>
    public int RemovedCount { get; private set; }

    /// <summary>Index in the older frame of the newer frame's entity <paramref name="index"/>, or -1 when it is new.</summary>
    public int PreviousIndex(int index) => _previousIndex[index];

    /// <summary>Older-frame index of the <paramref name="i"/>-th removed entity.</summary>
    public int Removed(int i) => _removed[i];

    public ReadOnlySpan<int> PreviousIndices => _previousIndex.AsSpan(0, Count);
    public ReadOnlySpan<int> RemovedIndices => _removed.AsSpan(0, RemovedCount);

    public void Update(long[] previous, int previousCount, long[] current, int currentCount)
    {
        Prepare(previousCount, currentCount);
        int p = 0, spawned = 0, removed = 0;
        for (int c = 0; c < currentCount; c++)
        {
            long id = current[c];
            while (p < previousCount && previous[p] < id) _removed[removed++] = p++;
            if (p < previousCount && previous[p] == id) _previousIndex[c] = p++;
            else { _previousIndex[c] = -1; spawned++; }
        }
        while (p < previousCount) _removed[removed++] = p++;
        Finish(currentCount, spawned, removed);
    }

    public void Update(int[] previous, int previousCount, int[] current, int currentCount)
    {
        Prepare(previousCount, currentCount);
        int p = 0, spawned = 0, removed = 0;
        for (int c = 0; c < currentCount; c++)
        {
            int id = current[c];
            while (p < previousCount && previous[p] < id) _removed[removed++] = p++;
            if (p < previousCount && previous[p] == id) _previousIndex[c] = p++;
            else { _previousIndex[c] = -1; spawned++; }
        }
        while (p < previousCount) _removed[removed++] = p++;
        Finish(currentCount, spawned, removed);
    }

    /// <summary>Grows the link's arrays for this many entities.</summary>
    public void Reserve(int count) => Prepare(count, count);

    private void Prepare(int previousCount, int currentCount)
    {
        MatchFrame.Ensure(ref _previousIndex, currentCount);
        MatchFrame.Ensure(ref _removed, previousCount);
    }

    private void Finish(int count, int spawned, int removed)
    {
        Count = count;
        SpawnedCount = spawned;
        RemovedCount = removed;
    }
}
