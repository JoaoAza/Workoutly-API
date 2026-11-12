using System.Globalization;
using Workoutly.Mobile.Services;
namespace Workoutly.Mobile;

public partial class PlanExercisePickerPage : ContentPage
{
    private readonly AccountClient _accounts;
    private readonly HashSet<string> _selected;
    private readonly Action<ExerciseItem> _onSelect;
    private ExerciseItem[] _exercises = [];
    private bool _busy;
    public PlanExercisePickerPage(AccountClient accounts, HashSet<string> selected, Action<ExerciseItem> onSelect)
    {
        InitializeComponent(); _accounts = accounts; _selected = selected; _onSelect = onSelect;
    }
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async Task LoadAsync()
    {
        if (_busy) return;
        SetBusy(true); ErrorText.IsVisible = false;
        _exercises = []; ExerciseList.ItemsSource = null; ExerciseList.IsVisible = false;
        ListStatus.Text = "Carregando exercícios…";
        try
        {
            _exercises = await _accounts.GetExercisesAsync(); ApplyFilter(); ExerciseList.IsVisible = true;
            RefreshButton.Text = "Atualizar"; ListStatus.Text = "Toque para incluir. Exercícios já adicionados ficam ocultos.";
        }
        catch (SessionExpiredException exception)
        {
            await DisplayAlertAsync("Acesso", exception.Message, "OK"); await Navigation.PopToRootAsync();
        }
        catch (AccountException exception)
        {
            ErrorText.Text = exception.Message; ErrorText.IsVisible = true;
            ListStatus.Text = "Lista não carregada."; RefreshButton.Text = "Tentar novamente";
        }
        finally { SetBusy(false); }
    }
    private void ApplyFilter()
    {
        var query = Search.Text?.Trim() ?? string.Empty;
        ExerciseList.ItemsSource = _exercises.Where(e => !_selected.Contains(e.Id) &&
            CultureInfo.CurrentCulture.CompareInfo.IndexOf($"{e.Name} {e.MuscleGroup} {e.Equipment}", query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0).ToArray();
    }
    private void OnSearch(object? sender, TextChangedEventArgs args) => ApplyFilter();
    private async void OnRefresh(object? sender, EventArgs args) => await LoadAsync();
    private async void OnCreate(object? sender, EventArgs args)
    {
        if (_busy) return;
        SetBusy(true);
        try { await Navigation.PushAsync(new ExercisePage(_accounts)); }
        finally { SetBusy(false); }
    }
    private async void OnSelected(object? sender, SelectionChangedEventArgs args)
    {
        if (_busy || args.CurrentSelection.FirstOrDefault() is not ExerciseItem item) return;
        SetBusy(true);
        try { _onSelect(item); await Navigation.PopAsync(); }
        finally { SetBusy(false); }
    }
    private void SetBusy(bool busy)
    {
        _busy = busy; Loading.IsVisible = Loading.IsRunning = busy;
        ExerciseList.IsEnabled = CreateButton.IsEnabled = RefreshButton.IsEnabled = !busy;
        Search.IsEnabled = !busy && ExerciseList.IsVisible;
    }
}
