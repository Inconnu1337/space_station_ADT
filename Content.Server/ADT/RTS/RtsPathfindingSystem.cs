using System.Numerics;
using Robust.Shared.Timing;

namespace Content.Server.ADT.RTS;

/// <summary>
/// Пасфайндинг RTS. Апстримный PathfindingSystem рассчитан на десяток NPC на станции;
/// здесь нужны сотни юнитов и групповые приказы, поэтому основной инструмент — flow field:
/// одна волна Дейкстры от точки назначения, которую потом читают все юниты приказа.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §7.
/// </summary>
public sealed class RtsPathfindingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Сколько полей держим в кэше. Больше и не нужно: приказов в секунду немного,
    /// а поле переиспользуется всей группой.
    /// </summary>
    private const int MaxCachedFields = 16;

    /// <summary>
    /// Недостижимый тайл. Заметно больше любой реальной дистанции на арене.
    /// </summary>
    private const int Unreachable = int.MaxValue;

    private readonly Dictionary<EntityUid, RtsArenaData> _arenas = new();
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, FlowField>> _fields = new();

    private static readonly Vector2i[] Neighbours =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
        new(1, 1),
        new(1, -1),
        new(-1, 1),
        new(-1, -1),
    };

    /// <summary>
    /// Привязывает готовую арену к карте. Зовётся генератором по окончании генерации.
    /// </summary>
    public void RegisterArena(EntityUid map, RtsArenaData arena)
    {
        _arenas[map] = arena;
        _fields[map] = new Dictionary<Vector2i, FlowField>();
    }

    public void ForgetArena(EntityUid map)
    {
        _arenas.Remove(map);
        _fields.Remove(map);
    }

    public RtsArenaData? GetArena(EntityUid map)
    {
        return _arenas.GetValueOrDefault(map);
    }

    /// <summary>
    /// Сбрасывает кэш полей карты. Зовётся, когда меняется проходимость: построили
    /// или снесли здание.
    /// </summary>
    public void InvalidateFields(EntityUid map)
    {
        if (_fields.TryGetValue(map, out var fields))
            fields.Clear();
    }

    public static Vector2i ToTile(Vector2 position)
    {
        return new Vector2i((int) MathF.Floor(position.X), (int) MathF.Floor(position.Y));
    }

    public static Vector2 ToCenter(Vector2i tile)
    {
        return new Vector2(tile.X + 0.5f, tile.Y + 0.5f);
    }

    /// <summary>
    /// Направление движения из тайла к цели по flow field.
    /// Возвращает false, если цель недостижима или арены нет.
    /// </summary>
    public bool TryGetDirection(EntityUid map, Vector2i from, Vector2i destination, out Vector2 direction)
    {
        direction = Vector2.Zero;

        if (!_arenas.TryGetValue(map, out var arena))
            return false;

        if (from == destination)
            return false;

        var field = GetField(map, arena, destination);

        if (field == null)
            return false;

        var current = field.GetDistance(arena, from);

        // Юнита вытолкнуло в непроходимый тайл: ведём его к любому соседу, который вообще
        // достижим, иначе он застрянет навсегда.
        var best = current;
        var bestOffset = Vector2i.Zero;

        for (var i = 0; i < Neighbours.Length; i++)
        {
            var offset = Neighbours[i];
            var neighbour = from + offset;

            if (!arena.IsPassable(neighbour))
                continue;

            // Через угол между двумя блоками не протискиваемся.
            if (offset.X != 0 && offset.Y != 0)
            {
                if (!arena.IsPassable(from + new Vector2i(offset.X, 0)) || !arena.IsPassable(from + new Vector2i(0, offset.Y)))
                    continue;
            }

            var distance = field.GetDistance(arena, neighbour);

            if (distance >= best)
                continue;

            best = distance;
            bestOffset = offset;
        }

        if (bestOffset == Vector2i.Zero)
            return false;

        direction = Vector2.Normalize(new Vector2(bestOffset.X, bestOffset.Y));
        return true;
    }

    private FlowField? GetField(EntityUid map, RtsArenaData arena, Vector2i destination)
    {
        if (!_fields.TryGetValue(map, out var fields))
            return null;

        if (fields.TryGetValue(destination, out var cached))
        {
            cached.LastUsed = _timing.CurTime;
            return cached;
        }

        if (fields.Count >= MaxCachedFields)
            DropOldest(fields);

        var field = BuildField(arena, destination);
        field.LastUsed = _timing.CurTime;
        fields[destination] = field;

        return field;
    }

    private void DropOldest(Dictionary<Vector2i, FlowField> fields)
    {
        var oldestKey = Vector2i.Zero;
        var oldestTime = TimeSpan.MaxValue;

        foreach (var (key, value) in fields)
        {
            if (value.LastUsed >= oldestTime)
                continue;

            oldestTime = value.LastUsed;
            oldestKey = key;
        }

        fields.Remove(oldestKey);
    }

    /// <summary>
    /// Волна по проходимым тайлам от точки назначения. Стоимость шага одинаковая для всех
    /// восьми направлений: диагонали получаются чуть «дешевле» правды, но на глаз это
    /// незаметно, а поле считается вдвое быстрее честного Дейкстры с очередью с приоритетом.
    /// </summary>
    private FlowField BuildField(RtsArenaData arena, Vector2i destination)
    {
        var field = new FlowField(arena.Size);
        var frontier = new Queue<Vector2i>();

        // Если целевой тайл непроходим (кликнули в скалу), начинаем от проходимых соседей:
        // юниты подойдут вплотную и остановятся.
        if (arena.IsPassable(destination))
        {
            field.SetDistance(arena, destination, 0);
            frontier.Enqueue(destination);
        }
        else
        {
            for (var i = 0; i < Neighbours.Length; i++)
            {
                var neighbour = destination + Neighbours[i];

                if (!arena.IsPassable(neighbour))
                    continue;

                field.SetDistance(arena, neighbour, 0);
                frontier.Enqueue(neighbour);
            }
        }

        while (frontier.TryDequeue(out var current))
        {
            var distance = field.GetDistance(arena, current) + 1;

            for (var i = 0; i < Neighbours.Length; i++)
            {
                var offset = Neighbours[i];
                var next = current + offset;

                if (!arena.IsPassable(next))
                    continue;

                if (offset.X != 0 && offset.Y != 0)
                {
                    if (!arena.IsPassable(current + new Vector2i(offset.X, 0)) || !arena.IsPassable(current + new Vector2i(0, offset.Y)))
                        continue;
                }

                if (field.GetDistance(arena, next) <= distance)
                    continue;

                field.SetDistance(arena, next, distance);
                frontier.Enqueue(next);
            }
        }

        return field;
    }

    /// <summary>
    /// Поле расстояний до одной точки назначения.
    /// </summary>
    private sealed class FlowField
    {
        public TimeSpan LastUsed;

        private readonly int[] _distance;

        public FlowField(Vector2i size)
        {
            _distance = new int[size.X * size.Y];
            Array.Fill(_distance, Unreachable);
        }

        public int GetDistance(RtsArenaData arena, Vector2i tile)
        {
            if (!arena.Contains(tile))
                return Unreachable;

            var local = tile - arena.Origin;
            return _distance[local.Y * arena.Size.X + local.X];
        }

        public void SetDistance(RtsArenaData arena, Vector2i tile, int value)
        {
            if (!arena.Contains(tile))
                return;

            var local = tile - arena.Origin;
            _distance[local.Y * arena.Size.X + local.X] = value;
        }
    }
}
