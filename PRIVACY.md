# Privacy

AFTERLIFE runs on your PC. It has **no telemetry, no analytics, no ads, no crash reporter and no self-updater**. The author receives nothing from it.

## What goes over the internet

AFTERLIFE connects only to Nexus Mods, and only for features you use:

| When | Where | What is sent |
|---|---|---|
| You save a Nexus key, at start, and when you use Nexus features | `api.nexusmods.com` (REST v1 and GraphQL v2) | your API key (as the request header Nexus requires) and the IDs of the mods, files and collections involved: key check, mod details and requirements, file lists, update checks, collection details |
| You download a mod or a collection | the download servers Nexus Mods points to | a normal file download |
| Mod details are shown | the picture address Nexus gives for that mod | a normal image download (cached in the library afterwards) |
| You use *Log in with Nexus Mods* (only once Nexus Mods registered AFTERLIFE) | `sso.nexusmods.com` and your browser | a random request ID; the key comes back to AFTERLIFE |

Every request identifies itself as AFTERLIFE with its version, as Nexus Mods asks applications to do. Without a Nexus key AFTERLIFE makes no network requests at all, except opening links in your browser when you click them.

Update checks run at start and then once an hour: one request for all mods, then file lists only for mods that changed. Mod details are cached for 7 days.

## What stays on your PC

- **Nexus API key**: encrypted with Windows DPAPI for your Windows user, in `%APPDATA%\AFTERLIFE`. Only your Windows account can decrypt it. **Remove key** in Settings deletes it. You can also revoke it on nexusmods.com at any time.
- **Settings**: `%APPDATA%\AFTERLIFE`.
- **Library** (mods, profiles, history, downloads, cached pictures): the folder you chose.
- **Logs**: `%LOCALAPPDATA%\AFTERLIFE\logs`. They contain paths and mod names, never your key.
- **Copy diagnostics** puts a text on your clipboard; nothing is sent. Check it before you paste it anywhere public: it contains folder paths, which may include your Windows user name.

## Links

Clicking links (Nexus pages, GitHub, Sponsors, e-mail) opens them in your browser or mail app. What happens there is covered by those sites' own privacy policies, for example the [Nexus Mods privacy policy](https://help.nexusmods.com/article/19-privacy-policy).

## Removing everything

**Settings → Remove AFTERLIFE from this PC…** deletes the key and the settings, gives `nxm://` links back to the previous app and, if you tick it, deletes the library.

Questions: silentbob@silentbob.cz
