using StellaSoraCommissionAssistant.Models;
using StellaSoraCommissionAssistant.ViewModels;

namespace StellaSoraCommissionAssistant.Views;

public partial class UpdateWindow
{
    private readonly UpdateViewModel _viewModel;

    public UpdateWindow()
    {
        InitializeComponent();
        _viewModel = new UpdateViewModel();
        DataContext = _viewModel;

        if (ProgramDataModel.Instance.SettingsData.DownloadSource == EDownloadSource.Gitee)
        {
            ApiOption0.IsChecked = true;
            ApiOption1.IsChecked = false;
        }
        else
        {
            ApiOption0.IsChecked = false;
            ApiOption1.IsChecked = true;
        }
        if (!ProgramDataModel.Instance.IsCheckingNewVersion & !ProgramDataModel.Instance.IsDownloadingFiles & !ProgramDataModel.Instance.IsReadyForApplyUpdate)
        {
            ProgramDataModel.Instance.UpdateInfoTitle = string.Empty;
            ProgramDataModel.Instance.UpdateInfoContent = string.Empty;
        }
    }

    private void ApiButtonChecked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ApiOption0.IsChecked == true)
        {
            ProgramDataModel.Instance.SettingsData.DownloadSource = EDownloadSource.Gitee;
        }
        else if (ApiOption1.IsChecked == true)
        {
            ProgramDataModel.Instance.SettingsData.DownloadSource = EDownloadSource.GitHub;
        }
    }
}
