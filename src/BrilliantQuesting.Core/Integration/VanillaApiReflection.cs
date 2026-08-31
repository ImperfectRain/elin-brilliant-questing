using System;
using System.Globalization;
using System.Reflection;

namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// Small reflection helpers for version-matched Elin adapter surfaces.
    ///
    /// Core stays headless: these helpers know only member names and broad shapes, not Elin
    /// types. The plugin supplies live objects; tests supply Elin-shaped stubs.
    /// </summary>
    public static class VanillaApiReflection
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;
        private const BindingFlags AnyField = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        public static MemberInfo ResolveReadableMember(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            PropertyInfo property = type.GetProperty(name, Instance);
            if (property != null)
            {
                return property;
            }

            FieldInfo field = type.GetField(name, Instance);
            if (field != null)
            {
                return field;
            }

            return type.GetMethod(name, Instance, null, Type.EmptyTypes, null);
        }

        public static bool TryReadInt(object target, string name, out int value)
        {
            value = 0;
            MemberInfo member = ResolveReadableMember(target?.GetType(), name);
            if (member == null)
            {
                return false;
            }

            try
            {
                object read = Read(member, target);
                if (read == null || read is string)
                {
                    return false;
                }

                value = Convert.ToInt32(read, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static MethodInfo ResolveHomeAdmission(Type branchType, Type charaType)
        {
            return branchType?.GetMethod("AddMemeber", Instance, null, new[] { charaType }, null);
        }

        public static MethodInfo ResolveMoveZone(Type charaType)
        {
            if (charaType == null)
            {
                return null;
            }

            MethodInfo[] methods = charaType.GetMethods(Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "MoveZone")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 2 || parameters[0].ParameterType.Name != "Zone")
                {
                    continue;
                }

                Type stateType = parameters[1].ParameterType;
                if (stateType.IsEnum && stateType.Name == "EnterState")
                {
                    return method;
                }
            }

            return null;
        }

        public static MethodInfo ResolveSpatialFindZone(Type spatialType)
        {
            MethodInfo method = spatialType?.GetMethod("Find", Instance, null, new[] { typeof(int) }, null);
            return method != null && method.ReturnType.Name == "Zone" ? method : null;
        }

        public static object ResolveEnterState(Type enterStateType)
        {
            if (enterStateType == null || !enterStateType.IsEnum)
            {
                return null;
            }

            string[] preferred = { "RandomVisit", "Auto" };
            for (int i = 0; i < preferred.Length; i++)
            {
                if (Enum.IsDefined(enterStateType, preferred[i]))
                {
                    return Enum.Parse(enterStateType, preferred[i]);
                }
            }

            Array values = Enum.GetValues(enterStateType);
            return values.Length == 0 ? null : values.GetValue(0);
        }

        public static MethodInfo ResolveRawSpeech(Type cardOrCharaType)
        {
            MethodInfo say = ResolveRawSpeech(cardOrCharaType, "SayRaw", requireTalkLogFlag: false);
            return say ?? ResolveRawSpeech(cardOrCharaType, "TalkRaw", requireTalkLogFlag: true);
        }

        public static object[] RawSpeechArguments(MethodInfo method, string line)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = line;
            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].HasDefaultValue)
                {
                    arguments[i] = parameters[i].DefaultValue;
                }
                else if (parameters[i].ParameterType == typeof(string))
                {
                    arguments[i] = string.Empty;
                }
                else if (parameters[i].ParameterType == typeof(bool))
                {
                    arguments[i] = true;
                }
            }

            return arguments;
        }

        public static MethodInfo ResolveQualityMethod(Type thingType)
        {
            return thingType?.GetMethod("GetTotalQuality", Instance, null, new[] { typeof(bool) }, null);
        }

        public static MemberInfo ResolveQualityMember(Type thingType)
        {
            return ResolveQualityMethod(thingType) ?? ResolveReadableMember(thingType, "Quality");
        }

        public static bool TryReadQuality(object thing, out int quality, out string source)
        {
            quality = 0;
            source = null;
            MemberInfo member = ResolveQualityMember(thing?.GetType());
            if (member == null)
            {
                return false;
            }

            try
            {
                object read = member is MethodInfo method
                    ? method.Invoke(thing, new object[] { true })
                    : Read(member, thing);
                if (read == null)
                {
                    return false;
                }

                quality = Math.Max(0, Convert.ToInt32(read, CultureInfo.InvariantCulture));
                source = member.Name;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryReadGuildRank(object guild, out int rank)
        {
            rank = 0;
            if (!TryReadBool(guild, "IsMember", out bool member) || !member)
            {
                return true;
            }

            object relation = ReadObject(guild, "relation");
            return TryReadInt(relation, "rank", out rank);
        }

        public static bool LooksGlobal(object chara)
        {
            object global = ReadObject(chara, "global");
            if (global != null)
            {
                return true;
            }

            return TryReadBool(chara, "IsGlobal", out bool isGlobal) && isGlobal;
        }

        public static bool HasTrueFlag(object target, string name)
        {
            return TryReadBool(target, name, out bool flag) && flag;
        }

        public static object ReadObject(object target, string name)
        {
            MemberInfo member = ResolveReadableMember(target?.GetType(), name);
            if (member == null)
            {
                return null;
            }

            try
            {
                return Read(member, target);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string ReadText(object target, params string[] names)
        {
            if (target == null || names == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < names.Length; i++)
            {
                object read = ReadObject(target, names[i]);
                if (read == null)
                {
                    continue;
                }

                string text = read.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }

            return string.Empty;
        }

        public static T GetKnownField<T>(object instance, string name) where T : class
        {
            Type type = instance?.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, AnyField);
                if (field != null && typeof(T).IsAssignableFrom(field.FieldType))
                {
                    return field.GetValue(field.IsStatic ? null : instance) as T;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static bool TryReadBool(object target, string name, out bool value)
        {
            value = false;
            object read = ReadObject(target, name);
            if (read is bool flag)
            {
                value = flag;
                return true;
            }

            if (read is int integer)
            {
                value = integer != 0;
                return true;
            }

            return false;
        }

        private static MethodInfo ResolveRawSpeech(Type type, string name, bool requireTalkLogFlag)
        {
            if (type == null)
            {
                return null;
            }

            MethodInfo[] methods = type.GetMethods(Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != name)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 1 || parameters[0].ParameterType != typeof(string))
                {
                    continue;
                }

                if (requireTalkLogFlag
                    && (parameters.Length < 4 || parameters[3].ParameterType != typeof(bool)))
                {
                    continue;
                }

                bool fillable = true;
                for (int p = 1; p < parameters.Length; p++)
                {
                    Type parameterType = parameters[p].ParameterType;
                    if (parameterType != typeof(string) && parameterType != typeof(bool))
                    {
                        fillable = false;
                        break;
                    }
                }

                if (fillable)
                {
                    return method;
                }
            }

            return null;
        }

        private static object Read(MemberInfo member, object target)
        {
            if (member is PropertyInfo property)
            {
                return property.GetValue(target, null);
            }

            if (member is FieldInfo field)
            {
                return field.GetValue(target);
            }

            return ((MethodInfo)member).Invoke(target, null);
        }
    }
}
