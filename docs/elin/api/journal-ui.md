# Journal UI

## Evidence and corrective pass — 8 September 2026

The latest user-provided live run confirms the top-level BQ tab, mounting beneath
`window.view`, OnInstantiate/OnSwitchContent, refresh/build calls, attached simulation,
Chronicle log export and tab-memory guard. **It does not confirm a correct visible BQ page.**
The visible page retained vanilla quest controls and test content. This supersedes the earlier
BQ-138 note claiming visibly populated BQ content from lifecycle diagnostics.

Installed method bodies were inspected with ILSpy 11, alongside local ApiDump metadata:

- `lib/Elin/Elin.dll` SHA256 `75100D0F8F04C19B1F0E8573C533DA229EFECF882B1B7C0B5DB28D8429C4DDF1`.
- `lib/Elin/Plugins.UI.dll` SHA256 `7F256F54D708653D832A4EFBF18173C41BB7AD039CE6C65273F9A731A28FDE21`.
- Inspection output stays under ignored `reference/elin/`; game source is not redistributed.

| Member | SOURCE-OBSERVED contract |
|---|---|
| `Window.AddTab` | Appends a Setting.Tab; does not require ContentQuest. |
| `Window.BuildTabs(int)` | Builds native top-level buttons, localizes labels, selects an index. |
| `Window.SwitchContent(int)` | Toggles instantiated content; prefab content is instantiated under view, then receives OnInstantiate; selected content receives OnSwitchContent. |
| `UIContent`, `UIContentView` | UIContent extends UINote with two empty virtual lifecycle methods; UIContentView adds no behavior. |
| `LayerJournal.OnSwitchContent` | Empty. HeaderIsListOf is true only for vanilla indices 2–4. |
| `UINote.Awake` | Initializes target/cur and gets the local LayoutGroup. |
| `UINote.Clear` | Calls layout.DestroyChildren(destroyInactive: true) and temporarily selects skin state. **Does not strip an arbitrary content hierarchy.** |
| `UINote.AddHeader`, `AddText`, `AddButton` | Instantiate generic HeaderNote, NoteText and ButtonNote resources beneath **layout**, not an arbitrary target or Window. These paths do not need prof. |
| `UINote.Build` | Rebuilds local/ancestor layout and restores temporary skin. Does not mount content or remove quest widgets. |
| `ContentQuest` | Owns list, textClient, textTitle, textDetail, textHours, textNote, textReward, textZone, portrait and buttonAbandon; switch populates quests and binds quest actions. |
| `UIScrollView` | Extends ScrollRect with Elin sensitivity and drag handling. Awake configures inertia and can adjust its scrollbar for Window stats; scrollbar must exist on activation. |

The former code cloned ContentQuest, removed only its root component, and reused note fields.
Removing that component did not remove its serialized child UI. A successful Build therefore
proved neither correct visual ownership nor visibility. Other observed journal templates (key
items, codex, factions, gallery, hall of fame) also have specialized content. No neutral prefab
hierarchy is presumed to exist.

## Current boundary

NativeJournalSurface retains one BQ LayerJournal tab and the existing memory guard. The guard
also runs after a rendering failure, so a failed BQ tab cannot poison the next vanilla open.
Mount failure removes only entries referencing that attempt's owned content and enables the
existing dialogue/log fallback. Neither successful mounting nor rollback edits a vanilla page.

NativeJournalRenderer creates a fresh inactive RectTransform/UIContent under window.view.
It copies only native root bounds/layer and scrollbar art/colors. **No content hierarchy is
cloned, no vanilla component is removed, and no mutable child reference is copied.** BQ creates
its own navigation layout, viewport/mask, vertical layout, content-size fitter, UIScrollView and
scrollbar. Images share read-only sprite assets; controls and listeners are new. Missing bounds
or native scrollbar styling fail closed. Root activation remains Window-owned.

Two internal pages use JournalPageRegistry: Overview (known active matters, standing, known
people, tagged claims) and Chronicle (the existing complete ChronicleNarrative.Export, with
sections, actions and disputed knowledge). Generic native note resources supply typography,
headers and buttons. Registry callbacks only read existing authorities; DTOs and selection
are transient. Unknown identity fallbacks become unnamed wording at the player boundary.
No confidence numbers, tension scores, NPC importance, tone tags or inspector reports are drawn.

Rendering restores SkinManager.tempSkin in a finally block even if a resource fails. Switch
exceptions are contained, owned widgets are disabled and dialogue/log fallback is enabled.
Diagnostics report bounds template/root type, mount parent, page count, selected page and
section/item counts; initial checks count surviving ContentQuest, UIList and Portrait components.
No per-frame BQ diagnostics run. Generic resource appearance and final layout remain runtime
questions, not facts established by compilation.

## Live verification — all items UNVERIFIED for this corrective build

Use the newly built Plugin DLL through the normal package installation procedure. This pass did
not install it into the game, load a save, or capture a live screenshot.

1. Load the existing test save.
2. Open Journal → Brilliant Questing.
3. Confirm no inherited Quests in progress, Reward, Remaining Time, Location, Client, Abandon,
   unrelated portrait, credit or sfsfesfesf appears in BQ.
4. Confirm actual BQ state is visible in Overview, including truthful knowledge tags.
5. Open Chronicle: confirm Garron, petty theft, property returned and
   `[Disputed] Vess stole silver ring`, plus what the player knew/did.
6. Switch Overview/Chronicle and use the Chronicle link. Check selection and refreshed contents.
7. Close/reopen the journal; confirm no crash or duplicated content.
8. Repeatedly switch vanilla tabs/BQ; internal selection should survive while that window lives.
9. Save/reload and reopen. No Unity selection or copied page data should enter the save.
10. Confirm vanilla Quests still lists, selects and displays its normal quest details correctly.
11. Confirm exactly one BQ top-level tab.
12. Check empty/new-save state and unavailable knowledge: no fabricated entries or placeholders.
13. Check long lists: native wheel/drag/scrollbar, clipping, wrapped text, no overflow, usable
    navigation and fit at the player's UI scale. Verify runtime bounds and native scrollbar
    sprite/tint, which metadata cannot establish.
14. Capture a screenshot and relevant BQ logs: template/root, parentIsWindowView=True, two pages,
    ContentQuest components=0, quest list/portrait components=0, page/section/item counts,
    Chronicle export and tab-memory normalization. Include any fallback reason.

Headless mount tests execute the production mounting/rollback code with Window/renderer doubles;
they prove exception containment, fallback, one-tab isolation and memory policy, **not Unity
rendering**. Projection tests prove read-only refresh, uncertainty, empty states, metadata
exclusion and named theft history. Neither replaces this visual checklist.
