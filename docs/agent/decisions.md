# Durable Agent Decisions

This file contains only decisions expected to remain useful across many commits. It is not a status log, changelog, or substitute for design documents.

Keep entries terse. If a decision stops being durable, update or remove it rather than preserving obsolete history here.

## D001 — Core is headless

`BrilliantQuesting.Core` must not depend on Elin, Unity, or BepInEx. Runtime-specific state crosses the boundary through adapter abstractions.

Reason: deterministic headless testing, simulation tooling, compatibility, and separation from game-version churn.

## D002 — Observe vanilla outcomes

Where Elin already resolves an action, Brilliant Questing should normally observe the resulting world state/event rather than implement a competing resolution system.

Reason: avoid double consequences and preserve compatibility with vanilla/modded mechanics.

## D003 — Epistemic layers stay separate

Truth, a person's belief, available proof, and an authority's judgment are different things and must not be collapsed into one state.

Reason: investigations, rumors, lies, false accusations, correction, and consequences depend on the distinctions.

## D004 — Narrative identity outlives vanilla object presence

Stable `EntityId` identity must survive unloaded, missing, dead, destroyed, or otherwise unavailable vanilla objects.

A binding records identity correspondence; it does not prove current existence.

## D005 — History is append-oriented

The event ledger represents what happened. Later interpretation should derive from recorded events rather than rewriting historical events to fit current knowledge.

## D006 — Runtime evidence outranks metadata discovery

Finding a class, method, field, trait, or hook in assemblies proves availability, not behavior.

Record runtime verification in `docs/elin-api-notes.md` when behavior matters.

## D007 — No authoritative runtime LLM

Runtime LLM output may not determine authoritative checks, facts, world state, persistence, or consequences.

## D008 — Player knowledge is not world omniscience

Background simulation may update NPC/world beliefs without silently giving the player those beliefs.

## D009 — Authored content, behavior, and save history are separate

C# expresses behavior. Authored data expresses content. Saves store history/content identifiers rather than treating authored prose as immutable save state.

## D010 — Existing Elin mechanics are preferred solution surfaces

If an existing Elin mechanic can constitute a valid solution, use/integrate it instead of creating a Brilliant Questing-only duplicate.

## D011 — Physical proof stays attached to a physical object

Reading an object for what it proves requires having that object: examination verbs work on the actor's own inventory, and a search recovers only what is in the actor's current zone.

Reason: keeps evidence something a player can carry, lose, sell or have taken, and keeps acquiring it — searching, following, lifting a pocket — real gameplay rather than a formality. Knowing an unprovable thing is a legitimate and distinct state.

## D012 — Standing gates contacts, never attempts

Thieves Guild rank, Karma and personal standing decide which characters will do criminal work for the player — receivers, forgers, carriers. They never gate a verb the player performs with their own hands.

Reason: a build should differ by which routes exist for it, not by rolling worse on the same route. Gating an attempt on membership would also collapse into the "low odds are a reason to hide the option" mistake the whole availability model exists to avoid. A contact who will not deal with a stranger is a genuine impossibility, in the same class as invoking guild authority without membership.

## D013 — Losing an object and unmaking it are different revocations

Proof revocation is keyed by the object, not by the claim. Destroying a thing strips it from everybody's proofs; parting with it strips it only from the person who parted with it. Neither touches belief.

Reason: a claim resting on two objects should keep the second when the first burns, and a fact-keyed revocation cannot express that. Selling evidence must leave the buyer able to produce it. Passing an item id to the fact-keyed call matches nothing and silently revokes nothing at all.

## D014 — Vanilla owns production; the simulation reads quality and never rolls it

Where an object already exists, the procedural layer reads what the game made and rolls nothing. A demand is a property constraint - kind, quality, worth - and goods that meet it are handed over without a check. A check is run only when there is no such object and the actor is working from raw materials, and even then it produces no new object: the stock is consumed and the demand is answered.

Reason: Elin already has cooking, brewing, alchemy and building. Rolling procedural dice over a finished Thing would be a second, worse crafting mechanic disagreeing with the first about what the player just made. Stating demand as a constraint rather than as a named item is what keeps it answerable by any route that produces the right thing, and keeps a shoddy object a wrong object rather than a bad roll. An unread quality is zero, which every threshold refuses - a build that cannot report quality loses the hand-over route and keeps the rest, rather than accepting goods on the strength of a field nobody filled in.

## D015 — A power you petition is a contact, not a tool

A deity gates whether the attempt exists. Which god a matter is in the gift of, the piety he asks of whoever asks him, and what is lying on his altar are preconditions: a follower of another god is refused by name, not given worse odds.

Reason: this looks like an exception to D012 and is not one. The god is the party doing the work, so declining to hear a stranger is exactly a contact who will not deal with you — the same class as a receiver who will not fence for an unknown, and not the same class as forbidding somebody to pick a lock with their own hands. Read the other way round, D012 would force every faith route into a modifier on one generic magic roll, which is what `PM 69` and `MD 13.6` exist to prevent. The test is whether the standing belongs to the actor's own hands or to somebody else's willingness.

Which god a matter belongs to is stated by the situation as a fact, never by a table of deities and their portfolios held in Core. Nothing in the simulation knows what Kumiromi is the god of, which is what keeps the routes generatable and survives Elin adding a religion.

## D016 — The verb registry is not the player-facing menu

Narrative verbs are simulation capabilities, not global UI entries. Player-facing actions are projected through contextual interaction surfaces and semantic intent families, then filtered by actor, target, knowledge state, world state and affordance.

Prefer shallow semantic paths such as `Talk -> Inquire -> Ask about Vurl`, `Talk -> Aggressive -> Intimidate`, or `Examine -> Study -> Read ledger` over a flat list of registered verbs.

The UI hierarchy must describe the player's intent, not the code's class hierarchy. Adding a new verb must not require exposing that verb everywhere, and the presentation layer must not reveal subjects or facts the player does not know.

Reason: the internal action vocabulary must grow without increasing cognitive load, leaking hidden information, or turning Brilliant Questing into a debug command menu.

## D017 — A datum the game did not answer is absent, not zero

Where a read can partly fail, the snapshot says which parts it got. `HomeState` carries `CapacityKnown` and `TryGetMetric` beside the values, and the adapter leaves an unread element out rather than defaulting it.

Reason: capability honesty (a whole integration reports unsupported) is too coarse once one read returns several numbers. A Home whose capacity silently defaulted to zero would look permanently full and close every shelter route; one whose Public Safety defaulted to zero would look like a slum and open the wrong ones. Callers with a threshold must ask whether the number was read, and the refusing direction is the safe one when it was not.

Add a new entry only when the decision is both load-bearing and durable.
