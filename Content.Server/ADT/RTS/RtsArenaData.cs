namespace Content.Server.ADT.RTS;

/// <summary>
/// Плоский серверный кэш арены. Один объект на матч вместо компонентов на тайлах.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §3.4.
/// </summary>
public sealed class RtsArenaData
{
    /// <summary>
    /// Тайл свободен для прохода.
    /// </summary>
    public const byte Passable = 0;

    /// <summary>
    /// Тайл заблокирован рельефом: пустота, вода, скала.
    /// </summary>
    public const byte BlockedTerrain = 1;

    /// <summary>
    /// Тайл заблокирован постройкой. Снимается при сносе здания.
    /// </summary>
    public const byte BlockedBuilding = 2;

    public readonly Vector2i Size;

    /// <summary>
    /// Левый нижний угол арены в координатах грида.
    /// </summary>
    public readonly Vector2i Origin;

    private readonly byte[] _passability;

    public RtsArenaData(Vector2i size)
    {
        Size = size;
        Origin = new Vector2i(-size.X / 2, -size.Y / 2);
        _passability = new byte[size.X * size.Y];
    }

    public bool Contains(Vector2i tile)
    {
        var local = tile - Origin;
        return local.X >= 0 && local.Y >= 0 && local.X < Size.X && local.Y < Size.Y;
    }

    public byte Get(Vector2i tile)
    {
        if (!Contains(tile))
            return BlockedTerrain;

        var local = tile - Origin;
        return _passability[local.Y * Size.X + local.X];
    }

    public void Set(Vector2i tile, byte value)
    {
        if (!Contains(tile))
            return;

        var local = tile - Origin;
        _passability[local.Y * Size.X + local.X] = value;
    }

    public bool IsPassable(Vector2i tile)
    {
        return Get(tile) == Passable;
    }
}
