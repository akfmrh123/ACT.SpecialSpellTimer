using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using ACT.SpecialSpellTimer.Config;
using ACT.SpecialSpellTimer.Config.Models;
using ACT.SpecialSpellTimer.Config.Views;
using ACT.SpecialSpellTimer.Sound;
using ACT.SpecialSpellTimer.Utility;
using FFXIV.Framework.Common;
using FFXIV.Framework.Extensions;
using FFXIV.Framework.Globalization;

namespace ACT.SpecialSpellTimer.Models
{
    /// <summary>
    /// ワンポイントテロップ
    /// </summary>
    [Serializable]
    [XmlType(TypeName = "OnePointTelop")]
    public class Ticker :
        TreeItemBase,
        IDisposable,
        ITrigger
    {
        [XmlIgnore]
        public override ItemTypes ItemType => ItemTypes.Ticker;

        #region ITrigger

        public void MatchTrigger(string logLine)
            => TickersController.Instance.MatchCore(this, logLine);

        #endregion ITrigger

        #region ITreeItem

        private bool enabled = false;

        [XmlIgnore]
        public override string DisplayText => this.Title;

        [XmlIgnore]
        public override int SortPriority { get; set; }

        [XmlIgnore]
        public override bool IsExpanded
        {
            get => false;
            set { }
        }

        [XmlElement(ElementName = "Enabled")]
        public override bool Enabled
        {
            get => this.enabled;
            set => this.SetProperty(ref this.enabled, value);
        }

        [XmlIgnore]
        public override ICollectionView Children => null;

        #endregion ITreeItem

        [XmlIgnore]
        private Timer delayedSoundTimer;

        [XmlIgnore]
        public bool ToClose { get; set; } = false;

        public Ticker()
        {
            this.Title = string.Empty;
            this.Keyword = string.Empty;
            this.KeywordToHide = string.Empty;
            this.Message = string.Empty;
            this.MatchSound = string.Empty;
            this.MatchTextToSpeak = string.Empty;
            this.DelaySound = string.Empty;
            this.DelayTextToSpeak = string.Empty;
            this.BackgroundColor = string.Empty;
            this.FontColor = string.Empty;
            this.FontOutlineColor = string.Empty;
            this.MatchedLog = string.Empty;
            this.MessageReplaced = string.Empty;
            this.RegexPattern = string.Empty;
            this.RegexPatternToHide = string.Empty;
            this.JobFilter = string.Empty;
            this.ZoneFilter = string.Empty;
            this.TimersMustRunningForStart = new Guid[0];
            this.TimersMustStoppingForStart = new Guid[0];
            this.Font = new FontInfo();
            this.KeywordReplaced = string.Empty;
            this.KeywordToHideReplaced = string.Empty;

            // ディレイサウンドタイマをセットする
            this.delayedSoundTimer = new Timer
            {
                AutoReset = false,
                Enabled = false
            };

            this.delayedSoundTimer.Elapsed += this.DelayedSoundTimer_Elapsed;
        }

        public Guid Guid { get; set; } = Guid.NewGuid();

        private double left = 0;
        private double top = 0;

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

        private bool isDesignMode = false;

        [XmlIgnore]
        public bool IsDesignMode
        {
            get => this.isDesignMode;
            set => this.SetProperty(ref this.isDesignMode, value);
        }

        public long ID { get; set; }

        public string Title { get; set; }

        public string Message { get; set; }

        [XmlIgnore]
        public string MessageReplaced { get; set; }

        [XmlIgnore]
        public DateTime MatchDateTime { get; set; }

        [XmlIgnore]
        public string MatchedLog { get; set; }

        public double Delay { get; set; } = 0;

        public double DisplayTime { get; set; } = 0;

        public FontInfo Font { get; set; } = FontInfo.DefaultFont;

        public string FontColor { get; set; }

        public string FontOutlineColor { get; set; }

        public int BackgroundAlpha { get; set; }

        public string BackgroundColor { get; set; }

        public bool AddMessageEnabled { get; set; }

        private bool progressBarEnabled;

        public bool ProgressBarEnabled
        {
            get => this.progressBarEnabled;
            set => this.SetProperty(ref this.progressBarEnabled, value);
        }

        public string JobFilter { get; set; }

        public string ZoneFilter { get; set; }

        public Guid[] TimersMustRunningForStart { get; set; }

        public Guid[] TimersMustStoppingForStart { get; set; }

        [XmlIgnore]
        public bool ForceHide { get; set; }

        #region Sequential TTS

        /// <summary>
        /// 同時再生を抑制してシーケンシャルにTTSを再生する
        /// </summary>
        public bool IsSequentialTTS { get; set; } = false;

        public void Play(string tts, AdvancedNoticeConfig config)
            => Spell.PlayCore(tts, this.IsSequentialTTS, config);

        #endregion Sequential TTS

        #region to Notice

        public string MatchTextToSpeak { get; set; }

        [XmlIgnore]
        public bool Delayed { get; set; }

        public string DelayTextToSpeak { get; set; }

        public AdvancedNoticeConfig MatchAdvancedConfig { get; set; } = new AdvancedNoticeConfig();

        public AdvancedNoticeConfig DelayAdvancedConfig { get; set; } = new AdvancedNoticeConfig();

        #endregion to Notice

        #region to Notice wave files

        [XmlIgnore]
        private string delaySound = string.Empty;

        [XmlIgnore]
        private string matchSound = string.Empty;

        [XmlIgnore]
        public string DelaySound { get => this.delaySound; set => this.delaySound = value; }

        [XmlElement(ElementName = "DelaySound")]
        public string DelaySoundToFile
        {
            get => Path.GetFileName(this.delaySound);
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    this.delaySound = Path.Combine(SoundController.Instance.WaveDirectory, value);
                }
            }
        }

        [XmlIgnore]
        public string MatchSound { get => this.matchSound; set => this.matchSound = value; }

        [XmlElement(ElementName = "MatchSound")]
        public string MatchSoundToFile
        {
            get => Path.GetFileName(this.matchSound);
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    this.matchSound = Path.Combine(SoundController.Instance.WaveDirectory, value);
                }
            }
        }

        #endregion to Notice wave files

        #region Performance Monitor

        [XmlIgnore]
        public double MatchingDuration { get; set; } = 0.0;

        [XmlIgnore]
        private DateTime matchingStartDateTime;

        public void StartMatching()
        {
            this.matchingStartDateTime = DateTime.Now;
        }

        public void EndMatching()
        {
            var ticks = (DateTime.Now - this.matchingStartDateTime).Ticks;
            if (ticks == 0)
            {
                return;
            }

            var cost = ticks / 1000;

            if (this.MatchingDuration != 0)
            {
                this.MatchingDuration += cost;
                this.MatchingDuration /= 2;
            }
            else
            {
                this.MatchingDuration += cost;
            }
        }

        #endregion Performance Monitor

        public void Dispose()
        {
            if (this.delayedSoundTimer != null)
            {
                this.delayedSoundTimer.Stop();
                this.delayedSoundTimer.Dispose();
                this.delayedSoundTimer = null;
            }
        }

        /// <summary>
        /// ディレイサウンドのタイマを開始する
        /// </summary>
        public void StartDelayedSoundTimer()
        {
            var timer = this.delayedSoundTimer;

            if (timer == null)
            {
                return;
            }

            if (timer.Enabled)
            {
                timer.Stop();
            }

            if (this.Delay <= 0 ||
                this.MatchDateTime <= DateTime.MinValue)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(this.DelaySound) &&
                string.IsNullOrWhiteSpace(this.DelayTextToSpeak))
            {
                return;
            }

            // タイマをセットする
            var timeToPlay = this.MatchDateTime.AddSeconds(this.Delay);
            var duration = (timeToPlay - DateTime.Now).TotalMilliseconds;

            if (duration > 0d)
            {
                // タイマスタート
                timer.Interval = duration;
                timer.Start();
            }
        }

        private void DelayedSoundTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Delayed = true;

            var regex = this.Regex;
            var wave = this.DelaySound;
            var speak = this.DelayTextToSpeak;

            this.Play(this.DelaySound, this.DelayAdvancedConfig);

            if (!string.IsNullOrWhiteSpace(this.DelayTextToSpeak))
            {
                if (regex == null ||
                    !speak.Contains("$"))
                {
                    this.Play(speak, this.DelayAdvancedConfig);
                    return;
                }

                var match = regex.Match(this.MatchedLog);
                speak = match.Result(speak);

                this.Play(speak, this.DelayAdvancedConfig);
            }
        }

        #region Clone

        public Ticker Clone() => (Ticker)this.MemberwiseClone();

        #endregion Clone

        #region NewTicker

        public static Ticker CreateNew()
        {
            var n = new Ticker();

            lock (TickerTable.Instance.Table)
            {
                n.ID = TickerTable.Instance.Table.Any() ?
                    TickerTable.Instance.Table.Max(x => x.ID) + 1 :
                    1;
            }

            n.Title = "New Ticker";
            n.DisplayTime = 3;
            n.FontColor = Colors.White.ToLegacy().ToHTML();
            n.FontOutlineColor = Colors.Crimson.ToLegacy().ToHTML();
            n.BackgroundColor = Colors.Transparent.ToLegacy().ToHTML();
            n.Top = 30.0d;
            n.Left = 40.0d;

            return n;
        }

        public Ticker CreateSimilarNew()
        {
            var n = Ticker.CreateNew();

            n.Title = this.Title + " New";
            n.Message = this.Message;
            n.Keyword = this.Keyword;
            n.KeywordToHide = this.KeywordToHide;
            n.RegexEnabled = this.RegexEnabled;
            n.Delay = this.Delay;
            n.DisplayTime = this.DisplayTime;
            n.AddMessageEnabled = this.AddMessageEnabled;
            n.ProgressBarEnabled = this.ProgressBarEnabled;
            n.FontColor = this.FontColor;
            n.FontOutlineColor = this.FontOutlineColor;
            n.Font = this.Font;
            n.BackgroundColor = this.BackgroundColor;
            n.BackgroundAlpha = this.BackgroundAlpha;
            n.Left = this.Left;
            n.Top = this.Top;
            n.JobFilter = this.JobFilter;
            n.ZoneFilter = this.ZoneFilter;
            n.TimersMustRunningForStart = this.TimersMustRunningForStart;
            n.TimersMustStoppingForStart = this.TimersMustStoppingForStart;

            n.MatchAdvancedConfig = this.MatchAdvancedConfig.Clone() as AdvancedNoticeConfig;
            n.DelayAdvancedConfig = this.DelayAdvancedConfig.Clone() as AdvancedNoticeConfig;
            n.IsSequentialTTS = this.IsSequentialTTS;

            return n;
        }

        #endregion NewTicker

        #region Regex compiler

        [XmlIgnore]
        public bool IsRealtimeCompile { get; set; } = false;

        private bool regexEnabled;
        private string keyword;
        private string keywordToHide;

        public bool RegexEnabled
        {
            get => this.regexEnabled;
            set
            {
                if (this.SetProperty(ref this.regexEnabled, value))
                {
                    this.KeywordReplaced = string.Empty;
                    this.KeywordToHideReplaced = string.Empty;

                    if (this.IsRealtimeCompile)
                    {
                        var ex = this.CompileRegex();
                        if (ex != null)
                        {
                            ModernMessageBox.ShowDialog(
                                "Regex compile error ! This is invalid keyword.",
                                "Regex compiler",
                                MessageBoxButton.OK,
                                ex);
                        }

                        ex = this.CompileRegexToHide();
                        if (ex != null)
                        {
                            ModernMessageBox.ShowDialog(
                                "Regex compile error ! This is invalid keyword.",
                                "Regex compiler",
                                MessageBoxButton.OK,
                                ex);
                        }
                    }
                }
            }
        }

        public string Keyword
        {
            get => this.keyword;
            set
            {
                if (this.SetProperty(ref this.keyword, value))
                {
                    this.KeywordReplaced = string.Empty;
                    if (this.IsRealtimeCompile)
                    {
                        var ex = this.CompileRegex();
                        if (ex != null)
                        {
                            ModernMessageBox.ShowDialog(
                                "Regex compile error ! This is invalid keyword.",
                                "Regex compiler",
                                MessageBoxButton.OK,
                                ex);
                        }
                    }
                }
            }
        }

        public string KeywordToHide
        {
            get => this.keywordToHide;
            set
            {
                if (this.SetProperty(ref this.keywordToHide, value))
                {
                    this.KeywordToHideReplaced = string.Empty;
                    if (this.IsRealtimeCompile)
                    {
                        var ex = this.CompileRegexToHide();
                        if (ex != null)
                        {
                            ModernMessageBox.ShowDialog(
                                "Regex compile error ! This is invalid keyword.",
                                "Regex compiler",
                                MessageBoxButton.OK,
                                ex);
                        }
                    }
                }
            }
        }

        [XmlIgnore]
        public string KeywordReplaced { get; set; }

        [XmlIgnore]
        public string KeywordToHideReplaced { get; set; }

        [XmlIgnore]
        public Regex Regex { get; set; }

        [XmlIgnore]
        public Regex RegexToHide { get; set; }

        [XmlIgnore]
        public string RegexPattern { get; set; }

        [XmlIgnore]
        public string RegexPatternToHide { get; set; }

        public Exception CompileRegex()
        {
            var pattern = string.Empty;

            try
            {
                this.KeywordReplaced = TableCompiler.Instance.GetMatchingKeyword(
                    this.KeywordReplaced,
                    this.Keyword);

                if (this.RegexEnabled)
                {
                    pattern = this.KeywordReplaced.ToRegexPattern();

                    if (this.Regex == null ||
                        this.RegexPattern != pattern)
                    {
                        this.Regex = pattern.ToRegex();
                        this.RegexPattern = pattern;
                    }
                }
                else
                {
                    this.Regex = null;
                    this.RegexPattern = string.Empty;
                }
            }
            catch (Exception ex)
            {
                return ex;
            }

            return null;
        }

        public Exception CompileRegexToHide()
        {
            var message = string.Empty;
            var pattern = string.Empty;

            try
            {
                this.KeywordToHideReplaced = TableCompiler.Instance.GetMatchingKeyword(
                    this.KeywordToHideReplaced,
                    this.KeywordToHide);

                if (this.RegexEnabled)
                {
                    pattern = this.KeywordToHideReplaced.ToRegexPattern();

                    if (this.RegexToHide == null ||
                        this.RegexPatternToHide != pattern)
                    {
                        this.RegexToHide = pattern.ToRegex();
                        this.RegexPatternToHide = pattern;
                    }
                }
                else
                {
                    this.RegexToHide = null;
                    this.RegexPatternToHide = string.Empty;
                }
            }
            catch (Exception ex)
            {
                return ex;
            }

            return null;
        }

        #endregion Regex compiler

        public void SimulateMatch()
        {
            // 擬似的にマッチ状態にする
            var now = DateTime.Now;
            this.MatchDateTime = now;

            this.Delayed = false;

            // マッチ時点のサウンドを再生する
            this.MatchAdvancedConfig.PlayWave(this.MatchSound);
            this.MatchAdvancedConfig.Speak(this.MatchTextToSpeak);

            // 遅延サウンドタイマを開始する
            this.StartDelayedSoundTimer();
        }
    }
}
