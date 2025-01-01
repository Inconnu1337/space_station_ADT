
using Content.Server.Salvage.Expeditions;
using Content.Shared.ADT.NinjaUpdate;
using Content.Shared.Ninja.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;

namespace Content.Server.ADT.NinjaUpdate.EntitySystems;

public sealed partial class NinjaSystem
{
    private void InitializeUsefulNinjaAbilities()
    {
        SubscribeLocalEvent<SpaceNinjaComponent, SpiderOSActionEvent>(OnSpiderOS);
    }

    private void OnSpiderOS(EntityUid uid, SpaceNinjaComponent component, SpiderOSActionEvent args)
    {
        // Получить шаттл, связанный с текущей станцией или пользователем.
        var salvageShuttleUid = GetSalvageShuttle(uid);

        if (salvageShuttleUid == null)
        {
            _popupSystem.PopupEntity("Не удалось найти подходящий шаттл с модулем спасения.", uid);
            return;
        }

        // Попробовать открыть интерфейс управления найденным шаттлом.
        if (!_uiSystem.TryToggleUi(uid, ShuttleConsoleUiKey.Key, salvageShuttleUid.Value))
        {
            _popupSystem.PopupEntity("Не удалось открыть интерфейс управления шаттлом.", uid);
        }
    }

    /// <summary>
    /// Возвращает шаттл с компонентом SalvageShuttleComponent, связанный с текущей станцией.
    /// </summary>
    private EntityUid? GetSalvageShuttle(EntityUid userUid)
    {
        var stationUid = _station.GetOwningStation(userUid);

        if (stationUid == null)
            return null;

        var query = AllEntityQuery<SalvageShuttleComponent, TransformComponent>();

        while (query.MoveNext(out var shuttleUid, out _, out var transform))
        {
            // Проверить, связан ли шаттл с текущей станцией.
            if (transform.GridUid != null &&
                TryComp<StationMemberComponent>(transform.GridUid, out var member) &&
                member.Station == stationUid)
            {
                return shuttleUid;
            }
        }

        return null;
    }
}
