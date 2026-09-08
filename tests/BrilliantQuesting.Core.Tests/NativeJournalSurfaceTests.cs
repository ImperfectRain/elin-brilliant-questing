using System;
using System.Reflection;
using BrilliantQuesting.Plugin;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class NativeJournalSurfaceTests
    {
        public NativeJournalSurfaceTests()
        {
            Set("_installed", false);
            Set("_patchesAvailable", false);
            Set("_disabled", false);
            NativeJournalRenderer.ThrowOnCreate = false;
            NativeJournalRenderer.Last = null;
            NativeJournalSurface.Install(new BepInEx.Logging.ManualLogSource());
        }

        [Fact]
        public void MountFailureLeavesVanillaTabsAndEnablesFallback()
        {
            Window window = Journal();
            var vanilla = window.setting.tabs[0];
            NativeJournalRenderer.ThrowOnCreate = true;
            Call("BeforeBuildTabs", window);
            Assert.Same(vanilla, Assert.Single(window.setting.tabs));
            Assert.False(vanilla.content.gameObject.Destroyed);
            Assert.True(NativeJournalSurface.UseDialogueFallback);
        }

        [Fact]
        public void PartialAppendFailureRollsBackOnlyOwnedContent()
        {
            Window window = Journal();
            var vanilla = window.setting.tabs[0];
            window.ThrowAfterAppend = true;
            Call("BeforeBuildTabs", window);
            Assert.Same(vanilla, Assert.Single(window.setting.tabs));
            Assert.False(vanilla.content.gameObject.Destroyed);
            Assert.True(NativeJournalRenderer.Last.gameObject.Destroyed);
            Assert.True(NativeJournalSurface.UseDialogueFallback);
        }

        [Fact]
        public void OneMountAndFailureDoNotDisableTabMemoryProtection()
        {
            Window window = Journal();
            Call("BeforeBuildTabs", window);
            Call("BeforeBuildTabs", window);
            Assert.Equal(2, window.setting.tabs.Count);
            Assert.False(NativeJournalSurface.UseDialogueFallback);
            Call("FailSurface", new InvalidOperationException("render failed"));
            window.dictTab["journal0"] = 1;
            Call("AfterOnKill", window);
            Assert.Equal(0, window.dictTab["journal0"]);
            window.dictTab["journal0"] = 99;
            Call("BeforeInit", window, window.layer);
            Assert.Equal(0, window.dictTab["journal0"]);
        }

        [Fact]
        public void OtherWindowsAndVanillaSelectionRemainUntouched()
        {
            Window window = new Window();
            Call("BeforeBuildTabs", window);
            Assert.Empty(window.setting.tabs);
            Window journal = Journal();
            journal.dictTab["journal0"] = 0;
            Call("BeforeInit", journal, journal.layer);
            Assert.Equal(0, journal.dictTab["journal0"]);
        }

        private static Window Journal()
        {
            var window = new Window { layer = new LayerJournal() };
            window.setting.tabs.Add(new Window.Tab { idLang = "Quests", content = new UIContent() });
            return window;
        }
        private static void Set(string name, object value) => typeof(NativeJournalSurface)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
        private static void Call(string name, params object[] args) => typeof(NativeJournalSurface)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    }
}
