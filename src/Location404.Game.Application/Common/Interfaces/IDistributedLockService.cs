namespace Location404.Game.Application.Common.Interfaces;

public interface IDistributedLockService
{
    Task<IDisposable?> AcquireLockAsync(string key, TimeSpan expiry);
}
