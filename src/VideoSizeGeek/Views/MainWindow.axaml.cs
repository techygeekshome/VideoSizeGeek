using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using VideoSizeGeek.ViewModels;
using TechyGeeksHome.Common;

namespace VideoSizeGeek.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        DataContextChanged += (_, _) => Wire();
        Wire();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void Wire()
    {
        if (Vm is null) return;
        Vm.AboutRequested += () => new AboutWindow(AppFacts.Info).ShowDialog(this);
        Vm.RequestOpenFileDialog = PickFileAsync;
        Vm.RequestRevealInExplorer = RevealInExplorer;
    }

    // ------------------------------------------------------------------ drag and drop

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm is null) return;
        var items = e.Data.GetFiles();
        var path = items?.Select(i => i.TryGetLocalPath()).FirstOrDefault(p => p is not null);
        if (path is not null)
            await Vm.LoadFileAsync(path);
    }

    // ------------------------------------------------------------------ file picker

    private async Task<string?> PickFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a video",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Video")
                {
                    Patterns = new[] { "*.mp4", "*.mov", "*.mkv", "*.avi", "*.webm", "*.m4v", "*.wmv" }
                },
                FilePickerFileTypes.All
            }
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    // ------------------------------------------------------------------ reveal in explorer

    private static void RevealInExplorer(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", $"-R \"{path}\"");
            else
                Process.Start("xdg-open", $"\"{System.IO.Path.GetDirectoryName(path)}\"");
        }
        catch (Exception ex)
        {
            Log.Write($"Reveal in explorer failed: {ex.Message}");
        }
    }

    private void OnRevealResult(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Vm?.RevealResult();
}
