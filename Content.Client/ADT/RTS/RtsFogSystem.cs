using System.Numerics;
using Content.Shared.ADT.RTS.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.ADT.RTS;

/// <summary>
/// Туман войны на клиенте.
///
/// Туман по сети НЕ передаётся, и это осознанно: сервер уже прячет вражеские сущности
/// через PVS, поэтому у клиента есть ровно те данные, из которых туман и считается —
/// собственные юниты и их радиусы обзора. Считать то же самое локально дешевле, чем гонять
/// по сети 36 килобайт на каждый пересчёт, и рассинхрону тут взяться неоткуда.
///
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §6.
/// </summary>
public sealed class RtsFogSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public const byte Unexplored = 0;
    public const byte Explored = 1;
    public const byte Visible = 2;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(250);

    private TimeSpan _nextUpdate;

    private byte[] _tiles = Array.Empty<byte>();

    /// <summary>
    /// Индексы тайлов, засвеченных в прошлом пересчёте. Гасим по списку, а не обходом
    /// всей арены.
    /// </summary>
    private readonly List<int> _visibleNow = new();

    /// <summary>
    /// Левый нижний угол арены в мировых координатах.
    /// </summary>
    public Vector2i Origin { get; private set; }

    public Vector2i Size { get; private set; }

    /// <summary>
    /// Есть ли вообще туман: false вне матча и в матче с выключенным туманом.
    /// </summary>
    public bool Active { get; private set; }

    public byte GetState(Vector2i tile)
    {
        if (!Active)
            return Visible;

        var local = tile - Origin;

        if (local.X < 0 || local.Y < 0 || local.X >= Size.X || local.Y >= Size.Y)
            return Unexplored;

        return _tiles[local.Y * Size.X + local.X];
    }

    /// <summary>
    /// Сбрасывает накопленную разведку. Зовётся при входе в матч и выходе из него.
    /// </summary>
    public void Reset()
    {
        Active = false;
        _tiles = Array.Empty<byte>();
        _visibleNow.Clear();
        Size = Vector2i.Zero;
        Origin = Vector2i.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        if (!TryGetCamera(out var camera))
        {
            if (Active)
                Reset();

            return;
        }

        if (!camera.FogOfWar)
        {
            Active = false;
            return;
        }

        EnsureSize(camera);
        Recalculate(camera);
    }

    private bool TryGetCamera(out RtsCameraComponent camera)
    {
        camera = default!;

        if (_players.LocalEntity is not { } local)
            return false;

        return TryComp(local, out camera!);
    }

    private void EnsureSize(RtsCameraComponent camera)
    {
        var size = new Vector2i((int) camera.Bounds.Width, (int) camera.Bounds.Height);

        if (size.X <= 0 || size.Y <= 0)
            return;

        if (Active && size == Size)
            return;

        Size = size;
        Origin = new Vector2i((int) camera.Bounds.Left, (int) camera.Bounds.Bottom);
        _tiles = new byte[size.X * size.Y];
        _visibleNow.Clear();
        Active = true;
    }

    private void Recalculate(RtsCameraComponent camera)
    {
        if (!Active)
            return;

        // Разведанность не снимается никогда, гаснет только текущая видимость.
        foreach (var index in _visibleNow)
        {
            _tiles[index] = Explored;
        }

        _visibleNow.Clear();

        var query = EntityQueryEnumerator<RtsVisionComponent, RtsOwnedComponent, TransformComponent>();
        while (query.MoveNext(out _, out var vision, out var owned, out var xform))
        {
            // Чужие юниты в поле зрения светить не должны — туман строится только по своим.
            if (owned.TeamIndex != camera.TeamIndex)
                continue;

            MarkCircle(_xform.GetWorldPosition(xform), vision.Range);
        }
    }

    private void MarkCircle(Vector2 position, float range)
    {
        var center = new Vector2i((int) MathF.Floor(position.X), (int) MathF.Floor(position.Y));
        var radius = (int) MathF.Ceiling(range);
        var squared = range * range;

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > squared)
                    continue;

                var local = center + new Vector2i(dx, dy) - Origin;

                if (local.X < 0 || local.Y < 0 || local.X >= Size.X || local.Y >= Size.Y)
                    continue;

                var index = local.Y * Size.X + local.X;

                if (_tiles[index] == Visible)
                    continue;

                _tiles[index] = Visible;
                _visibleNow.Add(index);
            }
        }
    }
}
