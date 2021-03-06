﻿using System;
using System.Reflection;

namespace NeinLinq
{
    static class ReflectionExtensions
    {
        public static T GetAttribute<T>(this MemberInfo member)
            where T : Attribute
        {
#if DOTNET || NETCORE45 || WPA81
            return member.GetCustomAttribute<T>();
#else
            return (T)Attribute.GetCustomAttribute(member, typeof(T));
#endif
        }

        public static MethodInfo GetMethodInfo(this Type type, string name, Type[] parameters)
        {
#if DOTNET || NETCORE45 || WPA81
            return type.GetRuntimeMethod(name, parameters);
#else
            return type.GetMethod(name, parameters);
#endif
        }

        public static bool IsInheritorOf(this Type type, Type other)
        {
#if DOTNET || NETCORE45 || WPA81
            return type.GetTypeInfo().IsSubclassOf(other);
#else
            return type.IsSubclassOf(other);
#endif
        }

        public static bool IsConstructedGenericType(this Type type)
        {
#if DOTNET || NETCORE45 || WPA81
            return type.IsConstructedGenericType;
#else
            return type.IsGenericType && !type.IsGenericTypeDefinition;
#endif
        }

        public static Type[] GetGenericTypeArguments(this Type type)
        {
#if DOTNET || NETCORE45 || WPA81
            return type.GenericTypeArguments;
#else
            return type.GetGenericArguments();
#endif
        }
    }
}
