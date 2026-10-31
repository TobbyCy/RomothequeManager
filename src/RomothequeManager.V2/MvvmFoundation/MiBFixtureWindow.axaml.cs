using Avalonia.Controls;
using Avalonia.Threading;
using RomothequeManager.V2.Inventory.Media.MiB;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Only CI-created fixtures may construct this view. No native folder picker.</summary>
internal partial class MiBFixtureWindow : Window
{
    private readonly MiBUiViewModel _model;
    // Avalonia XAML compiler requires a public default constructor; it is always fail-closed.
    public MiBFixtureWindow() => throw new InvalidOperationException("MI_B_FIXTURE_SERVICE_REQUIRED");
    internal MiBFixtureWindow(IMiBFixtureMediaApplicationService service, string fixtureRoot)
    {
        InitializeComponent();
        _model = new MiBUiViewModel(service, fixtureRoot, action => {
            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        });
        DataContext = _model;
        Closed += (_, _) => _ = _model.DisposeAsync();
    }
    internal MiBUiViewModel ModelForCi => _model;
}
