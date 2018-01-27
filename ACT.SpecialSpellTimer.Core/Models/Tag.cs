using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Xml.Serialization;
using ACT.SpecialSpellTimer.Config.Models;

namespace ACT.SpecialSpellTimer.Models
{
    [Serializable]
    public class Tag :
        TreeItemBase
    {
        #region プリセットタグ

        /// <summary>
        /// インポートタグ
        /// </summary>
        private static Tag importsTag = new Tag()
        {
            Name = "+Imports",
            SortPriority = 100,
        };

        public static Tag ImportsTag => importsTag;

        public static void SetImportTag(
            Tag tag)
        {
            tag.SortPriority = importsTag.SortPriority;
            importsTag = tag;
        }

        #endregion プリセットタグ

        private Guid id = Guid.NewGuid();
        private string name = string.Empty;

        public Tag()
        {
            this.SetupChildrenSource();
        }

        [XmlIgnore]
        public override ItemTypes ItemType => ItemTypes.Tag;

        public Guid ID
        {
            get => this.id;
            set
            {
                if (this.SetProperty(ref this.id, value))
                {
                    this.RefreshChildren();
                }
            }
        }

        public string Name
        {
            get => this.name;
            set => this.SetProperty(ref this.name, value);
        }

        [XmlIgnore]
        public bool IsDesignMode
        {
            get
            {
                if (!this.children.Any())
                {
                    return false;
                }

                var value = true;
                foreach (dynamic child in this.Children)
                {
                    value &= child?.IsDesignMode ?? false;
                }

                return value;
            }
            set
            {
                foreach (dynamic child in this.Children)
                {
                    if (child != null)
                    {
                        child.IsDesignMode = value;
                    }
                }
            }
        }

        #region ITreeItem

        private bool isExpanded = false;

        [XmlIgnore]
        public override string DisplayText => this.Name;

        public override int SortPriority { get; set; }

        public override bool IsExpanded
        {
            get => this.isExpanded;
            set => this.SetProperty(ref this.isExpanded, value);
        }

        [XmlIgnore]
        public override bool Enabled
        {
            get
            {
                if (!this.children.Any())
                {
                    return false;
                }

                var value = true;
                foreach (ITreeItem child in this.Children)
                {
                    value &= child?.Enabled ?? false;
                }

                return value;
            }
            set
            {
                foreach (ITreeItem child in this.Children)
                {
                    if (child != null)
                    {
                        child.Enabled = value;
                    }
                }
            }
        }

        [XmlIgnore]
        public override ICollectionView Children => this.childrenSource?.View;

        private ObservableCollection<ITreeItem> children;
        private CollectionViewSource childrenSource;

        public void SetupChildrenSource()
        {
            this.children = new ObservableCollection<ITreeItem>();
            this.childrenSource = new CollectionViewSource()
            {
                Source = this.children,
                IsLiveSortingRequested = true
            };

            this.childrenSource.SortDescriptions.AddRange(new[]
            {
                new SortDescription()
                {
                    PropertyName = nameof(ITreeItem.ItemType),
                    Direction = ListSortDirection.Ascending,
                },
                new SortDescription()
                {
                    PropertyName = nameof(ITreeItem.SortPriority),
                    Direction = ListSortDirection.Descending,
                },
                new SortDescription()
                {
                    PropertyName = nameof(ITreeItem.DisplayText),
                    Direction = ListSortDirection.Ascending,
                },
            });

            TagTable.Instance.ItemTags.CollectionChanged += this.ItemTagsOnCollectionChanged;
        }

        private void ItemTagsOnCollectionChanged(
            object sender,
            NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ItemTags item in e.NewItems)
                {
                    if (item.TagID == this.ID)
                    {
                        this.RefreshChildren();
                        return;
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (ItemTags item in e.OldItems)
                {
                    if (item.TagID == this.ID)
                    {
                        this.RefreshChildren();
                        return;
                    }
                }
            }
        }

        public void RefreshChildren()
        {
            var items =
                from x in TagTable.Instance.ItemTags
                where
                x.TagID == this.ID &&
                x.Item != null
                select
                x.Item;

            this.children.Clear();
            this.children.AddRange(items);

            this.RaisePropertyChanged(nameof(this.Enabled));
        }

        #endregion ITreeItem
    }
}
