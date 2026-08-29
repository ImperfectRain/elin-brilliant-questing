using System;
using System.Reflection;
using BepInEx.Logging;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Puts one line in somebody's mouth, using the game's own way of doing it where the build has
    /// one.
    ///
    /// The point of BQ-035 is that the player finds out about a situation without anything
    /// announcing it, so what comes out of here has to read as a person talking: their name, their
    /// words, and nothing else. No mod prefix, no thread name, no tag. The journal is where a
    /// player goes to see what they have collected; this is somebody in the street.
    ///
    /// Two routes, in order of preference. Elin's own raw-text speech, found by name because the
    /// method that takes a plain sentence rather than a language key is not something this
    /// repository has verified on a live build; failing that, the message log, attributed. The
    /// second route is not a degradation worth hiding - a bark reaches the player through the log
    /// in Elin anyway - but which one ran is worth knowing once, because it is the difference
    /// between a balloon over somebody's head and a line of text.
    ///
    /// A raw sentence is never passed to a method that wants a language key. That would print the
    /// key, or nothing, and would look like the mod having failed rather than like a person having
    /// spoken.
    /// </summary>
    internal static class ElinBark
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>Raw-text speech, in preference order. Anything taking a lang key is excluded.</summary>
        private static readonly string[] RawSayNames = { "SayRaw", "TalkRaw" };

        private static bool _routeReported;

        /// <summary>
        /// Says it, and reports whether the player could actually have heard it.
        ///
        /// False means nothing reached them, and the caller must not then teach them what was
        /// never said - a belief that arrives without a line is the omniscient journal the whole
        /// knowledge layer exists to prevent.
        /// </summary>
        internal static bool Speak(ElinBindings bindings, AmbientRemark remark, ManualLogSource log)
        {
            if (remark == null || string.IsNullOrEmpty(remark.Line))
            {
                return false;
            }

            Chara speaker = bindings?.ResolveChara(remark.Speaker);

            try
            {
                MethodInfo raw = speaker == null ? null : ResolveRawSay(speaker.GetType());
                if (raw != null)
                {
                    raw.Invoke(speaker, Arguments(raw, remark.Line));
                    Report(log, raw.DeclaringType.Name + "." + raw.Name);
                    return true;
                }

                // The speaker's own name, not the binding's - somebody the mod never bound is
                // still a person the world knows the name of.
                Msg.SayRaw(remark.SpeakerName + ": \"" + remark.Line + "\"");
                Report(log, "Msg.SayRaw");
                return true;
            }
            catch (Exception ex)
            {
                log?.LogWarning("BQ bark: " + remark.SpeakerName + " could not speak ("
                                + ex.GetType().Name + ": " + ex.Message + ").");
                return false;
            }
        }

        /// <summary>
        /// A speech method that takes a sentence. Trailing parameters are accepted only when the
        /// build states their defaults, so a signature that gained a reference argument still
        /// works and one this code would have to guess at is left alone.
        /// </summary>
        private static MethodInfo ResolveRawSay(Type charaType)
        {
            for (int i = 0; i < RawSayNames.Length; i++)
            {
                MethodInfo[] candidates = charaType.GetMethods(Flags);
                for (int c = 0; c < candidates.Length; c++)
                {
                    MethodInfo method = candidates[c];
                    if (method.Name != RawSayNames[i])
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string))
                    {
                        continue;
                    }

                    bool fillable = true;
                    for (int p = 1; p < parameters.Length; p++)
                    {
                        if (!parameters[p].IsOptional || !parameters[p].HasDefaultValue)
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
            }

            return null;
        }

        private static object[] Arguments(MethodInfo method, string line)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = line;
            for (int i = 1; i < parameters.Length; i++)
            {
                arguments[i] = parameters[i].DefaultValue;
            }

            return arguments;
        }

        private static void Report(ManualLogSource log, string route)
        {
            if (_routeReported)
            {
                return;
            }

            _routeReported = true;
            log?.LogInfo("BQ bark: ambient remarks are spoken through " + route + " on this build.");
        }
    }
}
