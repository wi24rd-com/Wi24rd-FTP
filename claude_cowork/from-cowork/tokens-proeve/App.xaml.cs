using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace TokensProeve;

/// <summary>
/// Fase A-prøve: indlæser Wi24rd UI Kit 2's Tokens.xaml i WPF, som pakkens README beskriver,
/// og skriver for hvert punkt OK, FEJL eller SE (en iagttagelse, der ikke tæller som fejl).
/// </summary>
public partial class App : Application
{
    private readonly StringBuilder _log = new();
    private int _fejl;
    private ResourceDictionary? _tema;

    // Ordret fra pakkens README, "Flet ind", punkt 3.
    public void SaetTema(bool moerk)
    {
        var nyt = (ResourceDictionary)FindResource(moerk ? "Wi24rdMoerk" : "Wi24rdLys");
        if (_tema != null) Resources.MergedDictionaries.Remove(_tema);
        Resources.MergedDictionaries.Add(nyt);
        _tema = nyt;
    }

    private void Linje(string s) { _log.AppendLine(s); Console.WriteLine(s); }

    private static string Kort(Exception ex)
    {
        while (ex.InnerException != null) ex = ex.InnerException;
        var m = ex.Message.Replace("\r", " ").Replace("\n", " ");
        return ex.GetType().Name + ": " + (m.Length > 220 ? m[..220] + "…" : m);
    }

    private void Tjek(string navn, Func<string> f)
    {
        try { Linje($"OK    {navn}: {f()}"); }
        catch (Exception ex) { _fejl++; Linje($"FEJL  {navn}: {Kort(ex)}"); }
    }

    private void Se(string navn, Func<string> f)
    {
        try { Linje($"SE    {navn}: {f()}"); }
        catch (Exception ex) { Linje($"SE    {navn}: kaster {Kort(ex)}"); }
    }

    // Indlæser Alias.xaml på ny, fletter den ind og henter den gamle nøgle.
    private ResourceDictionary? _alias;
    private string HentAlias(bool indlaes)
    {
        if (indlaes)
        {
            if (_alias != null) Resources.MergedDictionaries.Remove(_alias);
            _alias = new ResourceDictionary { Source = new Uri("/TokensProeve;component/Alias.xaml", UriKind.Relative) };
            Resources.MergedDictionaries.Add(_alias);
        }
        return Farve(_alias!["Brush.Bg"]);
    }

    private static void Kraev(bool ok, string hvis_ikke) { if (!ok) throw new InvalidOperationException(hvis_ikke); }

    private static string Farve(object? pensel) =>
        pensel is SolidColorBrush b ? b.Color.ToString() : "(ikke en SolidColorBrush: " + (pensel?.GetType().Name ?? "null") + ")";

    private static Type? ForventetType(string n)
    {
        if (n.EndsWith("Color")) return typeof(Color);
        if (n.EndsWith("Brush")) return typeof(SolidColorBrush);
        if (n.StartsWith("Wi24rdShadow")) return typeof(DropShadowEffect);
        if (n.EndsWith("Family") || n.StartsWith("Wi24rdFont")) return typeof(FontFamily);
        if (n.EndsWith("Weight")) return typeof(FontWeight);
        if (n.EndsWith("Thickness")) return typeof(Thickness);
        if (n.StartsWith("Wi24rdRadius")) return typeof(CornerRadius);
        if (n.EndsWith("Size") || n.EndsWith("Line") || n.EndsWith("Tracking") || n.StartsWith("Wi24rdSpace")) return typeof(double);
        return null;
    }

    private string TjekTyper(ResourceDictionary rd)
    {
        var antal = new SortedDictionary<string, int>();
        var forkert = new List<string>();
        foreach (var k in rd.Keys)
        {
            var n = k as string ?? k.ToString()!;
            var v = rd[k];
            var f = ForventetType(n);
            if (v == null || f == null || v.GetType() != f) forkert.Add($"{n} er {v?.GetType().Name ?? "null"}, ventet {f?.Name ?? "ukendt navneregel"}");
            var t = v?.GetType().Name ?? "null";
            antal[t] = antal.TryGetValue(t, out var a) ? a + 1 : 1;
        }
        Kraev(forkert.Count == 0, $"{forkert.Count} med forkert type: " + string.Join("; ", forkert.Take(8)));
        return string.Join(", ", antal.Select(p => $"{p.Value} {p.Key}"));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Console.OutputEncoding = Encoding.UTF8;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Linje("Tokens-prøve, Wi24rd UI Kit 2 pakke 2.6.0, " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
              + ", " + Environment.OSVersion.VersionString);
        Linje("");

        ResourceDictionary? lys = null, moerk = null;

        Tjek("01 Tokens.xaml indlæst via App.xaml (MergedDictionaries, Source)", () =>
        {
            Kraev(Resources.MergedDictionaries.Count == 1, $"ventede 1 flettet ordbog, fandt {Resources.MergedDictionaries.Count}");
            return $"1 flettet ordbog med {Resources.MergedDictionaries[0].Count} nøgler på øverste niveau";
        });
        Tjek("02 FindResource(\"Wi24rdLys\") giver en ResourceDictionary", () =>
        { lys = (ResourceDictionary)FindResource("Wi24rdLys"); return $"{lys.Count} nøgler"; });
        Tjek("03 FindResource(\"Wi24rdMoerk\") giver en ResourceDictionary", () =>
        { moerk = (ResourceDictionary)FindResource("Wi24rdMoerk"); return $"{moerk.Count} nøgler"; });
        Tjek("04 De to ordbøger har de samme nøgler", () =>
        {
            var a = lys!.Keys.Cast<object>().Select(k => k.ToString()!).ToHashSet();
            var b = moerk!.Keys.Cast<object>().Select(k => k.ToString()!).ToHashSet();
            Kraev(a.SetEquals(b), "forskel: " + string.Join(", ", a.Except(b).Concat(b.Except(a)).Take(10)));
            return $"{a.Count} ens";
        });
        Tjek("05 FindResource giver samme objekt hver gang (ellers kan SaetTema ikke fjerne det gamle tema)", () =>
        {
            Kraev(ReferenceEquals(lys, FindResource("Wi24rdLys")), "nyt objekt ved hvert opslag");
            return "samme objekt";
        });
        Tjek("06 xmlns:sys (assembly=System.Runtime): tal er System.Double, og alle typer følger navnereglen (lys)", () => TjekTyper(lys!));
        Tjek("07 Det samme for den mørke ordbog", () => TjekTyper(moerk!));

        Se("08 Før SaetTema: findes Wi24rdBgBrush på øverste niveau? (ventet: nej)", () =>
            TryFindResource("Wi24rdBgBrush") == null ? "nej, tokens kan først bruges, når SaetTema har kørt" : "ja");
        Se("09 Vejledningens trin 2 (gammel nøgle med StaticResource til kittet), hentet FØR SaetTema", () =>
            HentAlias(indlaes: true));

        Tjek("10 SaetTema(false): Wi24rdBgBrush er lysets #FFF3F5F8", () =>
        {
            SaetTema(false);
            var c = Farve(FindResource("Wi24rdBgBrush"));
            Kraev(c == "#FFF3F5F8", "fik " + c);
            return c;
        });
        Se("11 Vejledningens trin 2, indlæst EFTER SaetTema(false) (ventet: lysets #FFF3F5F8)", () => HentAlias(indlaes: true));

        // Et rigtigt vindue med DynamicResource, som FTP vil bruge det.
        Border? flade = null; Button? knap = null; Window? vindue = null;
        Tjek("13 Vindue med DynamicResource (kode og stilart) i lyst tema", () =>
        {
            flade = new Border { Padding = new Thickness(24) };
            flade.SetResourceReference(Border.BackgroundProperty, "Wi24rdBgBrush");
            knap = new Button { Content = "Prøve", Style = (Style)FindResource("Proeve.Knap") };
            flade.Child = knap;
            vindue = new Window { Title = "Tokens-prøve", Width = 320, Height = 160, Content = flade, ShowActivated = false };
            vindue.Show();
            vindue.UpdateLayout();
            var bd = (Border)knap.Template.FindName("bd", knap);
            Kraev(Farve(flade.Background) == "#FFF3F5F8", "fladen er " + Farve(flade.Background));
            Kraev(Farve(knap.Background) == "#FF007864", "knappen er " + Farve(knap.Background));
            Kraev(knap.FontSize == 14, "FontSize er " + knap.FontSize);
            Kraev(knap.Padding == new Thickness(12), "Padding er " + knap.Padding);
            Kraev(knap.FontWeight == FontWeights.SemiBold, "FontWeight er " + knap.FontWeight);
            Kraev(bd.CornerRadius == new CornerRadius(6), "CornerRadius er " + bd.CornerRadius);
            Kraev(bd.Effect is DropShadowEffect, "Effect er " + (bd.Effect?.GetType().Name ?? "null"));
            return $"flade {Farve(flade.Background)}, knap {Farve(knap.Background)}, 14 px, luft 12, radius 6, skygge, skrift \"{knap.FontFamily.Source}\"";
        });
        Tjek("14 SaetTema(true) slår igennem i det åbne vindue uden genstart", () =>
        {
            SaetTema(true);
            vindue!.UpdateLayout();
            Kraev(Farve(flade!.Background) == "#FF0B131E", "fladen er " + Farve(flade.Background));
            Kraev(Farve(knap!.Background) == "#FF35D6B3", "knappen er " + Farve(knap.Background));
            Kraev(Farve(knap.Foreground) == "#FF04231D", "knappens tekst er " + Farve(knap.Foreground));
            return $"flade {Farve(flade.Background)}, knap {Farve(knap.Background)}";
        });
        Tjek("15 Det gamle tema er fjernet (præcis én af Wi24rdLys og Wi24rdMoerk er flettet ind)", () =>
        {
            var temaer = Resources.MergedDictionaries.Count(d => ReferenceEquals(d, lys) || ReferenceEquals(d, moerk));
            Kraev(temaer == 1, $"fandt {temaer}");
            return "1";
        });
        Se("16 Vejledningens trin 2 efter skift til mørkt (ventet: stadig lysets farve, altså følger den ikke temaet)", () =>
            HentAlias(indlaes: false));
        Tjek("17 Tilbage til lyst", () =>
        {
            SaetTema(false);
            vindue!.UpdateLayout();
            Kraev(Farve(flade!.Background) == "#FFF3F5F8", "fladen er " + Farve(flade.Background));
            return Farve(flade.Background);
        });
        Se("18 Skrifter på denne maskine", () =>
        {
            var navne = Fonts.SystemFontFamilies.Select(f => f.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string Har(string n) => navne.Contains(n) ? "ja" : "nej";
            return $"Inter: {Har("Inter")}, JetBrains Mono: {Har("JetBrains Mono")}, Segoe UI: {Har("Segoe UI")}, Cascadia Mono: {Har("Cascadia Mono")}";
        });

        try { vindue?.Close(); } catch { /* ligegyldigt */ }
        Linje("");
        Linje(_fejl == 0 ? "RESULTAT: alle tjek er OK. Tokens.xaml virker som beskrevet i pakkens README."
                         : $"RESULTAT: {_fejl} tjek fejlede. Se linjerne med FEJL.");
        Shutdown(_fejl == 0 ? 0 : 1);
    }
}
