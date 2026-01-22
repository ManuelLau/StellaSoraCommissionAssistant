using StellaSoraCommissionAssistant.Models;
using StellaSoraCommissionAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StellaSoraCommissionAssistant.Views;

public partial class TaskSettingsView
{
    private readonly SettingsViewModel _viewModel;

    public TaskSettingsView()
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel();
        DataContext = _viewModel;
    }

    private void SettingsComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SettingsViewModel.UpdateConfigJsonFile();
    }

    private void SettingsCheckBoxClick(object sender, RoutedEventArgs e)
    {
        SettingsViewModel.UpdateConfigJsonFile();
    }

    private void ComboBoxPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 未展开时禁用鼠标滚动，以免误操作
        if (sender is ComboBox comboBox && !comboBox.IsDropDownOpen)
        {
            e.Handled = true;
        }
    }

    private void SelectedTimeChanged(object sender, HandyControl.Data.FunctionEventArgs<DateTime?> e)
    {
        SettingsViewModel.UpdateConfigJsonFile();
    }

    private void TextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        TextBox tb = (TextBox)sender;
        if (TimeOnly.TryParse(tb.Text, out TimeOnly result))
        {
            ProgramDataModel.Instance.SettingsData.AcquireFriendEnergyTime = result;
        }
        SettingsViewModel.UpdateConfigJsonFile();
    }

    private void TextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }
}
