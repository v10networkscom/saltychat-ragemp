// Copyright (c) 2019 saltmine.de - https://github.com/saltminede

using System;

namespace SaltyServer
{
    public static class Extension
    {
        public static bool TryGetSharedData<T>(this GTANetworkAPI.Entity entity, string key, out T value)
        {
            try
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
                    else if (valueHolder.GetType() == typeof(String))
                    {
                        value = Newtonsoft.Json.JsonConvert.DeserializeObject<T>((string)valueHolder);
                        return true;
                    }
                }
            }
            catch
            {
                // do nothing
            }

            value = default;
            return false;
        }
    }
}
