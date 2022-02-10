using EnvDTE;
using EnvDTE80;
using GoToFileOnLine.Models;
using GoToFileOnLine.Utilities;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace GoToFileOnLine
{
    public partial class GoToFileOnLineSearchWindow : DialogWindow
    {
        private List<ComboBoxItemModel> ProjectItems = new List<ComboBoxItemModel>();
        private ComboBoxItemModel _selectedItem;
        private IVsOutputWindow _outputWindow;
        private Guid _paneGuid = Guid.NewGuid();
        public Brush _defaultTextColor;

        public GoToFileOnLineSearchWindow()
        {
            InitializeComponent();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        protected override async void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            await GetAllSolutionFilesAsync();
            SearchBox.Focus();
            _defaultTextColor = SearchBox.Foreground;
        }

        private async Task GetAllSolutionFilesAsync()
        {
            try
            {
                ProjectItems.Clear();
                var dte2 = (DTE2)Package.GetGlobalService(typeof(DTE));
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                foreach (EnvDTE.Project project in dte2.Solution.Projects)
                {
                    ProjectItems.AddRange(await GetProjectFilesAsync(project.ProjectItems));
                }
                ProjectItems = ProjectItems.OrderBy(x => x.Name).ToList();
            }
            catch (Exception e)
            {
                await PrintToOutputAsync(e.ToString());
            }
        }

        public async Task<IEnumerable<ComboBoxItemModel>> GetProjectFilesAsync(ProjectItems items)
        {
            var result = new List<ComboBoxItemModel>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            foreach (ProjectItem item in items)
            {
                if (item.Name?.Contains(".") ?? false && item.ProjectItems != null && item.Properties?.Item("FullPath")?.Value != null)
                {
                    var cb = new ComboBoxItemModel
                    {
                        Name = item.Name!,
                        FullPath = item.Properties?.Item("FullPath")?.Value.ToString(),
                    };

                    if (!string.IsNullOrWhiteSpace(cb.FullPath))
                    {
                        result.Add(cb);
                    }
                }
                if (item.ProjectItems != null)
                {
                    result.AddRange(await GetProjectFilesAsync(item.ProjectItems));
                }
                if (item.SubProject != null && item.SubProject.ProjectItems != null)
                {
                    result.AddRange(await GetProjectFilesAsync(item.SubProject.ProjectItems));
                }
            }
            return result;
        }

        //From https://www.c-sharpcorner.com/UploadFile/201fc1/autocomplete-textbox-in-wpf-using-only-net-and-wpf-librari/
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void TextBox_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    if (_selectedItem != null)
                    {
                        await GoToFileAsync(_selectedItem);
                    }
                }
                if (e.Key == Key.Escape)
                {
                    Close();
                    return;
                }
                if (e.Key == Key.Down)
                {
                    if (_selectedItem != null)
                    {
                        foreach (var item in ResultStack.Children)
                        {
                            ((TextBlock)item).Background = Brushes.Transparent;
                            ((TextBlock)item).Foreground = _defaultTextColor;
                        }
                        if (_selectedItem.Index >= ResultStack.Children.Count - 1)
                        {
                            _selectedItem = ((TextBlock)ResultStack.Children[0]).Tag as ComboBoxItemModel;
                        }
                        else
                        {
                            _selectedItem = ((TextBlock)ResultStack.Children[_selectedItem.Index + 1]).Tag as ComboBoxItemModel;
                        }
                        SetColors((TextBlock)ResultStack.Children[_selectedItem.Index]);
                    }
                    else if (ResultStack.Children.Count > 0)
                    {
                        _selectedItem = ((TextBlock)ResultStack.Children[0]).Tag as ComboBoxItemModel;
                        SetColors((TextBlock)ResultStack.Children[_selectedItem.Index]);
                    }
                    return;
                }
                if (e.Key == Key.Up)
                {
                    if (_selectedItem != null)
                    {
                        ((TextBlock)ResultStack.Children[_selectedItem.Index]).Background = Brushes.Transparent;
                        ((TextBlock)ResultStack.Children[_selectedItem.Index]).Foreground = _defaultTextColor;

                        if (_selectedItem.Index < 1)
                        {
                            _selectedItem = ((TextBlock)ResultStack.Children[ResultStack.Children.Count - 1]).Tag as ComboBoxItemModel;
                        }
                        else
                        {
                            _selectedItem = ((TextBlock)ResultStack.Children[_selectedItem.Index - 1]).Tag as ComboBoxItemModel;
                        }
                        SetColors((TextBlock)ResultStack.Children[_selectedItem.Index]);
                    }
                    else if (ResultStack.Children.Count > 0)
                    {
                        _selectedItem = ((TextBlock)ResultStack.Children[ResultStack.Children.Count - 1]).Tag as ComboBoxItemModel;
                        SetColors((TextBlock)ResultStack.Children[_selectedItem.Index]);
                    }
                    return;
                }
                PerformQuery(sender);
            }
            catch (Exception ex)
            {
                await PrintToOutputAsync(ex.ToString());
            }
        }

        private void PerformQuery(object sender)
        {
            var border = (ResultStack.Parent as ScrollViewer).Parent as Border;
            var query = (sender as TextBox).Text;
            query = query.Contains(":") ? query.Split(':').First().Trim() : query;
            if (query.Length == 0)
            {
                // Clear
                ResultStack.Children.Clear();
                border.Visibility = Visibility.Collapsed;
                return;
            }
            else
            {
                border.Visibility = Visibility.Visible;
            }

            // Clear the list
            ResultStack.Children.Clear();
            var results = ProjectItems.Select(_ =>
            {
                var (score, positions) = FuzzySearch.ScoreFuzzy(_?.Name ?? "", query, query.ToLower());
                return new ComboBoxItemModel
                {
                    FullPath = _.FullPath,
                    Name = _.Name,
                    Score = score,
                    Positions = positions
                };
            });
            var counter = 0;
            var fullMatch = results.FirstOrDefault(_ => _.Positions.Count() == query.Length && _.Positions.Count() == _.Name.Length);
            if (fullMatch != null)
            {
                fullMatch.Index = counter;
                AddItem(fullMatch, true);
                return;

            }
            foreach (var item in results.Where(_ => _.Score > 0).OrderByDescending(_ => _.Score))
            {
                item.Index = counter;
                AddItem(item);
                counter++;
                if (counter > 10)
                {
                    break;
                }
            }
        }

        private void SetColors(TextBlock tb)
        {
            tb.Background = Brushes.DarkGray;
            tb.Foreground = Brushes.Black;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD101:Avoid unsupported async delegates", Justification = "<Pending>")]
        private void AddItem(ComboBoxItemModel p, bool select = false)
        {
            var block = new TextBlock();
            block.Foreground = _defaultTextColor;
            // Add the text
            if (p.Positions.Any())
            {
                for (int i = 0; i < p.Name.Length; i++)
                {
                    if (p.Positions.Contains(i))
                    {
                        block.Inlines.Add(new Run(p.Name[i].ToString()) { FontWeight = FontWeights.Bold });
                    }
                    else
                    {
                        block.Inlines.Add(p.Name[i].ToString());
                    }
                }
            }
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
            if (select)
            {
                _selectedItem = p;
                block.Background = Brushes.DarkGray;
                block.Foreground = Brushes.Black;
            }
            ResultStack.Children.Add(block);
        }

        private async Task GoToFileAsync(ComboBoxItemModel cb)
        {
            try
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
                if (viewAdapter != null)
                {
                    viewAdapter.CenterLines(lineNumber > 10 ? lineNumber - 10 : lineNumber, 20);
                    viewAdapter.SetCaretPos(lineNumber - 1, 0);
                    var lines = File.ReadAllLines(path);
                    if (lineText?.ToLower() == "e")
                    {
                        viewAdapter.SetSelection(lines.Count() -1, 0, lines.Count()-1, lines[lines.Count()-1].Length);
                    }
                    else if (lineText?.ToLower() == "s")
                    {
                        viewAdapter.SetSelection(0, 0, 0, lines[0].Length);
                    }
                    else if (lines.Count() > lineNumber)
                    {
                        viewAdapter.SetSelection(lineNumber - 1, 0, lineNumber - 1, lines[lineNumber - 1].Length);
                    }
                }
            }
            catch (Exception e)
            {
                await PrintToOutputAsync(e.ToString());
            }
            SearchBox.Text = string.Empty;
            Close();
        }

        private async Task PrintToOutputAsync(string text)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_outputWindow == null)
            {
                _outputWindow = (IVsOutputWindow)Package.GetGlobalService(typeof(SVsOutputWindow));
                _outputWindow.CreatePane(ref _paneGuid, "Go to file on line", 1, 1);
            }
            _outputWindow.GetPane(ref _paneGuid, out var outputPane);
            outputPane.Activate();
            outputPane.OutputString($"{text}{Environment.NewLine}");
        }

    }
}
