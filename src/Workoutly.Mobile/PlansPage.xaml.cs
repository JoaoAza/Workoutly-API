using Workoutly.Mobile.Services;
namespace Workoutly.Mobile;

public partial class PlansPage : ContentPage
{
    private readonly AccountClient _accounts;
    private readonly string? _studentId;
    private bool _busy;
    public bool CanStartWorkout => _studentId is null;
    public PlansPage(AccountClient accounts, string? studentId = null, string? studentName = null)
    {
        InitializeComponent();
        _accounts = accounts;
        _studentId = studentId;
        OnPropertyChanged(nameof(CanStartWorkout));
        if (studentId is not null)
        {
            Heading.Text = "Fichas do aluno";
            Subtitle.Text = studentName;
        }
    }
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async Task LoadAsync()
    {
        if (_busy) return;
        SetBusy(true);
        ErrorText.IsVisible = false;
        // Não manter dados de um aluno visíveis após perder o vínculo.
        PlansList.ItemsSource = null;
        PlansList.IsVisible = false;
        try
        {
            var plans = await _accounts.GetPlansAsync(_studentId);
            PlansList.ItemsSource = plans; PlansList.IsVisible = true;
            ListStatus.Text = $"{plans.Length} ficha(s) • abra para editar";
            RefreshButton.Text = "Atualizar";
        }
        catch (SessionExpiredException exception)
        {
            await DisplayAlertAsync("Acesso", exception.Message, "OK");
            await Navigation.PopToRootAsync();
        }
        catch (AccountException exception)
        {
            ErrorText.Text = exception.Message; ErrorText.IsVisible = true;
            ListStatus.Text = "Lista não carregada."; RefreshButton.Text = "Tentar novamente";
        }
        finally { SetBusy(false); }
    }
    private async void OnRefresh(object? sender, EventArgs args) => await LoadAsync();
    private async void OnCreate(object? sender, EventArgs args) => await OpenAsync(null);
    private async void OnOpen(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: PlanItem plan }) await OpenAsync(plan);
    }
    private async Task OpenAsync(PlanItem? plan)
    {
        if (_busy) return;
        SetBusy(true);
        try { await Navigation.PushAsync(new PlanPage(_accounts, _studentId, plan)); }
        finally { SetBusy(false); }
    }
    private async void OnStartWorkout(object? sender, EventArgs args)
    {
        if (_busy || _studentId is not null || sender is not Button { CommandParameter: PlanItem plan }) return;
        SetBusy(true); ErrorText.IsVisible = false;
        try
        {
            var current = await _accounts.GetPlanAsync(null, plan.Id);
            await Navigation.PushAsync(new WorkoutPage(_accounts, current));
        }
        catch (SessionExpiredException exception)
        {
            await DisplayAlertAsync("Acesso", exception.Message, "OK"); await Navigation.PopToRootAsync();
        }
        catch (AccountException exception) { ErrorText.Text = exception.Message; ErrorText.IsVisible = true; }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Loading.IsVisible = Loading.IsRunning = busy;
        if (busy) ListStatus.Text = "Carregando fichas…";
        CreateButton.IsEnabled = RefreshButton.IsEnabled = PlansList.IsEnabled = !busy;
    }
}
