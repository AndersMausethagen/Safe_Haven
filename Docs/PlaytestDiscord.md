# A Discord server for Safe Haven's playtests (2026-11-23)

A private server where builds are shared, testers give feedback, and the game's logs come back. The server itself has to be made in
Discord by the user. The steps are below, with the parts the project can automate marked **[tool]**.

## 1. The server

Create it: Discord › **+** › Create My Own › For me and my friends. Name it **Safe Haven Playtest**. Keep it **invite-only**: Server
Settings › Invites, links that expire, no public listing.

### Roles

| Role | Who | Can |
|---|---|---|
| **Developer** | you | everything; posts builds |
| **Playtester** | invited testers | read everything, post in feedback and logs, download builds |
| **Co-op crew** (optional) | testers who play together | a voice channel and a channel to arrange sessions |

Give the `@everyone` role no access to the BUILDS and FEEDBACK categories. Then only Playtesters see builds, and a stray invite shows
nothing.

### Channels

| Category | Channel | Type | Who writes | What for |
|---|---|---|---|---|
| START HERE | `#welcome` | text, read-only | you | What Safe Haven is, the rules (builds stay in the server), how to get the Playtester role |
| | `#how-to-playtest` | text, read-only | you | Install and update the build, F8 to report, "Send my reports" (Escape menu) and where the zip goes, co-op notes, known issues |
| | `#announcements` | announcement, read-only | you | A new build is up; what to test this week |
| BUILDS | `#builds` | text, read-only | the build tool **[tool]** | One post per build: version, the download, what changed, what to look at |
| | `#known-issues` | text, read-only | you | What is broken on purpose or already known, so it isn't reported ten times |
| FEEDBACK | `#feedback` | **forum** | everyone | One post per topic. Tags: Bug, Idea, Confusing, Liked, Balance, Performance, Co-op. Template below |
| | `#playtest-logs` | text | everyone (and **[tool]**) | The zips from "Send my reports" (reports, screenshots, run logs) |
| | `#screenshots` | text | everyone | Moments worth sharing |
| COMMUNITY | `#general`, `#off-topic` | text | everyone | Talk |
| | `Playtest session` | voice | everyone | Playing together, co-op tests, watching someone play |

**Forum post template** (`#feedback` › Post Guidelines): *What happened? What did you expect? Where (raid or Haven, roughly when)?
Did you press F8 at the moment (then say the time)?*

## 2. Builds: where they live

Playtest builds are a few gigabytes, far over Discord's attachment limit. Options:

1. **itch.io, restricted page (recommended).** Free; a private project page with download keys or a password. Upload with
   `butler`, itch's command-line tool: it sends only what changed, and testers using the itch app get updates automatically.
   - **[tool]** A `PublishBuild.ps1` after `BuildWindows.ps1` could:
     1. `butler push` the build to the page's Windows channel;
     2. post in `#builds` through a webhook: version, commit list since the last build, what to test.
2. **A shared drive link** (Google Drive or OneDrive): no setup, but manual, slow downloads, and no updates.
3. **Steam Playtest** (later): needs the game's own Steam app (the Steam Direct fee). The co-op code still uses the test app 480.
   It's the natural step once there is a store page.

## 3. Logs and reports coming back

Today, "Send my reports" zips the reports, the run logs and Unity's logs, and opens the folder; the tester drags the zip into Discord.
`Tools/Playtest/analyse.py` reads those zips. Two ways to make it smoother:

- **Keep it manual (no change).** The game sends nothing over the network, as decided on 2026-11-21.
- **One click in the game [tool].** "Send my reports" posts the zip straight to `#playtest-logs` through a webhook, after showing what
  goes and asking first. The webhook's address would be inside the build, so anyone with the build could post to that channel. Fine for
  friends; use a new webhook per build and delete old ones. Before sending, the game would blank the Windows user name in Unity's log
  (it holds file paths).

Collecting the zips **[tool]**: a small Discord bot, made in the Discord Developer Portal with only "read message history", lets
`Tools/Playtest/fetch_discord.py` download every new zip from `#playtest-logs` into a local folder for `analyse.py`.

## 4. What the user does, what the tools do

1. Make the server, the roles and the channels above.
2. `#builds` › Edit Channel › Integrations › Webhooks › New Webhook › Copy URL. Put it in
   `AgentScripts/discord.local.json` (kept out of git), never in chat or a committed file:
   `{ "builds": "https://discord.com/api/webhooks/…" }`. The same goes for `#playtest-logs` if one click in the game is wanted.
3. For itch.io:
   1. make the project (Restricted);
   2. install butler;
   3. run `butler login` once.

Then the tools can be built: `PublishBuild.ps1`, and optionally the in-game upload and the fetch script.
