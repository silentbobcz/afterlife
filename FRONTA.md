# AFTERLIFE: fronta pro modely (3. dávka)

Založeno 23. 11. 2026 po uzavření 2. dávky. **Nejnovější stav: větev `main`.** Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md), úkoly pro uživatele v [`UKOLY.md`](UKOLY.md). Starší dávky jsou v `archiv/` (jen archiv, stav z nich nečti).

Spouštěcí věta: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev main) a udělej balík O20.“

**Změna 23. 11.:** na GitHubu se od příštího vydání vydává **plná edice** (zip a instalátor) a vedle ní zip **AFTERLIFE Offline**, který se nahrává na Nexus Mods. Na Nexusu zůstává jen Offline. Instalátor jen pro plnou edici; Offline ho nemá.

## Pravidla dávky

- Pracuj ve své pracovní větvi založené z `main` a odesílej do ní. Do `main` slučuje jen uživatelův pokyn; tag a vydání dělá uživatel.
- Sestavení pro uživatele jen na pokyn. Testy a překlad: na počítači uživatele není .NET 10 SDK, pouští se `gh workflow run build.yml --ref <větev>`; ruční běh vyrobí i zkušební zip plné edice (artefakt `AFTERLIFE-full-test`).
- Co nešlo vidět běžet: v předávce „Neověřeno“ a uživateli položka A v `UKOLY.md`.
- Každý zápis do logu jde přes `ActivityLog` nebo chybový log okna.
- Předávka pod balíkem, nejvýš deset řádků. Čísla balíků pokračují: další volné je O22.

## Stav balíků

| Balík | Model | Co | Čeká na | Stav |
|---|---|---|---|---|
| O20 | Opus | Vydání plné edice na GitHubu: zip a instalátor plné edice, zip Offline pro Nexus | nic | může začít |
| O18 | Sonnet | Web: dvě edice ke stažení (GitHub plná, Nexus Offline) a proč | O20 (názvy souborů) | čeká |
| O19 | Sonnet | Návod: jak přejít na novou verzi (zip i instalátor), knihovna a nastavení zůstávají | O20 | čeká |
| O21 | Opus | Zaškrtnuté mody schovají APPLY a JACK IN: vysvětlit proč a nabídnout cestu zpět | nic | ✅ zkontrolováno, větev `o21-checked-mods`, čeká na sloučení |

Další balíky vzniknou z toho, co uživatel najde při zkoušení (A15 až A17 v `UKOLY.md`).

## O20. Vydání plné edice na GitHubu s instalátorem

Opus. Může začít hned. Uživatel 23. 11.: „vydáváme plnou verzi + instalátor, offline.zip pro Nexus“. Ruší se pravidlo z alpha.9, že vydání na GitHubu nese jen Offline, a rozhodnutí z 22. 11., že instalátor nebude.
1. Výchozí bod je vrácený commit `0f81aea` (instalátor Inno Setup pro uživatele bez admina, `installer/AFTERLIFE.iss`, krok ve `build.yml`, úprava `AppSettings.cs`, aby nainstalovaná kopie nenabízela knihovnu uvnitř složky, kterou spravuje odinstalátor). Obnov z něj jen tyto soubory: `git checkout 0f81aea -- installer/AFTERLIFE.iss`, a změny v `.github/workflows/build.yml`, `src/Afterlife.Infrastructure/AppSettings.cs` a `tests/Afterlife.Tests/RequirementTests.cs` přenes ručně podle `git show 0f81aea` (soubory se od té doby změnily). `git revert 3c83efe` nepoužívej: vrátil by do `FRONTA.md` a `UKOLY.md` zrušený balík O9 a skončil konfliktem. Texty v `README.md`, `docs/BUILDING.md`, `docs/USER-GUIDE.md` a `CHANGELOG.md` piš nově podle bodu 4. Pak přizpůsob: instalátor se staví ze složky **plné edice**, ne z Offline; název `AFTERLIFE-<verze>-win-x64-Setup.exe`. `AppId` v `.iss` neměň.
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

Sonnet. Čeká na O20. Podnět: uživatel na Nexusu měl potíže přenést mody do novější verze. Fakta: nastavení je v `%APPDATA%\AFTERLIFE\settings.json` (`AppSettings.cs`), logy v `%LOCALAPPDATA%\AFTERLIFE\logs`. **Knihovna bývá ve složce aplikace:** první spuštění ji navrhuje vedle `AFTERLIFE.exe` jako `AFTERLIFE Library` (`AppSettings.SuggestLibrary`, `FirstRunVm.cs`), jinde jen když ta složka nejde použít nebo si uživatel vybral jinou. Kdo starou složku aplikace smaže, smaže si mody; to je nejspíš i potíž pisatele z Nexusu. Cesta ke knihovně je uložená v nastavení (`LibraryRoot`), takže nová kopie rozbalená jinam knihovnu ve staré složce najde. Ověř v kódu, ne z tohoto odstavce.
1. Do `docs/USER-GUIDE.md` oddíl „Updating AFTERLIFE“. Začni tím, jak zjistit, kde knihovna je (Settings, cesta ke knihovně: ověř přesný název). Zip: bezpečný postup je zavřít aplikaci a rozbalit nový zip přes starou složku (soubory aplikace se přepíšou, `AFTERLIFE Library` zůstane); starou složku mazat jen tehdy, když v ní knihovna není, a napiš to jako varování. Instalátor: novější se nainstaluje přes starší. Přechod ze zipu na instalátor: knihovna zůstává ve staré složce zipu a aplikace ji dál čte odtud, složku tedy nemazat; popiš, zda a jak jde knihovna přesunout (ověř v kódu, co aplikace nabízí; pokud nic, napiš to do předávky jako nález, nevymýšlej ruční postup). Dál co zůstává (knihovna, profily, pořadí, kategorie, nastavení, klíč) a jak si pro jistotu uložit pořadí (EXPORT ORDER). Přechod ze zipu na instalátor a mezi plnou edicí a Offline (obě čtou stejné nastavení i knihovnu: ověř).
2. Krátký odkaz na oddíl v `README.md` (Quick start) a v FAQ na webu (`site/`), `npm run build` jako v O18.
3. Jen dokumentace, žádný kód; řádek do `CHANGELOG.md` jen pokud se mění text v aplikaci.

## O21. Zaškrtnuté mody schovají APPLY a JACK IN

Opus. Může začít hned. Hlášení z Nexusu (23. 11.): po přidání dalších modů „zmizelo“ APPLY a nešlo kliknout na JACK IN; ve spodní liště byly jen MOVE TO CATEGORY, ACTIVATE, DEACTIVATE a DELETE EVERYWHERE. Příčina: jakmile je zaškrtnutý aspoň jeden mod (`HasMultiSelection = _multi.Count > 0`, `UpdateChecked` v `MainViewModel.Library.cs`), lišta hromadných akcí v `MainWindow.xaml` (oddíl „Bulk actions“) nahradí lištu APPLY. Zaškrtnutí vzniká i nechtěně: Ctrl+klik a Shift+klik na řádek (`OnRowPreviewMouseDown` v `MainWindow.xaml.cs`), Ctrl+A; výběr navíc přežije obnovu seznamu (`keepMulti` v `MainViewModel.cs`), i když jsou zaškrtnuté řádky schované filtrem, hledáním nebo sbalenou kategorií. Cíl: uživatel vždy ví, proč APPLY nevidí, a jedním krokem se k němu vrátí. Rozložení okna se nemění.
1. Text v liště hromadných akcí: místo „N mods selected“ „N mods checked“ a pod tím ve stylu `Meta` „APPLY and JACK IN come back when nothing is checked“ (kratší znění smíš zvolit, musí se vejít do úzkého okna). Odkaz „Clear selection“ přejmenuj na „Clear selection (Esc)“.
2. Schované zaškrtnuté: když část zaškrtnutých řádků neprojde filtrem nebo hledáním, nebo leží ve sbalené kategorii, připiš „(K not visible)“ s tooltipem proč. Počítej ve viewmodelu, ne v XAML.
3. Esc zruší výběr. Pořadí v obsluze kláves v `MainWindow.xaml.cs`: zavřít dialog → vymazat hledání (má-li fokus) → zrušit výběr. Esc výběr neruší, když je otevřený dialog nebo se píše do textového pole.
4. Bez nového dialogu, vyskakovacího okna a zápisů do logu; stačí text v liště.
5. `MainWindow.xaml` má přes 800 řádků: do něj jen úprava textů a vazeb v existující liště. Nový kód (počet schovaných, Esc) do nového souboru vedle, například `MainViewModel.Selection.cs`.
6. Test v `tests/Afterlife.Tests` pro počet schovaných zaškrtnutých (filtr, hledání), pokud jde výpočet oddělit od WPF. Řádek do `CHANGELOG.md`; do `docs/USER-GUIDE.md` (oddíl o výběru modů) jedna věta: zaškrtnuté mody přepnou spodní lištu na hromadné akce, Esc nebo Clear selection vrátí APPLY. `USER-GUIDE.md` upravuje i O19 a `CHANGELOG.md` i O20: při sloučení dej pozor na konflikt.
7. Kontroly přes `gh workflow run build.yml --ref <větev>`. Okno neuvidíš: do `UKOLY.md` položka A (zaškrtnout dva mody, jeden schovat filtrem, zkontrolovat text a počet, Esc vrátí APPLY).

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
- Hlášky ve stavovém řádku s „mod(s)“: drobnost k příští úpravě textů.
- Fronta stránek pro účty zdarma u chybějících modů z load listu (dnes jen odkazy): až o ni uživatel řekne.

## Co přinesla 2. dávka (22. až 23. 11.)

Nastavení v záložkách (O8), volné pořadí modů s čísly, A→Z, klávesami a LOAD BEFORE (O10 až O12), fialová tlačítka FILTERS a SORT (O16), export a import load listu (O14, O15), nový dialog při vypnutí modu s opraveným výpočtem (O13, O17). Podrobnosti v `CHANGELOG.md` a `docs/HOW-IT-WORKS.md`.
