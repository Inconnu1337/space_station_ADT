using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.ADT.btr;
using Robust.Shared.Random;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Server.ADT.btr;

public sealed partial class APCEntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public void InitializeModules()
    {

    }

    private void APCFuelBroke(EntityUid uid, APCFuelTankComponent component)
    {
        if (component.Broken && _random.Prob(0.5f))
        {
            if (component.APC == null)
                return;

            if (!TryComp<APCEntityComponent>(component.APC, out var apcEntityComp))
                return;

            if (!TryComp<TransformComponent>(component.APC, out var apcTransform))
                return;

            if (!TryComp<SolutionContainerManagerComponent>(uid, out var solutionManager))
                return;

            if (!_solutionContainerSystem.TryGetSolution(uid, apcEntityComp.APCFuel, out var solution))
                return;

            if (solution != apcEntity.APCFuel)
            {
                return;
                //todo: raiselocalevent polomka dvigatela
            }

            while (/*кол-во жидкости в баке*/ > 0)
            {
                var spillAmount = _random.NextFloat(0.1f, 0.5f);
                spillAmount = Math.Min(spillAmount, /*кол-во жидкости в баке*/);

                    //todo: срать лужами
            }
        }
        else
        {
            Logger.Debug("внутреннее возгорание");
        }
    }
}
