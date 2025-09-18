using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecretAlliances.Core
{
    internal static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(
            this IDictionary<TKey, TValue> dict,
            TKey key,
            TValue defaultValue = default)
        {
            if (dict == null) return defaultValue;
            return dict.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}
