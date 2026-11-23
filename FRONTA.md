# AFTERLIFE: fronta pro modely (3. dávka)

Založeno 23. 11. 2026 po uzavření 2. dávky. **Nejnovější stav: větev `main`.** Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md), úkoly pro uživatele v [`UKOLY.md`](UKOLY.md). Starší dávky jsou v `archiv/` (jen archiv, stav z nich nečti).

Spouštěcí věta: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev main) a udělej balík O20.“

**Změna 23. 11.:** na GitHubu se od příštího vydání vydává **plná edice** (zip a instalátor) a vedle ní zip **AFTERLIFE Offline**, který se nahrává na Nexus Mods. Na Nexusu zůstává jen Offline. Instalátor jen pro plnou edici; Offline ho nemá.

## Pravidla dávky

- Pracuj ve své pracovní větvi založené z `main` a odesílej do ní. Do `main` slučuje jen uživatelův pokyn; tag a vydání dělá uživatel.
- Sestavení pro uživatele jen na pokyn. Testy a překlad: na počítači uživatele není .NET 10 SDK, pouští se `gh workflow run build.yml --ref <větev>`; ruční běh vyrobí i zkušební zip plné edice (artefakt `AFTERLIFE-full-test`).
- Co nešlo vidět běžet: v předávce „Neověřeno“ a uživateli položka A v `UKOLY.md`.
- Každý zápis do logu jde přes `ActivityLog` nebo chybový log okna.
- Předávka pod balíkem, nejvýš deset řádků. Čísla balíků pokračují: další volné je O21.

## Stav balíků

| Balík | Model | Co | Čeká na | Stav |
|---|---|---|---|---|
| O20 | Opus | Vydání plné edice na GitHubu: zip a instalátor plné edice, zip Offline pro Nexus | nic | může začít |
| O18 | Sonnet | Web: dvě edice ke stažení (GitHub plná, Nexus Offline) a proč | O20 (názvy souborů) | čeká |
| O19 | Sonnet | Návod: jak přejít na novou verzi (zip i instalátor), knihovna a nastavení zůstávají | O20 | čeká |

Další balíky vzniknou z toho, co uživatel najde při zkoušení (A15 až A17 v `UKOLY.md`).

## O20. Vydání plné edice na GitHubu s instalátorem

Opus. Může začít hned. Uživatel 23. 11.: „vydáváme plnou verzi + instalátor, offline.zip pro Nexus“. Ruší se pravidlo z alpha.9, že vydání na GitHubu nese jen Offline, a rozhodnutí z 22. 11., že instalátor nebude.
1. Výchozí bod je vrácený commit `0f81aea` (instalátor Inno Setup pro uživatele bez admina, `installer/AFTERLIFE.iss`, krok ve `build.yml`, úprava `AppSettings.cs`, aby nainstalovaná kopie nenabízela knihovnu uvnitř složky, kterou spravuje odinstalátor). Obnov ho (`git revert 3c83efe` nebo ručně) a přizpůsob: instalátor se staví ze složky **plné edice**, ne z Offline; název `AFTERLIFE-<verze>-win-x64-Setup.exe`. `AppId` v `.iss` neměň.
2. `build.yml`: při tagu vyrob tři soubory, každý se `.sha256`: `AFTERLIFE-<verze>-win-x64.zip` (plná edice, složka `AFTERLIFE`), `AFTERLIFE-<verze>-win-x64-Setup.exe` a `AFTERLIFE-Offline-<verze>-win-x64.zip` (pro Nexus, beze změny obsahu). Kontrola, že plná a Offline sestava nejsou totožné, zůstává. Ruční běh dál vyrábí zkušební artefakt `AFTERLIFE-full-test`.
3. Text vydání v `build.yml`: co stáhnout (instalátor doporučeně, nebo zip plné edice), že Offline zip je táž aplikace bez připojení k Nexusu a je to edice na Nexus Mods, SmartScreen a `.sha256` jako dosud. Instalátor ani odinstalátor nikdy nesmaže knihovnu ani nastavení.
4. Přepiš, co tvrdí „releases carry only AFTERLIFE Offline“: komentář v `build.yml` a `AppEdition.cs`, `README.md` (oddíl o Offline a Quick start: instalátor nebo zip), `docs/BUILDING.md`, `PRIVACY.md` (plná edice s klíčem volá Nexus: ověř, že popis sedí), FAQ v `site/` jen věcně (web celý řeší O18). Řádek do `CHANGELOG.md` pod nejvyšší nadpis.
5. Verzi ani tag neměň. Pusť `gh workflow run build.yml --ref <větev>` a ověř, že běh vyrobí instalátor; instalaci ve Windows nikdo neviděl, takže uživateli položka A: nainstalovat, spustit, najde knihovnu a hru, přeinstalovat novější sestavou, odinstalovat (knihovna zůstane).

## O18. Web: dvě edice ke stažení

Sonnet. Čeká na O20 (názvy souborů a text vydání). Uživatel 23. 11.: na webu <https://afterlife-13h.pages.dev> musí být jasné, co se kde stahuje. Od změny 23. 11.: na GitHubu plná edice (instalátor nebo zip) a Offline zip, na Nexus Mods jen **AFTERLIFE Offline**. Dnes `site/home.md` propaguje Premium a přímé stahování přes API; zmínka o Offline je malá poznámka `<p class="note">` pod tabulkou Premium.
1. V `site/home.md` hned pod úvodní odstavec a nad tlačítka ke stažení dej výrazný blok (nový styl v `site/style.css`, jen tokeny z `App.xaml`): plná edice z GitHubu (instalátor doporučeně, nebo zip), s klíčem k Nexusu umí stahovat, aktualizovat a kolekce; na Nexus Mods je AFTERLIFE Offline, která se k Nexusu nepřipojuje, a proč (Nexus dovoluje programům API, až aplikaci zaregistruje). Dvě až čtyři věty, odkaz do FAQ (`faq.html#what-is-afterlife-offline-and-why-do-some-buttons-have-a-lock`).
2. Oddíl „Better with Nexus Mods Premium“ zůstává a platí pro plnou edici; spodní poznámku o Offline sjednoť s blokem z bodu 1, ať se neopakuje.
3. Totéž ověř v `README.md` (oddíl ke stažení) a na začátku `docs/USER-GUIDE.md`; fakta ber z FAQ a z textu vydání v `build.yml`, nic nevymýšlej.
4. Ve složce `site` spusť `npm ci` a `npm run build`, zkontroluj `dist/index.html` v prohlížeči (i úzké okno). Po sloučení do `main` se web obnoví sám. Text pro popis na Nexusu (Offline, odkaz na plnou edici na GitHubu) dej do předávky; vkládá ho uživatel.

## O19. Návod: přechod na novou verzi

Sonnet. Čeká na O20. Podnět: uživatel na Nexusu měl potíže přenést mody do novější verze. Fakta: nastavení je v `%APPDATA%\AFTERLIFE\settings.json` (`AppSettings.cs`), knihovna ve složce, kterou si uživatel zvolil, logy v `%LOCALAPPDATA%\AFTERLIFE\logs`; nic z toho není ve složce aplikace. Ověř v kódu, ne z tohoto odstavce.
1. Do `docs/USER-GUIDE.md` oddíl „Updating AFTERLIFE“: instalátor (spustit novější, nainstaluje přes starou), zip (rozbalit novou složku kamkoli, starou smazat až po prvním spuštění nové), co zůstává (knihovna, profily, pořadí, kategorie, nastavení, klíč) a jak si pro jistotu uložit pořadí (EXPORT ORDER). Přechod ze zipu na instalátor a mezi plnou edicí a Offline (obě čtou stejné nastavení i knihovnu: ověř).
2. Krátký odkaz na oddíl v `README.md` (Quick start) a v FAQ na webu (`site/`), `npm run build` jako v O18.
3. Jen dokumentace, žádný kód; řádek do `CHANGELOG.md` jen pokud se mění text v aplikaci.

## Co do fronty nepatří

- Přestavba celého okna (návrhy W1 až W3) a styl MUTED: odložené alternativy.
- Instalátor pro AFTERLIFE Offline: není třeba, na Nexus jde zip (23. 11.).
- Hlášky ve stavovém řádku s „mod(s)“: drobnost k příští úpravě textů.
- Fronta stránek pro účty zdarma u chybějících modů z load listu (dnes jen odkazy): až o ni uživatel řekne.

## Co přinesla 2. dávka (22. až 23. 11.)

Nastavení v záložkách (O8), volné pořadí modů s čísly, A→Z, klávesami a LOAD BEFORE (O10 až O12), fialová tlačítka FILTERS a SORT (O16), export a import load listu (O14, O15), nový dialog při vypnutí modu s opraveným výpočtem (O13, O17). Podrobnosti v `CHANGELOG.md` a `docs/HOW-IT-WORKS.md`.
