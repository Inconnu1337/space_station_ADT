using Content.Shared.Atmos.Components;
using Robust.Server.Audio;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.ADT.EmitSoundOnFire;

public sealed class EmitSoundOnFireSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _time = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _time.CurTime;
        var query = EntityQueryEnumerator<EmitSoundOnFireComponent, FlammableComponent>();
        while (query.MoveNext(out var uid, out var emitSound, out var flammable))
        {
            if (!flammable.OnFire)
                continue;

            if (now < emitSound.NextSound)
                continue;

            _audio.PlayPvs(emitSound.Sound, uid);
            emitSound.NextSound = now + TimeSpan.FromSeconds(_random.Next(emitSound.MinInterval, emitSound.MaxInterval));
        }
    }
}
