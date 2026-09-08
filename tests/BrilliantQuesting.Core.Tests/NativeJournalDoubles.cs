// Only the Window/patch contracts used by the linked NativeJournalSurface are represented.
// This deliberately does not pretend to test Unity lifecycles, resources, or rendering.
using System;
using System.Collections.Generic;
using System.Reflection;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.World;

internal class Layer { public string uid = "layer"; }
internal sealed class LayerJournal : Layer { public LayerJournal() { uid = "journal"; } }
internal class UIContent { public UnityEngine.Object gameObject = new UnityEngine.Object(); }
internal sealed class Window
{
    public sealed class Tab { public string idLang; public UIContent content; public bool disable = false; }
    public sealed class Setting { public List<Tab> tabs = new List<Tab>(); }
    public Setting setting = new Setting();
    public Dictionary<string, int> dictTab = new Dictionary<string, int>();
    public Layer layer;
    public int windowIndex = 0;
    public bool ThrowAfterAppend;
    public T GetComponentInParent<T>() where T : Layer => layer as T;
    public int GetInstanceID() => 1;
    public void Init() { }
    public void OnKill() { }
    public void BuildTabs(int id) { }
    public void AddTab(string id, UIContent content, object action, object sprite, string tooltip)
    {
        setting.tabs.Add(new Tab { idLang = id, content = content });
        if (ThrowAfterAppend) throw new InvalidOperationException("append failed");
    }
}
namespace UnityEngine
{
    internal class Object
    {
        public bool Destroyed;
        public static void DestroyImmediate(Object obj) { obj.Destroyed = true; }
    }
}
namespace BepInEx.Logging
{
    internal sealed class ManualLogSource
    {
        public void LogInfo(object message) { }
        public void LogWarning(object message) { }
    }
}
namespace HarmonyLib
{
    internal sealed class Harmony
    {
        public Harmony(string id) { }
        public void Patch(MethodInfo method, HarmonyMethod prefix = null, HarmonyMethod postfix = null) { }
        public void UnpatchSelf() { }
    }
    internal sealed class HarmonyMethod { public HarmonyMethod(MethodInfo method) { } }
    internal static class AccessTools
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        public static MethodInfo Method(Type type, string name, Type[] parameters = null) => parameters == null
            ? type.GetMethod(name, Flags) : type.GetMethod(name, Flags, null, parameters, null);
        public static FieldInfo Field(Type type, string name) => type.GetField(name, Flags);
    }
}
namespace BrilliantQuesting.Plugin
{
    internal static class ModInfo { public const string Guid = "journal-contract-test"; }
    internal sealed class ElinVanillaState
    {
        public EntityId PlayerId => EntityId.None;
        public GameTime Now => GameTime.Zero;
    }
    internal sealed class NativeJournalRenderer : UIContent
    {
        internal static bool ThrowOnCreate;
        internal static NativeJournalRenderer Last;
        internal static NativeJournalRenderer Create(Window window, UIContent template,
            Action<NativeJournalRenderer> refresh, Action<Exception> fail, BepInEx.Logging.ManualLogSource log)
        {
            if (ThrowOnCreate) throw new InvalidOperationException("no native resources");
            return Last = new NativeJournalRenderer();
        }
        internal void Render(NarrativeWorldState world, ElinVanillaState vanilla) { }
    }
}
