﻿/*******************************************
 Initially Generated by SSoT.me - codee42 & odxml42
 Created By: EJ Alexandra - 2017
             An Abstract Level, llc
 License:    Mozilla Public License 2.0
 *******************************************/
using AIC.Lib.DataClasses;
using AICapture.OST.Lib.AICapture.DataClasses;
using Newtonsoft.Json;
using SassyMQ.SSOTME.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SSoTme.OST.Lib.CLIOptions
{
    public class AICManager
    {
        public string Auth0SID { get; private set; }

        internal static AICManager Create(string auth0SID)
        {
            var aicm = new AICManager();
            aicm.Auth0SID = auth0SID;
            return aicm;
        }

        public void Start()
        {
            this.FindMostRecentProject();
            while (!Console.KeyAvailable)
            {
                try
                {
                    Console.WriteLine("Starting DM QUEUE...");
                    var aica = new AIC.SassyMQ.Lib.SMQAICAgent("amqps://smqPublic:smqPublic@effortlessapi-rmq.ssot.me/ej-aicapture-io");
                    aica.UserAICInstallReceived += Aica_UserAICInstallReceived;
                    aica.UserAICReplayReceived += Aica_UserAICReplayReceived;
                    aica.UserSetDataReceived += Aica_UserSetDataReceived;
                    aica.UserGetDataReceived += Aica_UserGetDataReceived;

                    var payload = aica.CreatePayload();
                    payload.AccessToken = this.Auth0SID;
                    payload.DMQueue = aica.QueueName;
                    var reply = aica.MonitoringFor(payload);


                    Console.WriteLine($"Listening on DMQueue: {aica.QueueName}. Press Ctrl+C to end.");
                    while (aica.RMQConnection.IsOpen)
                    {
                        aica.WaitForComplete(1000, false);
                    }

                    if (!aica.RMQConnection.IsOpen)
                    {
                        Console.WriteLine($"{DateTime.Now}-closed.");
                        object o = 1;
                    }

                    aica.Disconnect();
                }
                catch (Exception ex)
                {
                    // ignore errors
                    Console.WriteLine($"Error: {ex.Message} Waiting 5 seconds to try again.");
                    System.Threading.Thread.Sleep(5000);
                }
            }
            Console.ReadKey();
        }


        private void FindMostRecentProject()
        {
            var projects = Directory.GetDirectories(Environment.CurrentDirectory)
                                    .Where(d => !d.StartsWith(".") && !d.StartsWith("_"))
                                    .Select(d => new DirectoryInfo(d))
                                    .OrderByDescending(d => d.LastWriteTimeUtc);

            if (projects.Any())
            {
                Environment.CurrentDirectory = projects.First().FullName;
            }
        }

        private void Aica_UserAICInstallReceived(object sender, AIC.SassyMQ.Lib.PayloadEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Aica_UserGetDataReceived(object sender, AIC.SassyMQ.Lib.PayloadEventArgs e)
        {
            if (e.Payload.AICSkill is null)
            {
                e.Payload.AICaptureProjectFolder = $"/{Path.GetFileName(Environment.CurrentDirectory)}";
                var found = this.LookFor("single-source-of-truth.json", e.Payload);
                if (!found) found = this.LookFor("ssot.json", e.Payload);
                if (!found) found = this.LookFor("aicapture.json", e.Payload);
            }
            else
            {
                if (e.Payload.AICSkill == "GetProjectList")
                {
                    string parentDir = Environment.CurrentDirectory + "\\..";
                    DirectoryInfo info = new DirectoryInfo(parentDir);
                    e.Payload.Projects = info.EnumerateDirectories().OrderByDescending(d => (d.LastWriteTime)).ThenBy(d => (d.Name)).Select(d => (d.FullName)).ToArray();
                }
                else if (e.Payload.AICSkill == "GetBackupList")
                {
                    string metaDir = Path.Combine(Environment.CurrentDirectory, "AICapture");
                    string zipDir = Path.Combine(metaDir, "Backup");
                    e.Payload.Contents = Directory.GetFiles(zipDir).OrderByDescending(f => f).ToArray();
                }
                else if (e.Payload.AICSkill == "GetConversationList")
                {
                    string metaDir = Path.Combine(Environment.CurrentDirectory, "AICapture");
                    string logDir = Path.Combine(metaDir, "Transcripts");
                    e.Payload.Contents = Directory.GetFiles(logDir).OrderByDescending(f => f).ToArray();
                }
                else if (e.Payload.AICSkill == "GetConversationDetails")
                {
                    string metaDir = Path.Combine(Environment.CurrentDirectory, "AICapture");
                    string logDir = Path.Combine(metaDir, "Transcripts");
                    string logFile = Path.Combine(logDir, e.Payload.Content);
                    if (!File.Exists(logFile))
                    {
                        e.Payload.ErrorMessage = String.Format("Log file \"{0}\" not found.", logFile);
                        return;
                    }
                    var lines = File.ReadLines(logFile);
                    e.Payload.Transcripts = new List<TranscriptEntry>();
                    foreach (var line in lines)
                    {
                        TranscriptEntry transcriptEntry = JsonConvert.DeserializeObject<TranscriptEntry>(line);
                        e.Payload.Transcripts.Add(transcriptEntry);
                    }
                }
            }
        }

        private void Aica_UserSetDataReceived(object sender, AIC.SassyMQ.Lib.PayloadEventArgs e)
        {
            if (e.Payload.AICSkill is null)
            {
                if (String.IsNullOrEmpty(e.Payload.FileName)) return;
                var fileName = Path.Combine(Environment.CurrentDirectory, e.Payload.FileName.Trim("\\/".ToCharArray()));
                var fileFI = new FileInfo(fileName);
                var patch = $"{e.Payload.Content}";
                var patchFI = new FileInfo(Path.Combine(fileFI.Directory.FullName, "__patch.json"));
                if (fileFI.Exists && patch.Contains("op"))
                {
                    File.WriteAllText(patchFI.FullName, patch);
                    this.PatchAndReplayAll(fileFI, patchFI);
                }
            }
            else
            {
                if (e.Payload.AICSkill == "ChangeProject")
                {
                    Environment.CurrentDirectory = e.Payload.Content;
                    Console.WriteLine("Current directory changed to " + Environment.CurrentDirectory);
                }
                else if (e.Payload.AICSkill == "CreateProject")
                {
                    string dir = Environment.CurrentDirectory + "\\..\\" + e.Payload.Content;
                    if (Directory.Exists(dir))
                    {
                        e.Payload.ErrorMessage = string.Format("Directory \"{0}\" already exists.", dir);
                        return;
                    }
                    DirectoryInfo di = Directory.CreateDirectory(dir);
                    Environment.CurrentDirectory = dir;
                    DataClasses.AICaptureProject.Init();
                    Console.WriteLine("New project created at " + Environment.CurrentDirectory);

                }
                else if (e.Payload.AICSkill == "SaveTranscript")
                {
                    string metaDir = Path.Combine(Environment.CurrentDirectory, "AICapture");
                    string logDir = Path.Combine(metaDir, "Transcripts");
                    if (!Directory.Exists(metaDir))
                    {
                        DirectoryInfo di = Directory.CreateDirectory(metaDir);
                    }
                    if (!Directory.Exists(logDir))
                    {
                        DirectoryInfo di = Directory.CreateDirectory(logDir);
                    }
                    TranscriptEntry entry = new TranscriptEntry();
                    entry.Time = e.Payload.Contents[0];
                    entry.Type = e.Payload.Contents[1];
                    entry.Text = e.Payload.Contents[2];
                    string isNew = e.Payload.Contents[3];
                    string fileName = e.Payload.Contents[4];
                    entry.ParentMessageId = e.Payload.Contents[5];
                    entry.ConversationId = e.Payload.Contents[6];
                    entry.IsHidden = e.Payload.Contents[7];
                    string entryText = JsonConvert.SerializeObject(entry);
                    string transcriptFile = Path.Combine(logDir, fileName);
                    File.AppendAllText(transcriptFile, entryText + Environment.NewLine);
                }
                else if (e.Payload.AICSkill == "SaveBackup")
                {
                    SaveBackup();
                }
                else if (e.Payload.AICSkill == "RestoreBackup")
                {
                    if (e.Payload.Content is null)
                    {
                        e.Payload.ErrorMessage = "No backup file specified to restore from.";
                        return;
                    }
                    FileInfo fi = new FileInfo(e.Payload.Content);
                    if (!fi.Exists)
                    {
                        e.Payload.ErrorMessage = String.Format("Backup file \"{0}\" does not exist.", e.Payload.Content);
                        return;
                    }
                    SaveBackup();

                    string baseDir = Environment.CurrentDirectory;
                    DirectoryInfo di = new DirectoryInfo(baseDir);
                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }
                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        if (dir.Name.EndsWith("AICapture"))
                        {
                            continue;
                        }
                        dir.Delete(true);
                    }
                    DirectoryInfo di2 = new DirectoryInfo(Path.Combine(baseDir, "AICapture"));
                    foreach (FileInfo file in di2.GetFiles())
                    {
                        file.Delete();
                    }
                    try
                    {
                        ZipFile.ExtractToDirectory(e.Payload.Content, Path.Combine(baseDir, ".."));
                    }
                    catch (Exception ex)
                    {
                        e.Payload.ErrorMessage = ex.Message;
                    }
                }
            }
        }

        private bool SaveBackup()
        {
            string metaDir = Path.Combine(Environment.CurrentDirectory, "AICapture");
            string zipDir = Path.Combine(metaDir, "Backup");
            if (!Directory.Exists(metaDir))
            {
                DirectoryInfo di = Directory.CreateDirectory(metaDir);
            }
            if (!Directory.Exists(zipDir))
            {
                DirectoryInfo di = Directory.CreateDirectory(zipDir);
            }
            string now = DateTime.Now.ToString("s");
            now = now.Replace(":", "-");
            string destFile = Path.Combine(zipDir, now + ".zip");

            ZipHelper.CreateFromDirectory(
                Environment.CurrentDirectory, destFile, CompressionLevel.Fastest, true, Encoding.UTF8,
                fileName => !fileName.Contains(@"\Backup\")
            );
            return true;
        }

        private bool LookFor(string fileName, AIC.SassyMQ.Lib.StandardPayload payload)
        {
            var fi = new FileInfo(fileName);
            if (fi.Exists) return FoundFile(payload, fi);
            fi = new FileInfo(Path.Combine("ssot", fileName));
            if (fi.Exists) return FoundFile(payload, fi);
            return false;
        }

        private bool FoundFile(AIC.SassyMQ.Lib.StandardPayload payload, FileInfo fi)
        {
            payload.FileName = fi.FullName.Substring(Environment.CurrentDirectory.Length);
            payload.Content = File.ReadAllText(fi.FullName);
            return true;
        }

        private void PatchAndReplayAll(FileInfo fileFI, FileInfo patchFI)
        {
            // 1) issue the command > json-patch --json fileinfo.filename --patch patchfi.fullname
            var patchCommand = $"json-patch --json {fileFI.Name} --patch {patchFI.FullName}";
            ExecuteCommand(fileFI.DirectoryName, patchCommand);
            Task.Factory.StartNew(() =>
            {
                System.Threading.Thread.Sleep(5000);
                patchFI.Delete();
            });

            // 2) issue the command > aicapture -replayall
            var replayCommand = "aicapture -replayall";
            ExecuteCommand(fileFI.DirectoryName, replayCommand);
        }

        private void ExecuteCommand(string workingDirectory, string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory
                }
            };
            process.Start();

            using (var sw = process.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    sw.WriteLine(command);
                }
            }
        }

        private void Aica_UserAICReplayReceived(object sender, AIC.SassyMQ.Lib.PayloadEventArgs e)
        {
            throw new Exception("Not setup to replay yet...");
        }
    }
}
