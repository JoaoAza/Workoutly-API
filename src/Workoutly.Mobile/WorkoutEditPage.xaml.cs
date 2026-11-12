using System.Globalization;
using System.Net;
using Workoutly.Mobile.Services;
namespace Workoutly.Mobile;

public partial class WorkoutEditPage : ContentPage, IConfirmLeave
{
    private sealed record SetRow(VerticalStackLayout View, Label Order, Entry Repetitions, Entry Weight);
    private sealed record ExerciseRows(PlanExerciseItem Item, VerticalStackLayout Panel, Label Empty, List<SetRow> Sets);
    private readonly AccountClient _accounts;
    private readonly string _id;
    private readonly List<ExerciseRows> _exercises = [];
    private int _version;
    private string _editId = Guid.NewGuid().ToString();
    private UpdateWorkoutPayload? _pending;
    private bool _busy;
    private bool _allowLeave;
    private string[] _baseline = [];

    public WorkoutEditPage(AccountClient accounts, WorkoutSessionItem session)
    {
        InitializeComponent(); _accounts = accounts; _id = session.Id; ShowSession(session);
        FormFeedback.FollowError(PageScroll, ErrorText);
    }
    private string[] FormValues() => new[] { HoursEntry.Text ?? "", MinutesEntry.Text ?? "", SecondsEntry.Text ?? "", NotesEntry.Text ?? "" }
        .Concat(_exercises.SelectMany(exercise => new[] { exercise.Item.ExerciseId }.Concat(exercise.Sets.SelectMany(set => new[] { set.Repetitions.Text ?? "", set.Weight.Text ?? "" })))).ToArray();
    private Style StyleFor(string key) => (Style)Application.Current!.Resources[key];
    private void ShowSession(WorkoutSessionItem session)
    {
        if (session.PlanExercises is null) throw new AccountException("O serviço não forneceu os exercícios da ficha original.");
        _version = session.Version; _editId = Guid.NewGuid().ToString(); _pending = null;
        Heading.Text = session.PlanName;
        DateText.Text = $"Início em {session.StartedAt.ToLocalTime():dd/MM/yyyy 'às' HH:mm} • ficha na versão {session.PlanVersion}";
        HoursEntry.Text = (session.DurationSeconds / 3600).ToString(CultureInfo.InvariantCulture);
        MinutesEntry.Text = (session.DurationSeconds / 60 % 60).ToString(CultureInfo.InvariantCulture);
        SecondsEntry.Text = (session.DurationSeconds % 60).ToString(CultureInfo.InvariantCulture);
        NotesEntry.Text = session.Notes;
        _exercises.Clear(); ExercisesPanel.Children.Clear();
        foreach (var item in session.PlanExercises)
        {
            var panel = new VerticalStackLayout { Spacing = 12 };
            var empty = new Label { Text = "Sem série registrada neste exercício.", Style = StyleFor("SecondaryText") };
            var rows = new ExerciseRows(item, panel, empty, []);
            _exercises.Add(rows);
            var actual = session.Exercises.FirstOrDefault(e => e.ExerciseId == item.ExerciseId);
            if (actual is not null) foreach (var set in actual.Sets) AddSet(rows, set);
            UpdateRows(rows);
            var add = new Button { Text = "Adicionar série", Style = StyleFor("SecondaryButton"), AutomationId = $"workout_edit_add_{item.ExerciseId}" };
            add.Clicked += (_, _) =>
            {
                if (_busy || _pending is not null) return;
                if (rows.Sets.Count >= 20) { ShowError("Use até 20 séries por exercício."); return; }
                AddSet(rows, null);
            };
            ExercisesPanel.Children.Add(new Border { Style = StyleFor("SurfaceCard"), Content = new VerticalStackLayout { Spacing = 12, Children = {
                new Label { Text = item.Name, FontFamily = "InterSemiBold", FontSize = 19 },
                new Label { Text = $"{item.MuscleGroup} • {item.Equipment}", Style = StyleFor("SecondaryText") }, empty, panel, add } } });
        }
        _baseline = FormValues();
    }
    private void AddSet(ExerciseRows exercise, WorkoutSetItem? set)
    {
        var key = Guid.NewGuid().ToString("N");
        var order = new Label { Style = StyleFor("FieldLabel") };
        var reps = new Entry { Text = set?.Repetitions.ToString(CultureInfo.InvariantCulture), Placeholder = "Repetições", Keyboard = Keyboard.Numeric, MaxLength = 3, AutomationId = $"workout_edit_reps_{key}" };
        var weight = new Entry { Text = set?.WeightKg.ToString("0.##", CultureInfo.CurrentCulture), Placeholder = "0 para peso corporal", Keyboard = Keyboard.Numeric, MaxLength = 7, AutomationId = $"workout_edit_weight_{key}" };
        var fields = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 8 };
        Border Field(string label, Entry entry) => new() { Style = StyleFor("InputBorder"), Padding = new Thickness(10, 6), Content = new VerticalStackLayout { Children = { new Label { Text = label, Style = StyleFor("FieldLabel") }, entry } } };
        fields.Add(Field("Repetições", reps)); fields.Add(Field("Carga (kg)", weight), 1);
        var remove = new Button { Text = "Remover série", Style = StyleFor("TextButton"), AutomationId = $"workout_edit_remove_{key}" };
        var view = new VerticalStackLayout { Spacing = 6, Children = { order, fields, remove } };
        var row = new SetRow(view, order, reps, weight);
        remove.Clicked += async (_, _) =>
        {
            if (_busy || _pending is not null || !exercise.Sets.Contains(row)) return;
            if (!await DisplayAlertAsync("Remover série?", "Esta série sairá da correção. A alteração só será aplicada ao salvar.", "Remover", "Cancelar")) return;
            exercise.Sets.Remove(row); exercise.Panel.Children.Remove(view); UpdateRows(exercise);
        };
        exercise.Sets.Add(row); exercise.Panel.Children.Add(view); UpdateRows(exercise);
    }
    private static void UpdateRows(ExerciseRows exercise)
    {
        for (var i = 0; i < exercise.Sets.Count; i++) exercise.Sets[i].Order.Text = $"Série {i + 1:00}";
        exercise.Empty.IsVisible = exercise.Sets.Count == 0;
    }
    private UpdateWorkoutPayload ReadCorrection()
    {
        if (!int.TryParse(HoursEntry.Text, out var hours) || hours is < 0 or > 24 ||
            !int.TryParse(MinutesEntry.Text, out var minutes) || minutes is < 0 or > 59 ||
            !int.TryParse(SecondsEntry.Text, out var seconds) || seconds is < 0 or > 59)
            throw new AccountException("Revise horas, minutos e segundos da duração.");
        var duration = hours * 3600 + minutes * 60 + seconds;
        if (duration is < 1 or > 86400) throw new AccountException("A duração deve estar entre 1 segundo e 24 horas.");
        var exercises = new List<WorkoutExercisePayload>();
        foreach (var exercise in _exercises)
        {
            var sets = new List<WorkoutSetItem>();
            foreach (var row in exercise.Sets)
            {
                if (!int.TryParse(row.Repetitions.Text, out var reps) || reps is < 1 or > 200 ||
                    !decimal.TryParse(row.Weight.Text?.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var weight) ||
                    weight is < 0 or > 2000 || decimal.Round(weight, 2) != weight)
                    throw new AccountException($"Revise as séries de {exercise.Item.Name}: 1 a 200 repetições e 0 a 2000 kg, com até duas casas decimais. Remova séries vazias que não foram realizadas.");
                sets.Add(new(reps, weight));
            }
            if (sets.Count > 0) exercises.Add(new(exercise.Item.ExerciseId, sets.ToArray()));
        }
        if (exercises.Count == 0) throw new AccountException("Mantenha pelo menos uma série realizada no treino.");
        return new(_editId, _version, duration, NotesEntry.Text?.Trim() ?? string.Empty, exercises.ToArray());
    }
    private async void OnSave(object? sender, EventArgs args)
    {
        if (_busy) return;
        SetBusy(true); ErrorText.IsVisible = false;
        try
        {
            if (_pending is null)
            {
                var correction = ReadCorrection();
                if (!await DisplayAlertAsync("Salvar correção?", "As séries, a duração e as observações serão atualizadas neste treino.", "Salvar", "Continuar editando")) return;
                _pending = correction;
            }
            Form.IsEnabled = false;
            await _accounts.UpdateWorkoutAsync(_id, _pending);
            _allowLeave = true;
            await Navigation.PopAsync();
        }
        catch (SessionExpiredException exception) { _allowLeave = true; await DisplayAlertAsync("Acesso", exception.Message, "OK"); await Navigation.PopToRootAsync(); }
        catch (AccountException exception)
        {
            if (exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict) _pending = null;
            ShowError(exception.Message);
        }
        finally
        {
            SetBusy(false);
            if (ErrorText.IsVisible) await PageScroll.ScrollToAsync(SaveButton, ScrollToPosition.End, true);
        }
    }
    private async void OnReload(object? sender, EventArgs args)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            if (!await DisplayAlertAsync("Recarregar registro?", "Os campos desta edição serão substituídos pelos dados atualmente salvos.", "Recarregar", "Cancelar")) return;
            var session = await _accounts.GetWorkoutAsync(_id);
            ShowSession(session); ErrorText.IsVisible = false;
        }
        catch (SessionExpiredException exception) { _allowLeave = true; await DisplayAlertAsync("Acesso", exception.Message, "OK"); await Navigation.PopToRootAsync(); }
        catch (AccountException exception) { ShowError(exception.Message); }
        finally { SetBusy(false); }
    }
    public Task<bool> CanLeaveAsync()
    {
        if (_allowLeave) return Task.FromResult(true);
        if (_busy) return Task.FromResult(false);
        return _pending is null && FormValues().SequenceEqual(_baseline) ? Task.FromResult(true) :
            DisplayAlertAsync("Descartar correção?", "Alterações ainda não salvas serão descartadas. Se o envio falhou, recarregue o registro para conferir o resultado.", "Descartar e voltar", "Continuar editando");
    }
    private async void OnCancel(object? sender, EventArgs args) { if (!_busy) await Navigation.PopAsync(); }
    private void SetBusy(bool busy)
    {
        _busy = busy; Loading.IsVisible = Loading.IsRunning = busy;
        SaveButton.IsEnabled = ReloadButton.IsEnabled = CancelButton.IsEnabled = !busy;
        Form.IsEnabled = !busy && _pending is null;
        PendingText.IsVisible = _pending is not null;
        SaveButton.Text = _pending is null ? "Salvar correção" : "Tentar salvar correção novamente";
    }
    private void ShowError(string message) { ErrorText.Text = message; ErrorText.IsVisible = true; }
}
