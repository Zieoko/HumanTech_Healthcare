using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace GravityDashboard;

public partial class MainWindow : Window
{
    private ApiClient? _api;
    private Crew? _selected;
    private CrewStatus? _lastStatus;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private string _currentIndicatorKey = "bone_density";
    private DateTime? _minDate, _maxDate;
    private string? _url;
    private readonly Dictionary<string, ObservableCollection<DateTimePoint>> _crewPoints = new();
    private readonly Dictionary<string, LineSeries<DateTimePoint>> _crewSeries = new();
    private readonly ObservableCollection<DateTimePoint> _baselinePoints = new();
    private readonly LineSeries<DateTimePoint> _baselineSeries;

    private static readonly (string Name, SKColor Color)[] CrewColors =
    {
        ("Crew01", new SKColor(0x00, 0xE5, 0xFF)),   // cyan
        ("Crew02", new SKColor(0xFF, 0x2D, 0x95)),   // magenta
        ("Crew03", new SKColor(0x2E, 0xE6, 0xA8)),   // menthe
    };

    public MainWindow()
    {
        InitializeComponent();

        foreach (var (name, color) in CrewColors)
        {
            var points = new ObservableCollection<DateTimePoint>();
            _crewPoints[name] = points;
            _crewSeries[name] = new LineSeries<DateTimePoint>
            {
                Name = name,
                Values = points,
                Fill = null,
                GeometrySize = 0,
                Stroke = new SolidColorPaint(color) { StrokeThickness = 2f },
            };
        }

        _baselineSeries = new LineSeries<DateTimePoint>
        {
            Name = "Baseline",
            Values = _baselinePoints,
            Fill = null,
            GeometrySize = 0,
            IsVisible = false,
            Stroke = new SolidColorPaint(new SKColor(0x8F, 0xB8, 0xD9))
            {
                StrokeThickness = 1.5f,
                PathEffect = new DashEffect(new float[] { 6, 6 }),
            },
        };

        TrendChart.Series = _crewSeries.Values.Concat(new ISeries[] { _baselineSeries }).ToArray();
        TrendChart.LegendPosition = LegendPosition.Top;
        TrendChart.LegendTextPaint = new SolidColorPaint(new SKColor(0xCF, 0xE6, 0xF5));

        TrendChart.YAxes = new Axis[]
        {
            new Axis
            {
                Name = "bpm",
                NamePaint = new SolidColorPaint(new SKColor(0x8F, 0xB8, 0xD9)),
                LabelsPaint = new SolidColorPaint(new SKColor(0x8F, 0xB8, 0xD9)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x1E, 0x3A, 0x55)) { StrokeThickness = 1 },
            }
        };

        TrendChart.XAxes = new Axis[]
        {
            new DateTimeAxis(TimeSpan.FromDays(14), d => d.ToString("dd/MM"))
            {
                LabelsPaint = new SolidColorPaint(new SKColor(0x8F, 0xB8, 0xD9)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x1E, 0x3A, 0x55)) { StrokeThickness = 1 },
            }
        };

        foreach (var kv in Labels.Map)
            IndicatorSelector.Items.Add(kv.Value);
        IndicatorSelector.SelectedIndex = 0;

        if (File.Exists("gravity_config.txt"))
        {
            _url = File.ReadAllText("gravity_config.txt");
            Loaded += async (_, _) => await ConnectAsync();
        }
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    // ---------- Connexion ----------

    private async void ConnectButton_Click(object sender, RoutedEventArgs e) => await ConnectAsync();

    private async Task ConnectAsync()
    {
        var url = (_url ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url)) return;

        _api = new ApiClient(url);
        File.WriteAllText("gravity_config.txt", url);
        try
        {
            var crews = await _api.GetCrewsAsync();
            CrewList.ItemsSource = crews;
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x2E, 0xE6, 0xA8));
            StatusText.Text = $"En ligne : {crews.Count} membre(s)";
            _timer.Start();
            await RefreshAsync();
            await LoadHistoriqueAsync();
        }
        catch (Exception ex)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x5C, 0x7A));
            StatusText.Text = "Connexion impossible : " + ex.Message;
        }
    }

    private void OpenChatButton_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null) { StatusText.Text = "Connecte-toi d'abord."; return; }
        new ChatWindow(_api) { Owner = this }.Show();
    }

    private async void CrewList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = CrewList.SelectedItem as Crew;
        PlanContent.Text = "Clique sur « Générer le plan » pour obtenir le plan de "
            + (_selected?.Name ?? "l'équipage") + ".";
        await RefreshAsync();
        await LoadHistoriqueAsync();
    }

    private async void GeneratePlanButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadPlanAsync();
    }

    // ---------- Rafraichissement ----------

    private async Task RefreshAsync()
    {
        if (_api == null) return;
        try
        {
            if (_selected != null)
            {
                var st = await _api.GetStatusAsync(_selected.Id);
                if (st != null)
                {
                    _lastStatus = st;
                    CrewTitle.Text = $"{st.Name} {_selected.Role}";
                    ScoreText.Text = (st.ScoreGlobal?.ToString("0.0") ?? "") + " %";
                    IndicatorsGrid.ItemsSource = st.Indicateurs
                        .Select(kv => new IndicatorRow(kv))
                        .ToList();
                    UpdateBaselineLine();
                }
            }
            AlertsGrid.ItemsSource = await _api.GetAlertsAsync();
        }
        catch { }
    }

    // ---------- Graphe ----------

    private async void IndicatorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentIndicatorKey = Labels.KeyFromFr(IndicatorSelector.SelectedItem?.ToString() ?? "bone_density");
        TrendChart.YAxes.First().Name = Labels.Unit(_currentIndicatorKey);
        await LoadHistoriqueAsync();
    }

    private async Task LoadHistoriqueAsync()
    {
        if (_api == null) return;
        var crews = CrewList.ItemsSource as List<Crew> ?? new List<Crew>();
        foreach (var crew in crews)
        {
            var serie = _crewSeries[crew.Name];
            var visible = _selected == null || _selected.Id == crew.Id;
            serie.IsVisible = visible;
            if (!visible) continue;

            try
            {
                var h = await _api.GetHistoriqueAsync(crew.Id, _currentIndicatorKey, 90);
                var pts = _crewPoints[crew.Name];
                pts.Clear();
                if (h?.Points == null) continue;
                foreach (var p in h.Points)
                    if (DateTime.TryParse(p.Date, CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var dt))
                        pts.Add(new DateTimePoint(dt, p.Valeur));
            }
            catch { }
        }
        AjusterAxes();
        UpdateBaselineLine();
        UpdateChartInfo();
    }

    private void AjusterAxes()
    {
        double? min = null, max = null;
        _minDate = null; _maxDate = null;
        foreach (var kv in _crewSeries)
        {
            if (!kv.Value.IsVisible) continue;
            foreach (var p in _crewPoints[kv.Key])
            {
                if (!p.Value.HasValue) continue;
                min = min.HasValue ? Math.Min(min.Value, p.Value.Value) : p.Value.Value;
                max = max.HasValue ? Math.Max(max.Value, p.Value.Value) : p.Value.Value;
                _minDate = _minDate.HasValue && _minDate.Value < p.DateTime ? _minDate : p.DateTime;
                _maxDate = _maxDate.HasValue && _maxDate.Value > p.DateTime ? _maxDate : p.DateTime;
            }
        }
        var axe = TrendChart.YAxes.First();
        if (min.HasValue && max.HasValue)
        {
            var marge = (max.Value - min.Value) * 0.15 + 0.5;
            axe.MinLimit = min.Value - marge;
            axe.MaxLimit = max.Value + marge;
        }
        else
        {
            axe.MinLimit = null;
            axe.MaxLimit = null;
        }
    }

    // ligne pointillee "Baseline" a la valeur de reference du membre selectionne
    private void UpdateBaselineLine()
    {
        _baselinePoints.Clear();
        double? baseVal = null;
        if (_selected != null && _lastStatus?.Indicateurs != null
            && _lastStatus.Indicateurs.TryGetValue(_currentIndicatorKey, out var ind))
            baseVal = ind.Baseline;

        if (baseVal.HasValue && _minDate.HasValue && _maxDate.HasValue)
        {
            _baselinePoints.Add(new DateTimePoint(_minDate.Value, baseVal.Value));
            _baselinePoints.Add(new DateTimePoint(_maxDate.Value, baseVal.Value));
            _baselineSeries.IsVisible = true;
        }
        else
        {
            _baselineSeries.IsVisible = false;
        }
    }

    // libelle contextuel : quel indicateur, quelle baseline
    private void UpdateChartInfo()
    {
        var unite = Labels.Unit(_currentIndicatorKey);
        var txt = Labels.Fr(_currentIndicatorKey) + (unite.Length > 0 ? $" ({unite})" : "");
        if (_baselineSeries.IsVisible && _baselinePoints.Count > 0)
            txt += $"   baseline : {_baselinePoints[0].Value:0.0} {unite} (pointillés)";
        ChartInfo.Text = txt;
    }

    // ---------- Plan ----------

    private async Task LoadPlanAsync()
    {
        if (_api == null) return;
        if (_selected == null)
        {
            PlanContent.Text = "Sélectionne d'abord un membre dans la liste ÉQUIPAGE.";
            return;
        }
        PlanContent.Text = "Calcul du plan par GRAVITY Core…";
        try
        {
            var plan = await _api.GetPlanAsync(_selected.Id);
            if (plan == null) return;
            if (plan.Erreur != null) { PlanContent.Text = plan.Erreur; return; }

            var sb = new StringBuilder();
            sb.AppendLine(plan.IndicateursEnAlerte.Count > 0
                ? "Indicateurs en alerte : " + string.Join(", ", plan.IndicateursEnAlerte.Select(Labels.Fr))
                : "Aucun indicateur en alerte : le membre est dans sa baseline.");
            if (plan.Blessures?.Count > 0)
                sb.AppendLine("Blessures : " + string.Join(", ", plan.Blessures)
                    + (plan.ImpactInterdit ? "  →  exercices à impact exclus, substitutions proposées." : ""));
            sb.AppendLine();
            foreach (var kv in plan.Plan)
            {
                sb.AppendLine($"{Labels.Fr(kv.Key)} ({Labels.Unit(kv.Key)}) :");
                foreach (var ex in kv.Value)
                    sb.AppendLine($"   • {ex.Nom} {ex.Dose} intensité {ex.Intensite}");
                sb.AppendLine();
            }
            if (plan.Substitutions?.Count > 0)
            {
                sb.AppendLine("Substitutions (blessure) :");
                foreach (var s in plan.Substitutions)
                    sb.AppendLine("   • " + s);
                sb.AppendLine();
            }
            sb.AppendLine(plan.Note);
            PlanContent.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            PlanContent.Text = "Erreur : " + ex.Message;
        }
    }
}