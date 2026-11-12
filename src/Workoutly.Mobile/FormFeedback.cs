namespace Workoutly.Mobile;

internal static class FormFeedback
{
    public static void FollowError(ScrollView scroll, Label error)
    {
        // A altura da mensagem só fica correta após o layout; rolar antes pode cortar linhas.
        error.SizeChanged += (_, _) =>
        {
            if (!error.IsVisible || error.Height <= 0) return;
            scroll.Dispatcher.Dispatch(async () =>
            {
                if (error.IsVisible && scroll.Handler is not null)
                    await scroll.ScrollToAsync(error, ScrollToPosition.End, true);
            });
        };
    }
}
