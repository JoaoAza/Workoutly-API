using System.Globalization;
using Workoutly.Mobile.Services;
namespace Workoutly.Mobile;

public partial class PlanPage : ContentPage, IConfirmLeave
{
    private sealed record Row(PlanExerciseItem Item, Border View, Label Order, Entry Sets, Entry Repetitions);
    private readonly AccountClient _accounts;
    private readonly string? _studentId;
    private readonly string _id;
    private readonly List<Row> _rows = [];
    private int _version;
    private bool _busy;
    private bool _allowLeave;
    private string[] _baseline = [];

    public PlanPage(AccountClient accounts, string? studentId, PlanItem? plan)
    {
        InitializeComponent();
        FormFeedback.FollowError(PageScroll, ErrorText);
        _accounts = accounts;
        _studentId = studentId;
        _id = plan?.Id ?? Guid.NewGuid().ToString();
        if (plan is not null) ShowPlan(plan);
        _baseline = FormValues();
    }
    private string[] FormValues() => new[] { NameEntry.Text ?? "", NotesEntry.Text ?? "" }
        .Concat(_rows.SelectMany(row => new[] { row.Item.ExerciseId, row.Sets.Text ?? "", row.Repetitions.Text ?? "" })).ToArray();
    public Task<bool> CanLeaveAsync()
    {
        if (_allowLeave) return Task.FromResult(true);
        if (_busy) return Task.FromResult(false);
        return FormValues().SequenceEqual(_baseline) ? Task.FromResult(true) :
            DisplayAlertAsync("Descartar alterações da ficha?", "Os campos ainda não salvos serão perdidos. Você pode continuar editando para salvar depois.", "Descartar e voltar", "Continuar editando");
    }
    private Style StyleFor(string key) => (Style)Application.Current!.Resources[key];
    private void ShowPlan(PlanItem plan)
    {
        _version = plan.Version;
        Heading.Text = "Editar ficha";
        NameEntry.Text = plan.Name;
        NotesEntry.Text = plan.Notes;
        _rows.Clear();
        ItemsPanel.Children.Clear();
        foreach (var item in plan.Exercises) AddRow(item);
        ReloadButton.IsVisible = DeleteButton.IsVisible = true;
        _baseline = FormValues();
    }
    private void AddRow(PlanExerciseItem item)
    {
        var order = new Label { Style = StyleFor("Eyebrow") };
        var sets = new Entry { Text = item.Sets.ToString(CultureInfo.InvariantCulture), Keyboard = Keyboard.Numeric, MaxLength = 2, AutomationId = $"plan_sets_{item.ExerciseId}" };
        var reps = new Entry { Text = item.Repetitions.ToString(CultureInfo.InvariantCulture), Keyboard = Keyboard.Numeric, MaxLength = 3, AutomationId = $"plan_reps_{item.ExerciseId}" };
        var numbers = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 12 };
        Border Field(string title, Entry entry) => new() { Style = StyleFor("InputBorder"), Content = new VerticalStackLayout { Children = { new Label { Text = title, Style = StyleFor("FieldLabel") }, entry } } };
        numbers.Add(Field("Séries (1–20)", sets));
        numbers.Add(Field("Repetições (1–200)", reps), 1);
        var actions = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 4 };
        var up = new Button { Text = "Subir", Style = StyleFor("TextButton") };
        var down = new Button { Text = "Descer", Style = StyleFor("TextButton") };
        var remove = new Button { Text = "Remover", Style = StyleFor("TextButton") };
        var view = new Border { Style = StyleFor("SurfaceCard"), Content = new VerticalStackLayout { Spacing = 10, Children = {
            order, new Label { Text = item.Name, FontFamily = "InterSemiBold", FontSize = 18 },
            new Label { Text = $"{item.MuscleGroup} • {item.Equipment}", Style = StyleFor("SecondaryText") },
            new Label { Text = item.Description, Style = StyleFor("SecondaryText"), IsVisible = !string.IsNullOrEmpty(item.Description) }, numbers, actions } } };
        var row = new Row(item, view, order, sets, reps);
        up.Clicked += (_, _) => Move(row, -1);
        down.Clicked += (_, _) => Move(row, 1);
        remove.Clicked += async (_, _) =>
        {
            if (_busy) return;
            _busy = true;
            try
            {
                if (!await DisplayAlertAsync("Remover exercício da ficha?", $"{item.Name} e suas metas sairão desta edição. A mudança só será aplicada ao salvar.", "Remover", "Cancelar")) return;
                _rows.Remove(row); ItemsPanel.Children.Remove(view); Renumber();
            }
            finally { _busy = false; }
        };
        actions.Add(up); actions.Add(down, 1); actions.Add(remove, 2);
        _rows.Add(row);
        ItemsPanel.Children.Add(view);
        Renumber();
    }
    private void Move(Row row, int delta)
    {
        if (_busy) return;
        var index = _rows.IndexOf(row);
        var target = index + delta;
        if (target < 0 || target >= _rows.Count) return;
        _rows.RemoveAt(index); _rows.Insert(target, row);
        ItemsPanel.Children.RemoveAt(index); ItemsPanel.Children.Insert(target, row.View);
        Renumber();
    }
    private void Renumber()
    {
        ErrorText.IsVisible = false;
        for (var i = 0; i < _rows.Count; i++) _rows[i].Order.Text = $"EXERCÍCIO {i + 1:00}";
        CountText.Text = $"Exercícios ({_rows.Count})";
    }
    private async void OnAdd(object? sender, EventArgs args)
    {
        if (_busy) return;
        if (_rows.Count >= 50) { ShowError("Use até 50 exercícios por ficha."); return; }
        _busy = true;
        try
        {
            await Navigation.PushAsync(new PlanExercisePickerPage(_accounts, _rows.Select(r => r.Item.ExerciseId).ToHashSet(), item =>
                AddRow(new(item.Id, item.Name, item.MuscleGroup, item.Equipment, item.Description, 3, 10))));
        }
        finally { _busy = false; }
    }
    private async void OnSave(object? sender, EventArgs args) => await ExecuteAsync(async () =>
    {
        var name = NameEntry.Text?.Trim() ?? string.Empty;
        if (name.Length < 2) throw new AccountException("Informe um nome entre 2 e 120 caracteres.");
        if (_rows.Count == 0) throw new AccountException("Adicione pelo menos um exercício à ficha.");
        var items = new List<PlanExercisePayload>();
        foreach (var row in _rows)
        {
            if (!int.TryParse(row.Sets.Text, out var sets) || sets is < 1 or > 20 || !int.TryParse(row.Repetitions.Text, out var reps) || reps is < 1 or > 200)
                throw new AccountException($"Revise {row.Item.Name}: use de 1 a 20 séries e de 1 a 200 repetições.");
            items.Add(new(row.Item.ExerciseId, sets, reps));
        }
        await _accounts.SavePlanAsync(_studentId, _id, _version, name, NotesEntry.Text ?? string.Empty, items.ToArray());
        _allowLeave = true;
        await Navigation.PopAsync();
    });
    private async void OnReload(object? sender, EventArgs args) => await ExecuteAsync(async () =>
    {
        if (!await DisplayAlertAsync("Recarregar ficha?", "Os campos em edição serão substituídos pela versão salva no serviço.", "Recarregar", "Cancelar")) return;
        ShowPlan(await _accounts.GetPlanAsync(_studentId, _id));
    });
    private async void OnDelete(object? sender, EventArgs args) => await ExecuteAsync(async () =>
    {
        if (!await DisplayAlertAsync("Excluir ficha?", "A ficha sairá da lista. As versões salvas serão preservadas.", "Excluir", "Cancelar")) return;
        await _accounts.DeletePlanAsync(_studentId, _id, _version);
        _allowLeave = true;
        await Navigation.PopAsync();
    });
    private async Task ExecuteAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        Form.IsEnabled = false;
        Loading.IsVisible = Loading.IsRunning = true;
        SaveButton.Text = "Aguarde…";
        ErrorText.IsVisible = false;
        try { await action(); }
        catch (SessionExpiredException exception)
        {
            await DisplayAlertAsync("Acesso", exception.Message, "OK");
            _allowLeave = true;
            await Navigation.PopToRootAsync();
        }
        catch (AccountException exception) { ShowError(exception.Message); }
        finally
        {
            _busy = false; Form.IsEnabled = true; Loading.IsVisible = Loading.IsRunning = false; SaveButton.Text = "Salvar ficha";
            if (ErrorText.IsVisible) await PageScroll.ScrollToAsync(ErrorText, ScrollToPosition.End, true);
        }
    }
    private void ShowError(string message) { ErrorText.Text = message; ErrorText.IsVisible = true; }
}
