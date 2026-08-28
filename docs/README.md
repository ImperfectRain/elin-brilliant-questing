# Documentation

Read in this order. Later documents assume the earlier ones and correct them where the game turned
out to disagree.

| | |
|---|---|
| [`implementation-roadmap.md`](implementation-roadmap.md) | **The plan.** Ordered, commit-sized steps from here to launch, with checkpoints, a system ledger, and an index proving every idea in every design document has a place. Start here. |
| [`design/master-design.md`](design/master-design.md) | The vision and the non-negotiable principles. Everything else serves this. A generated mirror of the `.docx` beside it, which is the source of truth. |
| [`design/post-master-findings.md`](design/post-master-findings.md) | Design reasoning developed after the master document: content classes, mechanical coverage, adventurer ecology, the shared action resolver, mod-ecosystem lessons, and the implementation doctrine. Three claims in it are marked **[superseded]**. |
| [`design/living-world-priorities.md`](design/living-world-priorities.md) | Third design reference: runtime-grounded priorities, player-facing UX doctrine, safe vanilla mutation, procedural locations, fun-first rules, and a priority roadmap from the current state. |
| [`design/character-dialogue-system.md`](design/character-dialogue-system.md) | The expression layer: personality, values, storylets, casting, speech acts, dialogue realization, Elin tone, scenes and beats. |
| [`design/engagement-and-reward.md`](design/engagement-and-reward.md) | Why a player would engage with the simulation at all: supply-line coupling, access as reward, and history as trophy case. Grounded in RimWorld, Dwarf Fortress, Kenshi, Fallen London and self-determination theory. |
| [`architecture.md`](architecture.md) | How the code is arranged and why the seams are where they are. |
| [`elin-api-notes.md`](elin-api-notes.md) | What the shipped assemblies actually expose, read with `tools/ApiDump`. **Authoritative on runtime facts** - where a design document disagrees with this, this wins. |
| [`elin-element-aliases.md`](elin-element-aliases.md) | The verified element alias table, read from a running game. Attributes, skills, Home Skills, policies. Data, not code - it cannot be recovered from the assembly. |
| [`plugin-build.md`](plugin-build.md) | Populating `lib/` from your own install, building the plugin, installing it, and the two-step activation that is easy to miss. |
| [`handoff-drama-projection.md`](handoff-drama-projection.md) | Cold-start brief written for the Drama projection work, which has since landed. Kept for its traps-and-constraints section, which still applies to any new contributor. |
| [`roadmap.md`](roadmap.md) | Historical record of the original phases and the Gate A/B evidence. Superseded for planning by `implementation-roadmap.md`. |

## Conventions

**Design documents describe intent; API notes describe fact.** The master design and the
post-master findings were written from research and reasoning. `elin-api-notes.md` was read off
`Elin.dll`. When they conflict, the assemblies are right.

**"Found" is not "works".** The roadmap distinguishes a located API from a verified one. Metadata
proves a member exists. Only running it inside Elin proves it behaves.

**Superseded claims are corrected in place, not deleted.** A reference document for coding agents
that contains known-false statements is worse than one with visible history, so overtaken passages
are marked rather than quietly removed.
