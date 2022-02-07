using EnvDTE;

namespace GoToFileOnLine
{
    [Command(PackageIds.GoToFileOnLineCommand)]
    internal sealed class GoToFileOnLineCommand : BaseCommand<GoToFileOnLineCommand>
    {

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var searchWindow = new GoToFileOnLineSearchWindow();
            var done = searchWindow.ShowDialog();
        }
    }
}
