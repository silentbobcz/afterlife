# AFTERLIFE: co čeká na tebe

Naposledy upraveno: 21. 11. 2026. Když něco vyřídíš, napiš číslo úkolu (třeba „A1 hotovo“ nebo „C4 první možnost“) v kterémkoli vláknu.

**Fronta modelů:** [FRONTA.md](FRONTA.md), větev `claude/kind-hopper-az2rax`. Do nového vlákna stačí napsat: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev claude/kind-hopper-az2rax) a udělej balík F3.“

Aplikace se nesestavuje, dokud nejsou hotové všechny úpravy a neřekneš. Položky A2 až A8 do té doby čekají; všechny se zkouší na jednom sestavení z větve `claude/kind-hopper-az2rax`.

## A. Vyzkoušet

| # | Co | Kde to najdu | Co mám udělat | Stav |
|---|---|---|---|---|
| A2 | Překryvná okna a start okna (O1) | Nové sestavení | 1. Maximalizuj okno, otevři Scan manual mods a historii stahování: seznamy mají sahat skoro dolů. 2. Zmenši okno na minimum: jako dnes. 3. Na 1920 × 1080 při 125 % okno nesmí přesahovat okraj. | ☐ čeká na sestavení |
| A3 | Dialog při vypnutí modu (O2) | Nové sestavení, seznam modů | 1. Vypni mod, který potřebuje jiný (ne core) mod: dialog ukáže „ONLY IT NEEDED, NOW ON“ s nezaškrtnutou volbou. 2. Vypni skriptový mod, který potřebuje jen CET: vypne se bez dotazu. 3. Jsou texty srozumitelné? | ☐ čeká na sestavení |
| A4 | Propojení modů ze ZIPů s Nexusem (F1) | Nové sestavení, plná edice; detail modu, „Link all unlinked mods…“ | 1. Klikni na odkaz a počkej na seznam. 2. Zkontroluj, že každý řádek patří ke správné stránce, a potvrď. 3. Přetáhni do okna ZIP stažený z Nexusu: má sám dostat obrázek a popis. Neověřeno: skutečná odpověď Nexusu. | ☐ čeká na sestavení |
| A5 | Panel filtrů (O4) | Nové sestavení; panel mezi PROFILES a MOD LIBRARY, tlačítko ⧩ FILTERS | 1. Zaškrtni Conflict a Update available, přepni MATCH ALL / MATCH ANY. 2. Klikni «, pak ✕, pak FILTERS: panel se sbalí, skryje a vrátí. 3. Zavři a otevři AFTERLIFE: panel je ve stejném stavu. | ☐ čeká na sestavení |
| A6 | Propojení s Nexusem z nastavení (O6) | Nové sestavení; Settings → 03 · NEXUS MODS | 1. Klikni na „Link all unlinked mods…“: nastavení se zavře a ukáže stejný seznam jako z detailu modu. | ☐ čeká na sestavení |
| A7 | Řazení seznamu (F2) | Nové sestavení; tlačítko „⇅ LOAD ORDER“ vedle FILTERS | 1. Proklikej NAME, NEWEST, STATE, SIZE: řadí se uvnitř bloků, core mody zůstávají. 2. Při řazení nejde mod přetáhnout a nad seznamem je vysvětlení. 3. Klikni „Back to load order“, dej APPLY: v plánu nesmí být žádná změna pořadí. | ☐ čeká na sestavení |
| A8 | Rychlejší seznam při stovkách modů (F2) | Nové sestavení; Settings → 05 · DISPLAY, „Draw only the visible rows…“ | 1. Změř čas od spuštění po zobrazení seznamu a zkus rolovat, vypnuto a zapnuto. 2. Se zapnutým zkus přetáhnout mod, sbalit blok a vyhledat mod. 3. Napiš, jestli seznam někde poskakuje. Neověřeno: nic z toho jsem neviděl běžet. | ☐ čeká na sestavení |

## C. Rozhodnout (stačí odpověď)

Doporučená možnost je vždy první.

| # | Co | O co jde | Možnosti | Stav |
|---|---|---|---|---|
| C3 | Sloučení do `main` a vydání alpha.10 | Nevratný krok. Vše hotové je v pracovní větvi, v `main` nic. | 1. Nejdřív sestavit a projít A2 až A8, pak sloučit a vydat. 2. Sloučit a vydat hned bez zkoušky: rychlejší, ale nic z toho ještě neběželo ve Windows. | ☐ čeká |
| C4 | Kontrola aktualizací při 600+ modech | Při startu a každou hodinu jde jeden dotaz na seznam změněných modů a čte se nejvýš 60 modů. Tlačítko CHECK UPDATES ale čte všechny, tedy 600+ dotazů. Tvůj návrh: přepínač kontroly při startu, tlačítko, core mody vždy, volba kontrolovat jen zapnuté mody. | 1. Přepínač, core mody vždy, rozsah při startu a tlačítko nově čte jen změněné mody (plná kontrola jako zvláštní volba). 2. Jen přepínač a core mody, tlačítko nechat číst vše: méně práce, jedno kliknutí stojí 600+ dotazů. Po rozhodnutí je to balík F3. | ☐ čeká |

## D. Dodat

| # | Co | Kde to vezmu | Stav |
|---|---|---|---|
| D1 | Odpověď pisatele komentáře | Komentáře u AFTERLIFE na Nexus Mods: snímek maximalizovaného okna, rozlišení a měřítko, příklad vypnutého modu, názvy ZIPů, pohledy, které chce. Vlož ji do vlákna, které na AFTERLIFE pracuje. | ☐ čeká |

## Hotovo

| Kdy | Co |
|---|---|
| 21. 11. | A1: v maximalizovaném okně alpha.9 hlavní seznam vyplní šířku i výšku, chyba v něm není. Pisatelovo „nezvětší se“ nejspíš míří na překryvná okna (opravuje O1); potvrdí to jeho snímek (D1). |
| 21. 11. | Fronta a úkoly jsou v repozitáři (`FRONTA.md`, `UKOLY.md`), aby je našla všechna vlákna i počítač doma. Dokumenty v Claude Docs už se nevedou. |
| 21. 11. | F2 hotové: řazení jen pro zobrazení a přepínač virtualizace. Kontrola O1, O2, O4 a O6 bez nálezu. 303 testů, překlad obou edic. |
| 21. 11. | F1 a O6 hotové: propojení modů ze ZIPů s Nexusem, z detailu modu i z nastavení. |
| 21. 11. | B1 a B2: zůstává dnešní vzhled s panelem filtrů (O4). Přestavba celého okna (W1, W2, W3) a styl MUTED jsou odložené jako alternativy. |
| 21. 11. | C2: nic se nesestavuje, dokud neřekneš; testy si vlákna pouštějí sama. |
| 21. 11. | C1 a C5: do pracovních větví na GitHubu se zapisuje; do `main` nic bez tvého pokynu. |
| 21. 11. | Rozbor komentáře z Nexus Mods proti kódu a anglické otázky pro pisatele. |
