using Countersoft.Foundation.Commons.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeckoboardConnector
{
    internal static class GeckoboardCache
    {
        class GeckoboardCacheItem
        {
            internal DateTime Created { get; set; }
            internal object Data { get; set; }
        }

        private static Dictionary<string, GeckoboardCacheItem> _cache = new Dictionary<string, GeckoboardCacheItem>();

        private static int _cacheDuration = 0;

        private static int GetCacheDuration()
        {
            if(_cacheDuration != 0)
            {
                return _cacheDuration;
            }

            try
            {
                _cacheDuration = System.Configuration.ConfigurationManager.AppSettings["geckoboard.CacheDuration"].ToInt(300);
            }
            catch
            {
                _cacheDuration = 300;
            }

            return _cacheDuration;
        }


        public static object Get(string cacheKey)
        {
            GeckoboardCacheItem item;

            if(_cache.TryGetValue(cacheKey, out item))
            {
                if((DateTime.UtcNow - item.Created).TotalSeconds < GetCacheDuration())
                {
                    return item.Data;
                }
            }

            return null;
        }

        public static void Set(string cacheKey, object data)
        {
            if(_cache.ContainsKey(cacheKey))
            {
                _cache.Remove(cacheKey);
            }

            _cache.Add(cacheKey, new GeckoboardCacheItem { Created = DateTime.UtcNow, Data = data });
        }
    }
}
