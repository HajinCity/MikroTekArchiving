using System.Runtime.Caching;

namespace MikroTek.Common.Utility
{
   
    public class CachingUtility
    {
        public static ObjectCache _cache = MemoryCache.Default;

        public static void SetUp(string key, object datas, int cacheDuration)
        {
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(cacheDuration);
            _cache.Set(key, datas, policy);
        }

        public static object? Get(string key)
        {
            return _cache[key];
        }

       
        public static T? Get<T>(string key) where T : class
        {
            return _cache[key] as T;
        }

        public static bool IsSet(string key)
        {
            return (_cache[key] != null);
        }

        public static object? Remove(string key)
        {
            return _cache.Remove(key);
        }
    }
}
