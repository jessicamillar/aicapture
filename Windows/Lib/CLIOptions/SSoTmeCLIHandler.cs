﻿/*******************************************
 Initially Generated by SSoT.me - codee42 & odxml42
 Created By: EJ Alexandra - 2017
             An Abstract Level, llc
 License:    Mozilla Public License 2.0
 *******************************************/
using Plossum.CommandLine;
using SassyMQ.Lib.RabbitMQ;
using SassyMQ.SSOTME.Lib.RabbitMQ;
using SassyMQ.SSOTME.Lib.RMQActors;
using SSoTme.OST.Lib.DataClasses;
using SSoTme.OST.Lib.Extensions;
using SSoTme.OST.Lib.SassySDK.Derived;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SSoTme.OST.Lib.CLIOptions
{


    public partial class SSoTmeCLIHandler
    {
        private SSOTMEPayload result;

        public SMQAccountHolder AccountHolder { get; private set; }
        public DMProxy CoordinatorProxy { get; private set; }


        private SSoTmeCLIHandler()
        {
            this.account = "";
            this.waitTimeout = 30000;
            this.input = new List<string>();
            this.parameters = new List<string>();
            this.addSetting = new List<string>();
            this.removeSetting = new List<string>();
        }

        public static SSoTmeCLIHandler CreateHandler(string commandLine)
        {
            var cliOptions = new SSoTmeCLIHandler();
            cliOptions.commandLine = commandLine;
            cliOptions.ParseCommand();
            return cliOptions;
        }


        public static SSoTmeCLIHandler CreateHandler(string[] args)
        {
            var cliOptions = new SSoTmeCLIHandler();
            cliOptions.args = args;
            cliOptions.ParseCommand();
            return cliOptions;
        }


        private void ParseCommand()
        {
            try
            {

                CommandLineParser parser = new CommandLineParser(this);
                if (!String.IsNullOrEmpty(this.commandLine)) parser.Parse(this.commandLine, false);
                else parser.Parse(this.args, false);

                this.HasRemainingArguments = parser.RemainingArguments.Any();

                if (String.IsNullOrEmpty(this.transpiler))
                {
                    this.transpiler = parser.RemainingArguments.FirstOrDefault().SafeToString();
                    if (this.transpiler.Contains("/"))
                    {
                        this.account = this.transpiler.Substring(0, this.transpiler.IndexOf("/"));
                        this.transpiler = this.transpiler.Substring(this.transpiler.IndexOf("/") + 1);
                    }
                }

                var additionalArgs = parser.RemainingArguments.Skip(1).ToList();
                for (var i = 0; i < additionalArgs.Count; i++)
                {
                    this.parameters.Add(String.Format("param{0}={1}", i + 1, additionalArgs[i]));
                }


                if (this.help)
                {

                    Console.WriteLine(parser.UsageInfo.GetHeaderAsString(78));

                    Console.WriteLine("\n\nSyntax: ssotme [account/]transpiler [Options]\n\n");

                    Console.WriteLine(parser.UsageInfo.GetOptionsAsString(78));
                    this.SuppressTranspile = true;
                }
                else if (this.init)
                {
                    this.SuppressTranspile = true;
                    
                    var force = this.args.Count() == 2 &&
                                this.args[1] == "force";

                    SSoTmeProject.Init(force);
                }
                else if (parser.HasErrors)
                {
                    var curColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(parser.UsageInfo.GetErrorsAsString(78));
                    this.ParseResult = -1;
                    Console.ForegroundColor = curColor;
                    this.SuppressTranspile = true;
                }
                else
                {
                    this.SSoTmeProject = SSoTmeProject.LoadOrFail(new DirectoryInfo(Environment.CurrentDirectory));

                    this.LoadInputFiles();

                    if (!ReferenceEquals(this.FileSet, null))
                    {
                        this.ZFSFileSetFile = this.FileSet.FileSetFiles.FirstOrDefault(fodFileSetFile => fodFileSetFile.RelativePath.EndsWith(".zfs", StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
            catch (Exception ex)
            {
                var curColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;

                Console.WriteLine("\n********************************\nERROR: {0}\n********************************\n\n", ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("\n\nPress any key to continue...\n");
                Console.WriteLine("\n\n");
                Console.ForegroundColor = curColor;

                Console.ReadKey();

            }
        }

        public int TranspilerProject(ProjectTranspiler projectTranspiler = null)
        {
            bool updateProject = false;
            try
            {
                var hasRemainingArguments = this.HasRemainingArguments;
                var zfsFileSetFile = this.ZFSFileSetFile;
                if (this.describe)
                {
                    this.SSoTmeProject.Describe(Environment.CurrentDirectory);
                }
                else if (this.descibeAll)
                {
                    this.SSoTmeProject.Describe();
                }
                else if (this.listSettings)
                {
                    this.SSoTmeProject.ListSettings();
                }
                else if (this.addSetting.Any())
                {
                    foreach (var setting in this.addSetting)
                    {
                        this.SSoTmeProject.AddSetting(setting);
                    }
                    this.SSoTmeProject.Save();
                }
                else if (this.removeSetting.Any())
                {
                    foreach (var setting in this.removeSetting)
                    {
                        this.SSoTmeProject.RemoveSetting(setting);
                    }
                    this.SSoTmeProject.Save();
                }
                else if (this.build)
                {
                    this.SSoTmeProject.Rebuild(Environment.CurrentDirectory, this.includeDisabled);
                    if (this.checkResults) this.SSoTmeProject.CreateDocs();
                }
                else if (this.buildAll)
                {
                    this.SSoTmeProject.Rebuild(this.includeDisabled);
                    this.SSoTmeProject.CreateDocs();

                }
                else if (this.checkResults || this.createDocs && !hasRemainingArguments)
                {
                    if (this.checkResults) this.SSoTmeProject.CheckResults();
                    else this.SSoTmeProject.CreateDocs();
                    updateProject = true;
                }
                else if (this.clean && !ReferenceEquals(zfsFileSetFile, null))
                {
                    var zfsFI = new FileInfo(zfsFileSetFile.RelativePath);
                    if (zfsFI.Exists)
                    {
                        var zippedFileSet = File.ReadAllBytes(zfsFI.FullName);
                        zippedFileSet.CleanZippedFileSet();
                        if (!this.preserveZFS) zfsFI.Delete();
                    }
                }
                else if (this.clean && !hasRemainingArguments)
                {
                    this.SSoTmeProject.Clean(Environment.CurrentDirectory, this.preserveZFS);
                }
                else if (this.cleanAll && !hasRemainingArguments)
                {
                    this.SSoTmeProject.Clean(this.preserveZFS);
                }
                else if (!hasRemainingArguments && !this.clean)
                {
                    var curColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Missing argument name of transpiler");
                    Console.ForegroundColor = curColor;
                    return -1;
                }
                else
                {
                    StartTranspile();

                    if (!ReferenceEquals(result.Exception, null))
                    {
                        var curColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("ERROR: " + result.Exception.Message);
                        Console.WriteLine(result.Exception.StackTrace);
                        Console.ForegroundColor = curColor;
                        return -1;
                    }
                    else
                    {
                        var finalResult = 0;

                        if (!ReferenceEquals(result.Transpiler, null))
                        {
                            Console.WriteLine("\n\nTRANSPILER MATCHED: {0}\n\n", result.Transpiler.Name);
                        }

                        if (this.clean) result.CleanFileSet();
                        else
                        {
                            finalResult = result.SaveFileSet(this.skipClean);
                            updateProject = true;
                        }
                        return finalResult;

                    }
                }

                return 0;
            }
            finally
            {
                if (!ReferenceEquals(AccountHolder, null)) AccountHolder.Disconnect();
                if (updateProject)
                {
                    if (this.install) this.SSoTmeProject.Install(result);
                    else if (!ReferenceEquals(projectTranspiler, null))
                    {
                        this.SSoTmeProject.Update(projectTranspiler, result);
                    }
                }
            }
        }

        internal void LoadOutputFiles(String lowerHyphoneName, String basePath, bool includeContents)
        {
            var zfsFileName = String.Format("{0}/.ssotme/{1}{2}.zfs", this.SSoTmeProject.RootPath, basePath, lowerHyphoneName);
            var zfsFI = new FileInfo(zfsFileName);
            if (zfsFI.Exists)
            {
                var fileSetXml = File.ReadAllBytes(zfsFI.FullName).UnzipToString();
                var fs = fileSetXml.ToFileSet();
                foreach (var fsf in fs.FileSetFiles)
                {
                    var relativePath = fsf.RelativePath.Trim("\\/\r\n\t ".ToCharArray());
                    fsf.OriginalRelativePath = Path.Combine(basePath, relativePath).Replace("\\", "/");
                    if (!includeContents) fsf.ClearContents();
                }
                this.OutputFileSet = fs;
            }
        }

        private void StartTranspile()
        {
            AccountHolder = new SMQAccountHolder();
            var currentSSoTmeKey = SSOTMEKey.GetSSoTmeKey(this.runAs);
            result = null;

            AccountHolder.ReplyTo += AccountHolder_ReplyTo;
            AccountHolder.Init(currentSSoTmeKey.EmailAddress, currentSSoTmeKey.Secret);


            var waitForCook = Task.Factory.StartNew(() =>
            {
                while (ReferenceEquals(result, null)) Thread.Sleep(100);
            });

            waitForCook.Wait(this.waitTimeout);

            if (ReferenceEquals(result, null))
            {
                result = AccountHolder.CreatePayload();
                result.Exception = new TimeoutException("Timed out waiting for cook");
            }
            result.SSoTmeProject = this.SSoTmeProject;
        }

        public string inputFileContents = "";
        public string transpiler = "";
        public string inputFileSetXml;
        public string[] args;
        public string commandLine;

        public FileSet FileSet { get; private set; }
        public bool HasRemainingArguments { get; private set; }
        public FileSetFile ZFSFileSetFile { get; private set; }
        public SSoTmeProject SSoTmeProject { get; set; }
        public int ParseResult { get; private set; }
        public FileSet OutputFileSet { get; private set; }
        public bool SuppressTranspile { get; private set; }

        public void LoadInputFiles()
        {
            var fs = new FileSet();
            if (!ReferenceEquals(this.input, null) && this.input.Any())
            {
                foreach (var input in this.input)
                {
                    if (!String.IsNullOrEmpty(input))
                    {
                        var inputFilePatterns = input.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        foreach (var filePattern in inputFilePatterns)
                        {
                            this.ImportFile(filePattern, fs);
                        }

                        if (fs.FileSetFiles.Any()) this.inputFileContents = fs.FileSetFiles.First().FileContents;

                    }
                }
            }
            this.inputFileSetXml = fs.ToXml();
            this.FileSet = fs;
        }

        private void ImportFile(string filePattern, FileSet fs)
        {
            var fileNameReplacement = String.Empty;
            if (filePattern.Contains("="))
            {
                fileNameReplacement = filePattern.Substring(0, filePattern.IndexOf("="));
                filePattern = filePattern.Substring(filePattern.IndexOf("=") + 1);
            }
            var di = new DirectoryInfo(Path.Combine(".", Path.GetDirectoryName(filePattern)));
            filePattern = Path.GetFileName(filePattern);

            var matchingFiles = new FileInfo[] { };
            if (di.Exists)
            {
                matchingFiles = di.GetFiles(filePattern);
            }
            if (!matchingFiles.Any())
            {
                var curColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n\nERROR:\n\n - No INPUT files matched {0} in {1}\n", filePattern, di.FullName);
                var fsf = new FileSetFile();
                fsf.RelativePath = Path.GetFileName(filePattern);
                fs.FileSetFiles.Add(fsf);

                Console.ForegroundColor = curColor;

            }

            foreach (var fi in matchingFiles)
            {
                var fsf = new FileSetFile();
                fsf.RelativePath = String.IsNullOrEmpty(fileNameReplacement) ? fi.Name : fileNameReplacement;
                fsf.OriginalRelativePath = fi.FullName.Substring(this.SSoTmeProject.RootPath.Length).Replace("\\", "/");
                fs.FileSetFiles.Add(fsf);

                if (fi.Exists)
                {
                    if (fi.IsBinaryFile())
                    {
                        fsf.ZippedBinaryFileContents = File.ReadAllBytes(fi.FullName).Zip();
                    }
                    else
                    {
                        fsf.ZippedFileContents = File.ReadAllText(fi.FullName).Zip();
                    }
                }
                else
                {
                    var curColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("INPUT Format: {0} did not match any files in {1}", filePattern, di.FullName);
                    Console.ForegroundColor = curColor;
                }
            }
        }



        private void AccountHolder_ReplyTo(object sender, SassyMQ.Lib.RabbitMQ.PayloadEventArgs<SSOTMEPayload> e)
        {
            if (e.Payload.IsLexiconTerm(LexiconTermEnum.accountholder_ping_ssotmecoordinator))
            {
                CoordinatorProxy = new DMProxy(e.Payload.DirectMessageQueue);
                Console.WriteLine("Got ping response");
                var payload = AccountHolder.CreatePayload();
                payload.SaveCLIOptions(this);
                payload.TranspileRequest = new TranspileRequest();
                payload.TranspileRequest.ZippedInputFileSet = this.inputFileSetXml.Zip();
                payload.CLIInputFileContents = String.Empty;
                AccountHolder.AccountHolderCommandLineTranspile(payload, CoordinatorProxy);
            }
            else if (e.Payload.IsLexiconTerm(LexiconTermEnum.accountholder_commandlinetranspile_ssotmecoordinator) ||
                    (e.Payload.IsLexiconTerm(LexiconTermEnum.accountholder_requesttranspile_ssotmecoordinator)))
            {
                result = e.Payload;
            }
        }
    }
}
