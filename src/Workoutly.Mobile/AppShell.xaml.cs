namespace Workoutly.Mobile;

public partial class AppShell : Shell
{
	private bool _confirmingLeave;
	public AppShell()
	{
		InitializeComponent();
	}

	protected override async void OnNavigating(ShellNavigatingEventArgs args)
	{
		base.OnNavigating(args);
		if (args.Cancelled || !args.CanCancel || args.Source is not (ShellNavigationSource.Pop or ShellNavigationSource.PopToRoot) || CurrentPage is not IConfirmLeave form) return;
		if (_confirmingLeave) { args.Cancel(); return; }
		var deferral = args.GetDeferral();
		if (deferral is null) return;
		_confirmingLeave = true;
		try { if (!await form.CanLeaveAsync()) args.Cancel(); }
		finally { _confirmingLeave = false; deferral.Complete(); }
	}
}

// Confirmação única para os retornos canceláveis do Shell.
internal interface IConfirmLeave
{
	Task<bool> CanLeaveAsync();
}
