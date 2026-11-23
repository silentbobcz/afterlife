# AFTERLIFE: co čeká na tebe

Naposledy upraveno: 23. 11. 2026. Když něco vyřídíš, napiš číslo úkolu (třeba „A1 hotovo“ nebo „C4 první možnost“) v kterémkoli vláknu.

**Fronta modelů:** [FRONTA.md](FRONTA.md). Nejnovější stav je v `main`. Může začít hned: O20 (vydání plné edice s instalátorem, Opus); po něm O18 a O19 (Sonnet). Nezávisle může začít O21 (proč zaškrtnuté mody schovají APPLY a jak se k němu vrátit, Opus). Spouštěcí věta: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev main) a udělej balík O20.“

Poslední pracovní verze plné edice je `AFTERLIFE-test-bf0d097-win-x64.zip` ve složce `zkusebni` (23. 11.): obsahuje odkaz v patičce a doplněný rychlý návod (A17), ale ještě se starou adresou webu. Pracuje s tvou skutečnou knihovnou a hrou, stejně jako nainstalovaná verze. Další verzi vyrobí ruční spuštění workflow `build` (artefakt `AFTERLIFE-full-test`); řekni si o ni.

## A. Vyzkoušet

| # | Co | Kde to najdu | Co mám udělat | Stav |
|---|---|---|---|---|
| A17 | Odkaz na web s návodem v patičce okna a doplněný rychlý návod | Další pracovní verze (zip `bf0d097` má ještě starou adresu webu); patička dole v hlavním okně, rychlý návod (QUICK GUIDE) | 1. V patičce vedle „© 2026 Silentbobcz …“ klikni na **Guide & website ↗**: otevře web v prohlížeči. Je dost vidět? 2. V rychlém návodu je „Full guide on the web ↗“ hned nahoře (dole už není); v About zůstává WEBSITE. 3. Rychlý návod má nově řádky EXPORT / IMPORT ORDER a LOAD BEFORE a v KEYS klávesy Ctrl+šipky; vejde se nadpis EXPORT / IMPORT ORDER do levého sloupce? Neověřeno: nic z toho neběželo v okně. | ☐ čeká na sestavení a D2 |

## C. Rozhodnout (stačí odpověď)

Doporučená možnost je vždy první.

| # | Co | O co jde | Možnosti | Stav |
|---|---|---|---|---|
| C8 | Tag a vydání 1.0.0-beta.1 | Kód je v `main` s verzí 1.0.0-beta.1, od 22. 11. i s nastavením v záložkách (A10). Vydání na GitHubu spustí až tag, a ten zakládáš ty. Od 23. 11. platí: na GitHub plná edice (instalátor a zip) a Offline zip, na Nexus jen Offline zip; to umí workflow až po balíku O20. | 1. Počkat na O20, pak založit tag `v1.0.0-beta.1` na GitHubu (Releases → Draft a new release → tag `v1.0.0-beta.1`, cíl `main`); vyjde plná edice s instalátorem i Offline. 2. Založit tag hned: vyjde jen Offline zip jako dosud. | ☐ čeká |

## D. Dodat

| # | Co | Kde to vezmu | Stav |
|---|---|---|---|
| D1 | Odpověď pisatele komentáře | Komentáře u AFTERLIFE na Nexus Mods: snímek maximalizovaného okna, rozlišení a měřítko, příklad vypnutého modu, názvy ZIPů, pohledy, které chce. Vlož ji do vlákna, které na AFTERLIFE pracuje. | ☐ čeká |
| D2 | Doména afterlife.silentbob.cz pro web | Cloudflare → Workers & Pages → projekt webu → Custom domains | Přidej vlastní doménu `afterlife.silentbob.cz` (Set up a custom domain). DNS záznam už existuje, ale 23. 11. web na té adrese vracel chybu 522: doména ještě není připojená k projektu Pages. Ověř, že <https://afterlife.silentbob.cz/guide.html> otevře návod. Aplikace, README a dokumentace už odkazují jen na tuto adresu; i odkaz na Nexusu přepiš. | ☐ čeká |

## Hotovo

| Kdy | Co |
|---|---|
| 23. 11. | Web má adresu afterlife.silentbob.cz: aplikace (patička, About, rychlý návod), README, návod a changelog odkazují jen na ni (větev `footer-web-link`). Doménu připojuješ v D2. |
| 23. 11. | Vyzkoušeno na `2fab46c`: A15 (dialog při vypnutí modu) a A16 (export a import load listu) v pořádku. A17: odkaz na web v About nikdo nenajde, přesunut do patičky okna; rychlý návod doplněn o load list, LOAD BEFORE a klávesy (větev `footer-web-link`). |
| 23. 11. | Rozhodnuto: na GitHubu se vydává plná edice (instalátor a zip) a vedle ní Offline zip, který jde na Nexus Mods; na Nexusu zůstává jen Offline. Instalátor jen pro plnou edici. Ruší se „vydání jen Offline“ (od alpha.9) a „instalátor nebude“ (22. 11.). Do kódu jde jako O20, web O18, návod k přechodu na novou verzi O19. |
| 23. 11. | C10: web s návodem běží na Cloudflare Pages, <https://afterlife-13h.pages.dev>, a obnovuje se po každé změně v `main`. Adresa je v README, v okně About (tlačítko WEBSITE) a pod vestavěným návodem; na Nexus ji vkládáš ty. Úvodní stránka webu ještě nezdůrazňuje, že ke stažení je edice Offline: oprava je balík O18. |
| 23. 11. | O15 (export a import load listu) a O17 (nový dialog při vypnutí modu) hotové a zkontrolované, 375 testů; zkouší se v A16 a A15. Druhá dávka fronty je uzavřená a v archivu. |
| 23. 11. | Vyzkoušeno na pracovní verzi `1c1256e`: A2, A7, A8, A13 (volné pořadí modů) a A14 (fialová tlačítka) v pořádku. A12 (web a dokumentace): bez výhrad teď, úpravy případně později. |
| 23. 11. | Schváleno: B4 (náhled volného pořadí), B5 (nové texty dialogu při vypnutí modu, do kódu jde jako O17) a B6 (formát load listu; u bodů D1 až D7 platí doporučené možnosti A, tlačítka EXPORT ORDER a IMPORT ORDER; do kódu jde jako O15). |
| 22. 11. | B7: vybrána varianta B (fialová) a dlouhý popisek; O16 je v kódu a zkontrolované, zkouší se v A14. O14 a O16 sloučeny do `main`. |
| 22. 11. | O14: návrh formátu load listu hotový (`docs/previews/load-list-format.md`), čeká na B6. |
| 22. 11. | Dva nové nápady zapsané do fronty: export a import load listu (O14 návrh formátu, O15 kód) a jiná barva a popisek tlačítek FILTERS a SORT (O16). |
| 22. 11. | Výroba jedním vláknem: O10 (náhled), O11 (volné pořadí v kódu), O12 (klávesy, LOAD BEFORE) a návrh textů O13 hotové a zkontrolované; sestavení a 335 testů sloučeno do `main`. Kontrola opravila tři věci (text v náhledu, kolizi názvu testů, nepřeložitelnou vsuvku). |
| 22. 11. | C9: nové mody při volném pořadí padají na konec MODS (bez kategorie). Aktualizace drží místo staré verze. |
| 22. 11. | Vyzkoušeno na sestavení `34f6b02`: A4, A5, A6, A9, A10 a A11 v pořádku. A3 v pořádku, ale logika dialogu při vypnutí modu není srozumitelná: texty se přepíšou (balík O13). A2: historie stahování v pořádku, Scan manual mods je krátké (zůstává otevřené). A7: čekal jsi ruční pořadí uvnitř MODS a kategorií; to je O10 až O12. |
| 22. 11. | B3 schváleno, O8 hotové a zkontrolované (bez nálezu): nastavení se záložkami vlevo je v kódu, zkouší se v A10. Instalátor (O9) je zrušený a jeho zkušební výroba vrácená. |
| 22. 11. | Balení: zůstává složka v zipu, k jednomu samorozbalovacímu exe se nevracíme. Samostatný instalátor se dělat nebude (zrušeno 22. 11.). |
| 22. 11. | C3 a C7: vše sloučeno do `main`, verze 1.0.0-beta.1. První dávka fronty je uzavřená a v archivu. Vydání čeká na tvůj tag (C8). |
| 22. 11. | Kontrola náhledu nastavení (O7): odpovídá aplikaci, všechny volby mají místo, texty sedí s tím, co je v kódu. Rozhodnutí je dál tvoje (B3). |
| 22. 11. | Logy: uživatelské jméno, název počítače, složky a tajné části odkazů se před zápisem nahrazují značkami. |
| 21. 11. | O7: náhled nastavení se záložkami vlevo je hotový, posuzuješ ho v B3. |
| 21. 11. | Pojistka limitu Nexusu hotová: aplikace zná limit, zastaví se před ním, počká a pokračuje sama; nastavení varuje před příliš častou plnou kontrolou. 313 testů. |
| 21. 11. | F3 hotové: kontrola aktualizací bez stropu 60, plné čtení každého modu jednou za N dní, volby v nastavení. 309 testů, překlad obou edic. |
| 21. 11. | C6: nastavení se přestaví celé, se záložkami vlevo. Nejdřív náhled (O7, B3), pak kód (O8). Volby z F3 jsou do té doby v prostém oddílu UPDATES. |
| 21. 11. | C4: kontrola aktualizací bude bez stropu 60, zapnuté a core mody první, tlačítko přes rozdíly, přepínač kontroly při startu a plné přečtení každého modu jednou za N dní (N v nastavení). Zadání je v balíku F3. |
| 21. 11. | A1: v maximalizovaném okně alpha.9 hlavní seznam vyplní šířku i výšku, chyba v něm není. Pisatelovo „nezvětší se“ nejspíš míří na překryvná okna (opravuje O1); potvrdí to jeho snímek (D1). |
| 21. 11. | Fronta a úkoly jsou v repozitáři (`FRONTA.md`, `UKOLY.md`), aby je našla všechna vlákna i počítač doma. Dokumenty v Claude Docs už se nevedou. |
| 21. 11. | F2 hotové: řazení jen pro zobrazení a přepínač virtualizace. Kontrola O1, O2, O4 a O6 bez nálezu. 303 testů, překlad obou edic. |
| 21. 11. | F1 a O6 hotové: propojení modů ze ZIPů s Nexusem, z detailu modu i z nastavení. |
| 21. 11. | B1 a B2: zůstává dnešní vzhled s panelem filtrů (O4). Přestavba celého okna (W1, W2, W3) a styl MUTED jsou odložené jako alternativy. |
| 21. 11. | C2: nic se nesestavuje, dokud neřekneš; testy si vlákna pouštějí sama. |
| 21. 11. | C1 a C5: do pracovních větví na GitHubu se zapisuje průběžně. |
| 21. 11. | Rozbor komentáře z Nexus Mods proti kódu a anglické otázky pro pisatele. |
