# Undersøgelse, fase A: Wi24rd FTP til Wi24rd UI Kit 2

2026-11-11. Pakke 2.3.0 (prøven er siden opdateret til 2.6.0; `Grimnir/design/ui-kit-2/pakke/`, kun læst). FTP-repoet ved commit `514c97c`, arbejdstræet var rent. Intet i programmet er ændret. Nyt i repoet er kun denne mappe (`claude_cowork/from-cowork/`).

## Kort

- **Virker `Tokens.xaml`?** Den bygger. Om den indlæses, er ikke prøvet endnu: det kræver Windows, og undersøgelsen er lavet på Mac'en. Prøven ligger klar i `tokens-proeve/` og skal køres på Windows-maskinen.
- **Skal rettes i kittet:** flyttevejledningens trin 2 kan ikke laves som skrevet, vejledningen mangler fire ting, og README mangler én sætning. Se "Rettelser til kittet".
- **Ønsker:** syv, i `KIT-OENSKER-FTP.md`.

## Hvad der er prøvet, og hvad der ikke er

| Hvad | Hvordan | Resultat |
|---|---|---|
| Kopien af `Tokens.xaml` er byte for byte som pakken | sha256 på pakken og på kopien | Ens: `64a470e2…cc2593` |
| `Tokens.xaml` bygger i et WPF-projekt | .NET 8 SDK (8.0.428), WPF's XAML-oversætter, i en kopi af FTP uden for repoet | 0 fejl, 0 advarsler; `Tokens.baml` dannes |
| `xmlns:sys` (`assembly=System.Runtime`) | Samme byg: oversætteren finder `sys:Double` | Godkendt ved byg |
| FTP bygger med filen flettet ind i `App.xaml` og `SaetTema` i `App.xaml.cs` | Samme kopi | 0 fejl, 0 advarsler |
| Prøven i `tokens-proeve/` bygger | Samme SDK | 0 fejl, 0 advarsler |
| `Tokens.xaml` **indlæses**: de to indlejrede ordbøger hentes med `FindResource`, temaet skifter i et åbent vindue | **Ikke prøvet.** Kræver Windows | Afventer `resultat.txt` |

Bygget er sket på Linux med `EnableWindowsTargeting`. Det er den samme oversætter som på Windows, men et WPF-program kan ikke startes dér. Byggefejl er altså udelukket; fejl ved indlæsning er ikke.

Min forventning er, at det virker: en `ResourceDictionary` med `x:Key` inde i en anden er lovlig XAML, og `FindResource` giver samme objekt hver gang, så `SaetTema` kan fjerne det gamle tema. Det er en forventning, ikke et resultat.

## Vejledningen holdt mod koden

Bekræftet, som det står i `flytning/WI24RD-FTP.md`:

- .NET 8 WPF, ét projekt, ingen UI-pakker. To vinduer (405 og 55 linjer), `App.xaml` 282 linjer.
- 30 pensler (28 ensfarvede og 2 farveforløb), 16 stilarter med navn.
- Farvekoder direkte i XAML: 37 i `App.xaml`, 7 i `MainWindow.xaml`, 0 i `AboutWindow.xaml`.
- Alt bruges med `StaticResource`; `DynamicResource` findes ikke.
- Kun lyst tema. Sprog via `L.cs` og to `.resx`.
- CI (`.github/workflows/ci.yml`) bygger via `build.ps1` og projektfilen. En ny XAML-fil under `src/Wi24rdFTP/` tages med af sig selv (set i prøvebygget). CI skal ikke ændres.
- `installer/Wi24rdFTP.iss` har ingen billeder eller farver; kun `WizardStyle=modern`.

Ikke i vejledningen:

1. **Koden henter pensler efter navn 13 steder.** `MainWindow.xaml.cs` linje 101–112 (slip-zonen), 484–499 (de tre statusprikker) og 539–541 (adgangskodens tegn) kalder `FindResource("Brush.…")`. Fjernes en gammel nøgle uden at rette her, bygger programmet fint og vælter, når linjen køres. Vejledningen søgte efter farvekoder i `.cs` og fandt derfor ingen.
2. **Koden sætter farven fast.** De 13 steder sætter `Fill`, `BorderBrush`, `Background` og `Foreground` til en pensel. Det følger ikke et temaskift. De skal bruge `SetResourceReference`, og adgangskoden skal tegnes igen ved skift.
3. **To stilarter uden navn** (`ListViewItem` og `GridViewColumnHeader`) ud over de 16. Den sidste sætter `FontSize`, `FontWeight` og `Foreground` to gange; kun den sidste gælder.
4. **6 af de 30 pensler bruges ingen steder:** `Brush.Navy`, `NavyLight`, `Cyan`, `GreenDark`, `GreenPale` og `RedPale`. De kan slettes uden erstatning.
5. **9 `MessageBox`** (1 i `App.xaml.cs`, 8 i `MainWindow.xaml.cs`). De er Windows' egne og følger hverken kittet eller mørkt tema.
6. **Ingen `.gitattributes`.** Git på Windows laver som standard linjeskift om ved udtjekning. Så er kopien af en kit-fil ikke længere byte for byte som pakken på Windows-maskinen. Kit-kopier skal have `-text`.
7. **Windows' titellinje bliver ikke mørk af sig selv** i .NET 8. Det skal programmet bede Windows om. Det hører til valget af skal.

## Hvor mange steder skal røres

| Hvad | `App.xaml` | `MainWindow.xaml` | `AboutWindow.xaml` | `.cs` | I alt |
|---|---|---|---|---|---|
| `StaticResource Brush.…` | 33 | 55 | 11 | | 99 |
| `FindResource("Brush.…")` | | | | 13 | 13 |
| Farvekoder | 37 | 7 | 0 | 0 | 44 |
| `StaticResource` til en stilart | 7 | 43 | 1 | | 51 |
| `FontSize` | 14 | 33 | 11 | | 58 |
| `FontFamily` | 11 | 19 | 2 | | 32 |
| `CornerRadius` | 5 | 10 | 1 | | 16 |
| `Margin` og `Padding` | 17 | 73 | 13 | | 103 |
| Ikontegn (Segoe MDL2) | 0 | 30 | 1 | 2 | 33 |
| `MessageBox` | | | | 9 | 9 |

De fem mest brugte pensler dækker 68 af de 99: `Brush.Muted` 18, `Brush.Accent` 16, `Brush.Border` 15, `Brush.Text` 13 og `Brush.Field` 6.

`Brush.Border` kan ikke oversættes med søg og erstat: kittet skelner mellem `Wi24rdBorderBrush` (kort) og `Wi24rdBorderStrongBrush` (felter og knapper), så de 15 steder skal ses ét for ét. Det samme gælder `Brush.Accent` (16 steder): handling og valgt er `Wi24rdAccentBrush`, ikoner ved etiketter er snarere `Wi24rdTextMutedBrush`, og oplysning er `Wi24rdInfoBrush`.

Statusprikkerne: `Brush.Ok`, `Amber`, `Grey` og `Red` bliver `Wi24rdOkMarkBrush`, `WarnMarkBrush`, `NoneMarkBrush` og `DangerMarkBrush`. Hver prik har allerede et ord ved siden af, så reglen om, at farve ikke bærer betydning alene, er opfyldt.

## Rettelser til kittet (Grimnir-tråden)

Ingen af dem er i `Tokens.xaml` endnu; det afgør prøven på Windows.

1. **Flyttevejledningens trin 2 bør udgå.** "Lad de 30 nøgler blive, men giv dem kittets farver" kræver `StaticResource` fra `App.xaml` til en kit-nøgle. Kittets nøgler findes først på øverste niveau, når `SaetTema` har kørt, altså efter at `App.xaml` er læst. Trinnet kan heller ikke følge et temaskift, som vejledningen selv skriver. Forslag: B1 går direkte til `{DynamicResource Wi24rd…Brush}` (99 steder i XAML og 13 i koden). Prøven har to linjer (09 og 11), der viser, hvad der faktisk sker.
2. **README, "Flet ind":** tilføj, at `SaetTema` skal kaldes i `OnStartup`, før det første vindue laves, og at ingen token findes før det.
3. **Vejledningen** mangler punkt 1–7 ovenfor. Tabellen i trin 2 siger `Wi24rdAccentBrush` for `Brush.Ok`; det er `Wi24rdOkBrush` siden 2.1.0, og prikkerne er `…MarkBrush`.
4. **Skrifterne i README:** tilføj, at pakkens woff2 er variable skrifter, og at WPF skal have statiske TTF-filer og en `pack://`-sti (ønske F1).
5. **README, "Sådan opdaterer du":** tilføj `.gitattributes`-linjen til kit-kopier.

Fejler prøven på Windows, er rettelsen i byggescriptet `design/build-tokens.mjs`: skriv de to temaer som flade filer (ønske F4). FTP ændrer ikke i sin kopi.

## Forslag til cyklusser, rettet

| Cyklus | Indhold |
|---|---|
| A (denne) | Færdig, når `resultat.txt` fra Windows er set, og Jan har valgt skal, skrift og tema |
| B1 | `Tokens.xaml` ind (med `KIT-VERSION.txt` og `.gitattributes`). Alle 99 + 13 penselopslag til `DynamicResource` og `SetResourceReference` med kittets nøgler. De 44 farvekoder væk. De 6 ubrugte pensler slettet. Lyst tema, fast |
| B2 | Tema: følger Windows (og eget valg, hvis Jan vælger det), mørk titellinje, adgangskodens farver i begge temaer. Begge vinduer ses i begge temaer |
| B3 | Stilarterne tegnet efter `KOMPONENTER.md`, skallen, skriften, dialogen i stedet for `MessageBox` |
| B4 | Skærmbilleder, banner, README; stilarterne løftet op i kittet |

B1 er større end i vejledningen (trin 2's mellemled er væk), men der bliver ét gennemløb af XAML'en i stedet for to.

## Ikke undersøgt

- Hvordan programmet ser ud med kittets farver. Intet er startet.
- Kontrast for de farver, der bliver FTP's egne (adgangskoden). Det hører til B2.
- `Scripts/Wi24rdFtp.ps1` og `Services/Backend.cs` er ikke læst; de har ikke med udseendet at gøre.
- Høj kontrast i Windows: om FTP skal gøre noget særligt, når brugeren har slået det til.
