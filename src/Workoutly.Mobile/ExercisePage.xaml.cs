using Workoutly.Mobile.Services;

namespace Workoutly.Mobile;

public partial class ExercisePage : ContentPage, IConfirmLeave
{
    private readonly AccountClient _accounts;
    private bool _busy;
    private bool _saved;
    private readonly bool _readOnly;

    public ExercisePage(AccountClient accounts, ExerciseItem? exercise = null)
    {
        InitializeComponent();
        FormFeedback.FollowError(PageScroll, ErrorText);
        _accounts = accounts;
        _readOnly = exercise is not null;
        if (exercise is null) return;
        Title = "Detalhes do exercício";
        Heading.Text = exercise.Name;
        OriginText.Text = exercise.Origin;
        if (exercise.Visual is { } visual)
        {
            IllustrationCard.IsVisible = true;
            ExerciseIllustration.Source = visual.ImageSource;
            SemanticProperties.SetDescription(ExerciseIllustration, $"Ilustração de {exercise.Name}");
            InstructionCard.IsVisible = !string.IsNullOrWhiteSpace(visual.QuickInstruction);
            InstructionText.Text = visual.QuickInstruction;
        }
        NameEntry.Text = exercise.Name;
        GroupEntry.Text = exercise.MuscleGroup;
        EquipmentEntry.Text = exercise.Equipment;
        DescriptionEditor.Text = string.IsNullOrEmpty(exercise.Description) ? "Sem descrição." : exercise.Description;
        NameEntry.IsReadOnly = GroupEntry.IsReadOnly = EquipmentEntry.IsReadOnly = DescriptionEditor.IsReadOnly = true;
        SaveButton.IsVisible = false;
        PrivacyNote.IsVisible = !exercise.IsShared;
        if (!exercise.IsOwned && exercise.IsInPlan)
            PrivacyText.Text = "Exercício disponibilizado nas suas fichas. Os demais exercícios privados do professor não são compartilhados.";
    }

    public Task<bool> CanLeaveAsync()
    {
        if (_saved || _readOnly) return Task.FromResult(true);
        if (_busy) return Task.FromResult(false);
        var changed = new[] { NameEntry.Text, GroupEntry.Text, EquipmentEntry.Text, DescriptionEditor.Text }.Any(value => !string.IsNullOrEmpty(value));
        return !changed ? Task.FromResult(true) : DisplayAlertAsync("Descartar novo exercício?", "Os campos ainda não salvos serão perdidos.", "Descartar e voltar", "Continuar editando");
    }

    private async void OnSave(object? sender, EventArgs args)
    {
        if (_busy || _saved) return;
        SetBusy(true);
        ErrorText.IsVisible = false;
        try
        {
            await _accounts.CreateExerciseAsync(NameEntry.Text ?? string.Empty, GroupEntry.Text ?? string.Empty,
                EquipmentEntry.Text ?? string.Empty, DescriptionEditor.Text ?? string.Empty);
            _saved = true;
            await Navigation.PopAsync();
        }
        catch (SessionExpiredException exception)
        {
            await DisplayAlertAsync("Acesso", exception.Message, "OK");
            _saved = true;
            await Navigation.PopToRootAsync();
        }
        catch (AccountException exception)
        {
            ErrorText.Text = exception.Message;
            ErrorText.IsVisible = true;
        }
        finally
        {
            SetBusy(false);
            if (ErrorText.IsVisible) await PageScroll.ScrollToAsync(ErrorText, ScrollToPosition.End, true);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Loading.IsVisible = Loading.IsRunning = busy;
        Fields.IsEnabled = SaveButton.IsEnabled = !busy && !_saved;
        SaveButton.Text = busy ? "Salvando exercício…" : "Salvar exercício";
    }

    private async void OnOpenIllustrationLicense(object? sender, TappedEventArgs args)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync("THIRD-PARTY-NOTICES.md");
        using var reader = new StreamReader(stream);
        var credits = await reader.ReadToEndAsync();
        await Navigation.PushAsync(new ContentPage
        {
            Title = "Créditos e licenças",
            Content = new ScrollView { Content = new Label { Text = credits, Padding = 20, FontSize = 13 } }
        });
    }
}
