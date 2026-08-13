<h1 align="center">Myriad's Pre-Rolls</h1>
<h3 align="center">A rules-based pre-roll intro plugin for Jellyfin</h3>

## About

This plugin plays a short "pre-roll" intro video before your movies and TV episodes — the same idea as a
studio logo or trailer reel before a theatrical film. You point it at a folder of intro clips, and it
picks one to play based on rules you set per video: what kind of content it applies to, which tags or
ratings it's restricted to, which users should never see it, and how often it should be picked relative
to your other intros.

## Credit

This plugin is a fork of the community-maintained **[jellyfin/jellyfin-plugin-intros](https://github.com/jellyfin/jellyfin-plugin-intros)**,
which itself descends from the original **[dkanada/jellyfin-plugin-intros](https://github.com/dkanada/jellyfin-plugin-intros)**
(now archived). All credit for the original plugin architecture — the local video detection, the
Jellyfin `IIntroProvider` integration, and the base plugin scaffolding — goes to those projects and their
contributors.

This fork rebuilds the rules engine and admin UI on top of that foundation: a new per-video rules model
(replacing the original's Genre/Studio/Date-range rule types), a redesigned collapsible rules interface,
an in-page video preview, and per-user controls.

## Installation

1. In Jellyfin, go to **Dashboard → Plugins → Repositories → Add**.
2. Enter this repository's manifest URL:
   ```
   https://raw.githubusercontent.com/MyriadRising/Myriad-intros/master/manifest.json
   ```
3. Save, then go to **Dashboard → Plugins → Catalog**.
4. Find **Myriad's Pre-Rolls** in the list and click **Install**.
5. Restart Jellyfin.

## Getting Started

After installing and restarting, go to **Dashboard → Plugins → My Plugins → Myriad's Pre-Rolls** to open
its settings page.

1. **Local Source** — enter the full path to a folder (or a single file) containing your intro videos.
   This is a path on the Jellyfin *server* itself, not your local computer — e.g. `/media/intros` on a
   Linux server, or wherever your intro clips live relative to the server's filesystem.
2. Click **Load Videos**. The plugin scans that folder and adds every video it finds as its own rule
   card below. Re-running this later picks up any new files you've added, without disturbing rules
   you've already set for existing videos.
3. Each intro now has its own **rule card** — this is where you control when it plays.

## Understanding a Rule Card

Every video you loaded gets one card. It starts **collapsed**, showing just the video's name and a
Frequency box:

```
▸ Halloween Intro                                    [ None ]  Freq [50]  [Enable]
```

Click anywhere on the name to expand it and see the full set of controls. The colored bar on the left
edge of the card (and the small pill badge on the header) tells you at a glance what the card is
currently set to:

| Color | Meaning |
|---|---|
| 🔵 Blue | Movies Only |
| 🟣 Purple | Shows Only |
| 🟢 Green | Both |
| 🔴 Red | None (this intro is off) |

### Play For

Choose whether this intro is eligible for Movies, Shows, Both, or None. **None effectively disables
this specific intro** — it will never be picked, no matter what else matches. The **Enable / Disable**
button in the header is a shortcut for flipping between *None* and *Both* without opening the card.

### Tags

Restrict this intro to only play before items that have at least one of the selected tags (tags come
from your Jellyfin library — whatever you've tagged your movies/shows with). Leave it on **All Tags**
to apply no tag restriction at all.

*Example:* if you've tagged a handful of movies with `Christmas`, you can restrict a snowy pre-roll to
the `Christmas` tag so it only ever shows up before those.

### Ratings

Same idea, but for official content ratings (e.g. `PG`, `TV-14`, `R`). Leave it on **All Ratings** for
no restriction.

*Example:* restrict a more intense-sounding intro to `R`/`TV-MA` content only, so it never plays in
front of something rated for kids.

### Exclude Users

**This is an exclude list, not an include list.** Leaving it on **No Exclusions** means everyone can see
this intro. If you check specific users here, *those users specifically will never see this intro* — it
still plays normally for everyone else.

*Example:* you have a jump-scare intro you love, but your kid's profile shouldn't see it. Check your
kid's name here. Everyone else's profile is unaffected.

*(This is different from the global per-user toggle described below, which turns off intros entirely
for a user — Exclude Users only blocks that one specific intro for the users you pick.)*

### Frequency

A number from 1–100 representing this intro's **relative weight** against the *other intros currently
eligible* for whatever's about to play. It is not a fixed percentage — it only matters compared to the
other eligible intros' Frequency values.

- If every eligible intro has the same Frequency (the default is 50 for all), they're all equally likely.
- An intro at Frequency 100 is roughly **twice** as likely to be picked as one at Frequency 50, but only
  among the intros that are actually eligible for that particular movie/show/user — Frequency values on
  intros that don't qualify (wrong tag, wrong rating, excluded user, etc.) don't factor in at all.

*Example:* you have 3 general-purpose intros at Frequency 50 and one favorite you want to show up about
twice as often as the others — set that one to 100. It'll now be picked roughly 2x as often as any one
of the other three, without needing to touch their values.

### Preview

Click **Preview** to play the actual video clip in an overlay, right on the settings page — useful for
double-checking you loaded the right file before fine-tuning its rules. Close it with the **×**, by
clicking outside the video, or pressing **Escape**.

### Sort By

Changes the *order the cards are displayed in* on this page (by Name, Frequency, Tag, or Rating) — this
is purely for your own browsing convenience and has **no effect on which intro actually gets picked**
during playback.

## Disable Intros Per-User (Global)

At the bottom of the page is a separate list of every Jellyfin user, with a checkbox next to each.
Checking a user here **disables all intros for that user entirely** — a hard override that ignores every
rule above. Use this for someone who just doesn't want pre-rolls at all, rather than excluding them from
one specific intro (that's what Exclude Users on an individual card is for).

## Putting It Together — A Worked Example

Say you've loaded 4 intros: `Generic A`, `Generic B`, `Halloween`, `Kids Safe`.

- `Generic A` and `Generic B`: Play For **Both**, no tag/rating restriction, Frequency 50 each.
- `Halloween`: Play For **Both**, Tags restricted to `Halloween`, Frequency 50.
- `Kids Safe`: Play For **Shows**, Ratings restricted to `TV-Y`/`TV-G`, Frequency 50.

Result: for a `Halloween`-tagged movie, only `Generic A`, `Generic B`, and `Halloween` are eligible (all
Movies-compatible) — each has an equal 1-in-3 chance, since `Kids Safe` doesn't qualify (it's
Shows-only and rating-restricted). For a `TV-G` kids' show, only `Generic A`, `Generic B`, and
`Kids Safe` are eligible — `Halloween` is excluded because that show isn't tagged `Halloween`.

## Development

To build this plugin yourself, you'll need the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```
cd Jellyfin.Plugin.LocalIntros
dotnet publish --configuration Release --output bin
```

For packaging a release (zip + manifest), see [JPRM](https://github.com/oddstr13/jellyfin-plugin-repository-manager).

## License

This plugin's code is distributed under the MIT License. See [LICENSE](./LICENSE.md) for more
information.
