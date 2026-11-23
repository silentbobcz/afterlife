# Redscript error analysis: proposed texts

A proposal, not in the app yet. Preview: [`redscript-error.html`](redscript-error.html). The texts are matter-of-fact:
a failed script compilation is common after a game update and easy to fix. Every state that offers to switch mods off
says that AFTERLIFE only switches them off and deletes nothing.

Placeholders: `{n}` a count, `{Mod}` a mod name, `{ver}` its version, `{Profile}` the selected profile, `{time}` a local
date and time ("23 Nov 2026, 21:14"), `{path}` a path relative to the game folder (`r6\scripts\SomeMod\Main.reds`),
`{line}:{col}` the position from the log, `{message}` the compiler message as written in the log.

## (a) Banner above the mod list

| Text | Proposed |
|---|---|
| Title | ! SCRIPT MODS DID NOT LOAD |
| Text, after returning from a launch | "The game could not compile redscript, so none of your script mods ran in the last session. The log names {n} mods. AFTERLIFE can switch them off for you; nothing is deleted." |
| Text, at app start (game started outside AFTERLIFE) | "The game could not compile redscript the last time it ran, so none of your script mods ran. The log names {n} mods. AFTERLIFE can switch them off for you; nothing is deleted." |
| Text, the log names no mod (only error lines) | "The game could not compile redscript, so none of your script mods ran in the last session. Open the analysis to see the files the log names." |
| One mod | "The log names 1 mod." |
| Button | ANALYZE |
| Link | Dismiss |
| Tooltip of Dismiss | "Hide until the game writes a new script log" |

## (b) Analysis window, culprits from the library

| Text | Proposed |
|---|---|
| Window title | REDSCRIPT ERROR |
| First line | "The game could not compile redscript, so no script mod is running. These mods from your library are named in the log." |
| Line under it | "Log written {time}, after the last launch · profile {Profile}" (log newer than the last launch, or the game was not launched from AFTERLIFE: "Log written {time} · profile {Profile}") |
| Section title | FROM YOUR LIBRARY |
| Section sentence | "Usually an older mod that does not match the game or a framework it needs. Switching it off lets the other script mods load again." |
| Card | `{Mod}` `{ver}`, then one line per error: `{path}:{line}:{col}  {message}` (more than 3 errors: the first 3 and "+ {n} more in the log") |
| Card checkbox | Switch off |
| Card note, mod already off in the profile (checkbox greyed out) | "Already off in this profile. Apply the changes to take it out of the game." |
| Card note, mod is on in the game but not in this profile (another profile was applied) | "Not in this profile, but still in the game from the last apply. Apply the changes to take it out." |
| Closing sentence | "AFTERLIFE only switches the mods off in this profile. Nothing is deleted; you can switch them on again at any time. Apply the changes, then launch the game again." |
| Buttons | Close · SWITCH OFF {n} MODS (one: SWITCH OFF 1 MOD; nothing checked: greyed out, tooltip "Check the mods to switch off") |
| After the button | The normal switch-off; the "SWITCH OFF …?" dialog follows when other mods need the checked ones (texts in [`switch-off-dialog-texts.md`](switch-off-dialog-texts.md)). Then the status line: "{n} changes wait: {n} mods go off". |
| Profile "Mods off" selected | Section sentence replaced by: "The selected profile is Mods off. Choose the profile you play with to switch mods off." Checkboxes and the button greyed out. |

## (c) Culprit outside the library

| Text | Proposed |
|---|---|
| First line, only outside culprits | "The game could not compile redscript, so no script mod is running. The log names a script that was not installed through AFTERLIFE." (several: "… names {n} scripts that were not installed through AFTERLIFE.") |
| Section title | NOT IN YOUR LIBRARY |
| Section sentence | "AFTERLIFE did not install these files, so it does not switch them off. Update the mod they came from, or move its folder out of r6\scripts while the game is closed." |
| Card | Folder name under `r6\scripts` (a file directly in `r6\scripts`: the file name), then `<game>\{path}` |
| Card link | Open folder |
| Extra hint under the cards | "“Scan manual mods” in the left panel can add these scripts to your library, so AFTERLIFE can switch them off next time." |
| Buttons, only outside culprits | CLOSE (there is nothing to switch off) |

## (d) Ask an AI

| Text | Proposed |
|---|---|
| Section title | ASK AN AI |
| Sentence | "Not sure what to do? Copy this text into an AI chat or a forum post. Nothing is sent anywhere; your user name is removed from the paths." |
| Button | COPY |
| Notice after copying | Copied to clipboard |

The text itself (built by the analysis, passed through `LogPrivacy.Scrub`):

```
Cyberpunk 2077 {game version}, redscript compilation failed.
Mod manager: AFTERLIFE. Mods named in redscript_rCURRENT.log:

- {Mod} {ver} (Nexus {id})
  {path}:{line}:{col} {message}
- {folder} (installed by hand)
  {path}

Frameworks on: {name} {ver}, {name} {ver}, ...

Which of these mods is out of date for this game version, and what should I
update or switch off?
```

## Other results (notice line above the list)

Shown only after the manual ANALYZE REDSCRIPT ERROR; the automatic check stays silent in these cases.

| Case | Tone | Proposed |
|---|---|---|
| Compilation OK | green | "Redscript compiled without errors in the last game session. Nothing to fix." |
| Log missing | orange | "No script log found in r6\logs. Start the game once with redscript installed, then analyze again." |
| Log older than the last launch | orange | "The script log is older than the last launch (log {time} · launch {time}), so it does not describe the last session. Start the game, return here and analyze again." |
| Log could not be read (locked, empty, cut off) | orange | "The script log could not be read completely. Close the game if it is still running, then analyze again." |
| Failed, but no file or mod could be identified | orange | Opens the window with only ASK AN AI; first line: "The game could not compile redscript, so no script mod is running. The log does not say which file caused it; the text below has what the log says." |

## Manual button

| Text | Proposed |
|---|---|
| Menu line (LIST & ORDER menu, first line) | ANALYZE REDSCRIPT ERROR |
| Its sentence | "Script mods not working? Read the game's script log and find the mod behind it" |
| Alternative: link under JACK IN | "Script mods not working? Analyze" |
| Greyed out, no game folder set | Tooltip: "Set the game folder in Settings first" |

## Words used and avoided

- "switch off", never "disable", "remove" or "uninstall" (the app says switch on / switch off everywhere).
- "script mods" in sentences, "redscript" only where the user may search for it (log name, window title, Ask an AI).
- No "crash", "broken", "corrupt" or "fatal": the game starts, only the scripts do not load.
