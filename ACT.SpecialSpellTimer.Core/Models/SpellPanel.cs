using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml.Serialization;
using ACT.SpecialSpellTimer.Config.Models;
using ACT.SpecialSpellTimer.Views;

namespace ACT.SpecialSpellTimer.Models
{
    public enum SpellOrders
    {
        None = 0,
        SortRecastTimeASC,
        SortRecastTimeDESC,
        SortPriority,
        SortMatchTime,
        Fixed
    }

    [Serializable]
    [XmlType(TypeName = "PanelSettings")]
    public class SpellPanel :
        TreeItemBase,
        ITrigger
    {
        #region プリセットパネル

        private static SpellPanel generalPanel = new SpellPanel()
        {
            PanelName = "+General",
            SortPriority = 100,
            SortOrder = SpellOrders.SortRecastTimeASC,
            Top = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height / 2,
            Left = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / 2,
        };

        public static SpellPanel GeneralPanel => generalPanel;

        public static void SetGeneralPanel(
            SpellPanel panel)
        {
            panel.SortPriority = generalPanel.SortPriority;
            generalPanel = panel;
        }

        #endregion プリセットパネル

        private double left = 0;
        private double top = 0;
        private string panelName = string.Empty;

        public SpellPanel()
        {
            this.SetupChildrenSource();
        }

        [XmlIgnore]
        public override ItemTypes ItemType => ItemTypes.SpellPanel;

        private Guid id = Guid.NewGuid();

        public Guid ID
        {
            get => this.id;
            set => this.SetProperty(ref this.id, value);
        }

        public double Left
        {
            get => this.left;
            set => this.SetProperty(ref this.left, Math.Round(value));
        }

        public double Top
        {
            get => this.top;
            set => this.SetProperty(ref this.top, Math.Round(value));
        }

        public string PanelName
        {
            get => this.panelName;
            set
            {
                if (this.SetProperty(ref this.panelName, value))
                {
                    this.RaisePropertyChanged(nameof(this.DisplayText));
                }
            }
        }

        private SpellOrders sortOrder = SpellOrders.None;

        public SpellOrders SortOrder
        {
            get => this.sortOrder;
            set => this.SetProperty(ref this.sortOrder, value);
        }

        [XmlIgnore]
        public bool IsDesignMode
        {
            get =>
                this.Spells.Count() < 1 ?
                false :
                this.Spells.Count() == this.Spells.Where(x => x.IsDesignMode).Count();
            set
            {
                foreach (var spell in this.Spells)
                {
                    spell.IsDesignMode = value;
                }

                this.RaisePropertyChanged();
            }
        }

        public bool FixedPositionSpell { get; set; } = false;

        public bool Horizontal { get; set; } = false;

        public double Margin { get; set; } = 0;

        [XmlIgnore]
        public bool ToClose { get; set; } = false;

        [XmlIgnore]
        public ISpellPanelWindow PanelWindow { get; set; } = null;

        [XmlIgnore]
        public IEnumerable<Spell> Spells => this.Children?.Cast<Spell>();

        #region Advanced Layout

        private bool enabledAdvancedLayout = false;

        public bool EnabledAdvancedLayout
        {
            get => this.enabledAdvancedLayout;
            set => this.SetProperty(ref this.enabledAdvancedLayout, value);
        }

        private bool isAdvancedDesignMode = false;

        [XmlIgnore]
        public bool IsAdvancedDesignMode
        {
            get => this.isAdvancedDesignMode;
            set => this.SetProperty(ref this.isAdvancedDesignMode, value);
        }

        private double width = 640;

        public double Width
        {
            get => this.width;
            set => this.SetProperty(ref this.width, Math.Round(value));
        }

        private double height = 480;

        public double Height
        {
            get => this.height;
            set => this.SetProperty(ref this.height, Math.Round(value));
        }

        private Color backgroudColor = Color.FromArgb(0x01, 0, 0, 0);

        public Color BackgroundColor
        {
            get => this.backgroudColor;
            set
            {
                if (this.SetProperty(ref this.backgroudColor, value))
                {
                    this.backgroundBrush = new SolidColorBrush(this.backgroudColor);
                    this.RaisePropertyChanged(nameof(this.BackgroundBrush));
                }
            }
        }

        private SolidColorBrush backgroundBrush;

        [XmlIgnore]
        public SolidColorBrush BackgroundBrush =>
            this.backgroundBrush ?? (this.backgroundBrush = new SolidColorBrush(this.BackgroundColor));

        #endregion Advanced Layout

        #region ITreeItem

        private bool isExpanded = false;

        [XmlIgnore]
        public override string DisplayText => this.PanelName;

        public override int SortPriority { get; set; }

        public override bool IsExpanded
        {
            get => this.isExpanded;
            set => this.SetProperty(ref this.isExpanded, value);
        }

        [XmlIgnore]
        public override bool Enabled
        {
            get =>
                this.Spells.Count() < 1 ?
                false :
                this.Spells.Count() == this.Spells.Where(x => x.Enabled).Count();
            set
            {
                foreach (var spell in this.Spells)
                {
                    spell.Enabled = value;
                }

                this.RaisePropertyChanged();
            }
        }

        [XmlIgnore]
        public override ICollectionView Children => this.childrenSource?.View;

        private CollectionViewSource childrenSource;

        public void SetupChildrenSource()
        {
            this.childrenSource = new CollectionViewSource()
            {
                Source = SpellTable.Instance.Table,
                IsLiveFilteringRequested = true,
                IsLiveSortingRequested = true,
            };

            this.childrenSource.Filter += (x, y) =>
             {
                 var spell = y.Item as Spell;
                 y.Accepted =
                    spell.PanelID == this.ID &&
                    !spell.IsInstance;
             };

            this.childrenSource.SortDescriptions.AddRange(new[]
            {
                new SortDescription()
                {
                    PropertyName = nameof(Spell.DisplayNo),
                    Direction = ListSortDirection.Ascending
                },
                new SortDescription()
                {
                    PropertyName = nameof(Spell.SpellTitle),
                    Direction = ListSortDirection.Ascending
                },
                new SortDescription()
                {
                    PropertyName = nameof(Spell.ID),
                    Direction = ListSortDirection.Ascending
                },
            });

            this.RaisePropertyChanged(nameof(this.Children));
            this.RaisePropertyChanged(nameof(this.Spells));
            this.RaisePropertyChanged(nameof(this.Enabled));
            this.RaisePropertyChanged(nameof(this.IsDesignMode));
        }

        #endregion ITreeItem

        public void MatchTrigger(string logLine)
        {
            // キャスト用のダミーメソッド
        }
    }
}
