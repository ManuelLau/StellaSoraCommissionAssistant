using StellaSoraCommissionAssistant.Models;
using StellaSoraCommissionAssistant.Utilities;
using StellaSoraCommissionAssistant.ViewModels;
using System.Windows;

namespace StellaSoraCommissionAssistant.Views;

public partial class AnnouncementWindow : Window
{
    public AnnouncementWindow()
    {
        InitializeComponent();
        DataContext = ProgramDataModel.Instance;

        MainTitle.Text = AnnouncementContent.MainTitle;
        LatestUpdateTitle.Text = AnnouncementContent.LatestUpdateTitle;
        LatestUpdateContent.Text = AnnouncementContent.LatestUpdateContent;
        NotesTitle.Text = AnnouncementContent.NotesTitle;
        NotesContent.Text = AnnouncementContent.NotesContent;
        UpdateHistoryMainTitle.Text = AnnouncementContent.UpdateHistoryMainTitle;
        UpdateHistoryTitle0.Text = AnnouncementContent.UpdateHistoryTitle0;
        UpdateHistoryContent0.Text = AnnouncementContent.UpdateHistoryContent0;
    }

    private void CheckBoxOnClick(object sender, RoutedEventArgs e)
    {
        SettingsViewModel.UpdateConfigJsonFile();
    }
}