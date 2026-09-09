using System.Threading.Tasks;
using Content.Server.ADT.Administration.Logs;
using Content.Server.Database;

namespace Content.Server.Administration.Logs;

public sealed partial class AdminLogManager
{
    public readonly AdtAdminLogStore AdtStore = new();

    private void InitializeAdtStore()
    {
        _dependencies.InjectDependencies(AdtStore);
        AdtStore.Initialize();
    }

    private async Task SaveAdtLogs(List<AdminLog> logs)
    {
        await AdtStore.Save(logs);

        if (AdtStore.WriteLegacy)
            await _db.AddAdminLogs(logs);
    }
}
