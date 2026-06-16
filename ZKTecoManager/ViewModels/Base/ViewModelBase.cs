using CommunityToolkit.Mvvm.ComponentModel;

namespace ZKTecoManager.ViewModels.Base;

public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    protected async Task RunBusyAsync(Func<Task> action, string? busyMessage = null)
    {
        IsBusy = true;
        StatusMessage = busyMessage ?? "Procesando...";
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
            StatusMessage = string.Empty;
        }
    }
}
