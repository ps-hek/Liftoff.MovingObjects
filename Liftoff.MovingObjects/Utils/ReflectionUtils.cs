using System;
using System.Linq;
using System.Reflection;

namespace Liftoff.MovingObjects.Utils;

internal static class ReflectionUtils
{
    public static T GetPrivateFieldValue<T>(object obj, string name)
    {
        var field = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
                    throw new NullReferenceException(name);
        return (T)field.GetValue(obj);
    }

    public static T GetPrivateFieldValueByType<T>(object obj)
    {
        var typ = obj.GetType();
        while (typ != null)
        {
            var field = obj.GetType()
                .GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(info => info.PropertyType == typeof(T));
            if (field != null)
                return (T)field.GetValue(obj);
            typ = typ.BaseType;
        }

        throw new Exception($"Field of type {typeof(T)} not found");
    }
}