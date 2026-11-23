# AFTERLIFE: co čeká na tebe

Naposledy upraveno: 23. 11. 2026. Když něco vyřídíš, napiš číslo úkolu (třeba „A18 hotovo“ nebo „C11 první možnost“) v kterémkoli vláknu.
Další volná čísla: A22 B10 C12 D4 · balíky S1 O22 F4

**Stav (23. 11.):** verze **1.0.0-beta.1 je vydaná** na GitHubu (<https://github.com/silentbobcz/afterlife/releases/tag/v1.0.0-beta.1>): instalátor a zip plné edice a Offline zip, každý se `.sha256`. Všechno je v `main`, žádná pracovní větev nezůstala.

**Fronta modelů:** třetí dávka je uzavřená a v `archiv/FRONTA-3-davka.md`; nová dávka zatím není (soubor `FRONTA.md` vznikne s ní). Odložené náměty: přesun knihovny do jiné složky z aplikace, sjednocení „selected“ a „checked“ v návodu, hlášky s „mod(s)“, fronta stránek pro účty zdarma u chybějících modů z load listu.

**Námět (23. 11., od uživatele): analýza chyby redscriptu.** Spadne-li překlad redscriptu, přestanou fungovat **všechny** skriptové mody, ale AFTERLIFE o tom neví a uživatel vidí jen poruchu nějakého jiného modu. Chyba je přitom čitelná: `<hra>/r6/logs/redscript_rCURRENT.log` končí oddílem `REDScript compilation has failed` a jmenovitým seznamem „caused by“ modů. Námět: po spuštění hry (nebo tlačítkem ANALYZE REDSCRIPT ERROR) log přečíst, viníka najít v knihovně a nabídnout vypnutí; co model nerozluští sám, vypsat jako hotový dotaz pro AI, který si uživatel zkopíruje. Nedělat to záplatou v `FrameworkCatalog.InferFromContent` (pravidlo na jeden typ v textu skriptu) — to řeší jeden mod, ne třídu problému.

**Námět (23. 11., od uživatele): kategorie NEW pro nově nainstalované mody.** Dnes nový mod padá na konec volného bloku MODS a mezi stovkami modů se ztratí. Námět: každý nově nainstalovaný mod spadne do kategorie NEW, která je ve výchozím stavu úplně nahoře; uživatel ji může posunout jako jinou kategorii a mody z ní přetahuje, kam patří. Prázdná NEW se schová, pozici si pamatuje a s dalším novým modem se objeví na stejném místě (rozhodnuto 23. 11.). Před návrhem ověřit: jak NEW ovlivní pořadí načítání (kategorie nese pořadí, NEW nahoře nesmí potichu přeskládat načítání), co s mody z importu load listu, aktualizací existujícího modu (ta do NEW nepatří) a s instalací více modů najednou (kolekce).

## A. Vyzkoušet

| # | Co | Kde to najdu | Co mám udělat | Stav |
|---|---|---|---|---|
| A21 | Web: dvě edice na úvodní stránce a návod k přechodu na novou verzi (O18, O19) | <https://afterlife.silentbob.cz> (obnoví se sám) a <https://afterlife.silentbob.cz/guide.html#updating-afterlife> | 1. Úvodní stránka: nad tlačítky dva rámečky (ON GITHUB plná edice, ON NEXUS MODS Offline), první tlačítko vede na GitHub. Vypadá to dobře i na telefonu? 2. Přečti oddíl Updating AFTERLIFE v návodu: je z něj jasné, že stará složka se nemaže, když je v ní knihovna? Neověřeno: vzhled rámečků pohledem (web se sestavil bez chyb). | ☐ čeká na tebe |

## D. Dodat

| # | Co | Kde to vezmu | Stav |
|---|---|---|---|
| D1 | Odpověď pisatele komentáře | Komentáře u AFTERLIFE na Nexus Mods: snímek maximalizovaného okna, rozlišení a měřítko, příklad vypnutého modu, názvy ZIPů, pohledy, které chce. Vlož ji do vlákna, které na AFTERLIFE pracuje. | ☐ čeká |
| D3 | Offline zip a nový popis na Nexus Mods | Složka `D:/_AI/afterlife/AFTERLIFE-1.0.0-beta.1-Nexus`: postup je v `POSTUP.md`, zip k nahrání, popis stránky, shrnutí, changelog a připnutý příspěvek jsou připravené. Na Nexus jen zip, nikdy exe. | ☐ čeká |

## Hotovo

Starší řádky jsou v [archiv/UKOLY-hotovo.md](archiv/UKOLY-hotovo.md).

| Kdy | Co |
|---|---|
| 23. 11. | **1.0.0-beta.1 vydána** (C8, C11): sloučeno vše včetně lišty se zaškrtnutými mody a nabídky LIST & ORDER, tag `v1.0.0-beta.1`, vydání má instalátor a zip plné edice a Offline zip. Třetí dávka fronty je uzavřená a v archivu. |
| 23. 11. | A18 (lišta APPLY při zaškrtnutých modech), A19 (nabídka LIST & ORDER) a A20 (instalátor) zkontrolovány a schváleny. Instalátor ani odinstalátor nemažou knihovnu ani nastavení; ověřeno i zkušební instalací a odinstalací. |
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
