# Architecture

Two rules explain nearly every decision in this codebase.

**One seam to the game.** `BrilliantQuesting.Core` has no reference to `Elin.dll`, BepInEx or Unity.
Everything it needs from the running game arrives through `Integration/IVanillaState.cs`, and
everything it needs to *create* in the game goes through `Integration/ISituationStager.cs`. Elin is
in active Early Access; when it moves, one file is repaired and the world model, verb library and
tests do not notice.

Because every write converges there, the seam is also where the mod's licence to change the game is
decided. Each write on `IVanillaState` declares what it does and to whom; `VanillaStateBase`
implements all of them once, refuses any the actor's `NarrativeMutationPolicy` does not permit, and
only then calls the implementation. A character the game will not classify keeps the reversible
reaches and loses relocation, absence and death (decision D019).

Where the mod moves somebody, it stores the intent and re-derives the enforcement.
`AbsenceLifecycle` holds the only state that says a person is away, and reconciles it against the
game on load, on the day turn and on a zone change - because a citizen refresh, a rebuilt zone and a
reloaded save all put people back where the game last wrote them. Absence is expressed as travel
rather than removal, so one character exists throughout and bringing them home is never refused
(decision D020).

**One place where consequences happen.** Verbs do not adjust affinity, Karma or knowledge directly.
They record an event. `Consequences/ConsequenceEngine.cs` derives the rest from a single table. That
is what lets the debug inspector answer "why did she react like that" without re-running the world.
The one contextual input to that derivation is `World/SocialPractices.cs`: what the room made of an
act changes how much of the room's reaction lands, and never what the law made of it or what the act
cost the person it happened to (decision D056).

## Layers

```
Foundation      EntityId, deterministic RNG, GameTime
                stable identity and reproducible seeds; everything else depends on these

Integration     IVanillaState, VanillaStateBase, NarrativeMutationPolicy,
                ISituationStager, CharacterBlueprint, SiteBlueprint,
                SandboxVanillaState, SandboxStager
                the only place that knows a game exists, and the one gate on changing it

World           NarrativeNpc, Organization, NarrativeSite, SiteGenesis, EntityRegistry,
                NarrativeWorldState, ActorAbsence, AbsenceLedger, AbsenceLifecycle,
                IdentityAffordances, ActorLocalInterpreter, SocialPractices
                the procedural database; the aggregate root is NarrativeWorldState, and the three
                derivations beside it say what an identity implies, what one observer makes of an
                event, and what the place an act happened in makes of it. SiteGenesis is the one
                way a place the mod owns comes into existence, and it runs once per place

Events          WorldEvent, EventLedger, EventTags
                append-only history plus queued dispatch (a reaction may append, never recurse)

Knowledge       Fact, KnowledgeRecord, KnowledgeGraph, RumorSystem
                objective truth, per-character belief, and whether either can be proved

Memory          MemoryRecord, MemoryLedger
                why an NPC feels what they feel, with consolidation so long saves stay small

Relationships   RelationshipGraph
                who is tied to whom, so harm propagates past its target

Checks          CheckProfile, CheckRequest, CheckResult, VanillaStyleCheckResolver
                semantic actions expressed in real Elin values, resolved the way vanilla does

Actions         NarrativeAction, ActionRegistry, ActionContext, Availability, Library/*
                the reusable RPG verbs and how they are discovered

Consequences    ConsequenceProfile(s), ConsequenceEngine
                events in, world changes out

Threads         NarrativeThread, EscalationStep, ThreadEngine
                unresolved causal chains that keep moving while the player is elsewhere

Situations      PettyTheftSituation, PettyTheftEscalation, TheftLaboratory
                one generated archetype, its escalation, and the harness that runs it headless

Continuity      CallbackHook(s), CallbackRecurrence, ItemProvenance, LocationHistory
                history read back per person, per object and per place; all three are derivations
                over the ledger with no store of their own, so old business cannot outlive or
                contradict what happened, and none of them can be produced for somebody who was
                never part of it. A place's legend is the same derivation compressed: what kept
                happening there, grouped by the kind of story it left

Dialogue        SpeechAct, SpeechActProfile, Disclosure, Deception, DialogueFragment,
                DialogueRealizer, VoiceProfile, ConversationState
                what is meant, decided separately from how it is said; the realizer holds no
                world and can only choose among authored phrases

Storylets       StoryletDefinition, StoryletCasting, StoryletChemistry, StoryletEngine,
                ActorIntent, StoryletRouter
                what is happening: a situation becomes a cast scene, and the scene routes on
                what its actors decide rather than on what an author wrote down

Content         ContentBundle, ContentBundleLoader, StoryletContent, DialogueFragmentContent
                storylets and fragments are compiled data; Core reads a bundle and never YAML

Persistence     Json, WorldStateSerializer, SaveMigrations
                versioned, human-readable, and restores without replaying history

Diagnostics     NarrativeInspector
                the "why?" tooling: options with their rejection reasons, belief spread, history,
                and a played scene beat by beat with the terms behind every decision
```

The dramatic path runs one way through those layers and never doubles back:

```
world state -> situation -> storylet found and cast -> beat
            -> the actor decides what to try to communicate  (ActorIntent, or Disclosure
               where somebody has just been asked about the claim)
            -> a check settles what is genuinely in doubt     (ICheckResolver)
            -> the meaning is worded                          (DialogueRealizer over content/)
            -> what happened is recorded as an event          (EventLedger -> ConsequenceEngine)
            -> the route out of the beat                      -> the next beat, or a resolution
```

Each arrow crosses a layer that cannot see the one after it. Storylets decide what is happening;
semantic acts decide what somebody is trying to communicate; character state decides how they
approach it; fragments decide how that meaning is expressed; resolvers decide what actually
happens. Wording is the only layer with no write access to anything, which is a fact about
`DialogueRealizer`'s constructor rather than a discipline anybody has to keep.

## Check resolution

`VanillaStyleCheckResolver` reproduces the arithmetic observed in Elin's own `Check`:

```
final DC = base DC
         + target level contribution
         + target element contribution
         - acting character's element/skill contribution
         + situational modifiers
roll 1d20 -> 20 is a critical pass, 1 a critical fail, otherwise roll >= final DC passes
```

Matching the shape is the point. When the runtime spike confirms it is safe to call vanilla
`Check.Perform` from a mod, swapping resolvers should not re-balance any content, because the
content was written against the same curve. `ICheckResolver` is the swap point.

Every term is recorded with a label, so `CheckResult.Explain()` prints the whole calculation:

```
check proc_pickpocket: base 13 +4 (target Perception) -2 (Pickpocket) -3 (Dexterity)
  +1 (value of the item) +1 (onlookers) => DC 14; rolled 18 => Pass
```

## Four kinds of requirement

`Availability` distinguishes them, and the distinction is load-bearing:

| Kind | Behaviour | Example |
|---|---|---|
| Hard impossibility | not offered | revealing a fact you have never heard |
| Vanilla capability limit | not offered, and says so | item transfers unavailable on this build |
| Contested check | offered, may fail | lying to someone who can prove otherwise |
| Systemic world action | no roll at all | handing back property, starting a fight |

Being bad at something is never in this table. A low skill changes the DC, not the menu.

## Why knowledge is a separate graph

A theft creates a fact. Whether anyone *knows* it depends on who was standing there. That single
separation is what produces witness intimidation, bribery, false testimony, alibis, coverups,
blackmail and revenge without any of them being written as features - they are all just queries
against who believes what and who can prove it.

It also gives the consequence layer a rule that turned out to matter: an event tagged `unnoticed`
changes nobody's affinity. Without it, a perfect pickpocket would silently tell the victim they had
been robbed, because affinity moving is itself information.

## Determinism

`DeterministicRng` is splitmix64 with a `Fork(label)` that derives an independent stream from a
label. Situations record their seed. The result is that a strange outcome can be reproduced exactly,
which is the difference between debugging a procedural system and guessing at it.
