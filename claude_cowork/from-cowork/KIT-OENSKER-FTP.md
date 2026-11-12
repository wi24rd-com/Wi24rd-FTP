# Ønsker til Wi24rd UI Kit 2 fra Wi24rd FTP

Skrevet 2026-11-11 ud fra pakke 2.3.0 og FTP-repoet ved commit `514c97c`. Til Grimnir-tråden, som ejer kittets kilde. FTP retter aldrig i kittet eller i sine kopier af det.

Indtil et ønske er bygget, laver FTP det i sine egne filer med kittets tokens og mærker stedet `KIT-ØNSKE Fn`, så det kan løftes op bagefter. Alle ønsker er tilføjelser; intet skal omdøbes eller fjernes.

| Nr. | Ønske | Haster |
|---|---|---|
| F1 | Skrifterne som TTF, og en fast sti i skrift-tokens | Før B3, hvis Jan vælger Inter |
| F2 | Ikonerne som WPF-geometri, og 14 nye ikoner | Før B3 |
| F3 | Farver til adgangskodens tegn | Før B2 (mørkt tema) |
| F4 | Flade ordbøger pr. tema ved siden af `Tokens.xaml` | Før B1, hvis prøven på Windows fejler; ellers når det passer |
| F5 | Tema-hjælper til WPF (svarer til `tema.js`) | Før B2 |
| F6 | Slip-zone som komponent | Kan vente |
| F7 | WPF-udgaven af komponenterne | Leveres af FTP i B4 |

## F1. Skrifterne som TTF

**I dag i FTP:** Segoe UI til tekst (32 steder med `FontFamily` i XAML), `Cascadia Mono, Consolas` til værdier og adgangskode.

**Mangler i 2.3.0:** Pakken har kun woff2, som WPF ikke læser. De fire filer er desuden variable skrifter (`…-wght-normal.woff2`), og WPF kan ikke vælge vægt i en variabel skrift. Og `Wi24rdFontSans` er `Inter, Segoe UI`: det finder kun en Inter, der er installeret på maskinen, ikke en, der ligger i programmet.

**Forslag:**
- Læg statiske TTF-filer i pakken under `wpf/fonts/`: Inter Regular (400) og SemiBold (600), JetBrains Mono Regular (400) og SemiBold (600; adgangskoden vises halvfed). De to OFL-tekster følger med.
- Beslut én fast mappe i programmet, fx `Wi24rdKit/Fonts/`, og lad skrift-tokens pege på den først: `pack://application:,,,/Wi24rdKit/Fonts/#Inter, Segoe UI`. Findes mappen ikke, falder WPF tilbage til Segoe UI, så et program uden skrifterne ser ud som i dag. Det ændrer en værdi, ikke et navn.
- Skriv i README, at filerne skal have byggehandlingen `Resource`.

**Indtil da:** FTP bruger `Wi24rdFontSans` og `Wi24rdFontMono`, som de er, og får Segoe UI og Cascadia Mono.

## F2. Ikonerne som WPF-geometri

**I dag i FTP:** Segoe MDL2 Assets som tegn i en `TextBlock`: 31 steder i XAML (22 forskellige tegn) og 2 i `Services/SharedFile.cs`.

**Mangler i 2.3.0:** Kittets 27 stregikoner findes kun som SVG-stier i `kit.js`. 8 af FTP's 22 findes i kittet (`fil`, `mappe`, `slet`, `kopier`, `laas`, `skjold`, `afspil`, `luk`). 14 findes ikke:

| FTP bruger det til | Tegn i dag | Forslag til navn |
|---|---|---|
| Oplysning (status, om, note) | E946 | `info` |
| Advarsel i banner | E7BA | `advarsel` |
| Flueben ("gratis, ingen sporing") | E73E | `flueben` |
| Server og forbindelse | E968 | `netvaerk` |
| Slip filer her | E753 | `upload` |
| Protokol | E71B | `link` |
| FTP-valget | E839 | `kabel` |
| Adresse | E774 | `globus` |
| Brugernavn | E77B | `bruger` |
| Adgangskode | E8D7 | `noegle` |
| Ny adgangskode | E72C | `opdater` |
| Stop | E71A | `stop` |
| Hent opdatering | E896 | `hent` |
| Tom liste | F12B | `tom` |

**Forslag:** Byggescriptet laver en `wpf/Ikoner.xaml` fra samme kilde som `kit.js`: én `Geometry` pr. ikon med nøglen `Wi24rdIkon` + navn (`Wi24rdIkonMappe`), og én stilart til `Path` (24 × 24, streg 2, runde ender, stregfarve fra tekstfarven, en 16 px-udgave som `ik`). De 14 nye tegnes i kilden, så web får dem også.

**Indtil da:** FTP beholder Segoe MDL2 Assets. Tegnene tager tekstfarven og virker i begge temaer.

## F3. Farver til adgangskodens tegn

**I dag i FTP:** Tal røde (`#D83A3A`), store bogstaver blå (`#1E7BFF`), små bogstaver grønne (`#1A9A4A`), og tre blege udgaver til forklaringen under feltet. Kun lyst tema. Den "blege" blå er ikke bleg (samme `#1E7BFF` med hvid tekst), så de tre mærker er ikke ens bygget.

**Mangler i 2.3.0:** Tre farver til tekst, der kan skelnes fra hinanden og læses på `--surface-raised` i begge temaer. Mærkefarverne og dataserierne må ikke bruges til tekst.

**Forslag:** Tre tokens med hver sin bløde bund: `--kode-tal`, `--kode-store`, `--kode-smaa` og `…-soft`, mindst 4,5:1 mod `--surface-raised` og mod deres egen bløde bund i begge temaer, og valgt, så rød-grøn farveblinde kan skelne dem. Reglen "farve bærer aldrig betydning alene" gælder også her: FTP giver tallene en understregning, så de kan kendes uden farve.

**Indtil da:** FTP bruger `Wi24rdDangerBrush`, `Wi24rdInfoBrush` og `Wi24rdOkBrush` med deres `…SoftBrush` som bund. De er tekstfarver i begge temaer. Det er lånt betydning (et tal er ikke en fejl), og derfor et ønske.

## F4. Flade ordbøger pr. tema

**I dag i FTP:** Ét tema; alle ressourcer ligger direkte i `Application.Resources`.

**Mangler i 2.3.0:** `Tokens.xaml` har de to temaer som indlejrede ordbøger med `x:Key`. Det giver tre ulemper, uanset om det virker på Windows:
- Ingen token findes, før `SaetTema` har kørt. `StaticResource` til en kit-nøgle i `App.xaml` kan derfor ikke regnes med (det er flyttevejledningens trin 2).
- Visual Studios designer kan ikke finde nogen token og viser vinduerne uden farver.
- Mønstret er usædvanligt i WPF. Det almindelige er én fil pr. tema, som skiftes ud i `MergedDictionaries`.

**Forslag:** Lad byggescriptet også skrive `tokens/Tokens.Lys.xaml` og `tokens/Tokens.Moerk.xaml`: de samme nøgler på øverste niveau. `Tokens.xaml` bliver, som den er. README viser begge veje. Fejler prøven på Windows, er de to flade filer også rettelsen.

**Indtil da:** FTP bruger `Tokens.xaml` og `SaetTema` som beskrevet, hvis prøven på Windows går godt.

## F5. Tema-hjælper til WPF

**I dag i FTP:** Intet læser Windows' valg af lyst og mørkt.

**Mangler i 2.3.0:** Web har `tema.js` (lys, mørk, system; husker valget; rigtigt tema fra første billede). WPF har kun `SaetTema` som eksempel i README. .NET 8 har ikke noget indbygget, så hvert WPF-program skal selv skrive: læs Windows' valg, lyt efter skift, husk brugerens valg, og gør Windows' titellinje mørk (ellers er den hvid over et mørkt vindue).

**Forslag:** En kildefil `wpf/Wi24rdTema.cs` i pakken med `Saet(lys | moerk | system)`, `Valg`, en hændelse ved skift, og mørk titellinje. Samme tre valg og samme ord som `tema.js`.

**Indtil da:** FTP skriver den i `Services/Tema.cs`, mærket `KIT-ØNSKE F5`.

## F6. Slip-zone

**I dag i FTP:** Et felt på 170 px med ikon, overskrift, undertekst og to knapper. Kanten og bunden skifter farve, mens filer trækkes hen over.

**Mangler i 2.3.0:** Kittet har ingen komponent til at slippe filer, hverken til web eller WPF, og ingen regel for, hvordan "slip her" ser ud.

**Forslag:** En komponent med to tilstande: hvile (`--surface-raised`, kant `--border-strong`, stiplet) og træk over (`--accent-soft`, kant `--accent`). Den ligner Tom tilstand og kan bygges på den.

**Indtil da:** FTP tegner den med de fire tokens ovenfor.

## F7. WPF-udgaven af komponenterne

Det er ikke et ønske, men det, FTP leverer tilbage i B4. FTP tegner disse stilarter efter `KOMPONENTER.md`, og de løftes op i kittet:

| FTP i dag | Kit 2 |
|---|---|
| `PrimaryButton`, `SecondaryButton`, `DangerButton`, `LinkButton` | Button: primær, sekundær, farlig, ghost (36 px) |
| `IconButton`, `HeaderIconButton`, `RowIconButton` | Knap med kun ikon (har ingen egen beskrivelse i kittet; bør få en) |
| `SegmentRadio` | Segmentknap |
| `Card`, `CardTitle` | Kasse |
| `Label`, `LabelIcon`, `Value`, `MonoValue` | TextField (skrivebeskyttet, mono, med kopiér) |
| `FileList` og stilarterne til rækker og hoved | DataTable |
| De tre bannere | Banner |
| Statusprikkerne (sat fra koden) | StatusChip og mærkefarverne |
| "Ingen filer endnu" | Tom tilstand |
| `ProgressBar` i bundlinjen | Fremdrift |
| `ToolTip` (5 steder) | Værktøjstip |
| `MessageBox` (9 steder) | Dialog |

Afklaret af Jan 2026-11-11 (står i kittets `flytning/WI24RD-FTP.md`):
- **Stop-knappen** må være fyldt rød. Kittets `danger` er kant og tekst; den fyldte udgave kræver en ny token til teksten på rødt og kommer i en ny pakke. Indtil da tegner FTP den selv, mærket `KIT-ØNSKE F7`.
- **Knap med kun ikon** får ikke sin egen beskrivelse i kittet. `IconButton`, `HeaderIconButton` og `RowIconButton` tegnes som Button med ikon og et navn, der siger, hvad der sker.
- **Dialog.** Windows' `MessageBox` følger hverken kittet eller mørkt tema. En WPF-dialog efter kittets mål er den største enkelte stilart i B3.
