using System;
using System.Data.Entity;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Caching;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Diagnostics;

namespace RezaB.DBUtilities
{
    /// <summary>
    /// Represents a cached setting from database.
    /// </summary>
    /// <typeparam name="T">Type of setting.</typeparam>
    /// <typeparam name="DBC">Database context.</typeparam>
    /// <typeparam name="DBT">Database table.</typeparam>
    public static class DBSetting<DBC, DBT> where DBC : DbContext, new() where DBT : class
    {
        private static object cache_lock = new object();
        internal static MemoryCache memoryCache = new MemoryCache("DBSettings");

        public static T Retrieve<T>(string name)
        {
            return RetrieveFromRuntimeCache<T>(name);
        }

        /// <summary>
        /// Retrieves a value from cache.
        /// </summary>
        /// <param name="cacheKey">Cache key</param>
        /// <returns>Cache value</returns>
        private static T RetrieveFromRuntimeCache<T>(string cacheKey)
        {
            lock (cache_lock)
            {
                if (!memoryCache.Contains(cacheKey))
                {
                    using (DBC db = new DBC())
                    {

                        T value = Convert<T>((db.Set<DBT>().Find(cacheKey) as dynamic).Value);
                        CacheItemPolicy policy = new CacheItemPolicy()
                        {
                            AbsoluteExpiration = DateTime.UtcNow.AddMinutes(15),
                            SlidingExpiration = ObjectCache.NoSlidingExpiration
                        };
                        memoryCache.Add(cacheKey, value, policy);
                        return value;
                    }
                }
                return (T)memoryCache.Get(cacheKey);
            }

        }

        private static T Convert<T>(string rawValue)
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter != null)
            {
                if (typeof(T) == typeof(DateTime))
                    return (T)converter.ConvertFromString(null, CultureInfo.InvariantCulture, rawValue);
                return (T)converter.ConvertFromInvariantString(rawValue);
            }
            return default(T);
        }

        /// <summary>
        /// Clears internal cache.
        /// </summary>
        /// <param name="key">Name of setting.</param>
        public static void ClearCache(string key)
        {
            memoryCache.Remove(key);
        }

        public static void Update(object settingsModel)
        {
            var properties = settingsModel.GetType().GetProperties();
            using (DBC db = new DBC())
            {
                foreach (var property in properties)
                {
                    if( property.GetCustomAttribute<SettingElementAttribute>() != null)
                    {
                        var dbSetting = db.Set<DBT>().Find(property.Name) as dynamic;
                        dbSetting.Value = System.Convert.ToString(property.GetValue(settingsModel), CultureInfo.InvariantCulture);
                    }
                }

                db.SaveChanges();
            }
            foreach (var property in properties)
                ClearCache(property.Name);
        }
    }
}
