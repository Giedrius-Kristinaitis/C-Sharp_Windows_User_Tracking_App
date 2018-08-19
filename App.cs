﻿using Microsoft.Win32;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System;

namespace IDK {

    /// <summary>
    /// Class that manages the application
    /// </summary>
    class App {

        // base file name for the status report
        private const string STATUS_REPORT_FILE_NAME = "STATUS_REPORT.json";

        /// <summary>
        /// Gets the name of the executable file
        /// </summary>
        /// <returns></returns>
        public static string GetExecutableName() {
            string[] pathParts = Application.ExecutablePath.Split(new char[] { '/', '\\' });
            return pathParts[pathParts.Length - 1];
        }

        /// <summary>
        /// Downloads a new executable from the server and updates the application
        /// </summary>
        /// <param name="remoteFile">name of the remote update file to download</param>
        public static void UpdateAppExecutable(string remoteFile) {
            // if the remote executable name matches the current executable then do not update
            if (GetExecutableName().Equals(remoteFile)) {
                return;
            }

            // everything's ok, update the app
            new Thread(delegate() {
                try {
                    Network.DownloadFile(Config.SERVER_BASE_URL + "update/" + remoteFile, remoteFile);
                    UnregisterAsStartupExecutable();
                    
                    // start the new app executable
                    Process process = new Process();
                    process.StartInfo.FileName = remoteFile;
                    process.StartInfo.Arguments = "\"" + GetExecutableName() + "\"";
                    process.Start();
                    
                    // exit the current application, which will be deleted by the newly started one
                    Application.Exit();
                } catch { }
            }).Start();
        }

        /// <summary>
        /// Performs necessary initialization
        /// </summary>
        /// <param name="args">arguments for the program</param>
        public static void Initialize(string[] args) {
            SendStatusReport();

            RegisterAsStartupExecutable();

            if (args.Length > 0) {
                DeleteOldExecutable(args[0]);
            }
        }

        /// <summary>
        /// Deletes the old executable of this application (usually done after an app update)
        /// </summary>
        /// <param name="file">file name of the old executable</param>
        private static void DeleteOldExecutable(string file) {
            Thread thread = new Thread(delegate () {
                // sleep for 10 seconds so that the old executable has time to exit
                Thread.Sleep(10000);

                try {
                    new FileInfo(file).Delete();
                } catch { }
            });

            thread.Start();
            thread.Join();
        }
        
        /// <summary>
        /// Registers this application to start automatically on windows startup
        /// </summary>
        private static void RegisterAsStartupExecutable() {
            try {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key.SetValue(Config.APP_NAME, Application.ExecutablePath);
                key.Dispose();
            } catch { }
        }

        /// <summary>
        /// Unregisters this application so that it no longer starts on windows startup
        /// </summary>
        private static void UnregisterAsStartupExecutable() {
            try {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key.DeleteValue(Config.APP_NAME, false);
                key.Dispose();
            } catch { }
        }

        /// <summary>
        /// Sends a status report to the server. Blocks the calling until completed
        /// </summary>
        private static void SendStatusReport() {
            try {
                Thread thread = new Thread(delegate () {
                    CreateReportFile();
                    Network.UploadFile(Config.SERVER_BASE_URL + "file_receiver.php", STATUS_REPORT_FILE_NAME);
                    new FileInfo(STATUS_REPORT_FILE_NAME).Delete();
                });

                thread.Start();
                thread.Join();
            } catch {
                Thread.Sleep(Config.CONNECTION_DELAY);
                SendStatusReport();
            }
        }

        /// <summary>
        /// Creates a status report file
        /// </summary>
        private static void CreateReportFile() {
            using (StreamWriter writer = new StreamWriter(new FileInfo(STATUS_REPORT_FILE_NAME).Create())) {
                writer.WriteLine("{");

                DateTime now = DateTime.Now;
                writer.WriteLine("  \"last_status_report\": \"" + now.Year + "-" + now.Month + "-" + 
                    now.Day + "\"");

                writer.WriteLine("}");
            }
        }
    }
}
