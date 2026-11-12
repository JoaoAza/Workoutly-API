using System.Globalization;
using Workoutly.Mobile.Services;

namespace Workoutly.Mobile;

public partial class ExercisesPage : ContentPage
{
    private readonly AccountClient _accounts;
    private ExerciseItem[] _exercises = [];
    private bool _busy;
    private bool _opening;

    public ExercisesPage(AccountClient accounts)
    {
        InitializeComponent();
        _accounts = accounts;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        SetBusy(true);
        ErrorText.IsVisible = false;
        _exercises = []; ExerciseList.ItemsSource = null; ExerciseList.IsVisible = false;
        StatusText.Text = "Carregando exercícios…";
        try
        {
            _exercises = await _accounts.GetExercisesAsync();
            ApplyFilter();
            ExerciseList.IsVisible = true; RefreshButton.Text = "Atualizar";
        }
        catch (SessionExpiredException exception)
        {
            _exercises = [];
            ApplyFilter();
            await DisplayAlertAsync("Acesso", exception.Message, "OK");
            await Navigation.PopToRootAsync();
        }
        catch (AccountException exception)
        {
            ErrorText.Text = exception.Message;
            ErrorText.IsVisible = true;
            StatusText.Text = "Lista não carregada."; RefreshButton.Text = "Tentar novamente";
        }
        finally { SetBusy(false); }
    }

    private void ApplyFilter()
    {
        var query = Search.Text?.Trim() ?? string.Empty;
        var filtered = _exercises.Where(item => (!OnlyMine.IsToggled || item.IsMine) &&
            (CultureInfo.CurrentCulture.CompareInfo.IndexOf($"{item.Name} {item.MuscleGroup} {item.Equipment}", query,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)).ToArray();
        ExerciseList.ItemsSource = filtered;
        StatusText.Text = $"{filtered.Length} exercício(s) • toque para ver detalhes";
    }

    private void OnSearch(object? sender, TextChangedEventArgs args) => ApplyFilter();
    private void OnFilter(object? sender, ToggledEventArgs args) => ApplyFilter();
    private async void OnRefresh(object? sender, EventArgs args) => await LoadAsync();

    private async void OnCreate(object? sender, EventArgs args)
    {
        if (_busy || _opening) return;
        _opening = true;
        try { await Navigation.PushAsync(new ExercisePage(_accounts)); }
        finally { _opening = false; }
    }

    private async void OnSelected(object? sender, SelectionChangedEventArgs args)
    {
        if (_busy || _opening || args.CurrentSelection.FirstOrDefault() is not ExerciseItem item) return;
        _opening = true;
        ExerciseList.SelectedItem = null;
        try { await Navigation.PushAsync(new ExercisePage(_accounts, item)); }
        finally { _opening = false; }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Loading.IsVisible = Loading.IsRunning = busy;
        CreateButton.IsEnabled = RefreshButton.IsEnabled = ExerciseList.IsEnabled = !busy;
        Search.IsEnabled = OnlyMine.IsEnabled = !busy && ExerciseList.IsVisible;
    }
}
