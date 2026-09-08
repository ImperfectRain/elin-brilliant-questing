using System;
using System.Linq;
using BepInEx.Logging;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Presentation;
using BrilliantQuesting.World;
using UnityEngine;
using UnityEngine.UI;

namespace BrilliantQuesting.Plugin
{
    /// <summary>A disposable native view. Owns its whole tree; keeps only page selection.</summary>
    internal sealed class NativeJournalRenderer : UIContent
    {
        private readonly JournalPageRegistry _pages = JournalPageRegistry.CreateDefault();
        private string _selected = "overview";
        private UINote _navigation;
        private UINote _body;
        private UIScrollView _scroll;
        private Action<NativeJournalRenderer> _refresh;
        private Action<Exception> _failure;
        private ManualLogSource _log;
        private bool _failed;

        internal static NativeJournalRenderer Create(Window window, UIContent template,
            Action<NativeJournalRenderer> refresh, Action<Exception> failure, ManualLogSource log)
        {
            GameObject root = new GameObject("BrilliantQuestingJournalContent", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                RectTransform rect = (RectTransform)root.transform;
                RectTransform original = template.transform as RectTransform;
                if (original == null || window.view == null)
                    throw new InvalidOperationException("Journal template has no usable native bounds.");
                root.layer = template.gameObject.layer;
                rect.SetParent(window.view.transform, false);
                rect.anchorMin = original.anchorMin;
                rect.anchorMax = original.anchorMax;
                rect.pivot = original.pivot;
                rect.sizeDelta = original.sizeDelta;
                rect.anchoredPosition = original.anchoredPosition;
                NativeJournalRenderer content = root.AddComponent<NativeJournalRenderer>();
                content._refresh = refresh;
                content._failure = failure;
                content._log = log;
                content.CreateWidgets(template);
                content.OnInstantiate();
                if (content._failed) throw new InvalidOperationException("Initial page rendering failed.");
                int questComponents = root.GetComponentsInChildren<ContentQuest>(true).Length;
                int questControls = root.GetComponentsInChildren<Portrait>(true).Length
                    + root.GetComponentsInChildren<UIList>(true).Length;
                if (questComponents != 0 || questControls != 0)
                    throw new InvalidOperationException("Unexpected quest components in the BQ-owned tree.");
                log?.LogInfo("Native Brilliant Questing journal: bounds template=" + template.GetType().Name
                    + "; root=" + content.GetType().Name + "; parentIsWindowView="
                    + (rect.parent == window.view.transform) + "; pages=" + content._pages.Pages.Count
                    + "; cloned template objects=0; ContentQuest components=" + questComponents
                    + "; quest list/portrait components=" + questControls + ".");
                return content;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private void CreateWidgets(UIContent template)
        {
            RectTransform nav = Child("Pages", transform);
            nav.anchorMin = new Vector2(0, 1);
            nav.anchorMax = Vector2.one;
            nav.offsetMin = new Vector2(12, -44);
            nav.offsetMax = new Vector2(-12, -8);
            HorizontalLayoutGroup row = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;
            _navigation = nav.gameObject.AddComponent<UINote>();
            _navigation.layout = row;

            RectTransform scrollRoot = Child("PagesScroll", transform);
            Stretch(scrollRoot, new Vector2(12, 10), new Vector2(-12, -52));
            _scroll = scrollRoot.gameObject.AddComponent<UIScrollView>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;

            // Reuse only native scrollbar art, never its GameObject, listeners, or references.
            Scrollbar nativeBar = template.GetComponentsInChildren<ScrollRect>(true)
                .Select(s => s.verticalScrollbar).FirstOrDefault(s => s != null);
            if (nativeBar == null || nativeBar.handleRect == null
                || !(nativeBar.targetGraphic is Image nativeHandle))
                throw new InvalidOperationException("No native scrollbar styling was available.");
            RectTransform bar = Child("Scrollbar", scrollRoot);
            Stretch(bar, Vector2.zero, Vector2.zero);
            bar.anchorMin = new Vector2(1, 0);
            bar.sizeDelta = new Vector2(Mathf.Max(12, ((RectTransform)nativeBar.transform).rect.width), 0);
            bar.pivot = new Vector2(1, 0.5f);
            CopyImage(bar.gameObject, nativeBar.GetComponent<Image>());
            RectTransform handle = Child("Handle", bar);
            Stretch(handle, Vector2.zero, Vector2.zero);
            Image graphic = CopyImage(handle.gameObject, nativeHandle);
            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = graphic;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.colors = nativeBar.colors;
            _scroll.verticalScrollbar = scrollbar;

            RectTransform viewport = Child("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, new Vector2(-bar.sizeDelta.x - 4, 0));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            _scroll.viewport = viewport;
            RectTransform body = Child("Sections", viewport);
            body.anchorMin = new Vector2(0, 1);
            body.anchorMax = Vector2.one;
            body.pivot = new Vector2(0.5f, 1);
            body.sizeDelta = Vector2.zero;
            VerticalLayoutGroup column = body.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 6;
            column.padding = new RectOffset(4, 4, 4, 12);
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            ContentSizeFitter fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _body = body.gameObject.AddComponent<UINote>();
            _body.layout = column;
            _scroll.content = body;
        }

        public override void OnInstantiate() => RefreshSafely();
        public override void OnSwitchContent(int idTab) => RefreshSafely();

        private void RefreshSafely()
        {
            if (_failed) return;
            try { _refresh(this); }
            catch (Exception ex)
            {
                _failed = true;
                // No exception may escape Window.SwitchContent. Remove only our visual tree.
                if (_navigation != null) _navigation.gameObject.SetActive(false);
                if (_scroll != null) _scroll.gameObject.SetActive(false);
                _failure(ex);
            }
        }

        internal void Render(NarrativeWorldState world, IVanillaState vanilla)
        {
            var available = _pages.Available(world, vanilla);
            JournalPageDescriptor page = available.FirstOrDefault(p => p.Id == _selected) ?? available.FirstOrDefault();
            _selected = page?.Id;
            var sections = page == null
                ? new[] { new JournalSection("Brilliant Questing", Array.Empty<JournalItem>(), "No pages are available yet.") }
                : page.Project(world, vanilla);
            // UINote.Clear/Build temporarily change global skin state. Always pair them,
            // including when a missing native resource causes a rendering exception.
            WithNote(_navigation, () =>
            {
                foreach (JournalPageDescriptor candidate in available)
                {
                    UIButton button = _navigation.AddButton(candidate.Label, () => SelectPage(candidate.Id));
                    button.interactable = candidate.Id != _selected;
                }
            });
            WithNote(_body, () =>
            {
                foreach (JournalSection section in sections)
                {
                    _body.AddHeader(section.Heading);
                    if (section.Items.Count == 0) _body.AddText(section.EmptyText, FontColor.Default);
                    foreach (JournalItem item in section.Items)
                    {
                        if (item.DestinationPageId == null) _body.AddText(item.Text, FontColor.Default);
                        else _body.AddButton(item.Text, () => SelectPage(item.DestinationPageId));
                    }
                }
            });
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1;
            _log?.LogInfo("Native Brilliant Questing journal page=" + (page?.Id ?? "none") + "; sections="
                + sections.Count + "; items=" + sections.Sum(s => s.Items.Count) + ".");
        }

        private void SelectPage(string id)
        {
            _selected = _pages.Resolve(id).Id;
            RefreshSafely();
        }

        private static void WithNote(UINote note, Action render)
        {
            SkinRootStatic prior = SkinManager.tempSkin;
            try { note.Clear(); render(); note.Build(); }
            finally { SkinManager.tempSkin = prior; }
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private static Image CopyImage(GameObject target, Image source)
        {
            Image image = target.AddComponent<Image>();
            image.sprite = source == null ? null : source.sprite;
            image.color = source == null ? Color.clear : source.color;
            image.type = source == null ? Image.Type.Simple : source.type;
            return image;
        }
    }
}
