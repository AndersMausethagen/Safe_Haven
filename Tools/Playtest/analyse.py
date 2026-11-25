"""Safe Haven playtest analysis: friends' report zips in, a ranked report of bugs, ideas and what the logs show out.

Each friend presses F8 in the game to write a report (Bug, Idea, Confusing or Liked, with a screenshot) and, when done, "Send my
reports" zips them with their run logs (Playtests/*.csv) and Unity's Player.log. Those zips are dropped in Discord; save them in
Tools/Playtest/Inbox and run:

  Tools/Playtest/.venv/Scripts/python.exe Tools/Playtest/analyse.py            estimate, ask, then analyse every zip in Inbox
  Tools/Playtest/.venv/Scripts/python.exe Tools/Playtest/analyse.py --dry-run  build every prompt and the estimate, send nothing
  Tools/Playtest/.venv/Scripts/python.exe Tools/Playtest/analyse.py --yes      no question before sending
  ... --inbox PATH (another folder of zips), --local (your own logs from LocalLow, as one more player: for trying it out)

Two passes. Each report is read on its own (Claude Sonnet 5.5): what it is, how bad, where in the game, how to make it happen
again, using the log rows around the moment it was written, its screenshot and any Unity errors. Then one read of everything
(Claude Opus 5.5): duplicates merged and ranked by how many players hit them, ideas grouped, and what the logs show that nobody
reported (where players die, stall or never go). Results/<date>/report.md is the result; findings.json holds each report's read.

The API key is the one on this PC (ANTHROPIC_API_KEY, or a profile from `ant auth login`); it never goes in the game. What is sent:
the reports' text and screenshots, digests of the logs (not the raw files) and error lines. Tell testers so.
"""
import argparse
import base64
import csv
import datetime
import io
import json
import os
import re
import sys
import zipfile
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
LOCAL = os.path.join(os.path.expanduser("~"), "AppData", "LocalLow", "DefaultCompany", "Safe Haven")

REPORT_MODEL = "claude-sonnet-5-5"
SUMMARY_MODEL = "claude-opus-5-5"
# Per million tokens, input / output (Claude API list prices, 2026-11).
PRICES = {REPORT_MODEL: (2.0, 10.0), SUMMARY_MODEL: (4.0, 20.0)}
FALLBACK_BETA = "server-side-fallback-2026-07-01"

# Rows that say what happened; the rest (positions, per-minute tallies) are summarised, not listed.
STORY_EVENTS = {"SessionStart", "SessionEnd", "SceneEntered", "PlayerDied", "DeathRecap", "QuestStarted", "QuestCompleted", "QuestFailed",
                "RaidTier", "RaidFinished", "RaidStarted", "Built", "Upgraded", "Cheat", "Assist", "SkillLevel", "PlaceFound",
                "RescuedResident", "ResidentArrived", "BreachStarted", "BreachEnded", "ModuleFitted", "RaidEvent", "Kit"}

GAME_CONTEXT = """Safe Haven is a single-player (and 4-player co-op) survival-crafting game in Unity, a Norse-folklore setting. The player
runs a home base, the Haven, and goes on timed raids through a portal into generated regions (Misty Woods, Troll Valley, Moss
Marsh) to gather, fight, rescue the lost expedition's members and follow the story; dying in a raid loses what was carried. The
game is a prototype being playtested by friends of the developer. Log rows: played_min (minutes played), scene, event (a game
event kind), subject (the asset or place), amount, detail."""


# ----- Reading the bundles -----

def read_csv_rows(text):
    rows = []
    for row in csv.DictReader(io.StringIO(text)):
        try:
            row["played_min"] = float(row.get("played_min") or 0)
        except ValueError:
            row["played_min"] = 0.0
        rows.append(row)
    return rows


def load_bundles(inbox, include_local, min_minutes, local_runs):
    """Every player's reports, runs and errors: from each zip in the inbox, and the local logs when asked. Runs shorter than
    min_minutes are left out (a menu opened and closed), unless a report was written in them; the local logs keep only the
    newest local_runs real ones (this PC's folder is mostly the Editor's and the tests' runs)."""
    players = []
    for name in sorted(os.listdir(inbox)) if os.path.isdir(inbox) else []:
        if name.lower().endswith(".zip"):
            players.append(load_zip(os.path.join(inbox, name)))
    if include_local:
        players.append(load_local())
    for p in players:
        reported = {r.get("run_id") for r in p["reports"]}
        kept = {k: v for k, v in p["runs"].items() if k in reported or (v and max(r["played_min"] for r in v) >= min_minutes)}
        if p["name"].startswith("local"):
            newest = sorted(kept)[-local_runs:]
            kept = {k: kept[k] for k in newest}
        p["runs"] = kept
    return [p for p in players if p["runs"] or p["reports"]]


def load_zip(path):
    with zipfile.ZipFile(path) as z:
        names = z.namelist()
        meta = json.loads(z.read("meta.json")) if "meta.json" in names else {}
        player = {"name": meta.get("player") or os.path.splitext(os.path.basename(path))[0], "meta": meta, "runs": {}, "reports": [], "errors": []}
        for n in names:
            low = n.lower()
            if low.startswith("playtests/") and low.endswith(".csv"):
                player["runs"][os.path.splitext(os.path.basename(n))[0]] = read_csv_rows(z.read(n).decode("utf-8", "replace"))
            elif low.startswith("reports/") and low.endswith(".json"):
                report = json.loads(z.read(n))
                shots = [os.path.splitext(n)[0] + ext for ext in (".jpg", ".png")]
                report["_screenshot"] = next((z.read(x) for x in shots if x in names), None)
                player["reports"].append(report)
            elif low.startswith("logs/") and low.endswith(".log"):
                player["errors"].extend(read_errors(z.read(n).decode("utf-8", "replace")))
    return player


def load_local():
    player = {"name": "local (developer)", "meta": {}, "runs": {}, "reports": [], "errors": []}
    folder = os.path.join(LOCAL, "Playtests")
    for name in sorted(os.listdir(folder)) if os.path.isdir(folder) else []:
        if name.endswith(".csv"):
            with open(os.path.join(folder, name), encoding="utf-8", errors="replace") as f:
                player["runs"][name[:-4]] = read_csv_rows(f.read())
    reports = os.path.join(LOCAL, "Reports")
    for name in sorted(os.listdir(reports)) if os.path.isdir(reports) else []:
        if name.endswith(".json"):
            with open(os.path.join(reports, name), encoding="utf-8") as f:
                report = json.load(f)
            shots = [os.path.join(reports, name[:-5] + ext) for ext in (".jpg", ".png")]
            report["_screenshot"] = next((open(x, "rb").read() for x in shots if os.path.isfile(x)), None)
            player["reports"].append(report)
    for log in ("Player.log", "Player-prev.log"):
        path = os.path.join(LOCAL, log)
        if os.path.isfile(path):
            with open(path, encoding="utf-8", errors="replace") as f:
                player["errors"].extend(read_errors(f.read()))
    return player


ERROR_START = re.compile(r"^(NullReferenceException|[A-Za-z.]*Exception|Error|Assertion failed|MissingReferenceException)\b[:\s]?(.*)")


def read_errors(text):
    """Distinct errors in a Unity Player.log: the message, its first stack line, and how often."""
    found = Counter()
    first_line = {}
    lines = text.splitlines()
    for i, line in enumerate(lines):
        m = ERROR_START.match(line.strip())
        if not m:
            continue
        message = line.strip()[:300]
        where = next((l.strip() for l in lines[i + 1:i + 6] if l.strip() and ("(at " in l or ":" in l)), "")
        key = (message, where[:200])
        found[key] += 1
        first_line.setdefault(key, i)
    return [{"message": m, "where": w, "count": c} for (m, w), c in found.most_common(40)]


# ----- Digests: a run's log in a few hundred lines -----

def digest_run(run_id, rows):
    if not rows:
        return f"Run {run_id}: empty"
    minutes = max(r["played_min"] for r in rows)
    sessions = len({r.get("session") for r in rows})
    counts = Counter(r["event"] for r in rows)
    out = [f"Run {run_id}: {minutes:.0f} min played over {sessions} session(s). Events: " +
           ", ".join(f"{k} {v}" for k, v in counts.most_common(18))]

    # Deaths with what hurt them just before.
    hurt = [r for r in rows if r["event"] == "Hurt"]
    for death in (r for r in rows if r["event"] == "PlayerDied"):
        before = [h for h in hurt if death["played_min"] - 1.0 <= h["played_min"] <= death["played_min"]]
        by = Counter(h.get("subject", "") for h in before)
        out.append(f"  death at {death['played_min']:.1f} min in {death.get('scene')}: {death.get('subject','')} {death.get('detail','')}"
                   + (f"; hurt just before by {dict(by)}" if by else ""))

    # The story of the run, row by row.
    out.append("  Timeline:")
    shown = 0
    for r in rows:
        if r["event"] in STORY_EVENTS and shown < 220:
            out.append(f"   {r['played_min']:6.1f} {r.get('scene','')[:18]:18} {r['event']:15} {r.get('subject','')[:40]} {r.get('amount','')} {r.get('detail','')[:80]}".rstrip())
            shown += 1

    # Long stretches without anything that counts as progress.
    progress = sorted(r["played_min"] for r in rows if r["event"] in ("QuestCompleted", "QuestStarted", "Built", "RaidFinished", "SkillLevel", "Crafted"))
    gaps = [(a, b) for a, b in zip(progress, progress[1:]) if b - a >= 12]
    if gaps:
        out.append("  Long stretches without progress: " + ", ".join(f"{a:.0f}-{b:.0f} min" for a, b in gaps))

    # Where they went in raids: how much of each map, in 20 m cells.
    cells = defaultdict(set)
    for r in rows:
        if r["event"] == "Position":
            try:
                x, z = (float(v) for v in r.get("subject", "").split(";"))
            except ValueError:
                continue
            cells[r.get("detail") or r.get("scene")].add((int(x // 20), int(z // 20)))
    for raid, visited in cells.items():
        out.append(f"  {raid}: walked through {len(visited)} 20 m cells")

    # Fighting style and what was made, as totals.
    combat = Counter()
    for r in rows:
        if r["event"] == "Combat":
            combat[r.get("subject", "")] += int(r.get("amount") or 0)
    if combat:
        out.append("  Combat totals: " + ", ".join(f"{k} {v}" for k, v in combat.most_common(10)))
    crafted = Counter(r.get("subject", "") for r in rows if r["event"] == "Crafted")
    if crafted:
        out.append("  Crafted: " + ", ".join(f"{k} x{v}" for k, v in crafted.most_common(15)))
    return "\n".join(out)


def rows_around(rows, minute, before=4.0, after=1.0):
    near = [r for r in rows if minute - before <= r["played_min"] <= minute + after and r["event"] != "Position"]
    return "\n".join(f"{r['played_min']:6.2f} {r.get('scene','')} {r['event']} {r.get('subject','')} {r.get('amount','')} {r.get('detail','')}".rstrip()
                     for r in near[-120:])


def screenshot_block(data):
    """The screenshot as an image block, made smaller first (1280 wide is plenty to see a bug)."""
    if not data:
        return None
    try:
        from PIL import Image
        img = Image.open(io.BytesIO(data)).convert("RGB")
        img.thumbnail((1280, 1280))
        buf = io.BytesIO()
        img.save(buf, "JPEG", quality=82)
        data = buf.getvalue()
        media = "image/jpeg"
    except Exception:
        media = "image/png"
    return {"type": "image", "source": {"type": "base64", "media_type": media, "data": base64.b64encode(data).decode()}}


# ----- The two passes -----

REPORT_SCHEMA = {
    "type": "object",
    "properties": {
        "kind": {"type": "string", "enum": ["bug", "idea", "confusing", "liked", "balance", "performance"]},
        "severity": {"type": "integer", "description": "1 cosmetic, 2 annoying, 3 blocks something, 4 crash, lost progress or softlock"},
        "title": {"type": "string", "description": "a short name for the issue, as a developer would file it"},
        "summary": {"type": "string"},
        "area": {"type": "string", "description": "the game system or place it is about, e.g. 'Haven building', 'Troll Valley raid', 'quest: The Toll', 'UI: bag'"},
        "repro_steps": {"type": "array", "items": {"type": "string"}},
        "evidence": {"type": "array", "items": {"type": "string"}, "description": "what in the log rows, errors or screenshot supports it"},
        "likely_cause": {"type": "string"},
    },
    "required": ["kind", "severity", "title", "summary", "area", "repro_steps", "evidence", "likely_cause"],
    "additionalProperties": False,
}


def report_request(player, report):
    run = player["runs"].get(report.get("run_id"), [])
    minute = float(report.get("played_min") or 0)
    text = [GAME_CONTEXT, "",
            f"A playtester ({player['name']}) wrote this report in the game. Read it with the log rows and errors around it, and the screenshot if any.",
            f"Their words ({report.get('kind','?')}): {report.get('text','').strip()}",
            f"Where: scene {report.get('scene')}, position {report.get('position')}, quest {report.get('quest')}, raid {report.get('raid')}, "
            f"{minute:.1f} min into run {report.get('run_id')}, build {player['meta'].get('build', '?')}, fps {report.get('fps')}.",
            "", "Log rows from the 4 minutes before it:", rows_around(run, minute) or "(none)",
            "", "Unity errors in this player's log:", json.dumps(player["errors"][:15], indent=0) if player["errors"] else "(none)",
            "", "Read it as a careful QA lead would. If the report is vague, infer from the evidence but say what is inferred."]
    content = [{"type": "text", "text": "\n".join(text)}]
    shot = screenshot_block(report.get("_screenshot"))
    if shot:
        content.insert(0, shot)
    return dict(model=REPORT_MODEL, max_tokens=4000, betas=[FALLBACK_BETA], fallbacks="default",
                output_config={"effort": "medium", "format": {"type": "json_schema", "schema": REPORT_SCHEMA}},
                messages=[{"role": "user", "content": content}])


def summary_request(players, findings):
    parts = [GAME_CONTEXT, "",
             f"{len(players)} playtesters played this build. Below: each report as already read one by one, then a digest of every "
             "player's run logs, and the Unity errors they hit.", "", "== Reports, read =="]
    for f in findings:
        parts.append(json.dumps({k: v for k, v in f.items() if not k.startswith("_")}, ensure_ascii=False))
    for p in players:
        parts.append(f"\n== Player: {p['name']} ({len(p['runs'])} runs, {len(p['reports'])} reports) ==")
        for run_id, rows in p["runs"].items():
            parts.append(digest_run(run_id, rows))
        if p["errors"]:
            parts.append("Errors: " + json.dumps(p["errors"][:25], ensure_ascii=False))
    parts += ["", """Write the playtest report for the developer, in Markdown:
1. **Fix first**: bugs ranked by severity times how many players hit them. Merge duplicates across players and say who hit each.
   For each: what happens, where, repro steps, the evidence, the likely cause.
2. **What the logs show that nobody reported**: errors, where players died repeatedly, long stretches without progress, places or
   systems nobody used, raids abandoned. Say how sure you are.
3. **Ideas and suggestions**, grouped by theme, with how many players asked.
4. **Confusing moments**: where players didn't know what to do, from reports and from the logs.
5. **What players liked.**
6. **Per player**: a few lines each: how far they got, play time, what went wrong for them.
7. **Next steps**: the 5-10 changes that would help the next playtest most, in order.
Be concrete and short. Don't invent what the data doesn't show."""]
    return dict(model=SUMMARY_MODEL, max_tokens=32000, betas=[FALLBACK_BETA], fallbacks="default",
                output_config={"effort": "high"}, messages=[{"role": "user", "content": "\n".join(parts)}])


def estimate(requests):
    """Rough dollars: 4 characters a token, images about 1,600 tokens, output at about a third of max_tokens."""
    total = 0.0
    for r in requests:
        chars = 0
        images = 0
        for block in r["messages"][0]["content"] if isinstance(r["messages"][0]["content"], list) else [{"type": "text", "text": r["messages"][0]["content"]}]:
            if block["type"] == "text":
                chars += len(block["text"])
            else:
                images += 1
        tokens_in = chars / 4 + images * 1600
        price_in, price_out = PRICES[r["model"]]
        total += tokens_in / 1e6 * price_in + (r["max_tokens"] / 3) / 1e6 * price_out
    return total


def send(client, request):
    response = client.beta.messages.create(**request)
    if response.stop_reason == "refusal":
        raise RuntimeError(f"declined: {getattr(response, 'stop_details', None)}")
    return "".join(b.text for b in response.content if b.type == "text"), response.usage


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--inbox", default=os.path.join(HERE, "Inbox"))
    ap.add_argument("--local", action="store_true", help="also read this PC's own logs and reports, as one more player")
    ap.add_argument("--dry-run", action="store_true", help="build the prompts and the estimate; send nothing")
    ap.add_argument("--yes", action="store_true", help="don't ask before sending")
    ap.add_argument("--min-minutes", type=float, default=5, help="leave out runs shorter than this (default 5)")
    ap.add_argument("--local-runs", type=int, default=4, help="how many of this PC's newest real runs --local reads (default 4)")
    args = ap.parse_args()

    players = load_bundles(args.inbox, args.local, args.min_minutes, args.local_runs)
    if not players:
        print(f"Nothing to read: no zips in {args.inbox}" + ("" if args.local else " (add --local to read this PC's logs)"))
        return 1

    stamp = datetime.datetime.now().strftime("%Y-%m-%d_%H%M")
    out = os.path.join(HERE, "Results", stamp)
    os.makedirs(out, exist_ok=True)
    reports = [(p, r) for p in players for r in p["reports"]]
    requests = [report_request(p, r) for p, r in reports]
    # The summary is estimated with placeholder findings the size a report's read usually is.
    draft = summary_request(players, [{"title": "x" * 600} for _ in reports])
    cost = estimate(requests + [draft])
    print(f"{len(players)} player(s), {sum(len(p['runs']) for p in players)} run log(s), {len(reports)} report(s).")
    print(f"Estimated cost: about ${cost:.2f} ({len(requests)} x {REPORT_MODEL}, 1 x {SUMMARY_MODEL}).")

    if args.dry_run:
        with open(os.path.join(out, "prompts.txt"), "w", encoding="utf-8") as f:
            for r in requests + [draft]:
                f.write(f"===== {r['model']} =====\n")
                for block in r["messages"][0]["content"] if isinstance(r["messages"][0]["content"], list) else [{"type": "text", "text": r["messages"][0]["content"]}]:
                    f.write(block["text"] + "\n" if block["type"] == "text" else "[screenshot]\n")
        print(f"Dry run: prompts written to {out}\\prompts.txt; nothing sent.")
        return 0

    if not args.yes and input("Send? [y/N] ").strip().lower() != "y":
        print("Nothing sent.")
        return 0

    import anthropic
    client = anthropic.Anthropic()
    findings = []
    spent = 0.0
    for i, ((player, report), request) in enumerate(zip(reports, requests), 1):
        try:
            text, usage = send(client, request)
            finding = json.loads(text)
        except Exception as e:  # one bad report doesn't stop the rest
            print(f"  report {i}: failed ({e})")
            finding = {"kind": report.get("kind", "?"), "title": "(not read)", "summary": report.get("text", ""), "error": str(e)}
            usage = None
        finding.update({"player": player["name"], "report_text": report.get("text", ""), "report_id": report.get("id")})
        findings.append(finding)
        if usage:
            p_in, p_out = PRICES[REPORT_MODEL]
            spent += usage.input_tokens / 1e6 * p_in + usage.output_tokens / 1e6 * p_out
        print(f"  report {i}/{len(reports)}: {finding.get('title')}")

    with open(os.path.join(out, "findings.json"), "w", encoding="utf-8") as f:
        json.dump(findings, f, indent=2, ensure_ascii=False)

    text, usage = send(client, summary_request(players, findings))
    p_in, p_out = PRICES[SUMMARY_MODEL]
    spent += usage.input_tokens / 1e6 * p_in + usage.output_tokens / 1e6 * p_out
    with open(os.path.join(out, "report.md"), "w", encoding="utf-8") as f:
        f.write(f"# Playtest report, {stamp}\n\n{len(players)} players, {len(reports)} reports. Spent about ${spent:.2f}.\n\n{text}\n")
    print(f"Done: {out}\\report.md (spent about ${spent:.2f}).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
