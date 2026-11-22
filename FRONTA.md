# AFTERLIFE: fronta pro modely (3. dávka)

Založeno 23. 11. 2026 po uzavření 2. dávky. **Nejnovější stav: větev `main`.** Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md), úkoly pro uživatele v [`UKOLY.md`](UKOLY.md). Starší dávky jsou v `archiv/` (jen archiv, stav z nich nečti).

Spouštěcí věta: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev main) a udělej balík O18.“

## Pravidla dávky

- Pracuj ve své pracovní větvi založené z `main` a odesílej do ní. Do `main` slučuje jen uživatelův pokyn; tag a vydání dělá uživatel.
- Sestavení pro uživatele jen na pokyn. Testy a překlad: na počítači uživatele není .NET 10 SDK, pouští se `gh workflow run build.yml --ref <větev>`; ruční běh vyrobí i zkušební zip plné edice (artefakt `AFTERLIFE-full-test`).
- Co nešlo vidět běžet: v předávce „Neověřeno“ a uživateli položka A v `UKOLY.md`.
- Každý zápis do logu jde přes `ActivityLog` nebo chybový log okna.
- Předávka pod balíkem, nejvýš deset řádků. Čísla balíků pokračují: další volné je O19.

## Stav balíků

| Balík | Model | Co | Čeká na | Stav |
|---|---|---|---|---|
| O18 | Sonnet | Web: viditelné upozornění, že ke stažení je AFTERLIFE Offline, a proč | nic | může začít |

Další balíky vzniknou z toho, co uživatel najde při zkoušení (A15 až A17 v ).

## O18. Web: upozornění na edici Offline

Sonnet. Může začít hned. Uživatel 23. 11.: na webu <https://afterlife-13h.pages.dev> chybí jasná poznámka, že ke stažení (na Nexusu i na GitHubu) je **AFTERLIFE Offline**, a proč. Dnes  hned pod úvodem propaguje Premium a přímé stahování přes API; jediná zmínka o Offline je malá poznámka  až pod tabulkou Premium.
1. V  hned pod úvodní odstavec a nad tlačítka ke stažení dej výrazný blok (nový styl v , jen tokeny z , například žlutý okraj): stahuje se AFTERLIFE Offline; proč: Nexus Mods dovoluje programům používat své API, až aplikaci zaregistruje, a do té doby se AFTERLIFE k Nexusu vůbec nepřipojuje; co funguje (vše, co pracuje se soubory na disku) a co je se zámkem (stahování, aktualizace, kolekce). Dvě až čtyři věty, odkaz do FAQ ().
2. Oddíl „Better with Nexus Mods Premium“ přepiš v budoucím čase („once the full edition is out…“) a přesuň pod oddíl „What it does“ s nadpisem, ze kterého je jasné, že jde o plánovanou plnou edici. Spodní poznámku pak smaž, ať se neopakuje. Důraz na výhody Premium zůstává (přání uživatele), jen nesmí vypadat jako popis toho, co se dnes stahuje.
3. Stejné ověření v  (oddíl ke stažení) a v  na začátku: krátká věta o Offline nahoře, pokud tam ještě není; fakta ber z FAQ, nic nevymýšlej.
4. added 1 package, and audited 2 packages in 740ms

found 0 vulnerabilities, zkontroluj  v prohlížeči (i úzké okno). Po sloučení do  se web obnoví sám. Text pro popis na Nexusu se stejným sdělením dej do předávky; vkládá ho uživatel.

## Co do fronty nepatří

- Přestavba celého okna (návrhy W1 až W3) a styl MUTED: odložené alternativy.
- Samostatný instalátor: zrušeno 22. 11.
- Hlášky ve stavovém řádku s „mod(s)“: drobnost k příští úpravě textů.
- Fronta stránek pro účty zdarma u chybějících modů z load listu (dnes jen odkazy): až o ni uživatel řekne.

## Co přinesla 2. dávka (22. až 23. 11.)

Nastavení v záložkách (O8), volné pořadí modů s čísly, A→Z, klávesami a LOAD BEFORE (O10 až O12), fialová tlačítka FILTERS a SORT (O16), export a import load listu (O14, O15), nový dialog při vypnutí modu s opraveným výpočtem (O13, O17). Podrobnosti v `CHANGELOG.md` a `docs/HOW-IT-WORKS.md`.
