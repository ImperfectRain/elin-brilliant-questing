# Documentation

Read in this order. Later documents assume the earlier ones and correct them where the game turned
out to disagree.

| | |
|---|---|
| [`design/master-design.md`](design/master-design.md) | The vision and the non-negotiable principles. Everything else serves this. A generated mirror of the `.docx` beside it, which is the source of truth. |
| [`design/post-master-findings.md`](design/post-master-findings.md) | Design reasoning developed after the master document: content classes, mechanical coverage, adventurer ecology, the shared action resolver, mod-ecosystem lessons, and the implementation doctrine. Three claims in it are marked **[superseded]**. |
| [`architecture.md`](architecture.md) | How the code is arranged and why the seams are where they are. |
| [`elin-api-notes.md`](elin-api-notes.md) | What the shipped assemblies actually expose, read with `tools/ApiDump`. **Authoritative on runtime facts** - where a design document disagrees with this, this wins. |
| [`elin-element-aliases.md`](elin-element-aliases.md) | The verified element alias table, read from a running game. Attributes, skills, Home Skills, policies. Data, not code - it cannot be recovered from the assembly. |
| [`plugin-build.md`](plugin-build.md) | Populating `lib/` from your own install, building the plugin, installing it, and the two-step activation that is easy to miss. |
| [`roadmap.md`](roadmap.md) | Phases, gates, and an honest account of what is done. |

## Conventions

**Design documents describe intent; API notes describe fact.** The master design and the
post-master findings were written from research and reasoning. `elin-api-notes.md` was read off
`Elin.dll`. When they conflict, the assemblies are right.

**"Found" is not "works".** The roadmap distinguishes a located API from a verified one. Metadata
proves a member exists. Only running it inside Elin proves it behaves.

**Superseded claims are corrected in place, not deleted.** A reference document for coding agents
that contains known-false statements is worse than one with visible history, so overtaken passages
are marked rather than quietly removed.
