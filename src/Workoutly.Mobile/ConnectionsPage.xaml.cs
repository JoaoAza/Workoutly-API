using Workoutly.Mobile.Services;

namespace Workoutly.Mobile;

public partial class ConnectionsPage : ContentPage
{
    private readonly AccountClient _accounts;
    private readonly bool _teacher;
    private bool _busy;
    private bool _hasCode;

    public ConnectionsPage(AccountClient accounts, bool teacher)
    {
        InitializeComponent();
        _accounts = accounts;
        _teacher = teacher;
        Title = Heading.Text = teacher ? "Meus alunos" : "Meu professor";
        Subtitle.Text = teacher ? "Compartilhe seu código com os alunos para receber os vínculos." : "Gerencie seu vínculo de acompanhamento.";
        TeacherPanel.IsVisible = teacher;
        StudentPanel.IsVisible = !teacher;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ExecuteAsync(RefreshAsync);
    }

    private async Task RefreshAsync()
    {
        InvitationCard.IsVisible = StudentsList.IsVisible = CurrentTeacherCard.IsVisible = JoinCard.IsVisible = false;
        StudentsList.ItemsSource = null; StudentsCount.Text = "Carregando alunos…";
        if (_teacher)
        {
            var invitation = await _accounts.GetInvitationAsync();
            var students = await _accounts.GetStudentsAsync();
            ShowInvitation(invitation);
            StudentsList.ItemsSource = students;
            StudentsList.IsVisible = true;
            StudentsCount.Text = $"Alunos vinculados ({students.Length})";
        }
        else ShowEnrollment(await _accounts.GetEnrollmentAsync());
        RefreshButton.Text = StudentRefreshButton.Text = "Atualizar";
    }

    private void ShowInvitation(TeacherInvitation invitation)
    {
        _hasCode = invitation.Code is not null;
        InvitationCode.Text = invitation.Code ?? "Sem código ativo";
        InvitationExpiry.Text = invitation.ExpiresAt is { } expiry ? $"Válido até {expiry.ToLocalTime():dd/MM/yyyy HH:mm}" : "O código permite que o aluno se vincule à sua conta.";
        GenerateButton.Text = _hasCode ? "Renovar código" : "Gerar código";
        InvitationCard.IsVisible = true;
    }

    private void ShowEnrollment(EnrollmentInfo enrollment)
    {
        CurrentTeacherCard.IsVisible = enrollment.Teacher is not null;
        JoinCard.IsVisible = enrollment.Teacher is null;
        TeacherName.Text = enrollment.Teacher?.Name ?? string.Empty;
        TeacherEmail.Text = enrollment.Teacher?.Email ?? string.Empty;
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (_busy) return;
        SetBusy(true);
        ErrorText.IsVisible = false;
        try { await action(); }
        catch (SessionExpiredException exception)
        {
            await DisplayAlertAsync("Acesso", exception.Message, "OK");
            await Navigation.PopToRootAsync();
        }
        catch (AccountException exception)
        {
            ErrorText.Text = exception.Message;
            ErrorText.IsVisible = true;
            StudentsCount.Text = "Não foi possível atualizar.";
            RefreshButton.Text = StudentRefreshButton.Text = "Tentar novamente";
        }
        finally { SetBusy(false); }
    }

    private async void OnRefresh(object? sender, EventArgs args) => await ExecuteAsync(RefreshAsync);

    private async void OnGenerate(object? sender, EventArgs args) => await ExecuteAsync(async () =>
    {
        if (_hasCode && !await DisplayAlertAsync("Renovar código?", "O código anterior deixará de aceitar novos vínculos. Os alunos já vinculados permanecem na sua lista.", "Renovar", "Cancelar")) return;
        InvitationCard.IsVisible = false;
        ShowInvitation(await _accounts.CreateInvitationAsync());
    });

    private async void OnJoin(object? sender, EventArgs args) => await ExecuteAsync(async () =>
    {
        var code = JoinCode.Text ?? string.Empty;
        if (code.Trim().Length != 12) throw new AccountException("Informe o código de 12 caracteres enviado pelo professor.");
        if (!await DisplayAlertAsync("Vincular professor?", "O professor verá seu nome, e-mail e histórico de treinos, inclusive anteriores ao vínculo, e poderá gerenciar suas fichas. Você poderá remover o vínculo depois.", "Vincular", "Cancelar")) return;
        ShowEnrollment(await _accounts.JoinTeacherAsync(code));
        JoinCode.Text = string.Empty;
    });

    private async void OnLeave(object? sender, EventArgs args) => await ExecuteAsync(async () =>
    {
        if (!await DisplayAlertAsync("Remover vínculo?", "Você deixará de aparecer na lista deste professor. Sua conta e seus registros serão preservados.", "Remover", "Cancelar")) return;
        await _accounts.LeaveTeacherAsync();
        ShowEnrollment(new(null));
    });

    private async void OnRemoveStudent(object? sender, EventArgs args)
    {
        if (sender is not Button { CommandParameter: string id }) return;
        await ExecuteAsync(async () =>
        {
            if (!await DisplayAlertAsync("Remover vínculo?", "O aluno deixará de aparecer na sua lista. A conta e os registros dele serão preservados.", "Remover", "Cancelar")) return;
            await _accounts.RemoveStudentAsync(id);
            await RefreshAsync();
        });
    }

    private async void OnStudentPlans(object? sender, EventArgs args)
    {
        if (_busy || sender is not Button { CommandParameter: AccountProfile student }) return;
        SetBusy(true);
        try { await Navigation.PushAsync(new PlansPage(_accounts, student.Id, student.Name)); }
        finally { SetBusy(false); }
    }

    private async void OnStudentHistory(object? sender, EventArgs args)
    {
        if (_busy || sender is not Button { CommandParameter: AccountProfile student }) return;
        SetBusy(true);
        try { await Navigation.PushAsync(new WorkoutsPage(_accounts, student.Id, student.Name)); }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Loading.IsVisible = Loading.IsRunning = busy;
        TeacherPanel.IsEnabled = StudentPanel.IsEnabled = !busy;
    }
}
