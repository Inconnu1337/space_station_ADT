using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.ADT.RTS.UI.Style;

/// <summary>
/// Толстые рамки в духе классических RTS. Отдельный хелпер вместо полноценной
/// таблицы стилей: RTS-интерфейс живёт на своём экране и ничего не наследует
/// от станционного HUD.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §5.2.
/// </summary>
public static class RtsStyle
{
    public static readonly Color WoodColor = Color.FromHex("#8A5A2B");
    public static readonly Color WheatColor = Color.FromHex("#D9B84A");
    public static readonly Color OreColor = Color.FromHex("#8C8C9A");
    public static readonly Color PopColor = Color.FromHex("#B14FC4");

    public static readonly Color PanelColor = Color.FromHex("#101018E8");
    public static readonly Color BorderColor = Color.FromHex("#5A9BD4");

    private const float BorderThickness = 2f;

    /// <summary>
    /// Квадрат-иконка ресурса слева от полоски.
    /// </summary>
    public static void ApplyIcon(PanelContainer panel, Color color)
    {
        panel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = color,
            BorderColor = Color.Black,
            BorderThickness = new Thickness(BorderThickness),
        };
    }

    /// <summary>
    /// Полоска со значением.
    /// </summary>
    public static void ApplyBar(PanelContainer panel, Color? border = null)
    {
        panel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = PanelColor,
            BorderColor = border ?? BorderColor,
            BorderThickness = new Thickness(BorderThickness),
        };

        BlockClicks(panel);
    }

    /// <summary>
    /// Общая подложка панели.
    /// </summary>
    public static void ApplyPanel(PanelContainer panel)
    {
        panel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = PanelColor,
            BorderColor = BorderColor,
            BorderThickness = new Thickness(BorderThickness),
        };

        BlockClicks(panel);
    }

    /// <summary>
    /// Панель гасит клики на себе.
    ///
    /// Экран RTS перекрывает мир хит-тестом и сам раздаёт мышь миру, поэтому без этого
    /// клик по HUD выделял бы юнитов «сквозь» панель. Stop останавливает всплытие события
    /// на самой панели, до экрана оно не доходит.
    /// </summary>
    private static void BlockClicks(Control control)
    {
        control.MouseFilter = Control.MouseFilterMode.Stop;
    }
}
