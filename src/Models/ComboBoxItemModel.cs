using System.Collections.Generic;

namespace GoToFileOnLine.Models
{
    public class ComboBoxItemModel
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public int Index { get; set; }
        public int Score { get; set; }
        public List<int> Positions { get; set; } = new List<int>();
    }
}
