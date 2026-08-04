using System.Numerics;
using Content.Server.Parallax;
using Content.Shared.ADT.RTS.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.ADT.RTS;

/// <summary>
/// Генерация арены RTS: планета на базе биома, порционная прогрузка тайлов,
/// симметричные стартовые точки и проверка связности.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §3.
/// </summary>
public sealed class RtsMapGenSystem : EntitySystem
{
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly RtsPathfindingSystem _pathfinding = default!;

    /// <summary>
    /// Шаблон биома арены. Лежит в Resources/Prototypes/ADT/RTS/biome_arena.yml.
    /// </summary>
    public const string ArenaBiome = "ADTRtsArena";

    /// <summary>
    /// Невидимая физическая рамка по периметру арены.
    /// </summary>
    private const string BorderProto = "ADTRtsArenaBorder";

    /// <summary>
    /// Толщина рамки в тайлах. Достаточно толстая, чтобы сквозь неё нельзя было
    /// проскочить за один тик на максимальной скорости камеры.
    /// </summary>
    private const float BorderThickness = 8f;

    /// <summary>
    /// Сколько строк тайлов генерируем за один тик. Подобрано так, чтобы генерация
    /// 192x192 занимала примерно секунду и не роняла тикрейт станции.
    /// </summary>
    private const int RowsPerTick = 8;

    /// <summary>
    /// На сколько тайлов от центра отодвигаются стартовые базы, в долях полуразмера арены.
    /// </summary>
    private const float SpawnDistanceFraction = 0.62f;

    /// <summary>
    /// Радиус спирального поиска проходимого тайла рядом с идеальной стартовой точкой.
    /// </summary>
    private const int SpawnSearchRadius = 24;

    private readonly List<(Vector2i Index, Tile Tile)> _tileBuffer = new();
    private readonly Dictionary<EntityUid, RtsArenaData> _arenas = new();

    /// <summary>
    /// Данные арены матча. Появляются после завершения генерации.
    /// </summary>
    public RtsArenaData? GetArena(EntityUid match)
    {
        return _arenas.GetValueOrDefault(match);
    }

    public void ForgetArena(EntityUid match)
    {
        _arenas.Remove(match);
    }

    /// <summary>
    /// Создаёт карту арены и вешает на неё биом. Карта остаётся неинициализированной,
    /// пока не сгенерируются все тайлы.
    /// </summary>
    public void CreateArenaMap(Entity<RtsMatchComponent> match)
    {
        var comp = match.Comp;
        var map = _map.CreateMap(out var mapId, runMapInit: false);

        _meta.SetEntityName(map, $"RTS arena (seed {comp.Seed})");
        _biome.EnsurePlanet(map, _proto.Index<BiomeTemplatePrototype>(ArenaBiome), comp.Seed, dayCycle: false);

        // Атмосферы на RTS-карте нет вообще: дышать на ней некому, симулировать нечего.
        // См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §3.2.
        RemComp<MapAtmosphereComponent>(map);

        comp.MapUid = map;
        comp.MapId = mapId;
        comp.GeneratedRows = 0;
    }

    /// <summary>
    /// Генерирует очередную порцию тайлов. Возвращает true, когда вся арена готова.
    /// </summary>
    public bool TickGeneration(Entity<RtsMatchComponent> match)
    {
        var comp = match.Comp;

        if (comp.MapUid is not { } map || !Exists(map))
            return false;

        if (!TryComp<MapGridComponent>(map, out var grid))
            return false;

        var half = comp.ArenaSize / 2;
        var startRow = comp.GeneratedRows;
        var endRow = Math.Min(startRow + RowsPerTick, comp.ArenaSize.Y);

        var bounds = new Box2(
            -half.X,
            -half.Y + startRow,
            half.X,
            -half.Y + endRow);

        _tileBuffer.Clear();
        _biome.ReserveTiles(map, bounds, _tileBuffer, mapGrid: grid);

        comp.GeneratedRows = endRow;
        return endRow >= comp.ArenaSize.Y;
    }

    /// <summary>
    /// Достраивает арену после генерации тайлов: снимает биом, считает проходимость,
    /// выбирает симметричные стартовые точки и проверяет, что базы связаны.
    /// Возвращает false, если арена не годится и нужен новый seed.
    /// </summary>
    public bool TryFinishArena(Entity<RtsMatchComponent> match)
    {
        var comp = match.Comp;

        if (comp.MapUid is not { } map || !TryComp<MapGridComponent>(map, out var grid))
            return false;

        // Дальше карта статична: подгружать новые чанки не нужно, а лишний биом
        // на карте — это лишняя работа каждый тик.
        _biome.SetEnabled((map, Comp<BiomeComponent>(map)), false);

        var arena = new RtsArenaData(comp.ArenaSize);
        BuildPassability(map, grid, arena);

        if (!TryPickSpawns(arena, out var spawnA, out var spawnB))
            return false;

        if (!AreConnected(arena, spawnA, spawnB))
            return false;

        SpawnBorder(map, comp);
        _arenas[match.Owner] = arena;
        _pathfinding.RegisterArena(map, arena);

        for (var i = 0; i < comp.Teams.Count; i++)
        {
            if (!TryComp<RtsTeamComponent>(comp.Teams[i], out var team))
                continue;

            team.SpawnTile = i == 0 ? spawnA : spawnB;
        }

        _map.InitializeMap(map);
        return true;
    }

    /// <summary>
    /// Границы арены в локальных координатах грида.
    /// </summary>
    public Box2 GetArenaBounds(RtsMatchComponent match)
    {
        var half = match.ArenaSize / 2;
        return new Box2(-half.X, -half.Y, half.X, half.Y);
    }

    /// <summary>
    /// Ставит невидимую физическую рамку по периметру арены.
    ///
    /// Именно физика, а не подрезание координаты каждый тик: подрезание на клиенте
    /// выглядит рывками, потому что предсказание уводит камеру за край, а состояние
    /// сервера дёргает её назад. Физика предсказывается штатно.
    /// </summary>
    private void SpawnBorder(EntityUid map, RtsMatchComponent match)
    {
        var half = match.ArenaSize / 2;
        var border = Spawn(BorderProto, new EntityCoordinates(map, Vector2.Zero));

        if (!TryComp<PhysicsComponent>(border, out var body) || !TryComp<FixturesComponent>(border, out var manager))
        {
            Log.Error("RTS: у прототипа границы арены нет физики, камера сможет улететь за карту.");
            return;
        }

        var mask = (int) CollisionGroup.GhostImpassable;
        var left = -half.X;
        var bottom = -half.Y;
        var right = half.X;
        var top = half.Y;
        var outer = BorderThickness;

        CreateBorderFixture(border, body, manager, "rts_border_left", new Box2(left - outer, bottom - outer, left, top + outer), mask);
        CreateBorderFixture(border, body, manager, "rts_border_right", new Box2(right, bottom - outer, right + outer, top + outer), mask);
        CreateBorderFixture(border, body, manager, "rts_border_bottom", new Box2(left, bottom - outer, right, bottom), mask);
        CreateBorderFixture(border, body, manager, "rts_border_top", new Box2(left, top, right, top + outer), mask);
    }

    private void CreateBorderFixture(
        EntityUid border,
        PhysicsComponent body,
        FixturesComponent manager,
        string id,
        Box2 box,
        int mask)
    {
        // PhysShapeAabb задаёт границы только через DataField, в коде их не выставить,
        // поэтому берём полигон.
        var shape = new PolygonShape();
        shape.SetAsBox(box);

        _fixtures.TryCreateFixture(border, shape, id, hard: true, collisionMask: mask, manager: manager, body: body);
    }

    private void BuildPassability(EntityUid map, MapGridComponent grid, RtsArenaData arena)
    {
        for (var y = 0; y < arena.Size.Y; y++)
        {
            for (var x = 0; x < arena.Size.X; x++)
            {
                var index = arena.Origin + new Vector2i(x, y);
                var passable = _map.TryGetTileRef(map, grid, index, out var tileRef) && !tileRef.Tile.IsEmpty;

                // Периметр всегда заблокирован, чтобы юниты не уходили за пределы арены.
                if (x == 0 || y == 0 || x == arena.Size.X - 1 || y == arena.Size.Y - 1)
                    passable = false;

                arena.Set(index, passable ? RtsArenaData.Passable : RtsArenaData.BlockedTerrain);
            }
        }
    }

    /// <summary>
    /// Стартовые точки строго точечно-симметричны относительно центра арены:
    /// это обязательное условие честности матча.
    /// </summary>
    private bool TryPickSpawns(RtsArenaData arena, out Vector2i spawnA, out Vector2i spawnB)
    {
        spawnA = default;
        spawnB = default;

        var half = arena.Size / 2;
        var distance = (int) (Math.Min(half.X, half.Y) * SpawnDistanceFraction);
        var angle = (float) (_random.NextDouble() * Math.Tau);

        var offset = new Vector2i(
            (int) MathF.Round(MathF.Cos(angle) * distance),
            (int) MathF.Round(MathF.Sin(angle) * distance));

        if (!TryFindPassableNear(arena, offset, out spawnA))
            return false;

        // Зеркалим найденную точку, а не идеальную: базы обязаны стоять симметрично.
        if (!TryFindPassableNear(arena, -spawnA, out spawnB))
            return false;

        return spawnA != spawnB;
    }

    private bool TryFindPassableNear(RtsArenaData arena, Vector2i desired, out Vector2i result)
    {
        if (arena.IsPassable(desired))
        {
            result = desired;
            return true;
        }

        for (var radius = 1; radius <= SpawnSearchRadius; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    // Идём только по контуру квадрата, внутренность уже проверена.
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                        continue;

                    var candidate = desired + new Vector2i(dx, dy);

                    if (!arena.IsPassable(candidate))
                        continue;

                    result = candidate;
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Проверка, что от одной базы можно дойти до другой. Без неё генерация может выдать
    /// арену, разрезанную водой пополам.
    /// </summary>
    private bool AreConnected(RtsArenaData arena, Vector2i from, Vector2i to)
    {
        var visited = new HashSet<Vector2i>();
        var frontier = new Queue<Vector2i>();

        visited.Add(from);
        frontier.Enqueue(from);

        while (frontier.TryDequeue(out var current))
        {
            if (current == to)
                return true;

            for (var i = 0; i < 4; i++)
            {
                var neighbour = current + CardinalOffsets[i];

                if (!arena.IsPassable(neighbour) || !visited.Add(neighbour))
                    continue;

                frontier.Enqueue(neighbour);
            }
        }

        return false;
    }

    private static readonly Vector2i[] CardinalOffsets =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    };
}
