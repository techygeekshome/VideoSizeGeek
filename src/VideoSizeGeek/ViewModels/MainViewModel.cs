using VideoSizeGeek.Core.Models;
using VideoSizeGeek.Core.Services;

namespace VideoSizeGeek.ViewModels;

/// <summary>One entry in the preset dropdown. Wraps a real SizePreset, or null for "Custom".</summary>
public sealed record PresetOption(string Name, SizePreset? Preset)
{
    public override string ToString() => Name;
    public static readonly PresetOption Custom = new("Custom size...", null);

    public static IReadOnlyList<PresetOption> All { get; } =
        SizePreset.All.Select(p => new PresetOption(p.Name, p))
            .Append(Custom)
            .ToList();
}

/// <summary>
/// One screen, one job: this app does not need a shell with pages. State for the file that is
/// loaded, the target chosen, the plan that comes out of that, and the encode itself.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly string? _ffmpegPath = FfmpegTools.FindFfmpeg();
    private readonly string? _ffprobePath = FfmpegTools.FindFfprobe();

    public MainViewModel()
    {
        PickFileCommand = new AsyncRelayCommand(PickFileAsync);
        StartCommand = new AsyncRelayCommand(StartAsync, () => CanStart);
        ShowAboutCommand = new RelayCommand(() => AboutRequested?.Invoke());
        SelectedPreset = PresetOption.All[0];
    }

    public event Action? AboutRequested;
    public Func<Task<string?>>? RequestOpenFileDialog;
    public Action<string>? RequestRevealInExplorer;

    public bool FfmpegAvailable => _ffmpegPath is not null && _ffprobePath is not null;
    public string FfmpegMissingMessage =>
        "VideoSizeGeek needs ffmpeg to encode video and does not bundle a copy. Install it from " +
        FfmpegTools.OfficialBuildsUrl + " and make sure ffmpeg.exe and ffprobe.exe are on your PATH, then restart VideoSizeGeek.";

    public IReadOnlyList<PresetOption> Presets => PresetOption.All;

    private string? _sourcePath;
    public string? SourcePath
    {
        get => _sourcePath;
        private set
        {
            if (!SetField(ref _sourcePath, value)) return;
            OnPropertyChanged(nameof(SourceFileName));
            OnPropertyChanged(nameof(HasSource));
        }
    }

    public string SourceFileName => SourcePath is null ? "" : Path.GetFileName(SourcePath);
    public bool HasSource => SourcePath is not null;

    private ProbeResult? _probe;
    public ProbeResult? Probe
    {
        get => _probe;
        private set { SetField(ref _probe, value); OnPropertyChanged(nameof(DurationText)); RecomputePlan(); }
    }

    public string DurationText => Probe is null ? "" : $"{TimeSpan.FromSeconds(Probe.DurationSeconds):mm\\:ss} long";

    private PresetOption _selectedPreset = PresetOption.Custom;
    public PresetOption SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (!SetField(ref _selectedPreset, value)) return;
            OnPropertyChanged(nameof(IsCustomSize));
            RecomputePlan();
        }
    }

    public bool IsCustomSize => SelectedPreset.Preset is null;

    private double _customMegabytes = 10;
    public double CustomMegabytes
    {
        get => _customMegabytes;
        set { if (SetField(ref _customMegabytes, value)) RecomputePlan(); }
    }

    private bool _includeAudio = true;
    public bool IncludeAudio
    {
        get => _includeAudio;
        set { if (SetField(ref _includeAudio, value)) RecomputePlan(); }
    }

    private EncodePlan? _plan;
    public EncodePlan? Plan
    {
        get => _plan;
        private set
        {
            if (!SetField(ref _plan, value)) return;
            OnPropertyChanged(nameof(PlanSummary));
            OnPropertyChanged(nameof(ShowFloorWarning));
            ((AsyncRelayCommand)StartCommand).RaiseCanExecuteChanged();
        }
    }

    public string PlanSummary => Plan is null ? "" :
        $"Target {FormatBytes(Plan.TargetBytes)}  |  video {Plan.VideoKbps} kbps" +
        (Plan.AudioIncluded ? $", audio {Plan.AudioKbps} kbps" : ", no audio");

    public bool ShowFloorWarning => Plan?.BelowQualityFloor == true;
    public string? FloorWarning => Plan?.FloorWarning;

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

    private double _progressFraction;
    public double ProgressFraction { get => _progressFraction; private set => SetField(ref _progressFraction, value); }

    private string _statusText = "";
    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }

    private string? _resultPath;
    public string? ResultPath
    {
        get => _resultPath;
        private set { SetField(ref _resultPath, value); OnPropertyChanged(nameof(HasResult)); }
    }
    public bool HasResult => ResultPath is not null;

    private bool CanStart => HasSource && Plan is not null && !IsBusy && FfmpegAvailable;

    public System.Windows.Input.ICommand PickFileCommand { get; }
    public System.Windows.Input.ICommand StartCommand { get; }
    public System.Windows.Input.ICommand ShowAboutCommand { get; }

    public async Task LoadFileAsync(string path)
    {
        if (_ffprobePath is null) return;

        StatusText = "Reading the file...";
        SourcePath = path;
        ResultPath = null;
        try
        {
            Probe = await MediaProbe.ProbeAsync(_ffprobePath, path);
            StatusText = "";
        }
        catch (MediaProbeException ex)
        {
            StatusText = $"Could not read that file: {ex.Message}";
            SourcePath = null;
            Probe = null;
        }
    }

    private async Task PickFileAsync()
    {
        if (RequestOpenFileDialog is null) return;
        var path = await RequestOpenFileDialog();
        if (path is not null)
            await LoadFileAsync(path);
    }

    private void RecomputePlan()
    {
        if (Probe is null) { Plan = null; return; }

        var targetBytes = SelectedPreset.Preset?.TargetBytes
            ?? (long)(CustomMegabytes * 1024 * 1024);

        if (targetBytes <= 0) { Plan = null; return; }

        try
        {
            Plan = BitrateBudget.CreatePlan(targetBytes, Probe.DurationSeconds, IncludeAudio && Probe.HasAudioStream);
        }
        catch (ArgumentOutOfRangeException)
        {
            Plan = null;
        }
    }

    private async Task StartAsync()
    {
        if (SourcePath is null || Plan is null || _ffmpegPath is null) return;

        IsBusy = true;
        ResultPath = null;
        ProgressFraction = 0;
        StatusText = "Starting...";

        try
        {
            var outputPath = NextFreeOutputPath(SourcePath, Plan.TargetBytes);
            var encoder = new TwoPassEncoder(_ffmpegPath);
            var progress = new Progress<EncodeProgress>(p =>
            {
                ProgressFraction = p.FractionComplete;
                StatusText = p.Message;
            });

            var outcome = await encoder.EncodeAsync(SourcePath, outputPath, Plan, progress);

            if (outcome.Success)
            {
                ResultPath = outcome.OutputPath;
                StatusText = outcome.Message;
            }
            else
            {
                StatusText = $"That did not work: {outcome.Message}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RevealResult()
    {
        if (ResultPath is not null)
            RequestRevealInExplorer?.Invoke(ResultPath);
    }

    private static string NextFreeOutputPath(string sourcePath, long targetBytes)
    {
        var dir = Path.GetDirectoryName(sourcePath) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        var label = FormatBytes(targetBytes).Replace(" ", "");
        var candidate = Path.Combine(dir, $"{stem}-{label}.mp4");
        var n = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(dir, $"{stem}-{label}-{n}.mp4");
            n++;
        }
        return candidate;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B"
    };
}
