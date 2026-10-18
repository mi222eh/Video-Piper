using System;
using Microsoft.Extensions.Logging;
using Uno.Resizetizer;

namespace VideoPiper;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    public static Window? MainWindowInstance { get; private set; }

    /// <summary>
    /// The application's service host (built from Uno's Hosting feature). View models and
    /// services are registered as singletons in <see cref="OnLaunched"/> and resolved from
    /// <c>Host.Services</>. Set once before navigation to <c>MainPage</c>.
    /// </summary>
    public static IHost Host { get; private set; } = null!;

    /// <summary>
    /// Posts an action to the UI thread. Falls back to executing inline when there is
    /// no live window (e.g. during startup before OnLaunched, or in unit tests).
    /// Wired to <c>LibraryItem.PostToUi</c> so model change notifications always fire on the UI thread.
    /// </summary>
    public static void PostToUiThread(Action action)
    {
        if (MainWindowInstance?.DispatcherQueue is { } queue)
        {
            if (queue.HasThreadAccess)
            {
                action();
            }
            else
            {
                queue.TryEnqueue(() => action());
            }
        }
        else
        {
            action();
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialize the ambient logging factory (no-op in release builds).
        InitializeLogging();
        // Load WinUI Resources (kept in code because App.xaml must stay empty for the XamlCompiler)
        Resources.Build(r => r.Merged(
            new Microsoft.UI.Xaml.Controls.XamlControlsResources()));

        // In this Uno/WinAppSDK theme build, the Light entry of XamlControlsResources for
        // "ApplicationPageBackgroundThemeBrush" resolves to the dark value, so the page
        // background stays dark in light mode. The app's own ThemeDictionaries are consulted
        // first by the ThemeResource resolver, so an app-owned token always resolves to the
        // intended color for the current theme.
        var pageBg = Application.Current.Resources;
        pageBg.ThemeDictionaries["Light"] = new ResourceDictionary
        {
            ["VpPageBackgroundBrush"] = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xF3, 0xF3, 0xF3))
        };
        pageBg.ThemeDictionaries["Dark"] = new ResourceDictionary
        {
            ["VpPageBackgroundBrush"] = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x20, 0x20, 0x20))
        };

        var builder = this.CreateBuilder(args)
            .Configure(host => host
                .ConfigureServices((context, services) =>
                {
                    // Services (interface -> implementation, singleton).
                    services.AddSingleton<Services.IToolUpdateService, Services.ToolUpdateService>();
                    services.AddSingleton<Services.ISystemService, Services.SystemService>();
                    services.AddSingleton<Services.IDownloadService, Services.DownloadService>();
                    services.AddSingleton<Services.ILibraryDownloadService, Services.LibraryDownloadService>();
                    services.AddSingleton<Services.ISearchService, Services.SearchService>();

                    // View models (resolved from App.Host.Services by MainPage).
                    services.AddSingleton<ViewModels.MainViewModel>();
                    services.AddSingleton<ViewModels.LibraryViewModel>();
                    services.AddSingleton<ViewModels.SettingsViewModel>();
                }));
        MainWindow = builder.Window;
        MainWindow.Title = "Video Piper";
        MainWindowInstance = MainWindow;
        Host = builder.Build();

        // The LibraryItem model is compiled standalone by the test project (no App
        // reference), so it cannot reach the dispatcher directly. Wire its
        // change-notification marshal to the UI thread here: worker threads mutate
        // bound models, and a PropertyChanged raised off the UI thread throws
        // COMException in the WinRT binding layer.
        Models.LibraryItem.PostToUi = PostToUiThread;
#if DEBUG
        MainWindow.UseStudio();
#endif


        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (MainWindow.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();

            // Place the frame in the current Window
            MainWindow.Content = rootFrame;

            rootFrame.NavigationFailed += OnNavigationFailed;
        }

        if (rootFrame.Content == null)
        {
            // When the navigation stack isn't restored navigate to the first page,
            // configuring the new page by passing relevant information as a navigation
            // parameter
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }

        var savedTheme = Services.PreferencesService.GetTheme();
        SetTheme(savedTheme != "light");

#if WINDOWS
        // Remember + restore the window size (AppWindow is WinAppSDK-only, so this lives
        // in the Windows block alongside the original fixed 1060x740 resize).
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
        if (hwnd != IntPtr.Zero)
        {
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow is not null)
            {
                var savedSize = Services.PreferencesService.GetWindowSize();
                appWindow.Resize(savedSize is { } s
                    ? new Windows.Graphics.SizeInt32(s.Width, s.Height)
                    : new Windows.Graphics.SizeInt32(1060, 740));

                // Persist the size the moment the window is about to close.
                appWindow.Closing += (_, _) =>
                {
                    var size = appWindow.Size;
                    if (size.Width > 0 && size.Height > 0)
                    {
                        Services.PreferencesService.SetWindowSize(size.Width, size.Height);
                    }
                };
            }
        }
#endif

        MainWindow.SetWindowIcon();
        // Ensure the current window is active
        MainWindow.Activate();
    }


    public static void SetTheme(bool isDark)
    {
        var theme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        if (MainWindowInstance?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
            if (root is Frame { Content: FrameworkElement page })
            {
                page.RequestedTheme = theme;
            }
        }
    }


    /// <summary>
    /// Invoked when Navigation to a certain page fails
    /// </summary>
    /// <param name="sender">The Frame which failed navigation</param>
    /// <param name="e">Details about the navigation failure</param>
    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }

    /// <summary>
    /// Configures global Uno Platform logging
    /// </summary>
    public static void InitializeLogging()
    {
#if DEBUG
        // Logging is disabled by default for release builds, as it incurs a significant
        // initialization cost from Microsoft.Extensions.Logging setup. If startup performance
        // is a concern for your application, keep this disabled. If you're running on the web or
        // desktop targets, you can use URL or command line parameters to enable it.
        //
        // For more performance documentation: https://platform.uno/docs/articles/Uno-UI-Performance.html

        var factory = LoggerFactory.Create(builder =>
        {
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__
            builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());

            // Log to the Visual Studio Debug console
            builder.AddConsole();
#else
            builder.AddConsole();
#endif

            // Exclude logs below this level
            builder.SetMinimumLevel(LogLevel.Information);

            // Default filters for Uno Platform namespaces
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);

            // Generic Xaml events
            // builder.AddFilter("Microsoft.UI.Xaml", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.VisualStateGroup", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.StateTriggerBase", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.UIElement", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.FrameworkElement", LogLevel.Trace );

            // Layouter specific messages
            // builder.AddFilter("Microsoft.UI.Xaml.Controls", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Layouter", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Panel", LogLevel.Debug );

            // builder.AddFilter("Windows.Storage", LogLevel.Debug );

            // Binding related messages
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );

            // Binder memory references tracking
            // builder.AddFilter("Uno.UI.DataBinding.BinderReferenceHolder", LogLevel.Debug );

            // DevServer and HotReload related
            // builder.AddFilter("Uno.UI.RemoteControl", LogLevel.Information);

            // Debug JS interop
            // builder.AddFilter("Uno.Foundation.WebAssemblyRuntime", LogLevel.Debug );
        });

        global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;
        Log.Initialize(factory);

#if HAS_UNO
        global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
    }
}
