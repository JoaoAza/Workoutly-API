using System.Diagnostics;
using System.Globalization;
using System.Net;
using Workoutly.Mobile.Services;
namespace Workoutly.Mobile;

public partial class WorkoutPage : ContentPage, IConfirmLeave
{
    private sealed record SetRow(Grid View, Entry Repetitions, Entry Weight, CheckBox Done);
    private sealed record ExerciseRows(PlanExerciseItem Item, VerticalStackLayout SetsPanel, List<SetRow> Sets);
    private readonly AccountClient _accounts;
    private readonly PlanItem _plan;
    private readonly string _id = Guid.NewGuid().ToString();
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();
    private readonly IDispatcherTimer _timer;
    private readonly List<ExerciseRows> _exercises = [];
    private CreateWorkoutPayload? _submission;
    private bool _busy;
    private bool _allowLeave;

    public WorkoutPage(AccountClient accounts, PlanItem plan)
    {
        InitializeComponent(); _accounts = accounts; _plan = plan;
        FormFeedback.FollowError(PageScroll, ErrorText);
        PlanName.Text = plan.Name;
        PlanNotes.Text = plan.Notes;
        PlanNotes.IsVisible = !string.IsNullOrEmpty(plan.Notes);
        foreach (var item in plan.Exercises) AddExercise(item);
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => UpdateDuration();
    }
    protected override void OnAppearing() { base.OnAppearing(); UpdateDuration(); if (_submission is null) _timer.Start(); }
    protected override void OnDisappearing() { _timer.Stop(); base.OnDisappearing(); }
    public Task<bool> CanLeaveAsync()
    {
        if (_allowLeave) return Task.FromResult(true);
        if (_busy) return Task.FromResult(false);
        return DisplayAlertAsync("Sair deste treino?", "Os dados que ainda não foram salvos serão descartados. Se houve falha no envio, confira o histórico antes de registrar outra vez.", "Sair do treino", "Continuar treino");
    }
    private int ElapsedSeconds => (int)Math.Min(int.MaxValue, Math.Max(1, Stopwatch.GetElapsedTime(_startTimestamp).TotalSeconds));
    private void UpdateDuration() => DurationText.Text = WorkoutSummaryItem.FormatDuration(_submission?.DurationSeconds ?? ElapsedSeconds);
    private Style StyleFor(string key) => (Style)Application.Current!.Resources[key];
    private void AddExercise(PlanExerciseItem item)
    {
        var panel = new VerticalStackLayout { Spacing = 8 };
        var rows = new ExerciseRows(item, panel, []);
        _exercises.Add(rows);
        for (var i = 0; i < item.Sets; i++) AddSet(rows);
        var add = new Button { Text = "Adicionar série", Style = StyleFor("TextButton") };
        var remove = new Button { Text = "Remover última série", Style = StyleFor("TextButton") };
        add.Clicked += (_, _) => { if (_busy || _submission is not null) return; if (rows.Sets.Count < 20) AddSet(rows); else ShowError("Use até 20 séries por exercício."); };
        remove.Clicked += async (_, _) =>
        {
            if (_busy || _submission is not null || rows.Sets.Count == 0) return;
            var row = rows.Sets[^1];
            if ((row.Done.IsChecked || !string.IsNullOrWhiteSpace(row.Repetitions.Text) || !string.IsNullOrWhiteSpace(row.Weight.Text)) &&
                !await DisplayAlertAsync("Remover série?", "Os campos desta série serão descartados.", "Remover", "Cancelar")) return;
            rows.Sets.RemoveAt(rows.Sets.Count - 1); panel.Children.Remove(row.View); UpdateCompleted();
        };
        ExercisesPanel.Children.Add(new Border { Style = StyleFor("SurfaceCard"), Content = new VerticalStackLayout { Spacing = 10, Children = {
            new Label { Text = item.Name, FontFamily = "InterSemiBold", FontSize = 19 },
            new Label { Text = $"Previsto: {item.Sets} séries × {item.Repetitions} repetições • {item.Equipment}", Style = StyleFor("SecondaryText") },
            panel, add, remove } } });
    }
    private void AddSet(ExerciseRows exercise)
    {
        var number = exercise.Sets.Count + 1;
        var reps = new Entry { Placeholder = $"Ex.: {exercise.Item.Repetitions}", Keyboard = Keyboard.Numeric, MaxLength = 3, AutomationId = $"workout_reps_{exercise.Item.ExerciseId}_{number}" };
        var weight = new Entry { Placeholder = "kg", Keyboard = Keyboard.Numeric, MaxLength = 7, AutomationId = $"workout_weight_{exercise.Item.ExerciseId}_{number}" };
        var done = new CheckBox { Color = (Color)Application.Current!.Resources["Accent"], VerticalOptions = LayoutOptions.Center, AutomationId = $"workout_done_{exercise.Item.ExerciseId}_{number}" };
        SemanticProperties.SetDescription(done, $"Série {number} feita de {exercise.Item.Name}");
        done.CheckedChanged += (_, _) => UpdateCompleted();
        Border Field(string label, Entry entry) => new() { Style = StyleFor("InputBorder"), Padding = new Thickness(8, 6), Content = new VerticalStackLayout { Children = { new Label { Text = label, Style = StyleFor("FieldLabel") }, entry } } };
        var view = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)], ColumnSpacing = 6 };
        view.Add(new Label { Text = $"{number:00}", FontSize = 12, VerticalOptions = LayoutOptions.Center });
        view.Add(Field("Reps", reps), 1); view.Add(Field("Carga (kg)", weight), 2);
        view.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { new Label { Text = "Feita", Style = StyleFor("FieldLabel"), HorizontalOptions = LayoutOptions.Center }, done } }, 3);
        exercise.Sets.Add(new(view, reps, weight, done)); exercise.SetsPanel.Children.Add(view);
    }
    private void UpdateCompleted()
    {
        var count = _exercises.Sum(e => e.Sets.Count(s => s.Done.IsChecked));
        CompletedText.Text = $"{count} série(s) marcada(s) como realizada(s)";
    }
    private WorkoutExercisePayload[] ReadPerformedSets()
    {
        var exercises = new List<WorkoutExercisePayload>();
        foreach (var exercise in _exercises)
        {
            var sets = new List<WorkoutSetItem>();
            foreach (var row in exercise.Sets)
            {
                if (!row.Done.IsChecked)
                {
                    if (!string.IsNullOrWhiteSpace(row.Repetitions.Text) || !string.IsNullOrWhiteSpace(row.Weight.Text))
                        throw new AccountException($"Em {exercise.Item.Name}, marque Feita na série preenchida ou limpe seus campos.");
                    continue;
                }
                if (!int.TryParse(row.Repetitions.Text, out var reps) || reps is < 1 or > 200 ||
                    !decimal.TryParse(row.Weight.Text?.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var weight) ||
                    weight is < 0 or > 2000 || decimal.Round(weight, 2) != weight)
                    throw new AccountException($"Revise {exercise.Item.Name}: 1 a 200 repetições e 0 a 2000 kg, com até duas casas decimais. Informe 0 para peso corporal.");
                sets.Add(new(reps, weight));
            }
            if (sets.Count > 0) exercises.Add(new(exercise.Item.ExerciseId, sets.ToArray()));
        }
        if (exercises.Count == 0) throw new AccountException("Registre e marque pelo menos uma série realizada para salvar o treino.");
        return exercises.ToArray();
    }
    private async void OnSave(object? sender, EventArgs args)
    {
        if (_busy) return;
        _busy = true; SaveButton.IsEnabled = false; ErrorText.IsVisible = false;
        try
        {
            if (_submission is null)
            {
                var exercises = ReadPerformedSets();
                var duration = ElapsedSeconds;
                if (duration > 86400) throw new AccountException("A duração máxima de um registro é de 24 horas.");
                if (!await DisplayAlertAsync("Concluir treino?", "Serão salvas somente as séries marcadas como Feita. Confira os valores antes de concluir.", "Concluir e salvar", "Continuar treino")) return;
                _submission = new(_id, _plan.Id, _plan.Version, _startedAt, duration, NotesEntry.Text?.Trim() ?? string.Empty, exercises);
                _timer.Stop(); UpdateDuration();
            }
            Form.IsEnabled = false; Loading.IsVisible = Loading.IsRunning = true;
            var saved = await _accounts.SaveWorkoutAsync(_submission);
            await Navigation.PushAsync(new WorkoutDetailsPage(_accounts, saved.Id));
            Navigation.RemovePage(this);
        }
        catch (SessionExpiredException exception)
        {
            _allowLeave = true;
            await DisplayAlertAsync("Acesso", exception.Message, "OK"); await Navigation.PopToRootAsync();
        }
        catch (AccountException exception)
        {
            if (exception.StatusCode == HttpStatusCode.BadRequest) { _submission = null; _timer.Start(); }
            ShowError(exception.Message);
        }
        finally
        {
            _busy = false; Form.IsEnabled = _submission is null;
            Loading.IsVisible = Loading.IsRunning = false; SaveButton.IsEnabled = true;
            SaveButton.Text = _submission is null ? "Concluir e salvar treino" : "Tentar salvar novamente";
            PendingText.IsVisible = SavedWorkoutsButton.IsVisible = _submission is not null;
            if (ErrorText.IsVisible) await PageScroll.ScrollToAsync(SaveButton, ScrollToPosition.End, true);
        }
    }
    private async void OnSavedWorkouts(object? sender, EventArgs args)
    {
        if (!_busy) await Navigation.PushAsync(new WorkoutsPage(_accounts));
    }
    private async void ShowError(string message)
    {
        ErrorText.Text = message; ErrorText.IsVisible = true;
        await PageScroll.ScrollToAsync(ErrorText, ScrollToPosition.End, true);
    }
}
