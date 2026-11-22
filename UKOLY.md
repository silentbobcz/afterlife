# AFTERLIFE: co čeká na tebe

Naposledy upraveno: 22. 11. 2026. Když něco vyřídíš, napiš číslo úkolu (třeba „A1 hotovo“ nebo „C4 první možnost“) v kterémkoli vláknu.

**Fronta modelů:** [FRONTA.md](FRONTA.md). Nejnovější stav je v `main` (včetně volného pořadí modů, O11 a O12). Pro modely čeká: O14 (návrh formátu load listu, Opus) a O16 (náhled tlačítek FILTERS a SORT, Sonnet), obojí může začít hned; zapracování textů dialogu po B5.

Zkušební sestavení plné edice 1.0.0-beta.1 z 22. 11. večer nese v názvu `34f6b02` (`AFTERLIFE-test-34f6b02-win-x64.zip`, u tebe ve složce `zkusebni`). Obsahuje vše z `main` včetně nastavení v záložkách; položky A2 až A11 se zkouší na něm. Starší zip (`6e633d0`) smaž. Pracuje s tvou skutečnou knihovnou a hrou, stejně jako nainstalovaná verze. Další takové sestavení vyrobí ruční spuštění workflow `build` (artefakt `AFTERLIFE-full-test`).

## A. Vyzkoušet

| # | Co | Kde to najdu | Co mám udělat | Stav |
|---|---|---|---|---|
| A2 | Okno Scan manual mods v maximalizovaném okně | Zkušební sestavení `34f6b02` | Historie stahování je v pořádku. Scan manual mods je krátké: napiš, která část (seznam nalezených modů nahoře, nebo „Removed earlier“ dole) a kolik řádků v ní zhruba máš; nejlíp pošli snímek. Podle toho se opraví. | ☐ čeká na tebe |
| A7 | Řazení seznamu pro zobrazení (F2) | Zkušební sestavení `34f6b02`; tlačítko „⇅ LOAD ORDER“ vedle FILTERS | Tohle tlačítko mění jen pohled (NAME, NEWEST, STATE, SIZE), pořadí načítání nemění. Ruční pořadí uvnitř MODS a kategorií, které jsi čekal, je samostatná práce: balíky O10 až O12, čekají na B4. Tady jen potvrď: 1. řazení funguje uvnitř bloků a core mody zůstávají; 2. „Back to load order“ a APPLY neukážou žádnou změnu pořadí. | ☐ čeká na tebe |
| A8 | Rychlejší seznam při stovkách modů (F2) | Nové sestavení; Settings → 05 · DISPLAY, „Draw only the visible rows…“ | 1. Změř čas od spuštění po zobrazení seznamu a zkus rolovat, vypnuto a zapnuto. 2. Se zapnutým zkus přetáhnout mod, sbalit blok a vyhledat mod. 3. Napiš, jestli seznam někde poskakuje. Neověřeno: nic z toho jsem neviděl běžet. | ☐ čeká na tebe |
| A12 | Web s návodem a opravená dokumentace | Náhled webu: ve složce `site` spusť `npm ci` a `npm run build`, pak otevři `site/dist/index.html`. Texty: `docs/USER-GUIDE.md`, `docs/FAQ.md`, `README.md`. V aplikaci tlačítko GUIDE nad seznamem (až v dalším zkušebním sestavení). | 1. Projdi úvodní stránku, hlavně oddíl o Nexus Premium: sedí tón? 2. V návodu si přečti oddíly 1 (dvě edice), 3 (filtry, řazení), 11 (aktualizace, limit, Premium) a 17 (nastavení). 3. Napiš, co chybí nebo je špatně. Neověřeno: dva nové řádky v rychlé nápovědě v okně (FILTERS, ⇅ SORT) nikdo neviděl běžet. | ☐ čeká na tebe |
| A13 | Volné pořadí modů (O11, O12) | Další zkušební sestavení z `main`; seznam modů | 1. Přetáhni mod za úchyt ⋮⋮ nahoru a dolů uvnitř MODS a uvnitř kategorie, pak víc zaškrtnutých najednou: čísla #1, #2 … se přepočítají a APPLY ukáže změnu pořadí. 2. Zkus A→Z v hlavičce MODS a kategorie, Ctrl+↑/↓ a Ctrl+Home/End na vybraném modu a tlačítko LOAD BEFORE v záložce Conflicts u souboru `.archive`, který prohrává. 3. Po prvním spuštění zkontroluj, že se seznam nepřeházel a APPLY nehlásí změnu, kterou jsi neudělal; přepni profil a zpět. Neověřeno: nic z toho neběželo v okně. | ☐ čeká na sestavení |

## B. Schválit

| # | Co | Kde to najdu | Na co se dívat | Stav |
|---|---|---|---|---|
| B4 | Náhled volného pořadí (O10) | `docs/previews/free-load-order.html` v repozitáři (dvojklik; po `git pull` v klonu). Přepínač LOAD ORDER / ⇅ NAME nahoře, tlačítka A→Z fungují na zkoušku. | Čísla pořadí na řádcích, čára při tažení, tlačítko A→Z a jeho potvrzení, mod s místními úpravami; pod náhledem texty hlášek. Stačí „B4 schváleno“ nebo co změnit. Kód (O11) se už píše podle zadání; co změníš, se do něj doplní. | ☐ čeká na tebe |
| B5 | Nové texty dialogu při vypnutí modu (O13) | `docs/previews/switch-off-dialog-texts.md`: tabulka „dnes → nově“ | Jsou nové věty srozumitelné? Každá ze tří částí má nadpis a jednu větu, co se stane s kterými mody. Stačí „B5 schváleno“ nebo co změnit; do aplikace se texty dostanou až potom. | ☐ čeká na tebe |

## C. Rozhodnout (stačí odpověď)

Doporučená možnost je vždy první.

| # | Co | O co jde | Možnosti | Stav |
|---|---|---|---|---|
| C10 | Zveřejnění webu s návodem na Cloudflare | Web se staví z repozitáře (složka `site`), ale projekt v Cloudflare Pages musíš založit ty ve svém účtu. Postup a hodnoty jsou v `site/README.md`. Potom se sám obnoví po každé změně v `main`. | 1. Založit projekt v Cloudflare Pages podle `site/README.md` a napsat adresu; doplní se do README, na Nexus a do aplikace. 2. Zatím nezveřejňovat, návod číst jen na GitHubu. | ☐ čeká |
| C8 | Tag a vydání 1.0.0-beta.1 | Kód je v `main` s verzí 1.0.0-beta.1, od 22. 11. i s nastavením v záložkách (A10). Vydání na GitHubu spustí až tag, a ten zakládáš ty. Vydá se edice Offline; plnou edici máš jako zkušební zip. | 1. Nejdřív projít A2 až A10 na zkušebním sestavení, pak založit tag `v1.0.0-beta.1` na GitHubu (Releases → Draft a new release → tag `v1.0.0-beta.1`, cíl `main`). 2. Založit tag hned. | ☐ čeká |

## D. Dodat

| # | Co | Kde to vezmu | Stav |
|---|---|---|---|
| D1 | Odpověď pisatele komentáře | Komentáře u AFTERLIFE na Nexus Mods: snímek maximalizovaného okna, rozlišení a měřítko, příklad vypnutého modu, názvy ZIPů, pohledy, které chce. Vlož ji do vlákna, které na AFTERLIFE pracuje. | ☐ čeká |

## Hotovo

| Kdy | Co |
|---|---|
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
