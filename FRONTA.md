# AFTERLIFE: fronta pro modely (3. dávka)

Založeno 23. 11. 2026 po uzavření 2. dávky, naposledy upraveno 23. 11. Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md), úkoly pro uživatele v [`UKOLY.md`](UKOLY.md). Starší dávky jsou v `archiv/` (jen archiv, stav z nich nečti).
Další volná čísla: S1 O22 F4

**Stav: všechny balíky dávky jsou hotové a čekají na sloučení do `main`** (C11 v `UKOLY.md`). Nejnovější kód je ve větvi `beta1-with-o21` (vše) a `beta1-release` (totéž bez O21 a nabídky LIST & ORDER). Po sloučení se dávka uzavře a soubor odejde do `archiv/`. Nový balík teď žádný nečeká; další vzniknou z toho, co uživatel najde při zkoušení (A18 až A21).

## Stav balíků

| Balík | Co | Stav |
|---|---|---|
| O20 | Vydání plné edice na GitHubu: zip a instalátor plné edice, zip Offline pro Nexus | ✅ hotovo, větev `beta1-release`, čeká na sloučení |
| O18 | Web: dvě edice ke stažení (GitHub plná, Nexus Offline) a proč | ✅ hotovo, větev `beta1-release`, čeká na sloučení |
| O19 | Návod: jak přejít na novou verzi (zip i instalátor) | ✅ hotovo, větev `beta1-release`, čeká na sloučení |
| O21 | Zaškrtnuté mody a lišta APPLY; nabídka LIST & ORDER | ✅ zkontrolováno, větev `o21-checked-mods` (je i v `beta1-with-o21`), čeká na sloučení |

Odkaz na web v patičce okna a doplněný rychlý návod (větev `footer-web-link`, A17 schváleno) jsou v obou větvích vydání také.

## Předávky

**Předávka O20, O18, O19** (23. 11., výrobní vlákno)
- Stav: hotovo, větev `beta1-release` (z `main` + `footer-web-link`), do `main` nesloučeno.
- Změněno: `.github/workflows/build.yml`, `installer/AFTERLIFE.iss`, `AppSettings.cs` a test v `RequirementTests.cs`, `AppEdition.cs` (komentář), `README.md`, `docs/USER-GUIDE.md`, `docs/FAQ.md`, `docs/BUILDING.md`, `docs/HOW-IT-WORKS.md`, `site/home.md`, `site/style.css`, `CHANGELOG.md`.
- Ověřeno: běh `build` na GitHubu (375 testů, zip plné edice, instalátor, zip Offline, každý se `.sha256`); tichá instalace a odinstalace instalátoru ve Windows, složka knihovny uvnitř zůstala; web se sestavil, kotvy sedí; tvrzení návodu proti kódu (cesta ke knihovně je v nastavení, obě edice ho sdílejí).
- Neověřeno: průvodce instalátoru v okně a první spuštění nainstalované kopie (A20); vzhled dvou rámečků na úvodní stránce webu (A21); krok vydání při tagu (text vydání a tři soubory) poběží poprvé až s tagem.
- Rozhodnuto bez uživatele: každý běh workflow staví obě edice i instalátor a chyba instalátoru shodí běh; zkušební artefakt `AFTERLIFE-full-test` nese zip i instalátor; „Updating AFTERLIFE“ je pododdíl kapitoly 2 návodu, aby se nepřečíslovaly kapitoly; první tlačítko na webu vede na GitHub.
- Nález: aplikace neumí knihovnu přesunout jinam; návod to říká a radí starou složku nechat. Přesun knihovny je námět na balík.
- Text pro popis na Nexus Mods (vkládá uživatel, D3): „This page carries AFTERLIFE Offline, which never connects to Nexus Mods. The full edition, which downloads, updates and adds collections with your Nexus Mods key, is on GitHub: https://github.com/silentbobcz/afterlife/releases · Guide: https://afterlife.silentbob.cz“
- Pro uživatele: A20, A21, C11, D3.

**Předávka O21** (23. 11., podřízený Opus, kontrola výrobní vlákno)
- Stav: hotovo, větev `o21-checked-mods`, do `main` nesloučeno.
- Změněno: `MainWindow.xaml` (lišta), `MainWindow.xaml.cs` (Esc), nové `MainViewModel.Selection.cs`, `MainViewModel.ToolsMenu.cs`, `Views/ToolsMenu.xaml`, `CheckedRows.cs` a `CheckedRowsTests.cs`; `CHANGELOG.md`, `USER-GUIDE.md`, `HOW-IT-WORKS.md`.
- Ověřeno: překlad a testy na GitHubu, rozdíl přečten proti kódu dvakrát (podruhé před vydáním, bez nálezu).
- Neověřeno: nic neběželo v okně; malá barevná tlačítka při najetí myší nejspíš zmodrají (šablona `SmallButton`); otevírání, zavírání a poloha nabídky LIST & ORDER.
- Po B8: při zaškrtnutí ustoupí jen řada malých nástrojů hromadným akcím, stavový řádek a APPLY zůstávají. Po B9: EXPORT LIST, EXPORT ORDER, IMPORT ORDER a RESET ORDER jsou v nabídce LIST & ORDER.
- Rozhodnuto bez uživatele: Esc výběr neruší, dokud je fokus v hledání nebo v otevřeném seznamu či nabídce.
- Pro uživatele: A18, A19.

**Předávka O21** (23. 11., podřízený Opus, kontrola výrobní vlákno)
- Stav: hotovo, větev `o21-checked-mods`, do `main` nesloučeno.
- Změněno: `MainWindow.xaml` (texty lišty), `MainWindow.xaml.cs` (Esc), nové `MainViewModel.Selection.cs`, `CheckedRows.cs` (Infrastructure, čistý výpočet) a `CheckedRowsTests.cs`; `CHANGELOG.md`, `USER-GUIDE.md`, `HOW-IT-WORKS.md`.
- Ověřeno: překlad a testy na GitHubu (ruční běh `build` na větvi), rozdíl přečten proti kódu.
- Neověřeno: nic neběželo v okně; výška lišty se třemi řádky v úzkém okně, Esc s fokusem na řádku seznamu.
- Rozhodnuto bez uživatele: Esc výběr neruší, dokud je fokus v hledání (ani v prázdném), v otevřeném seznamu nebo nabídce; tooltip je na celém prvním řádku a říká, že hromadné akce berou i schované mody; „folded sections“ místo „category“; jednotné číslo „1 mod checked“.
- Změna po B8 (uživatel schválil náhled `docs/previews/bulk-bar.html`): lišta se už nevyměňuje celá. Při zaškrtnutí ustoupí jen řada malých nástrojů hromadným akcím (malá tlačítka), stavový řádek, PREVIEW a APPLY zůstávají; vysvětlující věta odpadla. Body 1 a 4 zadání tím neplatí. Neověřeno navíc: malá barevná tlačítka při najetí myší zmodrají (šablona `SmallButton`).
- Navíc po B9 (mimo zadání balíku): EXPORT LIST, EXPORT ORDER, IMPORT ORDER a RESET ORDER jsou v nabídce LIST & ORDER (`Views/ToolsMenu.xaml`, `MainViewModel.ToolsMenu.cs`). Neověřeno: otevírání, zavírání a poloha nabídky v okně.
- Mimo balík: zaškrtnutí celé sekce přepočítává výběr po každém řádku (bylo i dřív); návod říká zaškrtnutým modům střídavě „selected“ a „checked“.
- Pro uživatele: A18 v `UKOLY.md`.

## Co do fronty nepatří

- Přestavba celého okna (návrhy W1 až W3) a styl MUTED: odložené alternativy.
- Instalátor pro AFTERLIFE Offline: není třeba, na Nexus jde zip (23. 11.).
- Hlášky ve stavovém řádku s „mod(s)“ a střídání „selected“ a „checked“ v návodu: drobnosti k příští úpravě textů.
- Přesun knihovny do jiné složky z aplikace: až o něj uživatel řekne.
- Fronta stránek pro účty zdarma u chybějících modů z load listu (dnes jen odkazy): až o ni uživatel řekne.

## Co přinesla 2. dávka (22. až 23. 11.)

Nastavení v záložkách (O8), volné pořadí modů s čísly, A→Z, klávesami a LOAD BEFORE (O10 až O12), fialová tlačítka FILTERS a SORT (O16), export a import load listu (O14, O15), nový dialog při vypnutí modu s opraveným výpočtem (O13, O17). Podrobnosti v `CHANGELOG.md` a `docs/HOW-IT-WORKS.md`.
