using System.Windows.Data;

namespace ACT.SpecialSpellTimer.Models
{
    public enum ItemTypes
    {
        Root,
        Tag,
        SpellPanel,
        Spell,
        Ticker,
    }

    public interface ITreeItem
    {
        ItemTypes ItemType { get; }

        string DisplayText { get; }

        bool IsExpanded { get; set; }

        bool IsEnabled { get; set; }

        CollectionViewSource Children { get; }
    }
}