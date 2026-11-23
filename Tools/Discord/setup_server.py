"""
Sets up the Safe Haven Playtest Discord server (Docs/PlaytestDiscord.md) through Discord's API, with a bot the user made and invited:
the roles, the private categories, the channels and their permissions, the feedback forum's tags, the webhooks the build and log
tools post through, and the first posts in #welcome and #how-to-playtest.

Safe to run again: anything already there (by name) is left as it is, and only what is missing is made.

Reads AgentScripts/discord.local.json (kept out of git):
    { "token": "<the bot's token>", "guild": "<the server's id>" }
and writes the webhooks it makes into the same file ("builds", "logs").

    python Tools/Discord/setup_server.py --dry-run    what it would make, without a token or the network
    python Tools/Discord/setup_server.py              make it
"""
import json
import os
import sys
import time
import urllib.error
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CONFIG = os.path.join(ROOT, "AgentScripts", "discord.local.json")
API = "https://discord.com/api/v10"

# Permission bits (https://discord.com/developers/docs/topics/permissions).
VIEW = 1 << 10
SEND = 1 << 11
HISTORY = 1 << 16
ATTACH = 1 << 15
REACT = 1 << 6
THREADS_PUBLIC = 1 << 35
SEND_IN_THREADS = 1 << 38
CONNECT = 1 << 20
SPEAK = 1 << 21
WRITE = SEND | SEND_IN_THREADS | THREADS_PUBLIC

TEXT, VOICE, CATEGORY, FORUM = 0, 2, 4, 15

ROLES = [
    {"name": "Developer", "color": 0xC9A15B, "hoist": True},
    {"name": "Playtester", "color": 0x6FA36B, "hoist": True},
]

# Each category: is it private (only Developer and Playtester see it), and its channels. A read-only channel: only the Developer
# (and the webhooks) post there.
LAYOUT = [
    ("START HERE", False, [
        {"name": "welcome", "type": TEXT, "read_only": True,
         "topic": "What Safe Haven is, the rules, and how to become a Playtester."},
        {"name": "how-to-playtest", "type": TEXT, "read_only": True,
         "topic": "Installing and updating the build, reporting with F8, sending your logs."},
        {"name": "announcements", "type": TEXT, "read_only": True, "topic": "New builds and what to test."},
    ]),
    ("BUILDS", True, [
        {"name": "builds", "type": TEXT, "read_only": True, "webhook": "builds",
         "topic": "One post per build: the download, what changed, what to look at."},
        {"name": "known-issues", "type": TEXT, "read_only": True, "topic": "Known problems: no need to report these."},
    ]),
    ("FEEDBACK", True, [
        {"name": "feedback", "type": FORUM,
         "topic": "One post per topic. What happened? What did you expect? Where (raid or Haven, roughly when)? Did you press F8 at the moment?",
         "tags": ["Bug", "Idea", "Confusing", "Liked", "Balance", "Performance", "Co-op"]},
        {"name": "playtest-logs", "type": TEXT, "webhook": "logs",
         "topic": "Drop the zip from Escape menu > Send my reports here (reports, screenshots, run logs)."},
        {"name": "screenshots", "type": TEXT, "topic": "Moments worth sharing."},
    ]),
    ("COMMUNITY", True, [
        {"name": "general", "type": TEXT, "topic": "Talk."},
        {"name": "off-topic", "type": TEXT, "topic": "Anything else."},
        {"name": "Playtest session", "type": VOICE},
    ]),
]

WELCOME = """**Welcome to the Safe Haven playtest.**

Safe Haven is a top-down survival and extraction game in a Nordic fairy-tale forest: build a home around an old portal, go out on timed raids, bring people home, and keep the hearth burning.

**Rules**
- The builds stay here: please don't share them or the download link.
- Be kind, and be honest: "this was boring" is as useful as "this was great".
- Spoilers are fine in #feedback; the game is unfinished and things will break.

New here? Ask the Developer for the **Playtester** role to see the builds and feedback channels. Then read #how-to-playtest."""

HOW_TO = """**How to playtest**

1. **Get the build** from #builds (the newest post). Unzip or install it anywhere and run `Safe Haven.exe`.
2. **Play as you like.** There's no right way: we want to see what you do.
3. **Press F8** the moment something is wrong, confusing, or great. It takes a screenshot and asks for a few words. That's the best feedback there is.
4. **Send your logs** when you stop: Escape menu > **Send my reports**. It makes a zip and opens its folder: drag that zip into #playtest-logs.
5. **Talk about it** in #feedback: one post per topic, with a tag (Bug, Idea, Confusing, Liked, Balance, Performance, Co-op).

The F1 menu has playtest cheats: runs that use them are marked, so use them freely if you're stuck."""


def load_config():
    if not os.path.exists(CONFIG):
        return {}
    with open(CONFIG, encoding="utf-8") as f:
        return json.load(f)


def save_config(config):
    with open(CONFIG, "w", encoding="utf-8") as f:
        json.dump(config, f, indent=2)


class Discord:
    def __init__(self, token):
        self.token = token

    def call(self, method, path, body=None):
        data = json.dumps(body).encode("utf-8") if body is not None else None
        for attempt in range(6):
            request = urllib.request.Request(API + path, data=data, method=method, headers={
                "Authorization": "Bot " + self.token,
                "Content-Type": "application/json",
                "User-Agent": "DiscordBot (https://discord.com, 1.0)",
            })
            try:
                with urllib.request.urlopen(request) as response:
                    text = response.read().decode("utf-8")
                    return json.loads(text) if text else None
            except urllib.error.HTTPError as error:
                if error.code == 429:
                    wait = json.loads(error.read().decode("utf-8")).get("retry_after", 1.0)
                    time.sleep(float(wait) + 0.2)
                    continue
                raise SystemExit(f"{method} {path} failed: {error.code} {error.read().decode('utf-8', 'ignore')}")
        raise SystemExit(f"{method} {path}: rate limited too long")


def overwrites(guild, roles, private, read_only):
    """Who may see and write: private hides it from everyone but the Developer and Playtesters; read-only leaves writing to the Developer."""
    everyone_deny = (VIEW if private else 0) | (WRITE if read_only else 0)
    tester_allow = (VIEW | HISTORY if private else 0)
    tester_deny = WRITE if read_only else 0
    rules = []
    if everyone_deny:
        rules.append({"id": guild, "type": 0, "allow": "0", "deny": str(everyone_deny)})
    if tester_allow or tester_deny:
        rules.append({"id": roles["Playtester"], "type": 0, "allow": str(tester_allow), "deny": str(tester_deny)})
    if private or read_only:
        rules.append({"id": roles["Developer"], "type": 0, "allow": str(VIEW | HISTORY | WRITE | ATTACH | REACT), "deny": "0"})
    return rules


def main():
    dry = "--dry-run" in sys.argv
    config = load_config()
    if dry:
        for role in ROLES:
            print(f"role {role['name']}")
        for category, private, channels in LAYOUT:
            print(f"category {category}{' (private)' if private else ''}")
            for channel in channels:
                kind = {TEXT: "text", VOICE: "voice", FORUM: "forum"}[channel["type"]]
                extras = [x for x in ("read-only" if channel.get("read_only") else "", f"webhook:{channel['webhook']}" if channel.get("webhook") else "",
                                      f"tags:{len(channel['tags'])}" if channel.get("tags") else "") if x]
                print(f"   #{channel['name']} ({kind}){' ' + ', '.join(extras) if extras else ''}")
        print("posts: #welcome, #how-to-playtest")
        return

    if not config.get("token") or not config.get("guild"):
        raise SystemExit(f"Put the bot's token and the server's id in {CONFIG}: {{\"token\": \"...\", \"guild\": \"...\"}}")

    api = Discord(config["token"])
    guild = str(config["guild"])
    had_roles = {role["name"]: role["id"] for role in api.call("GET", f"/guilds/{guild}/roles")}
    roles = {}
    for role in ROLES:
        if role["name"] in had_roles:
            roles[role["name"]] = had_roles[role["name"]]
            print(f"role {role['name']}: there already")
        else:
            made = api.call("POST", f"/guilds/{guild}/roles", {"name": role["name"], "color": role["color"], "hoist": role["hoist"], "mentionable": True})
            roles[role["name"]] = made["id"]
            print(f"role {role['name']}: made")

    had = api.call("GET", f"/guilds/{guild}/channels")
    by_name = {(c["name"].lower(), c.get("parent_id")): c for c in had}
    categories = {c["name"].lower(): c for c in had if c["type"] == CATEGORY}
    new_posts = {}
    for category, private, channels in LAYOUT:
        parent = categories.get(category.lower())
        if parent is None:
            parent = api.call("POST", f"/guilds/{guild}/channels", {"name": category, "type": CATEGORY,
                                                                   "permission_overwrites": overwrites(guild, roles, private, False)})
            print(f"category {category}: made")
        else:
            print(f"category {category}: there already")

        for channel in channels:
            key = (channel["name"].lower(), parent["id"])
            made = by_name.get(key)
            if made is None:
                body = {"name": channel["name"], "type": channel["type"], "parent_id": parent["id"],
                        "permission_overwrites": overwrites(guild, roles, private, channel.get("read_only", False))}
                if channel.get("topic") and channel["type"] != VOICE:
                    body["topic"] = channel["topic"]
                if channel.get("tags"):
                    body["available_tags"] = [{"name": tag} for tag in channel["tags"]]
                made = api.call("POST", f"/guilds/{guild}/channels", body)
                new_posts[channel["name"]] = made["id"]
                print(f"   #{channel['name']}: made")
            else:
                print(f"   #{channel['name']}: there already")

            hook = channel.get("webhook")
            if hook and not config.get(hook):
                made_hook = api.call("POST", f"/channels/{made['id']}/webhooks", {"name": f"Safe Haven {hook.title()}"})
                config[hook] = f"https://discord.com/api/webhooks/{made_hook['id']}/{made_hook['token']}"
                save_config(config)
                print(f"      webhook '{hook}': made and kept in {os.path.relpath(CONFIG, ROOT)}")

    # The first posts, only in channels made just now (a second run doesn't post them again).
    for name, text in (("welcome", WELCOME), ("how-to-playtest", HOW_TO)):
        if name in new_posts:
            api.call("POST", f"/channels/{new_posts[name]}/messages", {"content": text})
            print(f"posted in #{name}")

    print("Done. Give testers the Playtester role (right-click them > Roles).")


if __name__ == "__main__":
    main()
