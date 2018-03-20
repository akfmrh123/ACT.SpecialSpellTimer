using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ACT.SpecialSpellTimer.Models;
using ACT.SpecialSpellTimer.RaidTimeline;
using ACT.SpecialSpellTimer.RaidTimeline.Views;
using ACT.SpecialSpellTimer.resources;
using Advanced_Combat_Tracker;
using FFXIV.Framework.Common;
using FFXIV.Framework.Globalization;
using Prism.Commands;

namespace ACT.SpecialSpellTimer.Config.Views
{
    /// <summary>
    /// TimelineManagerView.xaml の相互作用ロジック
    /// </summary>
    public partial class TimelineManagerView :
        UserControl,
        ILocalizable,
        INotifyPropertyChanged
    {
        #region Logger

        private NLog.Logger AppLogger => FFXIV.Framework.Common.AppLog.DefaultLogger;

        #endregion Logger

        public TimelineManagerView()
        {
            this.InitializeComponent();
            this.SetLocale(Settings.Default.UILocale);
            this.LoadConfigViewResources();

            if (!WPFHelper.IsDesignMode)
            {
                WPFHelper.BeginInvoke(async () =>
                {
                    try
                    {
                        await Task.Run(() => TimelineManager.Instance.LoadTimelineModels());
                    }
                    catch (Exception ex)
                    {
                        TimelineModel.ShowRazorDumpFile();

                        ModernMessageBox.ShowDialog(
                            ex.Message,
                            "Timeline Loader",
                            MessageBoxButton.OK,
                            ex.InnerException);
                    }

                    // テーブルコンパイラにイベントを設定する
                    TableCompiler.Instance.CompileConditionChanged -= this.OnCompilerConditionChanged;
                    TableCompiler.Instance.CompileConditionChanged += this.OnCompilerConditionChanged;
                });
            }

            this.StyleListView.SelectionChanged += (x, y) =>
            {
                if (this.TimelineConfig.DesignMode)
                {
                    var selectedStyle = this.StyleListView.SelectedItem as TimelineStyle;

                    TimelineOverlay.HideDesignOverlay(false);
                    TimelineOverlay.ShowDesignOverlay(selectedStyle);

                    TimelineNoticeOverlay.HideDesignOverlay();
                    TimelineNoticeOverlay.ShowDesignOverlay(selectedStyle);
                }
            };

            this.timer.Tick += (x, y) =>
            {
                if (TimelineController.CurrentController != null)
                {
                    // ついでにスタートボタンのラベルを切り替える
                    this.StartButtonLabel = TimelineController.CurrentController.IsRunning ?
                        StopString :
                        StartString;
                }
            };

            this.timer.Start();
        }

        private DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        public string TimelineDirectory => TimelineManager.Instance.TimelineDirectory;

        public ObservableCollection<TimelineModel> TimelineModels => TimelineManager.Instance.TimelineModels;

        public TimelineSettings TimelineConfig => TimelineSettings.Instance;

        private const string StartString = "Start";
        private const string StopString = "Stop";

        private string startButtonLabel = StartString;

        public string StartButtonLabel
        {
            get => this.startButtonLabel;
            set => this.SetProperty(ref this.startButtonLabel, value);
        }

        #region Zone Changer

        private volatile bool isLoading = false;

        /// <summary>
        /// Tableコンパイラのコンパイルの前提条件が変わった
        /// </summary>
        /// <param name="sender">イベント発生元</param>
        /// <param name="e">イベント引数</param>
        private void OnCompilerConditionChanged(
            object sender,
            EventArgs e)
        {
            if (this.isLoading)
            {
                return;
            }

            this.isLoading = true;

            WPFHelper.BeginInvoke(() =>
            {
                try
                {
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                    this.LoadCurrentTimeline();
                }
                finally
                {
                    this.isLoading = false;
                }
            });
        }

        /// <summary>
        /// 現在のタイムラインをロードする
        /// </summary>
        private void LoadCurrentTimeline()
        {
            lock (this)
            {
                var timelines = TimelineManager.Instance.TimelineModels.ToArray();

                // グローバルトリガとリファンレスファイルをリロードする
                var toReloads = timelines.Where(x =>
                    x.IsGlobalZone ||
                    x.IsReference);
                foreach (var tl in toReloads)
                {
                    tl.Reload();
                    Thread.Yield();
                }

                // すでにコントローラがロードされていたらアンロードする
                foreach (var tl in timelines)
                {
                    tl.Controller.Unload();
                    tl.IsActive = false;
                }

                // 現在のゾーンで有効なタイムラインを取得する
                var newTimeline = timelines
                    .FirstOrDefault(x => x.Controller.IsAvailable);

                // 有効なTLが存在しないならばグローバルのいずれかをカレントにする
                if (newTimeline == null)
                {
                    newTimeline = toReloads.FirstOrDefault(x => x.IsGlobalZone);
                }

                if (newTimeline != null)
                {
                    // 該当のタイムラインファイルをリロードする
                    if (!newTimeline.IsGlobalZone)
                    {
                        newTimeline.Reload();
                    }

                    // コントローラをロードする
                    newTimeline.Controller.Load();
                    newTimeline.IsActive = true;

                    this.AppLogger.Trace($"[TL] Timeline auto loaded. active_timeline={newTimeline.TimelineName}.");

                    this.TimelineList.ScrollIntoView(newTimeline);
                }

                // グローバルトリガを初期化する
                TimelineManager.Instance.InitGlobalTriggers();
            }
        }

        #endregion Zone Changer

        #region Commands 左側ペイン

        private ICommand enabledTimelineCommand;

        public ICommand EnabledTimelineCommand =>
            this.enabledTimelineCommand ?? (this.enabledTimelineCommand = new DelegateCommand<bool?>((isChecked) =>
            {
                if (!isChecked.HasValue)
                {
                    return;
                }

                if (!isChecked.Value)
                {
                    TimelineOverlay.CloseTimeline();

                    var active = TimelineManager.Instance.TimelineModels.FirstOrDefault(x => x.IsActive);
                    if (active != null)
                    {
                        active.Controller.EndActivityLine();
                        active.Controller.Unload();
                        active.IsActive = false;
                    }
                }
            }));

        private ICommand openTimelineFolderCommand;

        public ICommand OpenTimelineFolderCommand =>
            this.openTimelineFolderCommand ?? (this.openTimelineFolderCommand = new DelegateCommand(() =>
            {
                if (Directory.Exists(this.TimelineDirectory))
                {
                    Process.Start(this.TimelineDirectory);
                }
            }));

        private ICommand reloadTimelineFolderCommand;

        public ICommand ReloadTimelineFolderCommand =>
            this.reloadTimelineFolderCommand ?? (this.reloadTimelineFolderCommand = new DelegateCommand(async () =>
            {
                try
                {
                    await Task.Run(() => TimelineManager.Instance.LoadTimelineModels());
                }
                catch (Exception ex)
                {
                    TimelineModel.ShowRazorDumpFile();

                    ModernMessageBox.ShowDialog(
                        ex.Message,
                        "Timeline Loader",
                        MessageBoxButton.OK,
                        ex.InnerException);

                    return;
                }

                this.LoadCurrentTimeline();
            }));

        private ICommand startTimelineCommand;

        public ICommand StartTimelineCommand =>
            this.startTimelineCommand ?? (this.startTimelineCommand = new DelegateCommand<Button>((button) =>
            {
                if (button == null)
                {
                    return;
                }

                if (!this.TimelineConfig.Enabled)
                {
                    return;
                }

                var activeTL = TimelineManager.Instance.TimelineModels.FirstOrDefault(x => x.IsActive);
                if (activeTL == null)
                {
                    return;
                }

                if (activeTL.Controller.IsRunning)
                {
                    activeTL.Controller.EndActivityLine();
                    this.StartButtonLabel = StartString;
                }
                else
                {
                    activeTL.Controller.StartActivityLine();
                    this.StartButtonLabel = StopString;
                }
            }));

        private ICommand testTimelineCommand;

        public ICommand TestTimelineCommand =>
            this.testTimelineCommand ?? (this.testTimelineCommand = new DelegateCommand(() =>
            {
                this.TestTimeline();
            }));

        private System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog()
        {
            RestoreDirectory = true,
            Filter = "CombatLog Files|*.log|All Files|*.*",
            FilterIndex = 0,
            DefaultExt = ".log",
            SupportMultiDottedExtensions = true,
        };

        /// <summary>
        /// タイムラインをテストする
        /// </summary>
        private void TestTimeline()
        {
            var result = this.openFileDialog.ShowDialog(ActGlobals.oFormActMain);
            if (result != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var view = new TimelineTesterView(this.openFileDialog.FileName);
            view.Show();
        }

        #endregion Commands 左側ペイン

        #region Commands 右側ペイン

        private ICommand showOverlayCommand;

        public ICommand ShowOverlayCommand =>
            this.showOverlayCommand ?? (this.showOverlayCommand = new DelegateCommand<bool?>((isChecked) =>
            {
                if (!isChecked.HasValue)
                {
                    return;
                }

                if (!isChecked.Value)
                {
                    TimelineOverlay.CloseTimeline();
                    TimelineNoticeOverlay.CloseNotice();
                    return;
                }

                var active = TimelineManager.Instance.TimelineModels.FirstOrDefault(x => x.IsActive);
                if (active != null)
                {
                    if (!active.IsGlobalZone)
                    {
                        TimelineOverlay.ShowTimeline(active);
                    }

                    TimelineNoticeOverlay.ShowNotice();
                }
            }));

        private ICommand clickthroughCommand;

        public ICommand ClickthroughCommand =>
            this.clickthroughCommand ?? (this.clickthroughCommand = new DelegateCommand<bool?>((isChecked) =>
            {
                if (!isChecked.HasValue)
                {
                    return;
                }

                TimelineOverlay.ChangeClickthrough(isChecked.Value);
                TimelineNoticeOverlay.ChangeClickthrough(isChecked.Value);
            }));

        private ICommand showDummyOverlayCommand;

        public ICommand ShowDummyOverlayCommand =>
            this.showDummyOverlayCommand ?? (this.showDummyOverlayCommand = new DelegateCommand<bool?>((isChecked) =>
            {
                if (!isChecked.HasValue)
                {
                    return;
                }

                if (isChecked.Value)
                {
                    var selectedStyle = this.StyleListView.SelectedItem as TimelineStyle;
                    TimelineOverlay.ShowDesignOverlay(selectedStyle);
                    TimelineNoticeOverlay.ShowDesignOverlay(selectedStyle);
                }
                else
                {
                    TimelineOverlay.HideDesignOverlay();
                    TimelineNoticeOverlay.HideDesignOverlay();
                }
            }));

        private ICommand addStyleCommand;

        public ICommand AddStyleCommand =>
            this.addStyleCommand ?? (this.addStyleCommand = new DelegateCommand(() =>
            {
                var style = default(TimelineStyle);

                if (this.StyleListView.SelectedItem != null)
                {
                    style = (this.StyleListView.SelectedItem as TimelineStyle).Clone();
                }
                else
                {
                    style = TimelineStyle.SuperDefaultStyle.Clone();
                }

                style.Name = "New Style";
                style.IsDefault = false;
                style.IsDefaultNotice = false;

                TimelineSettings.Instance.Styles.Add(style);
            }));

        private ICommand deleteStyleCommand;

        public ICommand DeleteStyleCommand =>
            this.deleteStyleCommand ?? (this.deleteStyleCommand = new DelegateCommand(() =>
            {
                if (this.StyleListView.SelectedItem != null)
                {
                    var style = this.StyleListView.SelectedItem as TimelineStyle;

                    if (TimelineSettings.Instance.Styles.Count > 1)
                    {
                        TimelineSettings.Instance.Styles.Remove(style);
                        this.StyleListView.SelectedIndex = 0;
                    }
                }
            }));

        private ICommand openResourcesFolderCommand;

        public ICommand OpenResourcesFolderCommand =>
            this.openResourcesFolderCommand ?? (this.openResourcesFolderCommand = new DelegateCommand(() =>
            {
                var dir = DirectoryHelper.FindSubDirectory(@"Resources\Styles");
                if (Directory.Exists(dir))
                {
                    Process.Start(dir);
                }
            }));

        #endregion Commands 右側ペイン

        /// <summary>
        /// トップアクティビティ or トップじゃないアクティビティへの表示制御を反映させる
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">event arg</param>
        private void TopActivityStyle_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<object> e)
        {
            TimelineController.CurrentController?.RefreshActivityLineVisibility();
            TimelineOverlay.BindingDummyTimeline?.Controller?.RefreshActivityLineVisibility();
        }

        #region ILocalizebale

        public void SetLocale(Locales locale) => this.ReloadLocaleDictionary(locale);

        #endregion ILocalizebale

        #region INotifyPropertyChanged

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(
            [CallerMemberName]string propertyName = null)
        {
            this.PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }

        protected virtual bool SetProperty<T>(
            ref T field,
            T value,
            [CallerMemberName]string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            this.PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));

            return true;
        }

        #endregion INotifyPropertyChanged
    }
}
