# Switch-off dialog: proposed texts

A proposal, not in the app yet. The dialog appears when a mod (or a selection, a category, all mods) is switched off and other mods are linked to it. The behaviour stays; only the words change, and every part gets one full sentence under its title. `{S}` is the mod name or "N mods".

| Text | Today | Proposed |
|---|---|---|
| Title | ALSO SWITCH OFF WHAT NEEDS IT? / SWITCH OFF | SWITCH OFF {S}? |
| First line | "{n} switched-on mod(s) need {S}. Without it they may not work or show errors in game." (and two other variants) | "{S} will be switched off in this profile." |
| Part 1 title | NEED IT, NOW ON | WILL STOP WORKING |
| Part 1 sentence | none | "These {n} mods need {S}. Without it, they may not work or may show errors in game." (one mod: "This mod needs {S}. Without it, it may not work or may show errors in game.") |
| Part 1 checkbox, ticked | Also switch off the {n} mod(s) that need it | Switch off these {n} mods too (one mod: Switch off this mod too) |
| Part 2 title | ONLY IT NEEDED, NOW ON | NOT NEEDED ANY MORE |
| Part 2 sentence | none | "{S} was the only switched-on mod that needed these {n} mods. Nothing else needs them now. Core mods always stay on." |
| Part 2 checkbox, unticked | Also switch off {n} mod(s) only it needed | Also switch off these {n} mods (one mod: Also switch off this mod) |
| Part 2 row | {Name} · needed by {X} | {Name} · only needed by {X} |
| Part 3 title | IT NEEDS, STAYS ON | STAYS ON |
| Part 3 sentence | none | "{S} needs these {n} mods, but other switched-on mods use them too, so they stay on." |
| Part 3 row | {Name} · also needed by {X} / · stays on | {Name} · also needed by {X} / · another mod still needs it |
| Buttons | SWITCH OFF, Cancel | unchanged |
| Collection, second dialog: title | ALSO SWITCH OFF WHAT IT NEEDED? | SWITCH OFF UNUSED MODS? |
| Collection, second dialog: text | "{Name} used {n} mod(s) outside the collection that nothing else switched on needs. Core mods stay on." | "{Name} will be switched off. These {n} mods outside the collection were needed only by it. Nothing else needs them now. Core mods always stay on." |
| Collection, second dialog: list title | NEEDED BY THE COLLECTION | NOT NEEDED ANY MORE |

The order of the three parts stays: what stops working, what you may also switch off, what is only for your information.

Where the texts live: `src/Afterlife.App/ViewModels/MainViewModel.Library.cs` (the method that builds the dialog), `MainViewModel.Collections.cs` (the second dialog of a collection), the template in `src/Afterlife.App/Views/Dialogs.xaml`. The sentence under a part title needs a small new slot in the dialog (`ConfirmVm` and the template).
