using System;
using System.ComponentModel;
using System.Net.Cache;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace AutoUpdaterDotNET
{
    internal partial class DownloadUpdateDialog : Form
    {
        private readonly string _DownloadUrl;

        private string _TempPath;

        private WebClient _WebClient;

        public DownloadUpdateDialog(string downloadUrl)
        {
            InitializeComponent();
            _DownloadUrl = downloadUrl;
        }

        private void DownloadUpdateDialogLoad(object sender, EventArgs e)
        {
            _WebClient = new WebClient();

            var uri = new Uri(_DownloadUrl);

            _TempPath = Path.Combine(Path.GetTempPath(), GetFileName(_DownloadUrl));

            _WebClient.DownloadProgressChanged += OnDownloadProgressChanged;
            _WebClient.DownloadFileCompleted += OnDownloadComplete;
            _WebClient.DownloadFileAsync(uri, _TempPath);
        }

        private void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            progressBar.Value = e.ProgressPercentage;
        }

        private void OnDownloadComplete(object sender, AsyncCompletedEventArgs e)
        {
            if (!e.Cancelled)
            {
                var processStartInfo = new ProcessStartInfo {FileName = _TempPath, UseShellExecute = true};
                var extension = Path.GetExtension(_TempPath);
                if (extension != null && extension.ToLower().Equals(".zip"))
                {
                    string installerPath = Path.Combine(Path.GetTempPath(), "ZipExtractor.exe");
                    File.WriteAllBytes(installerPath, Properties.Resources.ZipExtractor);
                    processStartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = installerPath,
                        Arguments = $"\"{_TempPath}\" \"{Assembly.GetEntryAssembly().Location}\""
                    };
                }
                try
                {
                    Process.Start(processStartInfo);
                }
                catch (Win32Exception exception)
                {
                    if (exception.NativeErrorCode != 1223)
                        throw;
                }

                var currentProcess = Process.GetCurrentProcess();
                foreach (var process in Process.GetProcessesByName(currentProcess.ProcessName))
                {
                    if (process.Id != currentProcess.Id)
                    {
                        process.Kill();
                    }
                }

                if (AutoUpdater.IsWinFormsApplication)
                {
                    Application.Exit();
                }
                else
                {
                    Environment.Exit(0);
                }
            }
        }

        private static string GetFileName(string url, string httpWebRequestMethod = "HEAD")
        {
            try
            {
                var fileName = string.Empty;
                var uri = new Uri(url);
                if (uri.Scheme.Equals(Uri.UriSchemeHttp) || uri.Scheme.Equals(Uri.UriSchemeHttps))
                {
                    var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                    httpWebRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
                    httpWebRequest.Method = httpWebRequestMethod;
                    httpWebRequest.AllowAutoRedirect = false;
                    var httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                    if (httpWebResponse.StatusCode.Equals(HttpStatusCode.Redirect) ||
                        httpWebResponse.StatusCode.Equals(HttpStatusCode.Moved) ||
                        httpWebResponse.StatusCode.Equals(HttpStatusCode.MovedPermanently))
                    {
                        if (httpWebResponse.Headers["Location"] != null)
                        {
                            var location = httpWebResponse.Headers["Location"];
                            fileName = GetFileName(location);
                            return fileName;
                        }
                    }
                    var contentDisposition = httpWebResponse.Headers["content-disposition"];
                    if (!string.IsNullOrEmpty(contentDisposition))
                    {
                        const string LOOK_FOR_FILE_NAME = "filename=";
                        var index = contentDisposition.IndexOf(LOOK_FOR_FILE_NAME, StringComparison.CurrentCultureIgnoreCase);
                        if (index >= 0)
                            fileName = contentDisposition.Substring(index + LOOK_FOR_FILE_NAME.Length);
                        if (fileName.StartsWith("\"") && fileName.EndsWith("\""))
                        {
                            fileName = fileName.Substring(1, fileName.Length - 2);
                        }
                    }
                }
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = Path.GetFileName(uri.LocalPath);
                }
                return fileName;
            }
            catch (WebException)
            {
                return GetFileName(url, "GET");
            }
        }

        private void DownloadUpdateDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            _WebClient.CancelAsync();
        }
    }
}
