# Tokens-prøve (fase A)

Prøver, om `Tokens.xaml` fra Wi24rd UI Kit 2 (pakke 2.6.0) kan indlæses af WPF, som pakkens README beskriver. Mappen er ikke en del af Wi24rd FTP og bygges ikke med programmet.

Kør på Windows-maskinen (kræver .NET 8 SDK, som allerede er der):

```powershell
cd C:\Wi24rd-FTP\claude_cowork\from-cowork\tokens-proeve
powershell -ExecutionPolicy Bypass -File .\koer.ps1
```

Et lille vindue blinker kort. Resultatet står i konsollen og i `resultat.txt` i samme mappe: en linje pr. tjek med OK, FEJL eller SE (en iagttagelse, der ikke tæller som fejl). Send `resultat.txt` tilbage.

Vælter programmet, før første linje er skrevet, er det `App.xaml`, der ikke kunne indlæse `Tokens/Tokens.xaml`. Fejlen står så i `resultat.txt`.

| Fil | Hvad |
|---|---|
| `Tokens/Tokens.xaml` | Uændret kopi fra pakken. Version og sha256 står i `Tokens/KIT-VERSION.txt` |
| `.gitattributes` | Holder git fra at ændre linjeskift i kopien, så den bliver ved med at være byte for byte som pakken |
| `App.xaml` | Fletter filen ind præcis som i README, og har en stilart, der bruger tokens med `DynamicResource` |
| `App.xaml.cs` | `SaetTema` ordret fra README, og 18 tjek |
| `Alias.xaml` | Flyttevejledningens trin 2: en gammel nøgle, der peger på kittet med `StaticResource` |
| `koer.ps1` | Tjekker kopien mod sha256, bygger, kører og gemmer resultatet |
