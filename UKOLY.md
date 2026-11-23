# AFTERLIFE: co čeká na tebe

Naposledy upraveno: 23. 11. 2026. Když něco vyřídíš, napiš číslo úkolu (třeba „A18 hotovo“ nebo „C11 první možnost“) v kterémkoli vláknu.
Další volná čísla: A22 B10 C12 D4

**Fronta modelů:** [FRONTA.md](FRONTA.md). Třetí dávka je hotová celá a čeká jen na sloučení do `main` (C11). Nový balík teď žádný nečeká.

**Kde co je (23. 11.):** `main` je zatím starý stav bez věcí níže. Všechno pro beta.1 je pohromadě ve větvi `beta1-with-o21`: odkaz na web v patičce (A17), plná edice s instalátorem (O20), návod k přechodu na novou verzi (O19), web se dvěma edicemi (O18), lišta se zaškrtnutými mody (O21) a nabídka LIST & ORDER. Tatáž větev bez posledních dvou věcí je `beta1-release`. Kontroly na GitHubu prošly na obou (375 testů, tři soubory vydání).

**Zkušební verze:** složka `zkusebni`, `AFTERLIFE-test-4c489a3-win-x64.zip` a `AFTERLIFE-test-4c489a3-win-x64-Setup.exe` (z větve `beta1-with-o21`, obsahuje vše). Pracuje s tvou skutečnou knihovnou a hrou. Starší zipy ve složce můžeš smazat.

## A. Vyzkoušet

| # | Co | Kde to najdu | Co mám udělat | Stav |
|---|---|---|---|---|
| A18 | Zaškrtnuté mody a lišta APPLY (O21, B8) | Zkušební zip `4c489a3`; spodní lišta pod seznamem | 1. Přepni jeden mod, pak zaškrtni dva: stavový řádek, PREVIEW a APPLY CHANGES zůstanou, jen řadu malých tlačítek vystřídá „2 checked“, MOVE TO CATEGORY, ACTIVATE, DEACTIVATE, DELETE EVERYWHERE a „Clear (Esc)“. APPLY jde zmáčknout. 2. Schovej jeden zaškrtnutý filtrem nebo hledáním: přibude „(1 not visible)“ a po najetí myší důvod. 3. Klikni do seznamu a zmáčkni Esc: výběr zmizí; Esc v hledání jen smaže text. Neověřeno: nic z toho neběželo v okně; malá barevná tlačítka nejspíš při najetí myší zmodrají. | ☐ čeká na tebe |
| A19 | Nabídka LIST & ORDER místo čtyř tlačítek (B9) | Zkušební zip `4c489a3`; řada malých tlačítek pod seznamem | 1. V řadě je jen REFRESH, CHECK UPDATES (a UPDATE ALL) a LIST & ORDER ▾. 2. Klik na LIST & ORDER otevře nabídku nahoru se čtyřmi řádky; klik mimo ji zavře. 3. Zkus EXPORT LIST a EXPORT ORDER z nabídky; RESET ORDER je zašedlý, dokud pořadí není vlastní. Neověřeno: nic z toho neběželo v okně, hlavně otevírání, zavírání a poloha nabídky. | ☐ čeká na tebe |
| A20 | Instalátor plné edice (O20) | `zkusebni/AFTERLIFE-test-4c489a3-win-x64-Setup.exe` | 1. Spusť instalátor (SmartScreen: More info → Run anyway), nech výchozí složku, na konci spusť AFTERLIFE: má najít tvou knihovnu a hru jako dosud. 2. Je AFTERLIFE v nabídce Start a ve Windows v Nainstalovaných aplikacích? 3. Odinstaluj ho tam: knihovna i nastavení musí zůstat. Ověřeno modelem: tichá instalace do zkušební složky, zápis do Windows, odinstalace, složka knihovny uvnitř zůstala. Neověřeno: průvodce instalátoru v okně, zástupci, první spuštění nainstalované kopie na čistém počítači. | ☐ čeká na tebe |
| A21 | Web: dvě edice na úvodní stránce a návod k přechodu na novou verzi (O18, O19) | Po sloučení do `main` <https://afterlife.silentbob.cz> (obnoví se sám) a <https://afterlife.silentbob.cz/guide.html#updating-afterlife> | 1. Úvodní stránka: nad tlačítky dva rámečky (ON GITHUB plná edice, ON NEXUS MODS Offline), první tlačítko vede na GitHub. Vypadá to dobře i na telefonu? 2. Přečti oddíl Updating AFTERLIFE v návodu: je z něj jasné, že stará složka se nemaže, když je v ní knihovna? Neověřeno: vzhled rámečků pohledem (web se sestavil bez chyb). | ☐ čeká na sloučení |

## C. Rozhodnout (stačí odpověď)

Doporučená možnost je vždy první.

| # | Co | O co jde | Možnosti | Stav |
|---|---|---|---|---|
| C11 | Co půjde do beta.1 a sloučení do `main` | A18 a A19 jsi schválil jako náhledy (B8, B9), ale v okně je ještě nikdo neviděl. Všechno ostatní je schválené nebo ověřené kontrolami. Sloučení do `main` potřebuje tvoje výslovné „sloučit“. | 1. Sloučit `beta1-with-o21` (všechno): beta.1 má i novou lištu a nabídku; riziko je drobná vada vzhledu, kterou opraví beta.2. 2. Sloučit `beta1-release` (bez A18 a A19): do vydání jde jen vyzkoušené, lišta a nabídka počkají na beta.2. 3. Nejdřív si projít A18 až A20 na zkušební verzi, pak rozhodnout. | ☐ čeká |
| C8 | Tag a vydání 1.0.0-beta.1 | Řekl jsi „proveď“ (23. 11.). Tag `v1.0.0-beta.1` na `main` spustí vydání: instalátor a zip plné edice a Offline zip, každý se `.sha256`. Vydání je veřejné a nejde vzít zpět beze stopy, proto jde až po C11. | 1. Tag založí model hned po sloučení (C11). 2. Tag založíš sám na GitHubu: Releases → Draft a new release → tag `v1.0.0-beta.1`, cíl `main`. | ☐ čeká na C11 |

## D. Dodat

| # | Co | Kde to vezmu | Stav |
|---|---|---|---|
| D1 | Odpověď pisatele komentáře | Komentáře u AFTERLIFE na Nexus Mods: snímek maximalizovaného okna, rozlišení a měřítko, příklad vypnutého modu, názvy ZIPů, pohledy, které chce. Vlož ji do vlákna, které na AFTERLIFE pracuje. | ☐ čeká |
| D3 | Po vydání: Offline zip a nový popis na Nexus Mods | Stránka vydání na GitHubu: stáhni `AFTERLIFE-Offline-1.0.0-beta.1-win-x64.zip` a nahraj ho na Nexus (jen zip, nikdy exe). Do popisu doplň adresu webu <https://afterlife.silentbob.cz> a větu z předávky O18 ve `FRONTA.md` (plná edice je na GitHubu). | ☐ čeká na C8 |

## Hotovo

Starší řádky jsou v [archiv/UKOLY-hotovo.md](archiv/UKOLY-hotovo.md).

| Kdy | Co |
|---|---|
| 23. 11. | A15 (dialog při vypnutí modu), A16 (export a import load listu) a A17 (odkaz na web v patičce, doplněný rychlý návod) schváleny. A15 a A16 jsou v `main`, A17 ve větvi vydání. |
| 23. 11. | O20, O18 a O19 hotové ve větvi `beta1-release`: workflow vyrábí instalátor a zip plné edice a Offline zip; README, návod, FAQ a web říkají, co se kde stahuje; nový oddíl Updating AFTERLIFE. Nainstalovaná kopie nenabízí knihovnu ve vlastní složce. Zkouší se v A20 a A21. |
| 23. 11. | D2: doména afterlife.silentbob.cz je připojená a web na ní odpovídá (u některých poskytovatelů se adresa ještě šíří). |
| 23. 11. | B9 schváleno: pod seznamem zůstává REFRESH, CHECK UPDATES a UPDATE ALL, čtyři občasná tlačítka jsou v nabídce LIST & ORDER (náhled `docs/previews/tools-menu.html`). V kódu, zkouší se v A19. |
| 23. 11. | B8 schváleno: při zaškrtnutí modů se vymění jen řada malých tlačítek, stavový řádek a APPLY zůstávají (náhled `docs/previews/bulk-bar.html`). V kódu (O21), zkouší se v A18. |
| 23. 11. | Web má adresu afterlife.silentbob.cz: aplikace (patička, About, rychlý návod), README, návod a changelog odkazují jen na ni. |
| 23. 11. | Rozhodnuto: na GitHubu se vydává plná edice (instalátor a zip) a vedle ní Offline zip, který jde na Nexus Mods; na Nexusu zůstává jen Offline. Instalátor jen pro plnou edici. |
| 23. 11. | C10: web s návodem běží na Cloudflare Pages a obnovuje se po každé změně v `main`. |
| 23. 11. | O15 (export a import load listu) a O17 (nový dialog při vypnutí modu) hotové a zkontrolované; druhá dávka fronty je uzavřená a v archivu. |
| 23. 11. | Vyzkoušeno na pracovní verzi `1c1256e`: A2, A7, A8, A13 (volné pořadí modů) a A14 (fialová tlačítka) v pořádku. A12 (web a dokumentace): bez výhrad. |
| 23. 11. | Schváleno: B4 (náhled volného pořadí), B5 (texty dialogu při vypnutí modu) a B6 (formát load listu). |
