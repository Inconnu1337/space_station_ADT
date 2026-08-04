using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.ADT.RTS.Overlays;

/// <summary>
/// Затемнение неразведанных и разведанных, но не просматриваемых сейчас тайлов.
///
/// Рисуем прямоугольниками, а не текстурой с шейдером: обходим только тайлы, попавшие в
/// кадр, и склеиваем подряд идущие одинаковые тайлы строки в один прямоугольник. На экран
/// влезает пара тысяч тайлов, после склейки это несколько десятков вызовов отрисовки —
/// дешевле, чем гонять текстуру арены в видеопамять четыре раза в секунду.
///
/// Края получаются жёсткими, по-тайловыми. Мягкая растушёвка через шейдер — задача полировки.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §6.4.
/// </summary>
public sealed class RtsFogOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    /// <summary>
    /// Неразведанное: сплошная чернота.
    /// </summary>
    private static readonly Color ShroudColor = Color.FromHex("#000000FF");

    /// <summary>
    /// Разведанное, но сейчас не видно: рельеф остаётся читаемым.
    /// </summary>
    private static readonly Color FogColor = Color.FromHex("#00000090");

    private readonly RtsFogSystem _fog;

    public RtsFogOverlay(RtsFogSystem fog)
    {
        _fog = fog;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _fog.Active;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var bounds = args.WorldAABB;

        var minX = (int) MathF.Floor(bounds.Left);
        var maxX = (int) MathF.Ceiling(bounds.Right);
        var minY = (int) MathF.Floor(bounds.Bottom);
        var maxY = (int) MathF.Ceiling(bounds.Top);

        for (var y = minY; y <= maxY; y++)
        {
            var runStart = 0;
            var runState = byte.MaxValue;

            for (var x = minX; x <= maxX + 1; x++)
            {
                // Лишняя итерация за правым краем закрывает последнюю серию.
                var state = x > maxX ? byte.MaxValue : _fog.GetState(new Vector2i(x, y));

                if (state == runState)
                    continue;

                FlushRun(handle, runState, runStart, x, y);

                runState = state;
                runStart = x;
            }
        }
    }

    /// <summary>
    /// Рисует одну склеенную серию тайлов строки.
    /// </summary>
    private static void FlushRun(DrawingHandleWorld handle, byte state, int from, int to, int y)
    {
        if (from >= to)
            return;

        Color color;

        switch (state)
        {
            case RtsFogSystem.Unexplored:
                color = ShroudColor;
                break;
            case RtsFogSystem.Explored:
                color = FogColor;
                break;
            default:
                return;
        }

        handle.DrawRect(new Box2(new Vector2(from, y), new Vector2(to, y + 1)), color);
    }
}
