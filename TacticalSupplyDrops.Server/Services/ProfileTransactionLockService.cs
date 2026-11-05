using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class ProfileTransactionLockService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim>
        ProfileLocks = new();

    public async Task<SemaphoreSlim> AcquireAsync(
        MongoId profileId,
        CancellationToken cancellationToken = default)
    {
        string profileKey = (string)profileId;

        SemaphoreSlim profileLock = ProfileLocks.GetOrAdd(
            profileKey,
            _ => new SemaphoreSlim(1, 1));

        await profileLock.WaitAsync(cancellationToken);

        return profileLock;
    }
}