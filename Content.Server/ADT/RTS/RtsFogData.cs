namespace Content.Server.ADT.RTS;

/// <summary>
/// Туман войны одной команды: по байту на тайл арены.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §6.1.
/// </summary>
public sealed class RtsFogData
{
    /// <summary>
    /// Не разведано: сплошная чернота, на миникарте пусто.
    /// </summary>
    public const byte Unexplored = 0;

    /// <summary>
    /// Разведано, но сейчас не видно: рельеф и запомненные здания, юнитов нет.
    /// </summary>
    public const byte Explored = 1;

    /// <summary>
    /// Видно прямо сейчас.
    /// </summary>
    public const byte Visible = 2;

    public readonly Vector2i Size;
    public readonly Vector2i Origin;

    private readonly byte[] _tiles;

    /// <summary>
    /// Тайлы, ставшие видимыми в этом пересчёте. Держим отдельным списком, чтобы на
    /// следующем тике снять с них видимость, не обходя всю арену.
    /// </summary>
    private readonly List<int> _visibleNow = new();

    public RtsFogData(Vector2i size)
    {
        Size = size;
        Origin = new Vector2i(-size.X / 2, -size.Y / 2);
        _tiles = new byte[size.X * size.Y];
    }

    public bool Contains(Vector2i tile)
    {
        var local = tile - Origin;
        return local.X >= 0 && local.Y >= 0 && local.X < Size.X && local.Y < Size.Y;
    }

    public byte Get(Vector2i tile)
    {
        if (!Contains(tile))
            return Unexplored;

        var local = tile - Origin;
        return _tiles[local.Y * Size.X + local.X];
    }

    public bool IsVisible(Vector2i tile)
    {
        return Get(tile) == Visible;
    }

    /// <summary>
    /// Помечает тайл видимым и разведанным.
    /// </summary>
    public void MarkVisible(Vector2i tile)
    {
        if (!Contains(tile))
            return;

        var local = tile - Origin;
        var index = local.Y * Size.X + local.X;

        if (_tiles[index] == Visible)
            return;

        _tiles[index] = Visible;
        _visibleNow.Add(index);
    }

    /// <summary>
    /// Опускает всё, что было видно, до «разведано». Разведанность не снимается никогда.
    /// </summary>
    public void ClearVisible()
    {
        foreach (var index in _visibleNow)
        {
            _tiles[index] = Explored;
        }

        _visibleNow.Clear();
    }

    /// <summary>
    /// Отдаёт всю карту тумана целиком. Нужно клиенту при первой синхронизации.
    /// </summary>
    public byte[] GetRaw()
    {
        return _tiles;
    }
}
