using Microsoft.Extensions.Logging;

namespace Workoutly.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Inter-Regular.otf", "InterRegular");
				fonts.AddFont("Inter-Medium.otf", "InterMedium");
				fonts.AddFont("Inter-SemiBold.otf", "InterSemiBold");
				fonts.AddFont("Inter-Bold.otf", "InterBold");
			});

        // Os contornos ficam no Border XAML; os campos continuam sendo controles nativos.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("WorkoutlyField", (handler, view) =>
        {
#if IOS
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#elif ANDROID
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
            handler.PlatformView.SetPadding(0, 0, 0, 0);
#endif
        });
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("WorkoutlyField", (handler, view) =>
        {
#if IOS
            handler.PlatformView.TextContainerInset = UIKit.UIEdgeInsets.Zero;
            handler.PlatformView.TextContainer.LineFragmentPadding = 0;
#elif ANDROID
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
            handler.PlatformView.SetPadding(0, 0, 0, 0);
#endif
        });

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
