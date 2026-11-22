# AFTERLIFE: co čeká na tebe

Naposledy upraveno: 22. 11. 2026. Když něco vyřídíš, napiš číslo úkolu (třeba „A1 hotovo“ nebo „C4 první možnost“) v kterémkoli vláknu.

**Fronta modelů:** [FRONTA.md](FRONTA.md), větev `main`. Po schválení B3 stačí do nového vlákna napsat: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev main) a udělej balík O8.“

Zkušební sestavení plné edice 1.0.0-beta.1 jsi dostal 22. 11. jako zip (název nese hash commitu). Položky A2 až A9 se zkouší na něm. Pracuje s tvou skutečnou knihovnou a hrou, stejně jako nainstalovaná verze.

## A. Vyzkoušet

| # | Co | Kde to najdu | Co mám udělat | Stav |
|---|---|---|---|---|
| A2 | Překryvná okna a start okna (O1) | Nové sestavení | 1. Maximalizuj okno, otevři Scan manual mods a historii stahování: seznamy mají sahat skoro dolů. 2. Zmenši okno na minimum: jako dnes. 3. Na 1920 × 1080 při 125 % okno nesmí přesahovat okraj. | ☐ čeká na tebe |
| A3 | Dialog při vypnutí modu (O2) | Nové sestavení, seznam modů | 1. Vypni mod, který potřebuje jiný (ne core) mod: dialog ukáže „ONLY IT NEEDED, NOW ON“ s nezaškrtnutou volbou. 2. Vypni skriptový mod, který potřebuje jen CET: vypne se bez dotazu. 3. Jsou texty srozumitelné? | ☐ čeká na tebe |
| A4 | Propojení modů ze ZIPů s Nexusem (F1) | Nové sestavení, plná edice; detail modu, „Link all unlinked mods…“ | 1. Klikni na odkaz a počkej na seznam. 2. Zkontroluj, že každý řádek patří ke správné stránce, a potvrď. 3. Přetáhni do okna ZIP stažený z Nexusu: má sám dostat obrázek a popis. Neověřeno: skutečná odpověď Nexusu. | ☐ čeká na tebe |
| A5 | Panel filtrů (O4) | Nové sestavení; panel mezi PROFILES a MOD LIBRARY, tlačítko ⧩ FILTERS | 1. Zaškrtni Conflict a Update available, přepni MATCH ALL / MATCH ANY. 2. Klikni «, pak ✕, pak FILTERS: panel se sbalí, skryje a vrátí. 3. Zavři a otevři AFTERLIFE: panel je ve stejném stavu. | ☐ čeká na tebe |
| A6 | Propojení s Nexusem z nastavení (O6) | Nové sestavení; Settings → 03 · NEXUS MODS | 1. Klikni na „Link all unlinked mods…“: nastavení se zavře a ukáže stejný seznam jako z detailu modu. | ☐ čeká na tebe |
| A7 | Řazení seznamu (F2) | Nové sestavení; tlačítko „⇅ LOAD ORDER“ vedle FILTERS | 1. Proklikej NAME, NEWEST, STATE, SIZE: řadí se uvnitř bloků, core mody zůstávají. 2. Při řazení nejde mod přetáhnout a nad seznamem je vysvětlení. 3. Klikni „Back to load order“, dej APPLY: v plánu nesmí být žádná změna pořadí. | ☐ čeká na tebe |
| A8 | Rychlejší seznam při stovkách modů (F2) | Nové sestavení; Settings → 05 · DISPLAY, „Draw only the visible rows…“ | 1. Změř čas od spuštění po zobrazení seznamu a zkus rolovat, vypnuto a zapnuto. 2. Se zapnutým zkus přetáhnout mod, sbalit blok a vyhledat mod. 3. Napiš, jestli seznam někde poskakuje. Neověřeno: nic z toho jsem neviděl běžet. | ☐ čeká na tebe |
| A9 | Kontrola aktualizací a oddíl UPDATES v nastavení (F3) | Další sestavení (po `e0a53b4`); Settings → UPDATES, tlačítko CHECK UPDATES | 1. Klikni CHECK UPDATES: má doběhnout rychle a říct, kolik modů má novější verzi. 2. V nastavení vypni a zapni automatickou kontrolu, změň počet dní; po zavření a otevření nastavení má hodnota zůstat. 3. „Check every mod now…“ přečte všechny mody: sleduj průběh v dolní liště a napiš, jak dlouho to trvalo a kolik modů máš. 4. Nastav počet dní na 1: řádek pod ním má ukázat počet dotazů denně; u velmi velké knihovny oranžové varování. Neověřeno: nic z toho jsem neviděl běžet; limity Nexusu znám jen z odpovědí serveru. | ☐ čeká na tebe |

## B. Promyslet a rozhodnout

| # | Co | Kde to najdu | Co mám rozhodnout | Stav |
|---|---|---|---|---|
| B3 | Nové nastavení se záložkami vlevo | [docs/previews/settings-tabs.html](docs/previews/settings-tabs.html), doma `D:/_AI/afterlife/docs/previews/settings-tabs.html` po `git pull` na `main`; otevři dvojklikem | Proklikej záložky, přepni okno 2560 × 1440 a edici OFFLINE; pod náhledem je, kam se která volba přesunula. Napiš „B3 schváleno“ nebo co změnit. Pak ho O8 přestaví v kódu. | ☐ čeká na tebe |

## C. Rozhodnout (stačí odpověď)

Doporučená možnost je vždy první.

| # | Co | O co jde | Možnosti | Stav |
|---|---|---|---|---|
| C8 | Tag a vydání 1.0.0-beta.1 | Kód je v `main` s verzí 1.0.0-beta.1. Vydání na GitHubu spustí až tag, a ten zakládáš ty. Vydá se edice Offline; plnou edici máš jako zkušební zip. | 1. Nejdřív projít A2 až A9 na zkušebním sestavení, pak založit tag `v1.0.0-beta.1` na GitHubu (Releases → Draft a new release → tag `v1.0.0-beta.1`, cíl `main`). 2. Založit tag hned. | ☐ čeká |

## D. Dodat

| # | Co | Kde to vezmu | Stav |
|---|---|---|---|
| D1 | Odpověď pisatele komentáře | Komentáře u AFTERLIFE na Nexus Mods: snímek maximalizovaného okna, rozlišení a měřítko, příklad vypnutého modu, názvy ZIPů, pohledy, které chce. Vlož ji do vlákna, které na AFTERLIFE pracuje. | ☐ čeká |
| D2 | Zpráva, že Nexus Mods schválil nahranou verzi | Stránka AFTERLIFE na Nexus Mods. Až schválení přijde, napiš to do vlákna: spustí se balík O9 (instalátor). | ☐ čeká |

## Hotovo

| Kdy | Co |
|---|---|
| 22. 11. | Balení: zůstává složka v zipu, k jednomu samorozbalovacímu exe se nevracíme. Po schválení na Nexusu přibude samostatný instalátor jako druhé stažení (balík O9, čeká na D2). |
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
| 21. 11. | C1 a C5: do pracovních větví na GitHubu se zapisuje; do `main` nic bez tvého pokynu. |
| 21. 11. | Rozbor komentáře z Nexus Mods proti kódu a anglické otázky pro pisatele. |
