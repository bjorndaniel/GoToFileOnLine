using EnvDTE;
using System.Windows;

namespace GoToFileOnLine
{
    [Command(PackageIds.GoToFileOnLineCommand)]
    internal sealed class GoToFileOnLineCommand : BaseCommand<GoToFileOnLineCommand>
    {

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var searchWindow = new GoToFileOnLineSearchWindow { Owner = Application.Current.MainWindow };
            var done = searchWindow.ShowDialog();
        }
    }
}
