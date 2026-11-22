# AFTERLIFE: fronta pro modely (3. dávka)

Založeno 23. 11. 2026 po uzavření 2. dávky. **Nejnovější stav: větev `main`.** Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md), úkoly pro uživatele v [`UKOLY.md`](UKOLY.md). Starší dávky jsou v `archiv/` (jen archiv, stav z nich nečti).

Spouštěcí věta: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev main) a udělej další balík.“

## Pravidla dávky

- Pracuj ve své pracovní větvi založené z `main` a odesílej do ní. Do `main` slučuje jen uživatelův pokyn; tag a vydání dělá uživatel.
- Sestavení pro uživatele jen na pokyn. Testy a překlad: na počítači uživatele není .NET 10 SDK, pouští se `gh workflow run build.yml --ref <větev>`; ruční běh vyrobí i zkušební zip plné edice (artefakt `AFTERLIFE-full-test`).
- Co nešlo vidět běžet: v předávce „Neověřeno“ a uživateli položka A v `UKOLY.md`.
- Každý zápis do logu jde přes `ActivityLog` nebo chybový log okna.
- Předávka pod balíkem, nejvýš deset řádků. Čísla balíků pokračují: další je O18.

## Stav balíků

Zatím žádný balík. Další vzniknou z toho, co uživatel najde při zkoušení (A15, A16 v `UKOLY.md`).

## Co do fronty nepatří

- Přestavba celého okna (návrhy W1 až W3) a styl MUTED: odložené alternativy.
- Samostatný instalátor: zrušeno 22. 11.
- Hlášky ve stavovém řádku s „mod(s)“: drobnost k příští úpravě textů.
- Fronta stránek pro účty zdarma u chybějících modů z load listu (dnes jen odkazy): až o ni uživatel řekne.

## Co přinesla 2. dávka (22. až 23. 11.)

Nastavení v záložkách (O8), volné pořadí modů s čísly, A→Z, klávesami a LOAD BEFORE (O10 až O12), fialová tlačítka FILTERS a SORT (O16), export a import load listu (O14, O15), nový dialog při vypnutí modu s opraveným výpočtem (O13, O17). Podrobnosti v `CHANGELOG.md` a `docs/HOW-IT-WORKS.md`.
