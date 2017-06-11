﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;
using ZipExtractor.Properties;

namespace ZipExtractor
{
    public partial class ZipExtractorForm : Form
    {
        private BackgroundWorker _BackgroundWorker;

        public ZipExtractorForm()
        {
            InitializeComponent();
        }

        private void ZipExtractorForm_Shown(object sender, EventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length.Equals(3))
            {
                foreach (var process in Process.GetProcesses())
                {
                    if (process.StartInfo.FileName.Equals(args[2]))
                    {
                        process.WaitForExit();
                    }
                }

                // Extract all the files.
                _BackgroundWorker = new BackgroundWorker
                {
                    WorkerReportsProgress = true,
                    WorkerSupportsCancellation = true
                };

                _BackgroundWorker.DoWork += (o, eventArgs) =>
                {
                    var path = Path.GetDirectoryName(args[2]);

                    // Open an existing zip file for reading.
                    ZipStorer zip = ZipStorer.Open(args[1], FileAccess.Read);

                    // Read the central directory collection.
                    List<ZipStorer.ZipFileEntry> dir = zip.ReadCentralDir();

                    for (var index = 0; index < dir.Count; index++)
                    {
                        if (_BackgroundWorker.CancellationPending)
                        {
                            eventArgs.Cancel = true;
                            zip.Close();
                            return;
                        }
                        ZipStorer.ZipFileEntry entry = dir[index];
                        zip.ExtractFile(entry, Path.Combine(path, entry.FilenameInZip));
                        _BackgroundWorker.ReportProgress((index + 1) * 100 / dir.Count, string.Format(Resources.CurrentFileExtracting, entry.FilenameInZip));
                    }

                    zip.Close();
                };

                _BackgroundWorker.ProgressChanged += (o, eventArgs) =>
                {
                    progressBar.Value = eventArgs.ProgressPercentage;
                    labelInformation.Text = eventArgs.UserState.ToString();
                };

                _BackgroundWorker.RunWorkerCompleted += (o, eventArgs) =>
                {
                    if (!eventArgs.Cancelled)
                    {
                        labelInformation.Text = @"Finished";
                        try
                        {
                            Process.Start(args[2]);
                        }
                        catch (Win32Exception exception)
                        {
                            if (exception.NativeErrorCode != 1223)
                                throw;
                        }
                        Application.Exit();
                    }
                };
                _BackgroundWorker.RunWorkerAsync();
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _BackgroundWorker?.CancelAsync();
        }
    }
}