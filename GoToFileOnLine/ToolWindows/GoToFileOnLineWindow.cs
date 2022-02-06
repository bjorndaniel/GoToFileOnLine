using Microsoft.VisualStudio.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GoToFileOnLine
{
    public class GoToFileOnLineWindow : BaseToolWindow<GoToFileOnLineWindow>
    {
        public override string GetTitle(int toolWindowId) => "Go to file on line";

        public override Type PaneType => typeof(Pane);

        public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            return Task.FromResult<FrameworkElement>(new GoToFileOnLineControl());
        }

        [Guid("58cc07f4-43ff-4741-bea0-43b03decd3e6")]
        internal class Pane : ToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.ToolWindow;
            }
        }
    }
}
