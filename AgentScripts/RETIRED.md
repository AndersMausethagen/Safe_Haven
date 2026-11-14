# Retired builders

On 2026-11-14 the scripts that rebuilt whole sections of the game were shut off at the user's request.

## Why

Each builder kept its own copy of what it made: quests, stories, scenes, prefabs, settings. Running it again wrote that
copy back over the project. That was only safe while every later change went into the same builder. Several times a change
landed somewhere else:
- a second builder (the prologue's boar);
- the Quest Map by hand;
- a change across the whole project (the controller's key prompts).

The next run of the old builder then put its older version back without warning. Work the user had seen and approved
came undone, and it was found only by playing.

## Now

- **The scenes and assets are the source of truth.** Nothing rebuilds them from a stored copy.
- **Changes are small and targeted:** one-off code (`unity command script eval`, or a throwaway script in the session's
  scratchpad) that changes only the nodes, fields or objects concerned.
- **Every change is checked with a before/after diff.** Snapshot what will change (story nodes and lines, quest fields,
  object transforms), make the edit, compare, and treat anything unintended as a regression to fix before committing.
- **Earlier states come back from git** (`git show <commit>:<path>`, `git checkout <commit> -- <path>`). Commits to
  `test` are frequent so that there is always a recent state to return to.
- **One owner per thing.** No two tools write the same asset or field.

## What is shut off

- **129 scripts in this folder.** Each starts with an `#error` line, so `unity command script run` refuses to compile it
  and prints the reason. The code is kept to read: how something was made, its numbers, its helper functions.
- **26 entries under the Editor's Safe Haven › Setup menu.** They call `RetiredBuilders.Refuse` and do nothing else.

## What still runs

- **The `Probe*` scripts and `UIOverlapAudit`, `VerifyGeneratedModel`:** they read or play; they don't author content.
- **`BuildDialogueVoices`:** it derives the voice bank from recorded clips (nobody edits that list by hand).
- **`RecordShaderWarmup`:** it records shader variants.
- **`BuildBenchmark`, `BuildDaylightTestScene`, `WorldDemo`:** they make their own test builds and scenes only.
- **`GenerateWorldTerrainShader.py`:** it generates code that is never edited by hand.
- **`RunTests.ps1`.**
- **The World Builder** (features → generated objects) and its tools. Generating from features is that tool's design, and
  `QuestTargetGuard` reports anything quests need that a rebuild would remove.
- **Shared "create what is missing" helpers** used by the World tools and the Sound Editor (they never overwrite).

To bring a retired script back for a one-off use, don't remove its `#error`. Copy the part you need into a targeted
script and diff the result.
