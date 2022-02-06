using System.Windows;
using System.Windows.Controls;

namespace GoToFileOnLine
{
    public partial class GoToFileOnLineControl : UserControl
    {
        public GoToFileOnLineControl()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            VS.MessageBox.Show("ToolWindow1Control", "Button clicked");
        }
    }
}
