using EnvDTE;
using EnvDTE80;
using GoToFileOnLine.Models;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GoToFileOnLine
{
    public partial class GoToFileOnLineSearchWindow : DialogWindow
    {
        private ObservableCollection<ComboBoxItemModel> ProjectItems = new ObservableCollection<ComboBoxItemModel>();
        private ComboBoxItemModel _selectedItem;

        public GoToFileOnLineSearchWindow()
        {
            InitializeComponent();

        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        protected override async void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            var dte2 = (DTE2)Package.GetGlobalService(typeof(DTE));
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            foreach (EnvDTE.Project proj in dte2.Solution.Projects)
            {
                foreach (ProjectItem item in proj.ProjectItems)
                {
                    if (item.Properties?.Item("FullPath")?.Value != null)
                    {
                        ProjectItems.Add(new ComboBoxItemModel
                        {
                            FullPath = item.Properties?.Item("FullPath")?.Value.ToString(),
                            Name = item.Name
                        });
                    }
                }
            }
            SearchBox.Focus();
        }


        //From https://www.c-sharpcorner.com/UploadFile/201fc1/autocomplete-textbox-in-wpf-using-only-net-and-wpf-librari/
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void TextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_selectedItem != null)
                {
                    await GoToFileAsync(_selectedItem);
                }
            }
            if (e.Key == Key.Down)
            {
                if (_selectedItem != null)
                {
                    ((TextBlock)ResultStack.Children[_selectedItem.Index]).Background = Brushes.Transparent;
                    if (_selectedItem.Index >= ResultStack.Children.Count - 1)
                    {
                        _selectedItem = ((TextBlock)ResultStack.Children[0]).Tag as ComboBoxItemModel;
                        ((TextBlock)ResultStack.Children[0]).Background = Brushes.DarkGray;
                    }
                    else
                    {
                        _selectedItem = ((TextBlock)ResultStack.Children[_selectedItem.Index + 1]).Tag as ComboBoxItemModel;
                        ((TextBlock)ResultStack.Children[_selectedItem.Index]).Background = Brushes.DarkGray;
                    }
                }
                else
                {
                    _selectedItem = ((TextBlock)ResultStack.Children[0]).Tag as ComboBoxItemModel;
                    ((TextBlock)ResultStack.Children[0]).Background = Brushes.DarkGray;
                }
                return;
            }
            if (e.Key == Key.Up)
            {
                if (_selectedItem != null)
                {
                    ((TextBlock)ResultStack.Children[_selectedItem.Index]).Background = Brushes.Transparent;
                    if (_selectedItem.Index < 1)
                    {
                        _selectedItem = ((TextBlock)ResultStack.Children[ResultStack.Children.Count - 1]).Tag as ComboBoxItemModel;
                        ((TextBlock)ResultStack.Children[_selectedItem.Index]).Background = Brushes.DarkGray;
                    }
                    else
                    {
                        _selectedItem = ((TextBlock)ResultStack.Children[_selectedItem.Index - 1]).Tag as ComboBoxItemModel;
                        ((TextBlock)ResultStack.Children[_selectedItem.Index]).Background = Brushes.DarkGray;
                    }
                }
                else
                {
                    _selectedItem = ((TextBlock)ResultStack.Children[ResultStack.Children.Count - 1]).Tag as ComboBoxItemModel;
                    ((TextBlock)ResultStack.Children[_selectedItem.Index]).Background = Brushes.DarkGray;
                }
                return;
            }
            var border = (ResultStack.Parent as ScrollViewer).Parent as Border;
            var query = (sender as TextBox).Text;
            query = query.Contains(":") ? query.Split(':').First() : query;
            if (query.Length == 0)
            {
                // Clear
                ResultStack.Children.Clear();
                border.Visibility = Visibility.Collapsed;
            }
            else
            {
                border.Visibility = Visibility.Visible;
            }

            // Clear the list
            ResultStack.Children.Clear();
            var results = ProjectItems.Where(_ => _.Name?.ToLower().Contains(query?.Trim().ToLower()) ?? false).ToList();
            var counter = 0;
            foreach (var item in results)
            {
                item.Index = counter;
                AddItem(item);
                counter++;
            }

            if (results.Count == 0)
            {
                ResultStack.Children.Add(new TextBlock() { Text = "No results found." });
            }
            else if (results.Count == 1)
            {
                _selectedItem = ((TextBlock)ResultStack.Children[0]).Tag as ComboBoxItemModel;
                ((TextBlock)ResultStack.Children[0]).Background = Brushes.DarkGray;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD101:Avoid unsupported async delegates", Justification = "<Pending>")]
        private async void AddItem(ComboBoxItemModel p)
        {
            var block = new TextBlock();

            // Add the text
            block.Text = p.Name ?? string.Empty;
            block.Tag = p;

            // A little style...
            block.Margin = new Thickness(2, 3, 2, 3);
            block.Cursor = Cursors.Hand;

            // Mouse events
            block.MouseLeftButtonUp += async (sender, e) =>
            {
                await GoToFileAsync((sender as TextBlock).Tag as ComboBoxItemModel);
            };

            block.MouseEnter += (sender, e) =>
            {
                var b = sender as TextBlock;
                b.Background = Brushes.DarkGray;
                _selectedItem = b.Tag as ComboBoxItemModel;
            };

            block.MouseLeave += (sender, e) =>
            {
                var b = sender as TextBlock;
                b.Background = Brushes.Transparent;
                _selectedItem = null;
            };

            ResultStack.Children.Add(block);
        }

        private async Task GoToFileAsync(ComboBoxItemModel cb)
        {
            var lineText = SearchBox.Text.Contains(":") ? SearchBox.Text.Split(':').Last() : "1";
            int.TryParse(lineText, out var lineNumber);
            var path = cb.FullPath;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var provider = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)Package.GetGlobalService(typeof(DTE));
            VsShellUtilities.OpenDocument(new ServiceProvider(provider),
                path, Guid.Empty,
                out _,
                out _,
                out _,
                out IVsTextView viewAdapter);
            viewAdapter.CenterLines(lineNumber > 10 ? lineNumber - 10 : lineNumber, 20);
            viewAdapter.SetCaretPos(lineNumber - 1, 0);
            var lines = File.ReadAllLines(path);
            if (lines.Count() > lineNumber)
            {
                viewAdapter.SetSelection(lineNumber - 1, 0, lineNumber - 1, lines[lineNumber - 1].Length);
            }
            Close();
        }
    }
}
