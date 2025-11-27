using Location404.Game.Application.Common.Interfaces;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace Location404.Game.Infrastructure.Common;

public class RedisDistributedLockService(
    IConnectionMultiplexer redis,
    ILogger<RedisDistributedLockService> logger
) : IDistributedLockService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<IDisposable?> AcquireLockAsync(string key, TimeSpan expiry)
    {
        var lockValue = Guid.NewGuid().ToString();
        var acquired = await _db.StringSetAsync(key, lockValue, expiry, When.NotExists);

        if (!acquired)
        {
            logger.LogDebug("Failed to acquire lock: {Key}", key);
            return null;
        }

        logger.LogDebug("Acquired lock: {Key}", key);
        return new RedisLock(_db, key, lockValue, logger);
    }

    private class RedisLock : IDisposable
    {
        private readonly IDatabase _db;
        private readonly string _key;
        private readonly string _value;
        private readonly ILogger _logger;
        private bool _disposed;

        public RedisLock(IDatabase db, string key, string value, ILogger logger)
        {
            _db = db;
            _key = key;
            _value = value;
            _logger = logger;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var lockScript = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            try
            {
                _db.ScriptEvaluate(lockScript, new RedisKey[] { _key }, new RedisValue[] { _value });
                _logger.LogDebug("Released lock: {Key}", _key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release lock: {Key}", _key);
            }
        }
    }
}
