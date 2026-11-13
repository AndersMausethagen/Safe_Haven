# Designgjennomgang: Safe Haven målt mot Game Design Specification v0.1

**Dato:** 13. november 2026
**Hva som er vurdert:** den nye spesifikasjonen (`Safe_Haven_Game_Design_Specification.md`, v0.1, konseptutkast) mot spillet slik det er bygget i dag.
**Grunnlag:** spesifikasjonen (hele), `Docs/GameDesignDocument.md` (GDD v0.4), `Docs/BaseProgression.md`, `Docs/RescueOrder.md`, `Docs/Territory.md`, `Docs/GearScoreAndRaidTiers.md`, `Docs/HavenWorldDesign.md`, `Docs/TODO.md`, `Assets/_Project/Scripts/ARCHITECTURE.md`, kode og data under `Assets/_Project`, og eierens beslutninger i minnenotatene.
**Begrensninger:** kun lesing. Spillet er ikke spilt, Unity-editoren var lukket, ingen tester er kjørt. Playtest-loggene er bare sett grovt på. Der jeg bare har dokumentasjon å gå etter og ikke har funnet det i koden, står det **(ubekreftet)**.

**Leseveiledning**

- `Scripts/` betyr `Assets/_Project/Scripts/`, `Data/` betyr `Assets/_Project/Data/`, `Resources/` betyr `Assets/_Project/Resources/`.
- «Spesifikasjonen» er den nye GDS-en. «GDD» er `Docs/GameDesignDocument.md`.
- Status i tabellene: **Bygget**, **Delvis**, **Mangler**, **Annerledes** (bygget, men på en annen måte enn spesifikasjonen sier).
- Kapittel 2 og 3 er fakta: hva spesifikasjonen sier og hva som finnes. Kapittel 4 til 8 er mine vurderinger, og jeg skriver det der jeg mener noe.

---

## 1. Kort sammendrag

**Hovedkonklusjon:** Spesifikasjonen beskriver i stor grad et spill Safe Haven allerede er. Portalen står i midten med en Ward rundt seg. Byggingen er hybrid, med fritt plasserte hovedbygninger og gjerder og murer som snapper. Bygninger låser opp funksjoner. Overlevende som reddes blir værende og lærer spilleren bygningene sine. Varslede baseangrep spilles aktivt og gir skader som kan repareres, og raidene driver progresjonen. Målt i bredde er spillet allerede forbi spesifikasjonens MVP (§15.1). Alle MVP-punktene finnes i en eller annen form: portal med flere nivåer, Workshop, Smithy, Kitchen og Watch Post, Carpenter, Blacksmith og Cook, egne rumours for redning og boss, ett varslet angrep med NPC-forsvarere, et automatisk vakttårn og bygningsskade som repareres.

**Men spesifikasjonen og spillet skiller lag på fem viktige punkter:**

1. **Spesifikasjonen nevner ikke det GDD-en kaller hjertet i spillet:** tidsbegrensede raids der man må ut i tide, og der man kan miste det man bærer. Den sier ingenting om klokke, Portal Sparks, extraction-punkter eller tapsregler ved død. Enten er dette et bevisst valg, eller så er spesifikasjonen skrevet uten GDD-en ved siden av. Det må avklares først (spørsmål 8.1).
2. **Det finnes ingen felles tier-ryggrad.** Spesifikasjonen vil at portalens Tier 0 til 4 skal binde alt sammen. Spillet har omtrent åtte parallelle stiger: gear-tier I-V, raid-tier I-IV, ward-nivå 0-III, Haven-rang (renown), bygningstier I-V, portalforbedringer, ferdighetsnivå 1-50 og arbeiderrang. I tillegg kommer territorium og hold.
3. **Angrep på hjemmet kommer fra fire systemer som overlapper:** ward breaches, corruption nights, territoriets pushes med vakthold ved steinene, og night prowlers med blight. Corruption nights blir hyppigere og større jo høyere Haven-rangen er, og rangen kommer fra bygninger. Det bryter direkte med spesifikasjonens prinsipp 11 («Ikke straff spilleren for å bygge»).
4. **NPC-ene blir bedre passivt.** Arbeidere stiger fra Novice til Master ved å telle det de lager selv (`SettlementSettings.workerRanks`). Spesifikasjonen sier rett ut at dette ikke skal skje (§7.3).
5. **Flere av spesifikasjonens bygninger og roller finnes ikke:** Infirmary, Scout Lodge (rollen ligger delvis i Watch Post), Common Hall, Guard Post eller Barracks, Community Morale og egne fiendetyper i forsvaret.

**Den største risikoen er ikke funksjoner som mangler, men at nesten ingenting er spilt.** Systemene er bygget og testet med regler og prober, men nesten ikke spilt for hånd. Dette sier dokumentasjonen selv: «unplayed by hand» står om hester, quests, territorium, raid-følgesvenner og redningsrekkefølgen. TODO-en fra 4. november sier at ingen raid uten juks varte over 175 sekunder mot en klokke på 16:40. De fem valideringsspørsmålene i spesifikasjonen (§15.2) kan ikke besvares i dag. Samtidig er kodebasen på rundt 173 000 linjer kjøretidskode og 88 000 linjer editorkode, og den har co-op for fire spillere. Det er en stor omfangsrisiko for én utvikler.

**Anbefalingen i én setning:** Slutt å gjøre spillet bredere en stund. Gjør portalens tier til den synlige ryggraden. Skjær angrepene på hjemmet ned til én tydelig modell. Gjør NPC-progresjonen milepælsbasert. Og spill spesifikasjonens slice for hånd (nytt spill fram til det første ekte forsvaret og ward-nivå I) før det bygges mer.

---

## 2. Hva spesifikasjonen sier

En kort gjengivelse kapittel for kapittel, så eieren kan se at jeg har forstått den.

| § | Innhold i korthet |
|---|---|
| 1 Visjon | Isometrisk action-RPG: utforskning, scavenging, instanced raids, karakterprogresjon og en permanent bosetning rundt en ødelagt portal. Kjerneidé: *Build a home worth defending.* Fem systemer som støtter hverandre: Settlement, Community, Production, Defense og Expeditions. |
| 2 Grunnvalg | Base og raids er hver sin instans. NPC-er er permanente. Byggingen er hybrid: fritt plasserte hovedbygninger og infrastruktur på grid eller snap. Baseforsvaret er aktivt og ikke tower defense. Alt kan justeres etter prototyping. |
| 3 Hovedloop | Explore & Raid → Return → Build & Upgrade → Craft & Prepare → Defend → Expand. Hvert raid skal gi minst én meningsfull progresjon. |
| 4 Portal og Ward | Portalen er kjernen med fire funksjoner: Ward (byggeareal), Protection (mot korrupsjon), Expeditions og Progression. Wardens radius bestemmer hvor man kan bygge. Portalen har Tier 0-4: Dormant, Awakened, Stabilized, Restored og Reclaimed. Oppgraderinger skal kreve mer enn ressurser: bygninger, NPC-er, nøkkelfunn, essences og historie. |
| 5 Bygging | Tre kategorier: hovedbygninger (definerte bygg, fri plassering og rotasjon, oppgraderes på stedet, flyttes uten tap), infrastruktur (grid eller snap) og dekorasjon (billig og mest kosmetisk, eventuelt med litt morale). Ugyldig plassering skal vises tydelig, og man skal ikke måtte løpe unødvendig. |
| 6 Bygninger | Portal/Ward og Workshop er obligatoriske. Smithy og Kitchen kommer tidlig, Scout Lodge er anbefalt, og Alchemist, Tannery, Infirmary og Common Hall er valgfrie. Hver bygning har sin NPC. Valgfrie bygg skal ikke være irreversible valg. |
| 7 NPC-er | Permanente, de gir funksjoner og kompetanse, og noen kan bli med som følgesvenner. Kompetansen har tre nivåer: Apprentice, Craftsman og Master. Progresjonen skal drives av handlinger og milepæler, aldri passiv venting eller XP fra å stå på jobb. Spilleren tar med en begrenset gruppe på raid. Kompetansen i basen forsvinner ikke når NPC-en er borte på raid. |
| 8 Baseforsvar | Aktivt og ikke tower defense. Fem faser: Warning, Preparation, Attack, Resolution og Recovery. Vanskeligheten følger portal-tier, områder, korrupsjon og historie, men ikke antall bygninger. Hver bygning har en forsvarsrolle. Fem fiendetyper: raske, tunge, portal-targeters, NPC-targeters og magiske. Enkel NPC-AI. Ingen permanent død, ingen sletting, varslet og ikke for ofte. Belønninger: Corrupted Essence, forsvarsmaterialer, oppskrifter og progresjon. |
| 9 Raids | Separate instanser. Fire typer som kan kombineres: Scavenging, Rescue, Expedition og Boss. Planleggingsskjermen viser område, vanskelighet, fiender, ressurser, mål, følgesvenn, forsyninger og kapasitet. Begrenset inventory er et strategisk valg. |
| 10 Tier 0-4 | Survival, Establishment, Development, Fortification og Restoration/Endgame, med innhold per tier. Tier 0 og 1 skal gå raskt, og nye materialer skal få en bruk kort tid etter at de er funnet. |
| 11 Første reise | Tolv steg: leir med ødelagt portal → tre, stein og planter → Workshop → redd Carpenter → planker → redd Blacksmith → Smithy → redd Cook → Kitchen → forsvarsverk → varslet angrep → essence til portalen → større Ward og nye områder. |
| 12 Community Morale | Et mulig og lett system. Det går opp med boliger, Common Hall, seire og nye NPC-er, og ned med skader, tap og mangel. Små bonuser og dialog. Det skal **ikke** bli et daglig vedlikeholdssystem som straffer spilleren for å dra på eventyr. |
| 13 Samspill | En tabell over aktivitet, umiddelbar gevinst og langsiktig effekt. Ingen aktivitet skal stå isolert. |
| 14 Prinsipper | Tolv regler. Blant dem: bygninger låser opp funksjoner, NPC-redning gir permanent verdi, NPC-progresjon skal være aktiv, korte produksjonskjeder, raids driver progresjon, aktivt forsvar, portalen binder progresjonen sammen, nederlag skaper arbeid og ikke tap, ikke straff bygging, og hvert materiale skal ha en tydelig bruk. |
| 15 MVP | Portal med to nivåer, Ward, fri plassering, Workshop, Smithy, Kitchen og Watchtower, Carpenter, Blacksmith og Cook, én instans hver for scavenging, rescue og boss, og ett varslet angrep med to fiendetyper. Fem spørsmål skal valideres: Vil spilleren tilbake? Er bygging tilfredsstillende? Er det meningsfullt å redde NPC-er? Merkes oppgraderinger? Er det morsomt å forsvare basen? |
| 16 Åpne spørsmål | Antall følgesvenner, skade på følgesvenner, raidlengde, hvor mye produksjon er automatisk, boliger, basestørrelse, hvor ofte angrep skjer, hånddesign mot prosedyre, kobling mellom karakter og tier, og gjenbruk av eksisterende data. |
| 17 Videre arbeid | Seks matriser (bygninger, NPC-er, ressurser, tiers, raids og forsvar) basert på de faktiske dataene i prosjektet, så designet blir en videreutvikling og ikke et parallelt system. |

**Merk:** Spesifikasjonen nevner ikke timer, extraction, tap ved død, survival (sult og tørst), farming, hester, transport, fiske, årstider, territorium, co-op eller ferdighetssystemet. Den bruker også navn som avviker fra spillet: «Corrupted Essence» i stedet for Cursed Essence, «Moss trolls» og «Mountain troll», «Carpenter/Artisan» ved Workshop og «Watchtower». Kombinert med §16 («Hvilke eksisterende ressurser ... kan gjenbrukes direkte?») tyder det på at den er skrevet ovenfra og ned, uten å se på GDD-en og byggingen. Det er ikke en feil, men det betyr at spesifikasjonen må **flettes inn** i det som finnes, ikke implementeres for seg.

---

## 3. Dekning: spesifikasjonen mot spillet i dag

### 3.1 Portal og Ward (§4, §6.1)

| Spesifikasjonen | Status | Merknad | Hvor |
|---|---|---|---|
| Portalen er den sentrale bygningen og stemmen i lysningen | Bygget | Portalen står i midten og har én meny for raids, ward, sparks og reparasjoner. Den snakker for alle åpne quests. | `Scripts/Settlement/HomePortal.cs`, `HomePortalMenu.cs`, `Scripts/Raids/RaidPortal.cs` |
| Ward definerer byggearealet | **Annerledes** | Siden 12. november bygges det over hele Haven Meadows (`buildReach` 165 m). Warden markerer bare den trygge sonen om natten. Maks radius er 16 m + 3 × 6 m (ward-nivåer) + 15 m (rang) = 49 m. | `HomePortal.InBuildLand`, `HomePortal.WardRadius`, GDD §6.4 (12. nov.) |
| Ward-radius vokser med oppgraderinger | Bygget | Tre ward-nivåer gir +6 m hver. Haven-rangene gir +3, 6, 10 og 15 m. | `Resources/SettlementSettings.asset` (wardLevels), `Resources/HavenSettings.asset` (ranks) |
| Protection: holder korrupsjonen borte | Annerledes | Night prowlers holder seg utenfor lyset. Corruption nights kommer uansett ward-nivå. Det som stopper korrupsjonen er territorium med status Secure, ikke portalen. | `Scripts/Settlement/CorruptionNights.cs`, `Docs/Territory.md` |
| Låser opp raidområder og vanskelighetsgrader | Bygget | Ward I, II og III åpner raid-tier II, III og IV (`wardLevelNeeded`). Troll Valley åpnes når Heartroot er beseiret. | `Scripts/Raids/RaidDifficulty.cs:20`, `Data/Raids/TrollValley.asset` (unlockedBy) |
| Portal Tier 0-4 | Delvis | Tilstandene finnes: ødelagt (3 Life Orbs for å reparere), vekket og ward I-III. Det blir fem tilstander, men de er ikke navngitt eller vist som tiers. I tillegg finnes tre egne portalforbedringer: Steadied, Bound og Moonlit (lomme og raskere extraction). | `SettlementSettings.cs` (WardLevel, PortalLevel) |
| Portalens utvikling synes | Delvis (ubekreftet) | Portalen ser annerledes ut sovende og vekket, og mosen sprer seg til den nye ward-kanten. Jeg fant ingen endring av selve portalmodellen per nivå. | `TerrainMossBloom` (ARCHITECTURE «Environment») |
| Oppgradering krever mer enn ressurser | **Mangler** | `WardLevel` har bare `cost` (Purified Essence 6/12/20, Life Orbs 0/4/6), `extraRadius`, `raidSeconds` og `opens`. Ingen krav om bygning, NPC, nøkkelobjekt eller historie. `BuildingLevel` har `requires`, men det har ikke `WardLevel`. | `Scripts/Settlement/SettlementSettings.cs:13-23` |
| Portalen er et mål under angrep | Bygget | Et Ward Anchor står ved portalen under en breach. Faller det, er forsvaret tapt. | `WardBreachSettings.wardAnchor`, `WardBreachDirector.cs` |

### 3.2 Byggesystemet (§5)

| Spesifikasjonen | Status | Merknad | Hvor |
|---|---|---|---|
| Hovedbygninger plasseres og roteres fritt | Bygget | G åpner byggemenyen. Rotasjon med hjulet, Alt gir fri plassering. | `Scripts/Building/BuildPlacer.cs`, `UI/BuildMenuWindow` |
| Innganger og interaksjonspunkter | Delvis | Gården foran inngangen holdes fri. Tolv bygg er kit-modeller etter konseptbilder. Stasjonene og resten er fortsatt primitiver. | ARCHITECTURE, GDD-tabellens §14-rad |
| Oppgraderes på stedet | Bygget | Stillaset reises igjen, modellen vokser, og hvert nivå får pynt. | `Scripts/Content/BuildingDefinition.cs` (BuildingLevel), `Construction` |
| Flyttes uten tap | Bygget | Flytting er gratis. | ARCHITECTURE §14-raden |
| Infrastruktur på grid eller snap | Bygget | Grid, snap kant i kant, vegger trukket i linjer og buer rundt warden (`LayArc`). | `Scripts/Building/BuildPlacement.cs` |
| Ugyldig plassering vises tydelig | Bygget | Gjennomsiktig forhåndsvisning med begrunnelse, for eksempel «Outside Haven Meadows». | `BuildGhost`, `HomePortal.cs` |
| Dekorasjon er tilgjengelig og billig | Delvis | Bare Lantern Post, Haven Banner, Herb Planter og Torch. De gir comfort, som igjen gir Well Rested. Ingen benker, statuer, blomster eller skilt. | `Data/Buildings/*.asset`, `HavenReadiness.cs` |
| Fysiske stasjoner man kan bruke | Bygget | `CraftingStation` og `StationWindow`. | `Scripts/UI/StationWindow` |
| Ikke unødvendig løping | Bygget | Kostnader trekkes fra lageret hjemme (`MaterialPool`), «Go there» fra H-vinduet (`HavenTravel`), og last i en kjerre i nærheten teller med. | `Scripts/Settlement/HavenTravel.cs`, `Crafting/MaterialPool` |

### 3.3 Bygninger (§6, §8.4)

Det finnes 35 bygningsdefinisjoner i `Data/Buildings/`. Visningsnavnene avviker fra filnavnene: ConstructionTable = Workshop, Sawbench = Woodcutter's Lodge, Forge = Smithy, TanningRack = Hunter's Lodge og Loom = Tailor's Shop.

| Spesifikasjonen | Status | Merknad | Hvor |
|---|---|---|---|
| **Workshop** (verktøy, sekker, tre til planker, byggekomponenter, reparasjon; Carpenter/Artisan) | Bygget, men **Annerledes** i innhold | Workshop I-V lager verktøy, enkel gear, steinblokker og reparasjoner, og fra III til V feltutstyr. Planker lages ved Woodcutter's Lodge (Tor), sekker ved Tailor's Shop. Ingen NPC hører til Workshop. | `Data/Buildings/ConstructionTable.asset`, `Docs/BaseProgression.md` |
| **Smithy** (smelting, våpen, rustning, reparasjon, modifikasjoner; Blacksmith) | Bygget | Smithy I-V smelter kobber, jern, moonsilver og trollstone og lager gear. Bjarne halverer reparasjonskostnaden. Modifikasjoner (empower og enchant) skjer ved Purification Altar, ikke ved smien. | `Data/Buildings/Forge.asset`, `Data/Settlement/Smith.asset` |
| **Kitchen** (mat, buffs, forberedelser, mat til følgesvenner; Cook) | Bygget, kommer **sent** | Kitchen I-III lager måltider med varige bonuser (to måltidsplasser). Cooken Yrsa er nummer 10 i redningsrekkefølgen. Ingen mat til følgesvenner. Før det finnes enkle bålmåltider. | `Data/Buildings/Kitchen.asset`, `Docs/RescueOrder.md` |
| **Alchemist** (potions, bomber, oljer, motstand, raffinering av essence) | Delvis, **Annerledes** | Cauldron I-III lager healing, tonics og Frostward. Purification Altar raffinerer essence og krever at Ingrid har jobben (`purifierJob`). Ingrid er **første** redning og er obligatorisk fordi ward-nivåene koster Purified Essence. Ingen bomber eller oljer. | `Data/Buildings/Cauldron.asset`, `PurificationAltar.asset`, `SettlementSettings.purifierJob` |
| **Tannery** (huder, tekstiler, sekker, lett rustning, utstyr til følgesvenner) | Delvis, **Annerledes** | Delt på to bygg: Hunter's Lodge (huder til lær, buer) og Tailor's Shop (lin, lærgear, sekker). Begge kreves for Smithy III, så de er ikke valgfrie. Det finnes saler og barding til hester, men ikke utstyr til følgesvenner. | `TanningRack.asset`, `Loom.asset`, `BaseProgression.md` («Later tiers ask for other buildings») |
| **Infirmary** (behandle skadde, healing, restitusjon; Healer) | **Mangler** | Ingen bygning og ingen skadetilstand. Følgesvenner blir slått ned og hjulpet opp, eller de blir «tatt av skogen». Stallen leger hester. | grep uten treff på Infirmary/Injur/Healer i `Scripts/` |
| **Scout Lodge** (info om raidområder, fiender, ressurser, mål, spor etter overlevende) | Delvis (slått sammen) | Watch Post I-III gir rumours, raid-runer og varsler hendelser (Foretold Events). Runa gir én rumour til. Briefingen viser kjente farer og sannsynlige funn. Den samme bygningen forsvarer også basen. | `WatchPost.asset`, `RaidPreparationWindow.Briefing.cs:101-133`, `Tracker.asset` (extraRumours 1) |
| **Common Hall** (sosialt, personlige oppdrag, fellesbonuser; Steward) | **Mangler** | Det nærmeste er samlingsplassen der beboerne spiser (bålet), festivaler, Sigrid som Leader (Haven Banner, +120 s raidtid) og beboernes småprat. | `SettlementSettings.residentGatheringPlace`, `Festivals`, `Data/Settlement/Leader.asset` |
| **Watchtower** (ranged damage, oversikt) | Bygget | Watch Post skyter fra Tier II, men bare under breaches. Warden gjør skuddene raskere. Et skudd er omtrent «ett slag med tidlig gear». | `Scripts/Settlement/WatchPostArcher.cs` |
| **Guard Post / Barracks** | Mangler som bygg, Delvis som rolle | Vakter kommer fra jobb (`JobDefinition.defendsAs`: Warden og Hunter). De står ved Watch Post eller i en ring rundt portalen. | `ResidentDefender.cs`, `Warden.asset`, `Hunter.asset` |
| Barrikader og feller | Bygget | Palisade, Stone Wall (tre ganger palisadens helse), Skigard og grind, Spike Trap, Iron Trap og Runic Ward Totem, som sakker ned og sliter på angripere. | `Data/Buildings/`, `WardTotem.cs`, `SpikeTrap.cs` |
| Valgfrie bygg skal ikke være irreversible | Delvis | Ingenting kan låses ute. Men senere tiers krever andre bygg, så de «valgfrie» Tannery og Alchemist er nødvendige for jern og portal. | `BuildingLevel.requires` |
| (Ikke i spesifikasjonen) | Bygget | Woodcutter's Lodge, Farm Plot, Beehive, Well, Sauna, Hut, Cottage, Storage Shed, Small Chest, Trading Post, Wainwright's Yard, Dock, Stable, Elder Tree, Campfire og Torch. | `Data/Buildings/` |

### 3.4 NPC-systemet (§7)

| Spesifikasjonen | Status | Merknad | Hvor |
|---|---|---|---|
| NPC-er er permanente | Bygget, med forbehold | Det finnes 11 navngitte beboere, og ingen dør. En følgesvenn kan bli «tatt av skogen». Da må hen finnes igjen via en rumour, og kommer hjem som fremmed uten jobb og uten minner. Minnene kommer tilbake ett om gangen. | `Data/Prologue/Residents/*`, `Data/Settlement/{Ingrid,Asa,Gunnar,Hild,Yrsa}.asset`, ARCHITECTURE «Going along» |
| Redning gir funksjon, bygning eller kompetanse | Bygget | Personen lærer spilleren arbeidsplassene sine når hen kommer hjem (`ResidentNpc.TeachWhatTheyKnow`). Rekkefølgen har 14 steg, og hvert steg svarer på et problem spilleren nettopp har kjent på. | `Docs/RescueOrder.md`, `BuildRescueOrder.cs` |
| Apprentice, Craftsman og Master låser opp oppskrifter | **Annerledes** | Hvor dype oppskriftene er, bestemmes av **bygningens** tier (I-V), ikke av NPC-en. NPC-en har arbeiderrang (Novice, Apprentice, Journeyman, Expert, Master) som gir +10 til +40 % fart og 3 til 12 % sjanse for en ekstra vare. | `SettlementSettings.cs:39-66` |
| Progresjon via handlinger og milepæler, ikke passiv XP | **Annerledes (direkte motstrid)** | `ResidentRecord.WorkDone` teller det arbeideren lager «on their own», og rangen følger antallet. Ingen oppdrag eller milepæl gir høyere rang. | `SettlementState.cs:45-46`, `SettlementState.cs:502` |
| Personlige oppdrag og historier | Delvis | Det finnes redningsquests («Bring X Home»), «Hands at Home», bestillinger med kvalitetskrav (`HavenRequestDefinition`), ønsker om raidvarer, minnelinjer og hjemkomstlinjer. Ingen quest utvikler en spesialist. | `Data/Settlement/Requests`, `HavenRequestBoard.cs`, `Docs/QuestRedesign.md` |
| Begrenset gruppe på raid | **Annerledes** | Nøyaktig én følgesvenn per raid. | `RaidSession.Companion`, `Scripts/Raids/RaidCompanions.cs` |
| Hver følgesvenn har en tydelig rolle eller evne | Delvis | Det finnes bare to typer (Fighter og Archer) og ordrer. Bånd-rang (Trusted ved 3 raids, Sworn ved 8) gir helse og skade. Healing-støtte finnes bare hos følgesvennene i prologen. | `Data/Companions/`, `Scripts/Prologue/PrologueCompanion.cs:21,36` |
| Følgesvenner utvikles gjennom bruk | Bygget | `CompanionBond` og minner. | `Scripts/Settlement/CompanionBond.cs` |
| Kompetansen blir værende når NPC-en er på raid | Bygget (ubekreftet i spill) | Jeg fant ikke noe unntak i `WorkerProduction` for en beboer som er med på raid. Blir personen tatt av skogen, går jobben til `VacantJobs`, men bygningene vedkommende har lært bort, er fortsatt kjent. | `Scripts/Settlement/WorkerProduction.cs` |
| Boliger | Delvis | Hut og Cottage gir senger. En seng gir +25 % arbeidsfart og comfort, men er aldri et krav (eierens regel). | `BaseProgression.md` («Beds never gate anyone») |
| (Ikke i spesifikasjonen) | Bygget | Leksjoner: en beboer lærer spilleren en ferdighet én gang mellom hvert raid (`JobDefinition.teaches`). | `ResidentNpc`, `ExperienceTracker.TakeLesson` |

### 3.5 Aktivt baseforsvar (§8)

| Spesifikasjonen | Status | Merknad | Hvor |
|---|---|---|---|
| Aktivt forsvar der spilleren kjemper | Bygget | Fire breaches i fast rekkefølge. Spilleren bruker den vanlige kampen. | `Scripts/Settlement/WardBreachDirector.cs`, `Data/Settlement/*Breach.asset` |
| Warning: portalen er ustabil, NPC-er varsler, retning antydes | Delvis | Det kommer en melding, portalen viser «pending»-tekst, og angrepsveiene tegnes på bakken så snart et angrep venter. Beboerne sier ingenting om det: ingen treff på breach i `ResidentAsides.cs` eller `ResidentNpc.cs`. | `WardBreachDirector.cs:1-17, 196, 616-666` |
| Preparation: lage forsyninger, reparere, plassere feller, velge følgesvenner, flytte forsvarere | Delvis | Spilleren starter forsvaret ved portalen når hen er klar, og kan bygge og reparere. Vaktene er bestemt av jobben og kan ikke velges eller plasseres. | `ResidentDefender.cs`, `ResidentLife.GuardPost()` |
| Attack fra bestemte inngangspunkter | Bygget | To til tre veier inn og bølger med 35 s mellomrom. Rundt 10 til 18 angripere per breach (summen av count-feltene). | `BreachLanes.cs`, `WardBreachDirector.NextWaveAfter` |
| NPC-er kjemper sammen med spilleren | Bygget | Warden og Hunter holder post og reiser seg selv. De andre søker ly ved portalen. | `ResidentDefender.cs` |
| Fiender angriper bygninger, NPC-er og portalen | Delvis | Halvparten går mot warden og resten mot nærmeste bygning. De angriper den som kommer nær. Ingen går målrettet etter beboerne. | `Scripts/Settlement/HomeAssault.cs` |
| Resolution: belønning ved seier, skade ved tap | Bygget | Bygninger blir ødelagt, men aldri borte (`HealthFloor`). Tapt forsvar gjør warden 4 m mindre i to raids, og forsøket kan tas på nytt. | `WardBreachSettings.cs:36-41` |
| Recovery: reparere og behandle skadde | Delvis | Alt repareres samtidig ved portalen for 30 % av kostnaden, og Carpenter gir 50 % rabatt. Det finnes ingen skadde NPC-er å behandle. | `BuildingRepairs.cs`, `Carpenter.asset` |
| Vanskeligheten øker ikke med antall bygninger | **Delvis, brutt** | Breaches kommer i fast rekkefølge, og den siste blir 20 % sterkere for hver gjentakelse. Men **corruption nights** blir hyppigere (17 % pluss 3 % per rang, maks 45 %) og større (3 pluss 1 per to ranger, maks 8) med Haven-rangen, og rangen er renown fra bygninger. | `Resources/CorruptionSettings.asset`, `CorruptionSettings.ChanceAt/CountAt`, `HavenRenown` |
| Forsvarsrolle per bygning | Delvis | Portal (anker), Watch Post (skyter), murer og feller, og Totem (sakker ned) finnes. Det finnes ingen Guard Post. Smithy lager ikke forsvarsutstyr til NPC-er, Alchemist lager ikke bomber, og ingen Infirmary behandler skadde. | se 3.3 |
| Fem fiendetyper (raske, tunge, portal, NPC, magiske) | Delvis | Breachene har ulike skapninger (rootlings, ulver, bog spitters, draugar, blight boars, moss shamans og små troll), men ingen har en egen angrepsrolle. Alle følger den samme målregelen. | `Data/Settlement/*Breach.asset`, `HomeAssault.cs` |
| Enkle regler for forsvars-AI | Delvis | De holder posisjon og går etter nærmeste eller prioriterte mål (`CompanionTactics`). Jeg fant ingen patruljerute, ingen beskyttelse av et bestemt bygg og ingen retrett ved lav helse for vakter. | `Scripts/Companions/CompanionTactics.cs` |
| Ingen død eller sletting, varslet, ikke for ofte | Bygget for breaches | Det går tre raids mellom hver breach. Den kan utsettes i to raids, og kommer aldri under raid eller lasting. Corruption nights kommer derimot tilfeldig ved nattefall med 20 s varsel. | `WardBreachSettings.cs:12-21`, `CorruptionSettings.cs:13-35` |
| Belønning: Corrupted Essence, materialer, oppskrifter, progresjon | Delvis, **Annerledes** | Bare Life Orbs (2, 3, 4 og 6), som brukes i ward II og III og til sparks. Essence er utelatt med vilje (GDD §6.3 og §7.3, beslutning 31. oktober). Etter en seier streifer flere prowlers om natten. | `Data/Settlement/*Breach.asset` (reward), `NightProwlers.cs` |
| (Ikke i spesifikasjonen) Raid svekker et kommende angrep | Bygget | Rumouren BreachSource kan gjøre en kommende breach opptil 60 % svakere. | `Data/Raids/Rumours/BreachSource.asset`, `SettlementState.BreachWeakening` |
| Eksempelet Corrupted Assault | Mangler som innhold | Byggeklossene finnes: Small Moss Troll, Old Moss Troll og Bergjutul (fjelltroll). | `Data/Monsters/` |

**Andre trusler mot hjemmet som spesifikasjonen ikke nevner:** `CorruptionNights` (aktiv i `SafeHaven.unity`), `NightProwlers` med `Blight`, og territoriets pushes med vakthold ved grensesteinene (`Scripts/Territory/`). Hjemmet har altså fire angrepssystemer, ikke ett.

### 3.6 Raids og ekspedisjoner (§9)

| Spesifikasjonen | Status | Merknad | Hvor |
|---|---|---|---|
| Separate instanser startet fra basen | Bygget | Hvert raid får et nytt kart generert fra seed. | `RaidLauncher.cs`, `RaidDirector.cs`, ARCHITECTURE «Generated raid maps» |
| Scout Lodge informerer, portalen starter | Delvis | Watch Post informerer, portalen starter. | se 3.3 |
| Scavenging | Bygget | Grunnraidet: noder, kister og hendelser. | `Data/Raids/MistyWoods.asset` |
| Rescue | Bygget (som rumours) | Én rumour per person. Den venter på personen før («After Home»), er haster og står alltid i briefingen. | `Data/Raids/Rumours/{Bjarne,Tor,...}.asset` |
| Expedition (sjeldne materialer, artefakter, oppskrifter) | Delvis | Raid-tier II til IV, rumours om kunnskap (Folio, Ledger, Bathhouse), relikvier og Troll Valley. Troll Valley bruker scenen fra Misty Woods med egne regler og har ingen egen grafikk. | `Data/Raids/TrollValley.asset`, ARCHITECTURE «What the boss changes» |
| Boss | Delvis | Bare én `BossDefinition` (The Heartroot), via en rumour fra raid nummer tre. Old Moss Troll vandrer rundt som apex, men er ingen boss. | `Data/Monsters/Boss The Heartroot.asset`, `BuildOldMossTroll.cs` |
| Typer kan kombineres i ett raid | Bygget | Slik er spillet bygget: alle typene finnes som rumours i det samme raidet. | `RaidRumours.For` |
| Planleggingsskjermen | Bygget | Viser tidspunkt, kart, rumours, varslede hendelser, ønsker, kjente farer, sannsynlige funn, tier, sparks, runer, følgesvenn, tapsregler og det man bærer. Det finnes ikke ett «primærmål»; målene er rumours. | `Scripts/UI/RaidPreparationWindow*.cs` |
| Begrenset inventory som strategisk valg | Bygget | Vektgrense på 150, slots, sekker, straff for tung sekk, safe pocket (1 til 3 plasser) og kjerrer. | ARCHITECTURE «Loot», «A heavy bag», «Transport» |
| (Ikke i spesifikasjonen) | Bygget | Klokke på 1000 s (`MistyWoods.asset:47`), Portal Sparks, faresteg med lootbonus, extraction som må holdes, tidspunkt på døgnet, hendelser med valg som har en pris, og store raids. | ARCHITECTURE «Raid tension», «Raid events» |

### 3.7 Tier 0-4 og den første reisen (§10, §11)

| Tier | Status | Hva som finnes, og hva som avviker |
|---|---|---|
| **0 Survival** | Bygget | Prologen og hjemkomsten: bålet, tre steiner (Life Orbs) og døra (portalreparasjon). Workshop I, Small Chest, Hut, steinverktøy, gjengrodd lysning og et ødelagt skur. Første NPC møtes først på raid 1 (Ingrid). |
| **1 Establishment** | Delvis | Workshop II, Smithy (Bjarne på raid 2), planker (Tor som nummer 3), Palisade, Skigard, Spike Trap og Watch Post (Eirik som nummer 7). Første breach kommer etter første extraction og er snill (ulvene jakter bare spilleren). Den første ekte kommer tre raids senere. **Kitchen og Cook kommer sent** (Yrsa er nummer 10). |
| **2 Development** | Delvis | Alchemist kommer **tidligere** (Ingrid er først). Tannery tilsvarer Hunter's Lodge (Gunnar som nummer 8) og Tailor's Shop (Asa som nummer 9). Watch Post II og III og raid-tier II og III med nye fiendeevner finnes. **Infirmary og spesialiserte følgesvenner mangler.** |
| **3 Fortification** | Delvis | Bygningstier III og IV, Stone Wall (Hild, Workshop III), Masterwork-kvalitet, empower, enchant og relikvier, og raid-tier III og IV. **Guard Posts, unike NPC-spesialiseringer og egne store angrep mangler**; den siste breachen gjentas bare 20 % sterkere. |
| **4 Endgame** | Stort sett mangler | Det finnes gear av tier V (Trollstone), men ikke noe raid av tier V. Portalforbedringen Moonlit finnes. **Endgame-raids, angrep knyttet til historien, unike forsvarssystemer og «ancient technology» mangler.** Akt II og III av historien er ikke bygget (GDD §4.4). |

| Steg i §11 | Status |
|---|---|
| 1 Leir med ødelagt portal, bål og arbeidsplass | Bygget |
| 2 Første ekspedisjon gir tre, stein og planter | Annerledes: dette samles i Haven. Første raid gir Ingrid, essence og kobber. |
| 3 Primitive verktøy og enkel Workshop | Bygget |
| 4 Redd en Carpenter som åpner mer i Workshop | Annerledes: den første som reddes er Alchemist. Carpenter (Tor) kommer som nummer tre og åpner Woodcutter's Lodge. |
| 5 Workshop lager planker | Annerledes: det gjør Woodcutter's Lodge |
| 6-7 Redd Blacksmith, som åpner Smithy | Bygget (raid 2) |
| 8 Redd Cook, som åpner Kitchen | Annerledes: Cooken er nummer 10 |
| 9 De første forsvarsverkene | Bygget |
| 10 Varslet angrep der layout, NPC-er og forsvarsverk teller | Bygget (det første er snilt, det andre er ekte) |
| 11 Seieren gir det portalen trenger | Delvis: Life Orbs, men oppgraderingen koster mest Purified Essence |
| 12 Større Ward og nye raidområder | Bygget |

### 3.8 Morale, samspill, prinsipper og MVP (§12-§15)

| Spesifikasjonen | Status | Merknad og hvor |
|---|---|---|
| Community Morale | **Mangler** | Ingen treff på morale i `Scripts/`. Byggeklossene finnes: comfort (`HavenReadiness`), Blight (`SettlementState.Blight`), festivaler, beboernes småprat, renown og ranger. |
| §13: Scavenging-raid gir materialer, så bygninger | Bygget | `Docs/BaseProgression.md` |
| §13: Rescue-raid gir NPC, så kompetanse | Bygget | `Docs/RescueOrder.md` |
| §13: Expedition-raid gir sjeldne ting og høyere tier | Delvis | Raid-tiers finnes, men bare to destinasjoner. |
| §13: Boss-raid gir unike komponenter | Delvis | Én boss (Heartroot). Trophy og opplåsing av Troll Valley. |
| §13: Workshop gir verktøy, så mer effektiv innsamling | Bygget | Verktøytiers og +1 stokk per hogst. |
| §13: Smithy gir bedre overlevelse | Bygget | Gear score og raid-tiers (`Docs/GearScoreAndRaidTiers.md`) |
| §13: Kitchen gir bedre forberedelse | Bygget, kommer sent | `PlayerBoons` |
| §13: Forsvarsverk gir bedre sjanse | Bygget | Murer, feller, Watch Post og Totem |
| §13: Forsvar gir essence og portalprogresjon | Delvis | Bare Life Orbs |
| §13: Utvikle NPC-er gir oppskrifter og evner | **Mangler** | Arbeiderrang gir bare fart. |
| P1 Bygninger låser opp funksjoner | Bygget | Stasjoner og oppskrifter med tier-krav |
| P2 Redning gir permanent verdi | Bygget | |
| P3 NPC-progresjon skal være aktiv | **Brutt** | Arbeiderrang etter antall produserte varer |
| P4 Fleksibel og oversiktlig bygging | Bygget | |
| P5 Dekorasjon skal være tilgjengelig | Delvis | Fire objekter |
| P6 Korte produksjonskjeder | Bygget, med forbehold | Charcoal ble lagt til som tredje ledd i smelting, med vilje som en måte å bruke opp ved på. |
| P7 Raids driver progresjon | Bygget, og sterkt | GDD §7.7 «What only raids can give» |
| P8 Aktivt forsvar | Bygget | |
| P9 Portalen binder progresjonen sammen | Delvis | Ward-nivå åpner raid-tiers, men mye av progresjonen ligger i bygningstiers, ranger og territorium. |
| P10 Nederlag skaper arbeid | Bygget, og sterkt | Reparasjoner, en mindre ward og safe pocket |
| P11 Ikke straff bygging | **Brutt** | Corruption nights følger rangen |
| P12 Hvert materiale har en tydelig bruk | Bygget, med forbehold | `HavenAnswers` sier «Nobody at home can use these yet». Det er 47 materialer og 7 ressurser med egne søyler. |
| MVP §15.1 | Bygget, og mer til | Alt i listen finnes. Unntaket er NPC-progresjon slik den er beskrevet. |
| Validering §15.2 | **Ikke gjort** | Se 4.8 |

### 3.9 Det spillet har, men spesifikasjonen ikke nevner

Timed extraction og tapsregler. Hunger og tørst. Farming og birøkt. Fiske med fisk man kan se. Hester (temming, avl, stall, ridning). Kjerrer, trillebår og robåt. Tretten ferdigheter med 130 perks og Insight. Essence-alteret og relikvier. Fem slags quests og story graphs. Territoriet (Haven's Reach). Årstider, vinter og festivaler. Co-op for fire spillere over Steam. Stealth og lockpicking. En dyp nærkamp med parry, poise og backstab. Cinematics og innspilte replikker. Alt dette må enten holdes utenfor spesifikasjonen med vilje, eller skrives inn i den. I dag gjør den ingen av delene.

---

## 4. Gameplay-analyse mot ønsket følelse

*Fra her er det mine vurderinger.*

### 4.1 Følelsen spesifikasjonen vil ha

Spesifikasjonen vil ha fire ting. **Et hjem som er verdt å forsvare** (kjerneidéen). **Mennesker som betyr noe** (oppsummeringen: «menneskene i den betyr noe»). **Raids med konkrete grunner** (prinsipp 7). Og **nederlag som skaper arbeid i stedet for tap** (prinsipp 10). I tillegg skal starten gå raskt (§10.1), og systemene skal henge sammen så spilleren forstår hvorfor hver aktivitet er nyttig (§13).

### 4.2 Øyeblikk til øyeblikk

- **Kamp:** Dette er spillets sterkeste del. Kampen er langsom og bevisst, med combos, parry, poise, perfekt dodge og backstab, og fiendene merker deg med sansene sine (`Scripts/Combat`, `Scripts/AI/Perception`). Det er lesbart og gir rom for mestring. **Men** spesifikasjonen kaller spillet et «action-RPG» og beskriver forsvar med 16 angripere (Corrupted Assault). En kamp laget for to eller tre motstandere om gangen kan bli tung eller kaotisk mot bølger. Ifølge TODO-en skjedde nesten halvparten av de loggførte dødsfallene i prologen (11 av 23). Ferdighetssystemet gjør dessuten nybegynneren svakere med vilje (`Docs/Skills.md`). Det første inntrykket er altså en risiko.
- **Samling og bygging:** Minispill med tidsvindu for øks, hakke og sigd, stillas man hamrer opp, en modell som vokser og et «pop» når den er ferdig. Det er fysisk og tilfredsstillende på papiret. Men stasjonene er fortsatt primitiver. Det kan få spesifikasjonens spørsmål 2 («tilfredsstillende å plassere og oppgradere?») til å få et dårligere svar enn systemet fortjener.
- **Klarhet:** Grensesnittet (Hearthline) er nå felles for alle vinduer. Men spilleren har mange valutaer å holde styr på: Life Orb, Life Mote, Radiant Life Orb, Orb Tincture, Portal Spark, Cursed og Purified Essence, Pact Rune, Old Silver Penny, Insight, renown, comfort, blight og hold. Hver har en grunn til å finnes, men summen er mye for et spill med prinsippet «Produksjonskjeder skal være korte og forståelige».

### 4.3 Et raid

- **Det som virker:** Raidet har en tydelig bue. Briefingen gir en grunn fra første sekund (rumours), kartet er det samme som briefingen lovet, hendelsene har valg med en oppgitt pris, fasene trapper opp med lootbonus for å bli lenger, og extraction holdes mens beacon-lyset trekker skogen mot sirkelen. Kjerra og safe pocket gir ekte valg. Dette er rikere enn spesifikasjonens raid. Det dekker §9.2, §9.3, §9.4 og prinsipp 7 bedre enn spesifikasjonen selv beskriver dem.
- **Tempo:** Klokken er 16:40 (`MistyWoods.asset:47`), og et stort raid er 29:10. GDD-en sier 12 minutter og at det aldri er testet (§26.2). Fasene og utgangene er skrevet for 12 minutter. Ingen har spilt et helt raid for hånd og målt det. Spesifikasjonen spør om dette selv (§16).
- **Hvor mange beslutninger:** I ett raid kan det samtidig stå to eller tre rumours, tilfeldige hendelser, ønsker fra hjemmet, quests, rot's heart for territoriet, en breach-kilde, en savnet person og en følgesvenn som blør ut. Det er bra hvis spilleren må velge mellom dem. Det er dårlig hvis det føles som en handleliste. Det vet ingen ennå.
- **Risiko og belønning:** Det er lesbart. «At risk» står på HUD-en, tapsreglene står i briefingen, og gear score har ingen skjult straff. Det er en av styrkene.

### 4.4 En dag i Haven

- **Det som virker:** H-vinduet (Overview, Stores, Buildings, People, Raids og Lands) gjør Haven til en liten koloni man styrer fra ett sted, med «Go there». Arbeiderne produserer fra lageret også under raids. Stasjonenes køer går videre. Beboerne har døgnrytme, småprat og leksjoner. Det er spesifikasjonens «hjem og lite samfunn» bygget ut i detalj.
- **Risikoen:** Hjemmet har fått mange plikter. Corruption nights kan komme ved 17 til 45 % av nattefallene. Hold i territoriet slites ned hver «turn», og et raid teller som en turn (`Docs/Territory.md`: «Erosion 1 + 4 × blight per turn»). Blight sprer seg fra netter man taper. Bestillinger og ønsker står og venter. Leksjonen må tas «once between raids». Hver av dem er rimelig for seg. Samlet nærmer de seg det spesifikasjonen advarer mot i §12: «et daglig vedlikeholdssystem som straffer spilleren for å dra på eventyr». GDD §6.3 lover også at «No enemies spawn arbitrarily while the player is farming or crafting». Corruption nights holder ikke det løftet fullt ut.
- **Det som mangler:** Common Hall og morale, altså et sted og et tall som viser at leiren blir et samfunn. TODO-ens punkt «A village that remembers» er delvis løst med sorglinjer og hjemkomstlinjer, men landsbyen reagerer fortsatt lite.

### 4.5 Forsvaret

- **Det som virker:** Fasene finnes og fungerer. Angrepsveiene er kjent før angrepet, så murer og feller har en hensikt. Bygninger blir aldri borte. Et tapt forsvar gir arbeid (30 % i reparasjon og en mindre ward), ikke tap. Koblingen der et raid svekker en kommende breach (BreachSource) er nøyaktig den typen samspill spesifikasjonen ber om.
- **Svakhet 1, hva som står på spill:** Fienden truer bygninger og et ward-anker. Beboerne løper til portalen, og vaktene reiser seg selv. Ingen *mennesker* er i fare. «Build a home worth defending» handler om folk. Spesifikasjonens NPC-targeters er den direkte spaken for det, uten å bryte regelen om ingen død.
- **Svakhet 2, forberedelse:** Spilleren kan ikke velge eller plassere vakter, og fiendetypene har ingen roller. Da blir valgene i forberedelsen stort sett «bygg flere murer».
- **Svakhet 3, belønningen:** Life Orbs alene, pluss vanskeligere netter etterpå. En seier gjør hjemmet farligere. Det er motsatt av «en seier beskytter noe».
- **Svakhet 4, at forsvaret blir utvannet:** Når corruption nights, pushes og prowlers også angriper hjemmet, er en breach ikke lenger *hendelsen*. Den er én trussel av fire.

### 4.6 Den lange buen over tiers

- **Det som virker:** «Tiers mean one thing everywhere» (`BaseProgression.md`, regel 1). Et raid av tier N betaler for gear av tier N+1. Hver oppgradering må åpne noe nytt (`HavenProgressionTests.NoUpgrade_IsOnlySpeed`). Redningsrekkefølgen viser problemet før svaret. Dette er solid progresjonsdesign.
- **Tempo, regnet men ikke spilt:** Kobber tar to raids, full kobberrustning to raids til, jern fem til seks og moonsilver fem til seks (`BaseProgression.md`, «Expected pace»). Det blir 15 til 20 raids til tier IV, pluss doble byggekostnader (888 eik for å bygge alt én gang). Med 20 til 30 minutter per raid inkludert hjemmet er det seks til ti timer. Det passer «slow but not grindy» hvis det stemmer. Men tallene er beregnet og aldri målt. Breaches, territorium og corruption kommer i tillegg.
- **Starten:** Spesifikasjonen vil at Tier 0 og 1 skal gå raskt. Målt før quest-redesignet: prologen tok 21 til 24 minutter, og ingen ekte kjøring nådde et raid etter 27 minutter (52 minutter med juks; minnenotatet om quest-redesignet). Etter redesignet er ingenting målt.
- **Slutten:** Det er én boss, Troll Valley har ingen egen grafikk eller boss, det finnes ikke noe raid av tier V, og akt II og III mangler. Spesifikasjonens Tier 4 har nesten ingen motpart. Det er greit på dette stadiet, men det betyr at «den lange buen» i dag slutter rundt tier III.
- **Ryggraden:** Spilleren har ingen enkelt måte å se «hvor langt er jeg kommet?». Ward-nivå, Haven-rang, bygningstiers, raid-tier, gear score, ferdighetsnivå og territoriets hold måler hver sin ting. Spesifikasjonens poeng om at portalen skal binde progresjonen sammen, er riktig og er det viktigste den tilfører.

### 4.7 Hvor loopene henger sammen, og hvor de ikke gjør det

| Kobling | Henger sammen? |
|---|---|
| Raid → hjem (folk, kunnskap, essence, materialer) | Ja, sterkt (`Docs/RescueOrder.md`, GDD §7.7) |
| Hjem → raid (tid, Well Rested, rumours, lommer, feltutstyr) | Ja (`HavenReadiness`, Raids-fanen i H) |
| Raid → forsvar (BreachSource, rot's heart) | Ja, sjelden god kobling |
| Forsvar → portal (Life Orbs til ward II og III) | Svakt |
| Forsvar → raid | Nei. En seier gir ingenting man kan ta med ut, bare flere prowlers. |
| NPC-utvikling → nye muligheter | Nei, bare fart |
| Kitchen → følgesvenner | Nei |
| Dekorasjon og comfort → raid | Ja (Well Rested), men usynlig i hverdagen |
| Territorium → hjem (renown, tithe, ingen korrupsjon) | Ja, men som en egen loop med egen valuta (Pact Rune) |

### 4.8 De fem valideringsspørsmålene (§15.2), slik jeg vurderer dem uten å ha spilt

1. **Vil spilleren tilbake til basen?** Trolig ja. Arbeidet som er gjort, de nye menneskene og at problemet vises før svaret, trekker hjem. Men hjemkomstsammendraget nevner bare beboernes arbeid. Ferdige køer, moden avling og hva det nye byttet gjør mulig er ikke med (TODO «A fuller homecoming»).
2. **Er det tilfredsstillende å plassere og oppgradere?** Mekanikken ja, men grafikken er for det meste primitiver. Det bør testes med bygg som har ekte modeller.
3. **Er det meningsfullt å redde en NPC?** Ja, dette er sterkest: de lærer bort bygninger, jobber, leksjoner og har egne linjer.
4. **Merkes oppgraderinger og ny kompetanse?** Blandet. Oppgraderinger som åpner nye oppskrifter merkes. Prosentbonuser (Well Rested, 1,1 ganger arbeidsfart) merkes nesten ikke.
5. **Er det morsomt å forsvare basen?** Ukjent. Dette er det minst validerte og svakest belønnede leddet (4.5).

---

## 5. Styrker (verdt å beholde)

1. **Data i ScriptableObjects, oppbygging i skript og regler som tester.** Nesten all tuning ligger i definisjoner og settings (`GameSettings<T>`), innhold bygges av gjentakbare builder-skript (`AgentScripts/Build*.cs`), og reglene er vanlig C# med tester (190 testfiler). Spesifikasjonens matriser (§17) kan genereres rett fra dataene.
2. **«What only raids give» og «Raids with a reason from home».** Prinsippet om at Haven bare lager mer av det den kan, mens alt nytt må hentes ute, er den beste formuleringen av spesifikasjonens prinsipp 7 jeg har sett. Rumours gjør at hvert raid har en grunn fra første sekund (GDD §7.5 og §7.7).
3. **Redning der problemet kommer først.** `HavenAnswers` viser hva som ble liggende igjen og hvem som ville løst det, før personen er funnet. Det gjør at det å redde noen føles som lettelse og ikke som en sjekkliste. Spesifikasjonens §7.1 i sin beste form.
4. **Nederlag gir arbeid og aldri tap.** Bygninger kan bli ødelagt, men ikke borte. Gear man tok med beholdes med slitasje. Safe pocket. Følgesvenner dør ikke, men glemmer. Det er prinsipp 10 gjennomført konsekvent.
5. **Regelen for oppgraderinger.** «No upgrade is only speed» håndheves av en test. Det svarer på spesifikasjonens spørsmål 4 på systemnivå.
6. **Raid og forsvar er koblet.** BreachSource og rot's heart lar et raid gjøre et angrep på hjemmet mindre. Det er nøyaktig spesifikasjonens «systemene skal belønne hverandre».
7. **H-vinduet og HavenTravel.** Kolonistyring fra ett sted, uten løping, som spesifikasjonen ber om i §5.2.
8. **Planleggingsskjermen.** Den dekker hele §9.3, med tapsregler i klartekst.
9. **Lesbar risiko.** Gear score uten skjult straff (eierens beslutning) og «At risk» på HUD-en.
10. **Kampen.** En dyp, lesbar nærkamp og stealth gir spillet en identitet spesifikasjonen ikke engang ber om.

---

## 6. Svakheter og risikoer

### 6.1 Der spesifikasjonen motsier tatte beslutninger

Dette er punktene eieren må se. Hvert av dem må avgjøres; se spørsmålene i kapittel 8.

| # | Spesifikasjonen sier | Det som er besluttet eller bygget | Kilde | Spørsmål |
|---|---|---|---|---|
| 1 | Raids er instanser med mål; timer og tap nevnes ikke | Tidsbegrenset extraction med tapsregler er kjernen (GDD §1.3, §7 og §8, pillar C) | GDD v0.3 og v0.4 | 8.1 |
| 2 | Ward definerer byggearealet (§4.2) | Det bygges over hele Haven Meadows. Warden er bare trygg sone (eierens ønske, 12. nov.). | GDD §6.4, TODO «Queued» punkt 6, `HomePortal.InBuildLand` | 8.2 |
| 3 | NPC-progresjon via milepæler, ikke passiv XP (§7.3) | Arbeiderrang etter antall produserte varer | `SettlementSettings.workerRanks`, minnenotatet om skills (7. nov.: «residents' ranks») | 8.9 |
| 4 | Antall bygninger skal ikke øke angrepene (§8.3, P11) | Corruption nights følger Haven-rangen, og rangen er renown fra bygninger | `CorruptionSettings`, innført etter playtest 5. nov. | 8.5, 8.6 |
| 5 | Forsvar gir Corrupted Essence (§8.7) | Essence kommer bare fra raids. Angripere i breaches har ingen loot (31. okt.). | GDD §6.3 og §7.3 | 8.7 |
| 6 | Workshop lager planker, og Carpenter hører til Workshop (§6.2) | Woodcutter's Lodge lager planker, og Tor jobber der | `BaseProgression.md` (2. nov.) | 8.18 |
| 7 | Rekkefølge: Carpenter, så Blacksmith, så Cook (§11) | Ingrid, så Bjarne, så Tor, og Yrsa (Cook) som nummer 10 (godkjent 9. nov.) | `Docs/RescueOrder.md` | 8.10 |
| 8 | Alchemist er valgfri og kommer i Tier 2 (§6.10, §10) | Alchemist er første redning og nødvendig for purification og ward-nivåer | `RescueOrder.md`, `SettlementSettings.purifierJob` | 8.10, 8.15 |
| 9 | Tannery og Alchemist er valgfrie (§6.10) | Smithy III krever Hunter's Lodge og Tailor's Shop | `BaseProgression.md` | 8.15 |
| 10 | Begrenset gruppe følgesvenner (§7.4) | Én per raid (GDD §15.5) | `RaidSession.Companion` | 8.11 |
| 11 | Infirmary behandler skadde følgesvenner (§6.7) | Følgesvenner skades ikke. De glemmer og må finnes igjen (eierens beslutning 3. nov.). | GDD §8.4, minnenotatet om raid-følgesvenner | 8.12 |
| 12 | Morale er lett og uten vedlikehold (§12) | Territoriets hold slites ned hvert raid og hver morgen. Blight sprer seg. | `Docs/Territory.md`, `SettlementState.Blight` | 8.13, 8.14 |
| 13 | Angrep skal være varslet og planlagt (§8.7) | Corruption nights er tilfeldige med 20 s varsel. GDD §6.3 lover at fiender ikke dukker opp vilkårlig hjemme. | `CorruptionSettings.delay`, GDD §6.3 | 8.5 |
| 14 | Navn: Corrupted Essence, Moss og Mountain troll, Watchtower, Scout Lodge | Cursed Essence, Small og Old Moss Troll, Bergjutul, Watch Post | `Data/` | 8.18 |

### 6.2 Svakheter i designet

1. **Ingenting er validert.** Dette er hovedrisikoen (se kapittel 1). Tallene for tempo er beregnet. Systemene er prøvd med regler og prober fra CLI, men ikke med en spiller. TODO-ens første punkt fra 4. november («Make a build and play one full raid without cheats») ser fortsatt ut til å stå åpent.
2. **For mange parallelle progresjonsstiger og valutaer.** Det finnes ingen felles svar på «hvor langt er jeg kommet?» (4.6, 4.2).
3. **Angrepssystemene for hjemmet overlapper, og vedlikeholdet sniker seg inn** (4.4, 4.5). Det mest spesifikasjonsnære systemet, breaches, blir utvannet av tre andre.
4. **NPC-er er bonuser mer enn mennesker som utvikler seg.** Rangen er passiv. Følgesvenner finnes i bare to typer. Det finnes ingen Common Hall og få personlige oppdrag. Spesifikasjonens egen setning: «NPC-er skal ikke bare være generiske arbeidere» treffer dagens bygg.
5. **Forsvaret har svak belønning, abstrakt innsats og lite å velge i forberedelsen** (4.5).
6. **Tynt i toppen.** Én boss, to destinasjoner som deler én scene, ingen raid-tier V og ingen endgame.
7. **Grafikken kan forvrenge playtesten.** Primitive stasjoner gjør spørsmål 2 vanskelig å svare ærlig på. Det er i tråd med eierens regel om systemer før polish, men det bør tas med i tolkningen av en test.
8. **Dokumentene sier ulike ting.** Statustabellen i ARCHITECTURE er utdatert: den sier 300 s raid, «one recruit» og «No companions in raids». TODO-en sier det samme. GDD §15.3 sier Sigrid gir 30 s, mens dataene sier 120 s (`Data/Settlement/Leader.asset`). Nå kommer en tredje kilde, spesifikasjonen, som ikke bygger på de to andre. Uten én sannhet vil framtidig arbeid trekke i ulike retninger.
9. **Mulig konflikt mellom angrepsveier og bebyggelse (ubekreftet).** Angrepsveiene starter 16 m utenfor ward-kanten (`WardBreachSettings.laneDistance`), og warden er høyst 49 m. Bygg kan nå stå inntil omtrent 165 m ut. Angripere kan dermed starte midt i eller bak bebyggelsen. Det bør sjekkes i Play.

### 6.3 Omfang og prosjektrisiko for én utvikler

- **Størrelse:** rundt 173 000 linjer kjøretidskode og 88 000 linjer editorkode, 67 quest-assets, 35 bygninger, 27 monsterdefinisjoner og 47 materialer.
- **Co-op koster ekstra for hvert nye system.** Reglene for «Co-op by default» (HavenSync, en runner og puppets, `HavenDecisions`) gjør at morale, roller i forsvaret og NPC-milepæler hver må ha en regel for co-op og tester for det (`QuestCoopTests`, `CoopWireTests`). GDD §3.1 sa at co-op ikke skulle styre arkitekturen i MVP. Nå gjør det det i praksis.
- **Unreal-porten:** Minnenotatet fra 24. oktober beskriver en port til UE 5.6 i et eget repo. Jeg har ikke sjekket hvor den står. Hvis den lever, er det et helt spill til å vedlikeholde.
- **Agenter bygger fortere enn eieren rekker å spille.** Mange systemer er merket «unplayed by hand». Hvert nytt system øker gjelden i validering.

---

## 7. Anbefalinger

Prioritert. Hvert punkt sier hva som bør gjøres, hvorfor, omtrentlig innsats (S, M eller L) og hva det bygger på. Alt holder seg til eierens linje: systemer før polish, data i ScriptableObjects og ingen skjulte straffer.

### MÅ

**M1. Spill spesifikasjonens slice for hånd før noe nytt bygges.**
*Hva:* En fast protokoll. Nytt spill, juksemenyen av, og spill til ward-nivå I og den første ekte breachen (breach nummer to). Etter hver økt gis spørsmålene i §15.2 en karakter fra 1 til 5, med én setning hver. Merk agentkjøringer i loggen først (det står i TODO-en), så ekte spill kan skilles ut.
*Hvorfor:* Ingen av de fem spørsmålene kan besvares i dag. Alt annet i denne listen er gjetning til det er gjort.
*Innsats:* S i kode, men tid ved tastaturet.
*Bygger på:* `PlaytestLogger`, milepælene i `PlaytestSettings` (First raid over og First extraction), `CheatSettings`, Skills-siden i Systems Editor.

**M2. Én sannhet: flett spesifikasjonen inn i GDD-en (v0.5).**
*Hva:* Svar på spørsmålene i kapittel 8, og før svarene inn i GDD §26 som beslutninger. Ta inn spesifikasjonens portal-tiers, morale og forsvarsroller der de velges. Oppdater statustabellen i ARCHITECTURE, som er utdatert.
*Hvorfor:* Tre kilder som sier ulike ting gir arbeid som trekker i ulike retninger (6.2 punkt 8).
*Innsats:* S til M (dokumentarbeid).
*Bygger på:* formatet for beslutningslogg i GDD §26.1.

**M3. Gjør portal-tier til den synlige ryggraden.**
*Hva:* Definer Portal Tier 0-4 som de fem tilstandene som allerede finnes: ikke reparert, reparert, ward I, ward II og ward III. Gi dem spesifikasjonens navn (Dormant, Awakened, Stabilized, Restored og Reclaimed). Vis dem i H-vinduets Overview og i portalvinduet sammen med det hver tier åpner (raid-tier, bygninger, breaches). Gi `WardLevel` en `requires`-liste som `BuildingLevel` allerede har: bygning på et visst nivå, beboere hjemme, en beseiret boss eller et nøkkelfunn. De andre stigene beholdes, men vises som «det som trengs til neste tier».
*Hvorfor:* Det er det viktigste spesifikasjonen tilfører (P9), og det løser svakheten med for mange stiger uten å kaste noe.
*Innsats:* M.
*Bygger på:* `SettlementSettings.WardLevel`, `BuildingUnlocks`, `BuildingLevel.requires`, `SettlementState.MarkBossDefeated`, `RaidDifficulty.wardLevelNeeded` og linjene for neste mål i `HavenOverview`.

**M4. Fjern bruddet på prinsipp 11.**
*Hva:* La corruption nights skalere med portal-tier, raid-tier, blight eller historie, ikke med Haven-rang.
*Hvorfor:* Slik det er nå, straffer systemet bygging, og det strider både mot spesifikasjonen og mot GDD-ens løfte om trygghet.
*Innsats:* S.
*Bygger på:* `CorruptionSettings.ChanceAt/CountAt`.

**M5. Én modell for angrep på hjemmet.**
*Hva:* La breaches være spesifikasjonens aktive forsvar. Bestem hva som skjer med corruption nights: enten blir de en liten, varslet variant av breach (for eksempel varslet ett raid i forveien, med angrepsveiene synlige), eller så slås de av som standard. Bestem også hva som skjer med slitasjen i territoriet. Mitt forslag er at den bare skjer under blight eller ved pushes, ikke i hver turn.
*Hvorfor:* Det gjør forsvaret til en hendelse igjen og holder hjemmet trygt (§12, §8.7 og GDD §6.3).
*Innsats:* S til M, avhengig av valget.
*Bygger på:* `WardBreachSettings`, `CorruptionSettings`, `TerritorySettings` (erosion) og `BreachLanes`.

### BØR

**B1. NPC-kompetanse via milepæler (Apprentice, Craftsman og Master).**
*Hva:* Rangen skal bestemmes av tre ting: bygningens tier, en personlig deed-quest per spesialist og ett raidfunn. Master åpner én unik oppskrift eller oppgradering per person. Fartsbonusen kan bli, men skal ikke være grunnen.
*Hvorfor:* Det er P3 og §7.2 og §7.3, og det gjør folk til mer enn fartsbonuser.
*Innsats:* M.
*Bygger på:* `JobDefinition`, `ResidentRecord`, deed-objectives (Assign Job, Upgrade Building, Rescue Person og `QuestTargetDefinition`), `HavenRequestDefinition` og `RecipeDefinition`. Eierens regel om deed-objectives følges: ingen Quest Event per person.

**B2. Roller for angriperne i forsvaret.**
*Hva:* En `BreachRole` på `BreachGroup` som `HomeAssault` leser: Runner (mot warden), Breaker (mot murer og bygg), Seeker (mot vakter og beboere) og Hexer (svekker warden eller totemer på avstand). En Seeker som når en beboer gjør hen midlertidig «Shaken», uten død.
*Hvorfor:* Det gir §8.5, gjør layouten viktigere og setter mennesker på spill (4.5).
*Innsats:* M.
*Bygger på:* `BreachGroup`, `HomeAssault`, prioriterte mål i `MonsterTargeting` og `StatusEffectDefinition`.

**B3. Mer å velge i forberedelsen.**
*Hva:* Mens en breach venter, kan spilleren gi én eller to vakter en post (Watch Post, en grind, portalen eller et bygg) fra H-vinduet. Portalvinduet sier hvem som kommer og fra hvor («rootlings og bog spitters fra nord og øst»). Vaktene trekker seg tilbake ved lav helse.
*Hvorfor:* Det gir §8.2 og §8.6, og det gir beslutninger før angrepet.
*Innsats:* M.
*Bygger på:* `ResidentDefender`, `JobDefinition.defendsAs`, `BreachLanes.Describe` og `CompanionOrder.Retreat`.

**B4. Et vunnet forsvar skal gi noe som merkes.**
*Hva:* Hver breach lærer bort én forsvarsoppskrift eller oppgradering (for eksempel Iron Trap II, et Totem-nivå eller piler til Watch Post) i tillegg til Life Orbs. Vurder et eget forsvarsmateriale som også trengs til portal-tiers. Cursed Essence holdes utenfor hvis beslutningen fra 31. oktober står.
*Hvorfor:* Det gir §8.7 og §13 («Forsvare basen gir portal- og forsvarsprogresjon»).
*Innsats:* S.
*Bygger på:* `WardBreachDefinition.reward`, blueprint-items (Learned At Home) og `wonEvent`.

**B5. Kitchen og mat skal merkes tidligere.**
*Hva:* Flytt Yrsa (Cook) frem i redningsrekkefølgen, for eksempel rett etter Tor, eller la bålmåltidene få en liten varig bonus. Vurder én måltidsplass til følgesvennen.
*Hvorfor:* Det er §6.4, §6.10 og §11, og mat er en av de enkleste måtene å gjøre forberedelsen konkret på.
*Innsats:* S (data i `BuildRescueOrder`), M for mat til følgesvennen.
*Bygger på:* `RaidRumourDefinition.afterHome`, `PlayerBoons` og `SurvivalSettings.mealSlots`.

**B6. Egne evner for følgesvennene.**
*Hva:* Gi hver i ekspedisjonen én kjennetegnende evne fra kode som allerede finnes. Ingrid healer, slik følgesvennene i prologen gjør. Gunnar finner kister og vilt. Eirik trekker angrepene mot seg. Runa markerer rumours. Tor reparerer i felt.
*Hvorfor:* Det gir §7.4 («tydelig rolle eller ferdighet»), og valget av følgesvenn blir et ekte valg.
*Innsats:* M.
*Bygger på:* `PrologueCompanion` (support heal), `CompanionDefinition` og `RaidCompanions`.

**B7. En lett «Hearth»- eller morale-visning, uten forfall.**
*Hva:* Et tall i H-vinduet satt sammen av det som finnes: comfort, beboere med seng, festivaler, vunne og tapte breaches og blight. Det synker aldri av at spilleren er borte. Effektene er små: størrelsen på Well Rested, beboernes replikker og litt rabatt på reparasjoner.
*Hvorfor:* Det gir §12 uten nye plikter, og det gjør det synlig at leiren blir et samfunn.
*Innsats:* S til M.
*Bygger på:* `HavenReadiness`, `SettlementState.Blight`, `Festivals` og `ResidentAsides`.

**B8. Lag spesifikasjonens matriser fra dataene (§17).**
*Hva:* En editoreksport til Markdown med bygninger, nivåer og oppskrifter, NPC-er, jobber og redning, ressurser og tiers, en raid-katalog og breach-oversikt.
*Hvorfor:* Matrisene blir sanne og oppdaterer seg selv, i stedet for å bli et dokument til som kan bli utdatert.
*Innsats:* S til M.
*Bygger på:* Content Editor, `ContentAudit`, `BuildingDefinition`, `RecipeDefinition`, `ResidentDefinition`, `RaidDefinition` og `WardBreachDefinition`.

### KAN

- **K1. Infirmary som en rolle**, for eksempel ved Cauldron tier II. Den behandler «Shaken» beboere og følgesvenner, men bare hvis B2 velges. M.
- **K2. Common Hall som oppgradering av samlingsplassen.** Den huser festivaler, personlige quests og morale-visningen. M.
- **K3. Scout Lodge.** Vis det Watch Post vet som en egen fane eller rolle før en egen bygning vurderes. S.
- **K4. Mer dekorasjon**, som benker, statuer, blomster og skilt, som billige `BuildingDefinition`-er der comfort teller én gang per type. S per objekt, men krever grafikk.
- **K5. Bomber og oljer ved Cauldron** som forbruksvarer i forsvaret. M.
- **K6. Portalmodellen endrer seg per tier.** M, grafikk. Senere, etter regelen om systemer før polish.
- **K7. Endgame:** raid-tier V og en boss nummer to (Old Moss Troll er nærmest). L. Etter M1.

**Bør fryses til M1 er gjort:** mer co-op, hester inn i raids, nye biomer og Unreal-porten (hvis den lever). Feilrettinger kan fortsette.

---

## 8. Designspørsmål til eieren

Hvert spørsmål kan besvares for seg. Anbefalingen står sist.

**8.1 Er tidsbegrenset extraction fortsatt kjernen?**
*Kontekst:* GDD-en bygger på klokke, extraction og tap ved død (pillar C). Spesifikasjonen nevner ikke noe av det.
- A: Ja, som nå. Spesifikasjonen får et kapittel om det.
- B: Ja, men mykere. Ingen klokke på Rescue- og Boss-rumours; klokken gjelder bare scavenging.
- C: Nei. Raids blir instanser med mål og uten klokke.

*Anbefaling:* A. Det er det mest gjennomarbeidede systemet i spillet og det som gjør raidene annerledes enn andre spill.

**8.2 Hva er Ward: grensen for bygging eller for trygghet?**
*Kontekst:* Spesifikasjonen sier byggegrense. Siden 12. november er den trygghetsgrense, og man bygger i hele Haven Meadows.
- A: Trygghet, som nå. Spesifikasjonen rettes.
- B: Byggegrense, som i spesifikasjonen. Den gamle regelen kommer tilbake.
- C: En blanding. Man bygger i hele Meadows, men bygg innenfor warden får fordeler (ingen prowlers, comfort, billigere reparasjon), og angrepsveiene starter alltid utenfor det som er bygget.

*Anbefaling:* C. Det beholder friheten du ba om og gir spesifikasjonens «ytterområder trenger forsvar» (§4.2) mening.

**8.3 Skal portal-tier være ryggraden?**
*Kontekst:* Det finnes omtrent åtte stiger og ingen felles tier (4.6).
- A: Ja. Ward-nivå (0-III) og reparasjon blir Tier 0-4, og de andre stigene vises som krav til neste tier (M3).
- B: En egen bosetningstier ved siden av, satt sammen av renown og rang.
- C: Behold det som er.

*Anbefaling:* A.

**8.4 Hva skal kreves for neste portal-tier?**
*Kontekst:* I dag koster det bare Purified Essence og Life Orbs.
- A: Bare ressurser, som nå.
- B: Ressurser pluss bygninger på et visst nivå.
- C: Ressurser, bygninger, en bestemt person hjemme og en boss eller et nøkkelfunn (spesifikasjonen §4.3).

*Anbefaling:* C, men med bare ett eller to krav i tillegg per tier, så det ikke blir en sjekkliste.

**8.5 Hvilke angrep på hjemmet skal finnes?**
*Kontekst:* I dag er det fire: breaches, corruption nights, pushes og prowlers.
- A: Bare breaches. Corruption nights slås av, og prowlers blir værende utenfor lyset.
- B: Breaches pluss corruption nights som en liten, varslet variant (varslet ett raid i forveien, med synlige angrepsveier).
- C: Alle fire, som nå.

*Anbefaling:* B. Hvis du vil ha færre systemer å vedlikeholde, A.

**8.6 Hva skal styre hvor sterke og hyppige angrepene er?**
*Kontekst:* Breaches kommer i fast rekkefølge. Corruption følger rangen, og rangen kommer fra bygninger.
- A: Portal-tier og raid-tier.
- B: Blight og historien.
- C: En blanding av A og B, men aldri antall bygninger eller renown.

*Anbefaling:* C.

**8.7 Hva skal et vunnet forsvar gi?**
*Kontekst:* I dag bare Life Orbs, og nettene blir vanskeligere etterpå. Spesifikasjonen vil ha essence. Beslutningen fra 31. oktober sier nei til essence fra forsvar.
- A: Forsvarsoppskrifter og et eget materiale, uten essence (B4).
- B: Litt Cursed Essence også, som snur beslutningen fra 31. oktober.
- C: Bare Life Orbs, som nå.

*Anbefaling:* A.

**8.8 Skal beboerne selv være i fare under et forsvar?**
*Kontekst:* I dag søker de ly og blir ikke angrepet. Spesifikasjonen har NPC-targeters.
- A: Nei, som nå.
- B: Ja. Angripere av typen Seeker kan gjøre beboere midlertidig «Shaken», som går over etter et raid eller behandles. Ingen død.
- C: Ja, med skade som gir permanente spor.

*Anbefaling:* B. Det er den enkleste måten å gjøre «worth defending» personlig på.

**8.9 Hvordan skal NPC-ene bli bedre?**
*Kontekst:* I dag ved å telle det de lager. Spesifikasjonen sier milepæler.
- A: Som nå.
- B: Milepæler: bygningstier, en personlig quest og et funn, der Master åpner noe unikt (B1).
- C: Begge: milepælene gir rangen, og det de lager gir litt fart innenfor rangen.

*Anbefaling:* C.

**8.10 Hvilken rekkefølge skal spesialistene komme i?**
*Kontekst:* Den godkjente rekkefølgen er Ingrid, Bjarne, Tor og så videre, med Yrsa som nummer 10. Spesifikasjonen sier Carpenter, Blacksmith og så Cook.
- A: Behold rekkefølgen fra 9. november.
- B: Behold Ingrid, Bjarne og Tor, men flytt Yrsa frem til nummer 4 eller 5.
- C: Bruk spesifikasjonens rekkefølge.

*Anbefaling:* B. Ingrid må stå først fordi historien sender spilleren dit og essence-loopen trenger henne.

**8.11 Hvor mange følgesvenner kan være med på et raid?**
- A: Én, som nå.
- B: Én nå og to fra portal-tier 3.
- C: To til tre fra start.

*Anbefaling:* B. AI, co-op og balanse koster mye for hver ekstra følgesvenn.

**8.12 Hva skjer med en følgesvenn som faller?**
*Kontekst:* I dag blør de ut på 45 s, glemmes, må finnes igjen og kommer hjem som fremmede. Spesifikasjonen har Infirmary og skader.
- A: Som nå.
- B: Et mellomtrinn. Den som blir hjulpet opp, kommer hjem «Wounded» og hviler ett raid eller behandles. Den som blør ut, glemmer som nå.
- C: Bare midlertidig ute (Wounded), og ingen som glemmes.

*Anbefaling:* A til etter playtesten. B hvis du vil gi Infirmary en jobb.

**8.13 Skal Community Morale bygges, og hvordan?**
- A: Nei.
- B: En lett visning satt sammen av det som finnes, uten forfall (B7).
- C: Et fullt system med behov.

*Anbefaling:* B.

**8.14 Skal territoriet (Haven's Reach) slite seg ned av seg selv?**
*Kontekst:* I dag slites hold med 1 + 4 × blight per turn, og et raid teller som en turn.
- A: Som nå.
- B: Bare under blight. Uten blight står hold stille.
- C: Aldri. Hold endres bare av pushes, oppgaver og runer.

*Anbefaling:* B, eller C hvis playtesten viser at det maser.

**8.15 Skal de manglende bygningene (Infirmary, Scout Lodge, Common Hall og Guard Post) være egne bygg eller roller på dem som finnes?**
- A: Egne bygg, som i spesifikasjonen.
- B: Roller: Infirmary ved Cauldron, Scout ved Watch Post, Hall som oppgradering av samlingsplassen og vakter ved Watch Post.
- C: Hopp over dem.

*Anbefaling:* B. Det gir funksjonene uten fire nye modeller og fire nye kjeder av krav.

**8.16 Hvilken slice skal valideres først, og hva fryses i mellomtiden?**
- A: Spesifikasjonens slice laget av det som finnes: nytt spill til ward I og breach nummer to, omtrent 90 til 120 minutter (M1).
- B: Hele Tier 0 til 2.
- C: En egen testscene for forsvar.

*Anbefaling:* A, og samtidig fryse co-op, hester i raids og nye biomer.

**8.17 Hvor langt skal et raid være?**
*Kontekst:* GDD-en sier 12 minutter, dataene sier 16:40, et stort raid er 29:10, og ingen av delene er spilt.
- A: 12 minutter, som GDD-en og fasene er skrevet for.
- B: 16:40, som nå.
- C: La playtesten avgjøre. Start på 12 minutter og mål hvor lenge spilleren faktisk blir.

*Anbefaling:* C.

**8.18 Hvilke navn og hvilken rollefordeling skal gjelde: spesifikasjonens eller spillets?**
*Kontekst:* Corrupted og Cursed Essence, Watchtower og Watch Post, planker ved Workshop eller ved Lodge.
- A: Spillets navn og fordeling. Spesifikasjonen rettes.
- B: Spesifikasjonens navn og fordeling. Spillet endres.

*Anbefaling:* A. Det koster ingenting, og saves, quests og lagrede id-er holder seg.

---

## Vedlegg A: Kilder lest

- Spesifikasjonen: `C:\Users\ander\Downloads\Safe_Haven_Game_Design_Specification.md` (hele).
- `Docs/GameDesignDocument.md` (§1-§8, §13-§17 og §23-§28 lest i sin helhet, resten skumlest), `Docs/BaseProgression.md`, `Docs/RescueOrder.md`, `Docs/Territory.md`, `Docs/GearScoreAndRaidTiers.md` (del), `Docs/HavenWorldDesign.md` (del) og `Docs/TODO.md` (utvalgte deler).
- `Assets/_Project/Scripts/ARCHITECTURE.md`: statustabellen, «Base building», «Skills», «Raid events», «Raid tension», «What only raids give», «Raids with a reason from home», «Going along, set-pieces and bosses», «The living world», «The Haven, lived in», «Transport», «Ward breaches», «Night» og «Essence».
- Kode som er sjekket: `Scripts/Settlement/{SettlementSettings,SettlementState,HomePortal,WardBreachDefinition,WardBreachSettings,WardBreachDirector,CorruptionNights,CorruptionSettings,HomeAssault,ResidentDefender,WatchPostArcher,WardTotem,HavenReadiness,WorkerProduction,JobDefinition,ResidentDefinition,HavenTravel,HavenSettings}.cs`, `Scripts/Content/BuildingDefinition.cs`, `Scripts/Raids/{RaidCompanions,RaidPlan,RaidDifficulty}.cs`, `Scripts/Companions/CompanionDefinition.cs`, `Scripts/UI/RaidPreparationWindow.Briefing.cs` og `Scripts/Prologue/PrologueCompanion.cs`.
- Data som er sjekket: `Data/Buildings/*` (35), `Data/Settlement/*` (breaches, jobber og beboere), `Data/Raids/*` (to raids og rumours), `Data/Monsters/`, `Data/Companions/`, `Data/Items/Materials/` (47), `Data/ResourceInventory/Definitions/` (7), `Resources/{SettlementSettings,HavenSettings,CorruptionSettings}.asset` og `Scenes/SafeHaven.unity` (at `CorruptionNights` er aktiv).
- Minnenotater: colony-direction, progression-research, gear-tiers, skills, raid-companions, quest-redesign, quest-deeds, haven-rework, haven-terraces, horses, coop, backstab-design, portal-voice, systems-before-polish, playtest-logs, cheat-menu og unreal-port.
- Playtest-logger: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven\Playtests` (1129 filer, 50 med en `RaidFinished`-rad). Jeg har ikke skilt menneskelige kjøringer fra agentkjøringer, så loggene er ikke brukt som tall i denne gjennomgangen, bare som en indikasjon.
