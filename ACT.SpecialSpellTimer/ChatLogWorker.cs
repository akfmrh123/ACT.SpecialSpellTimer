﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using ACT.SpecialSpellTimer.Config;

namespace ACT.SpecialSpellTimer
{
    public class ChatLogWorker
    {
        #region Singleton

        private static ChatLogWorker instance = new ChatLogWorker();

        public static ChatLogWorker Instance => instance;

        #endregion Singleton

        private readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(10);
        private readonly Encoding UTF8Encoding = new UTF8Encoding(false);

        private volatile StringBuilder logBuffer = new StringBuilder();

        private Thread writeThread;
        private volatile bool writeThreadRunning;

        private string OutputDirectory => Settings.Default.SaveLogDirectory;

        private bool OutputEnabled =>
            Settings.Default.SaveLogEnabled &&
            !string.IsNullOrEmpty(this.OutputFile);

        private string OutputFile =>
            !string.IsNullOrEmpty(OutputDirectory) ?
            Path.Combine(
                this.OutputDirectory,
                $@"ACT.SpecialSpellTimer.Chatlog.{DateTime.Now.ToString("yyyy-MM-dd")}.log") :
            string.Empty;

        public void AppendLine(
            string text)
        {
            try
            {
                if (!this.OutputEnabled)
                {
                    return;
                }

                lock (this.logBuffer)
                {
                    this.logBuffer.AppendLine(text);
                }
            }
            catch (Exception)
            {
            }
        }

        public void AppendLines(
            List<string> logLineList)
        {
            try
            {
                if (!this.OutputEnabled)
                {
                    return;
                }

                lock (this.logBuffer)
                {
                    this.logBuffer.AppendLine(string.Join(
                        Environment.NewLine,
                        logLineList.ToArray()));
                }
            }
            catch (Exception)
            {
            }
        }

        public void Begin()
        {
            lock (this.logBuffer)
            {
                this.logBuffer.Clear();
            }

            this.writeThread = new Thread(() =>
            {
                while (this.writeThreadRunning)
                {
                    try
                    {
                        this.Flush();
                    }
                    catch (ThreadAbortException)
                    {
                        this.writeThreadRunning = false;
                        return;
                    }
                    catch (Exception)
                    {
                    }

                    Thread.Sleep(FlushInterval);
                }
            });

            this.writeThreadRunning = true;
            this.writeThread.Priority = ThreadPriority.Lowest;
            this.writeThread.Start();
        }

        public void End()
        {
            this.writeThreadRunning = false;

            if (this.writeThread != null)
            {
                this.writeThread.Join(TimeSpan.FromSeconds(2));
                if (this.writeThread.IsAlive)
                {
                    this.writeThread.Abort();
                }

                this.writeThread = null;
            }

            this.Flush();
        }

        public void Flush()
        {
            try
            {
                if (!this.OutputEnabled)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(this.OutputDirectory))
                {
                    if (!Directory.Exists(this.OutputDirectory))
                    {
                        Directory.CreateDirectory(this.OutputDirectory);
                    }
                }

                lock (this.logBuffer)
                {
                    if (this.logBuffer.Length <= 0)
                    {
                        return;
                    }

                    File.AppendAllText(
                        this.OutputFile,
                        this.logBuffer.ToString(),
                        this.UTF8Encoding);

                    this.logBuffer.Clear();
                }
            }
            catch (Exception)
            {
            }
        }
    }
}