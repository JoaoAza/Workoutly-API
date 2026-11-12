using Workoutly.Mobile.Services;
namespace Workoutly.Mobile;

public partial class WorkoutsPage : ContentPage
{
    private readonly AccountClient _accounts;
    private readonly string? _studentId;
    private readonly int _offsetMinutes = (int)DateTimeOffset.Now.Offset.TotalMinutes;
    private bool _busy;
    private bool _initialized;

    public WorkoutsPage(AccountClient accounts, string? studentId = null, string? studentName = null)
    {
        InitializeComponent(); _accounts = accounts; _studentId = studentId;
        if (studentId is not null)
        {
            Heading.Text = "Histórico do aluno";
            Subtitle.Text = $"{studentName} • acompanhamento somente de leitura";
        }
        FromDate.MinimumDate = ToDate.MinimumDate = new(2000, 1, 1);
        FromDate.MaximumDate = ToDate.MaximumDate = DateTime.Today;
        FromDate.Date = DateTime.Today.AddDays(-29); ToDate.Date = DateTime.Today;
        PeriodHint.Text = $"Até 366 dias • datas no fuso UTC{(_offsetMinutes < 0 ? "−" : "+")}{Math.Abs(_offsetMinutes) / 60:00}:{Math.Abs(_offsetMinutes) % 60:00}";
        _initialized = true;
    }
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }

    private void ClearResults()
    {
        ResultPanel.IsVisible = false;
        BindableLayout.SetItemsSource(WorkoutsList, null);
        ExercisePicker.ItemsSource = null; PointsPanel.Children.Clear(); LoadChart.Drawable = null;
    }
    private void OnPeriodChanged(object? sender, DateChangedEventArgs args)
    {
        if (!_initialized || _busy) return;
        ClearResults(); ErrorText.IsVisible = false;
    }
    private async Task LoadAsync()
    {
        if (_busy) return;
        SetBusy(true); ErrorText.IsVisible = false; ClearResults();
        try
        {
            if (FromDate.Date is not { } start || ToDate.Date is not { } end)
                throw new AccountException("Escolha as duas datas do período.");
            var from = DateOnly.FromDateTime(start); var to = DateOnly.FromDateTime(end);
            if (to < from || to.DayNumber - from.DayNumber >= 366)
                throw new AccountException("Informe as datas em ordem e um período de até 366 dias.");
            var history = await _accounts.GetHistoryAsync(from, to, _offsetMinutes, _studentId);
            SessionCount.Text = history.SessionCount.ToString(); ActiveDays.Text = history.ActiveDays.ToString();
            Volume.Text = $"{history.VolumeKg:0.##} kg"; SetCount.Text = history.SetCount.ToString();
            TotalsText.Text = $"{history.RepetitionCount} repetições • {history.DurationSeconds / 3600:00}:{history.DurationSeconds / 60 % 60:00}:{history.DurationSeconds % 60:00} de treino\nVolume = soma de repetições × carga das séries registradas.";
            FrequencyText.Text = history.Days.Length == 0 ? "Nenhum dia com treino registrado." : string.Join("\n", history.Days.Select(day => $"{day.Date:dd/MM/yyyy} — {day.SessionCount} treino(s)"));
            BindableLayout.SetItemsSource(WorkoutsList, history.Sessions);
            EmptyText.IsVisible = history.Sessions.Length == 0;
            EvolutionCard.IsVisible = history.Exercises.Length > 0;
            ExercisePicker.ItemsSource = history.Exercises;
            if (history.Exercises.Length > 0) ExercisePicker.SelectedIndex = 0;
            ResultPanel.IsVisible = true;
        }
        catch (SessionExpiredException exception) { await DisplayAlertAsync("Acesso", exception.Message, "OK"); await Navigation.PopToRootAsync(); }
        catch (AccountException exception) { ErrorText.Text = exception.Message; ErrorText.IsVisible = true; }
        finally { SetBusy(false); }
    }
    private void OnExerciseChanged(object? sender, EventArgs args)
    {
        PointsPanel.Children.Clear(); LoadChart.Drawable = null;
        if (ExercisePicker.SelectedItem is not ExerciseEvolutionItem exercise) return;
        LoadChart.Drawable = new LoadEvolutionChart(exercise.Points,
            (Color)Application.Current!.Resources["Accent"], (Color)Application.Current.Resources["TextSecondary"]);
        LoadChart.Invalidate();
        ChartCaption.Text = $"{exercise.Points[0].StartedAt.ToOffset(TimeSpan.FromMinutes(_offsetMinutes)):dd/MM/yyyy} a {exercise.Points[^1].StartedAt.ToOffset(TimeSpan.FromMinutes(_offsetMinutes)):dd/MM/yyyy} • {exercise.Points.Length} registro(s)" +
            (exercise.Points.Length == 1 ? "\nUm registro: ainda não há comparação ao longo do tempo." : "");
        foreach (var point in exercise.Points)
        {
            PointsPanel.Children.Add(new Label
            {
                Text = $"{point.StartedAt.ToOffset(TimeSpan.FromMinutes(_offsetMinutes)):dd/MM/yyyy HH:mm} • maior carga {point.MaxWeightKg:0.##} kg\n{point.SetCount} série(s) • {point.RepetitionCount} repetições • volume {point.VolumeKg:0.##} kg",
                Style = (Style)Application.Current.Resources["SecondaryText"]
            });
        }
    }
    private async void OnRefresh(object? sender, EventArgs args) => await LoadAsync();
    private async void OnOpen(object? sender, EventArgs args)
    {
        if (_busy || sender is not Button { CommandParameter: string id }) return;
        SetBusy(true);
        try { await Navigation.PushAsync(new WorkoutDetailsPage(_accounts, id, _studentId)); }
        finally { SetBusy(false); }
    }
    private void SetBusy(bool busy)
    {
        _busy = busy; Loading.IsVisible = Loading.IsRunning = busy;
        RefreshButton.IsEnabled = FromDate.IsEnabled = ToDate.IsEnabled = WorkoutsList.IsEnabled = !busy;
    }
}

internal sealed class LoadEvolutionChart(LoadPointItem[] points, Color accent, Color secondary) : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (points.Length == 0) return;
        const float left = 53, top = 12, bottom = 22;
        var width = Math.Max(1, dirtyRect.Width - left - 10);
        var height = Math.Max(1, dirtyRect.Height - top - bottom);
        var max = (float)points.Max(p => p.MaxWeightKg);
        var scale = max > 0 ? max : 1;
        canvas.StrokeColor = secondary; canvas.StrokeSize = 1;
        canvas.DrawLine(left, top, left, top + height); canvas.DrawLine(left, top + height, left + width, top + height);
        canvas.FontColor = secondary; canvas.FontSize = 11;
        canvas.DrawString($"{max:0.##}", 0, top - 6, left - 5, 18, HorizontalAlignment.Right, VerticalAlignment.Center);
        canvas.DrawString("0 kg", 0, top + height - 9, left - 5, 18, HorizontalAlignment.Right, VerticalAlignment.Center);
        var first = points[0].StartedAt.ToUnixTimeMilliseconds();
        var span = points[^1].StartedAt.ToUnixTimeMilliseconds() - first;
        PointF Position(LoadPointItem point) => new(left + (span == 0 ? width / 2 : (float)((double)(point.StartedAt.ToUnixTimeMilliseconds() - first) / span * width)), top + height - (float)point.MaxWeightKg / scale * height);
        canvas.StrokeColor = canvas.FillColor = accent; canvas.StrokeSize = 2;
        for (var i = 0; i < points.Length; i++)
        {
            var p = Position(points[i]);
            if (i > 0) { var previous = Position(points[i - 1]); canvas.DrawLine(previous.X, previous.Y, p.X, p.Y); }
            canvas.FillCircle(p.X, p.Y, 4);
        }
    }
}
