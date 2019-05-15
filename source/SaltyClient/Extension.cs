// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;

namespace SaltyClient
{
    public static class Extension
    {
        public static bool TryGetSharedData<T>(this RAGE.Elements.Entity entity, string key, out T value)
        {
            object valueHolder = entity.GetSharedData(key.ToString());

            if (valueHolder != null)
            {
                if (typeof(T).IsEnum)
                {
                    if (Enum.IsDefined(typeof(T), valueHolder))
                    {
                        value = (T)valueHolder;
                        return true;
                    }
                }
                else if (valueHolder is T)
                {
                    value = (T)valueHolder;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
