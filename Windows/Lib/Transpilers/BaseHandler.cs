/*******************************************
 Initially Generated by SSoT.me - codee42 & odxml42
 Created By: EJ Alexandra - 2017
             An Abstract Level, llc
 License:    Mozilla Public License 2.0
 *******************************************/
using SSoTme.OST.Lib.DataClasses;
using System;
using System.Linq;
using SassyMQ.SSOTME.Lib.RMQActors;
using SSoTme.OST.Lib.Extensions;
using System.Net;
using System.IO;
using SassyMQ.Lib.RabbitMQ;
using System.Xml;
using System.Text;
using System.Collections.Generic;
using GotDotNet.Exslt;
using System.Xml.Xsl;
using SSoTme.OST.Lib.Transpilers;
using System.Text.RegularExpressions;

namespace SSoTme.OST.Transpilers
{
    public abstract class BaseHandler
    {
        public BaseHandler(Transpiler sourceTranspiler, SSOTMEPayload payload)
        {
            this.Payload = payload;
            this.ScreenName = payload.ScreenName;

            this.Parameters = new Dictionary<string, string>();
            this.OutputFileSet = new FileSet();
            this.OutputFileSet.FileSetId = sourceTranspiler.TranspilerId;
            this.SourceTranspiler = sourceTranspiler;
            this.RootDirInfo = new DirectoryInfo(Path.Combine("C:\\temp\\", Guid.NewGuid().ToString()));
            this.RootDirInfo.Create();

            if (String.IsNullOrEmpty(this.Payload.CLIInputFileSetXml) && !String.IsNullOrEmpty(this.Payload.CLIInputFileSetJson))
            {
                this.Payload.CLIInputFileSetXml = this.Payload.CLIInputFileSetJson.JsonToXml().OuterXml;
            }

            if (String.IsNullOrEmpty(this.Payload.CLIInputFileSetXml)) this.InputFileSet = new FileSet();
            else this.InputFileSet = this.Payload.CLIInputFileSetXml.ToFileSet();

            if (!ReferenceEquals(this.Payload.CLIInput, null) && this.Payload.CLIInput.Any())
            {
                this.InputFileName = this.Payload.CLIInput.First();
            }
            else this.InputFileName = "";

            this.OutputFileName = this.Payload.CLIOutput;


            if (String.IsNullOrEmpty(this.OutputFileName))
            {
                this.OutputFileName = this.GetParameterByName("OutputFileName");
                if (String.IsNullOrEmpty(this.OutputFileName))
                {
                    this.OutputFileName = "output.txt";
                }
            }

            this.Parameters = this.PopulateParameters();

        }

        public string GetFileName(string defaultFileName)
        {
            if (String.Equals(this.OutputFileName, "output.txt", StringComparison.OrdinalIgnoreCase))
            {
                this.OutputFileName = defaultFileName;
                return defaultFileName;
            }
            else return this.OutputFileName;
        }

        public byte[] GetSingleBinaryFileContents()
        {
            if (this.InputFileSet.FileSetFiles.Any())
            {
                return this.InputFileSet.FileSetFiles[0].GetFileSetFileBinaryContents();
            }
            else return new Byte[] { };

        }

        public SimpleHtmlPage DownloadGsheetAsCsv(string url, bool trimAfterHyphenInName = true)
        {
            url = url.SafeToString();
            SimpleHtmlPage page = new SimpleHtmlPage();
            var wc = new WebClient();

            if (url.Contains("/edit#gid=")) url = url.Replace("/edit#gid=", "/export?format=csv&gid=");
            page.HtmlContent = wc.DownloadString(url);

            var contentDisposition = wc.ResponseHeaders["content-disposition"].SafeToString();
            var matches = Regex.Match(contentDisposition, "filename=\\\"([a-z,A-Z,0-9,-]*).*");
            page.Title = matches.Groups[1].Value;

            if (page.Title.Contains("-")) page.Title = page.Title.Substring(page.Title.IndexOf("-") + 1);

            return page;
        }

        public byte[] DownloadGsheetAsXlsx(string url)
        {
            url = url.SafeToString();
            var wc = new WebClient();

            if (url.Contains("/edit#gid=")) url = url.Replace("/edit#gid=", "/export?format=xlsx&gid=");
            else if (url.Contains("/edit")) url = url.Replace("/edit", "/export?format=xlsx");
            var bytes = wc.DownloadData(url);

            return bytes;
        }

        public SimpleHtmlPage DownloadGDocAsTextFile(string url, bool trimAfterHyphenInName = true)
        {
            url = url.SafeToString();
            SimpleHtmlPage page = new SimpleHtmlPage();
            var wc = new WebClient();

            if (url.Contains("/edit")) url = url.Substring(0, url.IndexOf("/edit")) + "/export?format=txt";

            page.HtmlContent = wc.DownloadString(url);

            var contentDisposition = wc.ResponseHeaders["content-disposition"].SafeToString();
            var matches = Regex.Match(contentDisposition, "filename=\\\"([a-z,A-Z,0-9,-]*).*");
            page.Title = matches.Groups[1].Value;

            if (page.Title.Contains("-")) page.Title = page.Title.Substring(page.Title.IndexOf("-") + 1);

            return page;
        }


        public FileSet ProcessXslt(string xml, string xslt)
        {
            this.Xml = xml;
            this.Xslt = xslt;
            return this.ProcessXslt();
        }

        public void AddResourceFolderToOutput(string resourceFolderPartialName, bool alwaysOvewrite = true)
        {
            var matchingResourceNames = this.GetType()
                                            .Assembly
                                            .GetManifestResourceNames()
                                            .Where(whereRN => whereRN.IndexOf(resourceFolderPartialName, StringComparison.OrdinalIgnoreCase) != -1);
            foreach (var matchingResourceName in matchingResourceNames)
            {
                var basePath = matchingResourceName.Substring(0, matchingResourceName.IndexOf(resourceFolderPartialName, StringComparison.OrdinalIgnoreCase) + resourceFolderPartialName.Length + 1);
                this.AddResourceToOutput(basePath, matchingResourceName, alwaysOvewrite);
            }
        }

        private void AddResourceToOutput(String basePath, string matchingResourceName, bool alwaysOvewrite)
        {
            FileSetFile fsf = FileSetFileFromResource(basePath, matchingResourceName, alwaysOvewrite);
            this.OutputFileSet.FileSetFiles.Add(fsf);
        }

        private FileSetFile FileSetFileFromResource(string basePath, string matchingResourceName, bool alwaysOvewrite)
        {
            var resourceStream = this.GetType()
                                     .Assembly
                                     .GetManifestResourceStream(matchingResourceName);
            var memoryStream = new MemoryStream();
            resourceStream.CopyTo(memoryStream);

            var dotPath = matchingResourceName.Substring(basePath.Length);
            while (dotPath.Count(countChar => countChar == '.') > 1)
            {
                dotPath = dotPath.Substring(0, dotPath.IndexOf(".")) + "/" + dotPath.Substring(dotPath.IndexOf(".") + 1);
            }

            FileSetFile fsf = new FileSetFile();
            fsf.RelativePath = dotPath;
            fsf.BinaryFileContents = memoryStream.ToArray();
            fsf.AlwaysOverwrite = alwaysOvewrite;
            return fsf;
        }

        public void AddFileSetToOutput(FileSet transpilersFileSet)
        {
            foreach (var fileSetFile in transpilersFileSet.FileSetFiles)
            {
                this.OutputFileSet.FileSetFiles.Add(fileSetFile);
            }
        }


        public void AddOutputFile(string output, bool alwaysOverwrite = true)
        {
            this.AddOutputFile(this.OutputFileName, output, alwaysOverwrite);
        }

        public string GetParameterByName(string parameterName)
        {
            if (this.Parameters.Keys.Any(anyKey => anyKey.Equals(parameterName, StringComparison.OrdinalIgnoreCase)))
            {
                return this.Parameters[parameterName];
            }
            else return String.Empty;
        }

        public void AddOutputFile(string relativePath, byte[] fileContents, bool alwaysOverwrite = true)
        {
            var fsf = new FileSetFile();
            fsf.RelativePath = relativePath;
            fsf.ZippedBinaryFileContents = fileContents.Zip();
            fsf.AlwaysOverwrite = alwaysOverwrite;
            this.OutputFileSet.FileSetFiles.Add(fsf);
        }

        public void AddOutputFile(string relativePath, string fileContents, bool alwaysOverwrite = true)
        {
            var fsf = new FileSetFile();
            fsf.RelativePath = relativePath;
            fsf.FileContents = fileContents;
            fsf.AlwaysOverwrite = alwaysOverwrite;
            this.OutputFileSet.FileSetFiles.Add(fsf);
        }

        public void WriteInputToRoot()
        {
            this.InputFileSet.WriteTo(this.RootDirInfo);
        }

        public void WriteToRoot(string fileName, string textContents)
        {
            var fs = new FileSet();
            var fsf = new FileSetFile();
            fsf.RelativePath = fileName;
            fsf.FileContents = textContents;
            fs.FileSetFiles.Add(fsf);
            fs.WriteTo(this.RootDirInfo);
        }

        public void WriteToRoot(string fileName, byte[] bytes)
        {
            var fs = new FileSet();
            var fsf = new FileSetFile();
            fsf.RelativePath = fileName;
            fsf.BinaryFileContents = bytes;
            fs.FileSetFiles.Add(fsf);
            fs.WriteTo(this.RootDirInfo);
        }

        public void WriteResourceToRoot(string resourceName)
        {
            var matchingResourceNames = this.GetType()
                                .Assembly
                                .GetManifestResourceNames()
                                .Where(whereRN => whereRN.IndexOf(resourceName, StringComparison.OrdinalIgnoreCase) != -1);
            foreach (var matchingResourceName in matchingResourceNames)
            {
                var basePath = matchingResourceName.Substring(0, matchingResourceName.IndexOf(resourceName, StringComparison.OrdinalIgnoreCase));
                this.WriteResourceToRoot(basePath, matchingResourceName);
            }

        }

        private void WriteResourceToRoot(string basePath, string matchingResourceName)
        {
            var fs = new FileSet();
            var fsf = this.FileSetFileFromResource(basePath, matchingResourceName, true);
            fs.FileSetFiles.Add(fsf);
            fs.WriteTo(this.RootDirInfo);
        }

        private Dictionary<string, string> PopulateParameters()
        {
            var dict = this.Parameters;
            if (!ReferenceEquals(this.Payload.CLIParams, null))
            {

                foreach (var cliParameter in this.Payload.CLIParams)
                {
                    if (cliParameter.SafeToString().Contains("="))
                    {
                        var key = cliParameter.Substring(0, cliParameter.IndexOf("="));
                        var value = cliParameter.Substring(cliParameter.IndexOf("=") + 1);
                        dict.Add(key, value);
                    }
                    else dict.Add(cliParameter, String.Empty);
                }
            }

            dict["input-filename"] = this.InputFileName;
            dict["output-filename"] = this.OutputFileName;

            return dict;
        }


        public Transpiler SourceTranspiler;
        private DirectoryInfo _rootDirInfo;
        public DirectoryInfo RootDirInfo
        {
            get
            {
                if (!_rootDirInfo.Exists) _rootDirInfo.Create();
                return _rootDirInfo;
            }
            private set { _rootDirInfo = value; }
        }
        public Dictionary<string, string> Parameters { get; private set; }

        public void LoadXmlFromFileInfo(FileInfo inputFI)
        {
            // Find the first argument
            if (!inputFI.Exists) throw new Exception(String.Format("Can't load XML File: '{0}'", inputFI.FullName));
            else this.Xml = File.ReadAllText(inputFI.FullName);
        }

        public void AddParameter(string parameterName, string parameterValue)
        {
            this.Parameters[parameterName] = parameterValue;
        }

        public void WriteOutputToRoot()
        {
            this.OutputFileSet.WriteTo(this.RootDirInfo);
        }

        public string ReadRootFile(string fileName)
        {
            var fileToRead = new FileInfo(Path.Combine(this.RootDirInfo.FullName, fileName));
            if (!fileToRead.Exists) return String.Empty;
            else return File.ReadAllText(fileToRead.FullName);
        }

        public void LoadInputFileSetFile(FileSetFile inputFileSetFile)
        {
            var finalPath = Path.Combine(this.RootDirInfo.FullName, Path.GetFileName(inputFileSetFile.RelativePath));
            File.WriteAllText(finalPath, inputFileSetFile.GetFileSetFileContents());
        }

        public void LoadXsltFromPartialResourceName(string xsltPartialName)
        {
            xsltPartialName = xsltPartialName.ToLower();
            String resourceName = this.GetType().Assembly.GetManifestResourceNames().FirstOrDefault(fodResourceName => fodResourceName.ToLower().Contains(xsltPartialName));
            if (String.IsNullOrEmpty(resourceName)) throw new Exception(String.Format("Can't match resource name: {0}", xsltPartialName));
            else this.Xslt = new StreamReader(this.GetType().Assembly.GetManifestResourceStream(resourceName)).ReadToEnd();
        }

        public void LoadXsltFromResourceFile(string resourceName)
        {
            this.Xslt = new StreamReader(this.GetType().Assembly.GetManifestResourceStream(resourceName)).ReadToEnd();
        }

        public FileSet ProcessXsltFromPartialResourceName(String xml, string xsltPartialName)
        {
            this.Xml = xml;
            this.LoadXsltFromPartialResourceName(xsltPartialName);
            return this.ProcessXslt();
        }

        public FileSet ProcessXslt()
        {
            var xslt = this.Xslt;
            var xml = this.Xml;
            var rootPath = this.RootDirInfo.FullName;
            if (String.IsNullOrEmpty(this.Xslt)) throw new Exception("Must load xslt before calling Execute()");
            else if (String.IsNullOrEmpty(this.Xml)) throw new Exception("No xml loaded for xslt to process");
            else
            {
                Environment.CurrentDirectory = rootPath;

                ExsltTransform t = PrepareTransformationObject(rootPath, this.Xslt);

                XsltArgumentList al = new XsltArgumentList();
                al.AddParam("dish-root", String.Empty, rootPath);
                al.AddParam("codee-root", String.Empty, rootPath);
                al.AddParam("xml-root", String.Empty, rootPath);
                al.AddParam("xslt-root", String.Empty, rootPath);

                foreach (var dict in this.Parameters)
                {
                    al.AddParam(dict.Key, String.Empty, dict.Value.SafeToString());
                }

                String inputXml = this.Xml;

                String newFileContents = String.Empty;

                XmlDocument doc = new XmlDocument();
                inputXml = inputXml.Trim();
                doc.LoadXml(inputXml);
                MemoryStream ms = new MemoryStream();
                try
                {
                    t.Transform(doc.CreateNavigator(), al, ms);

                    ms.Position = 0;

                    String currentDoc = (new UTF8Encoding(false)).GetString(ms.GetBuffer(), 0, (int)ms.Length);
                    if (currentDoc.StartsWith("<?xml")) currentDoc = currentDoc.Substring(currentDoc.IndexOf("?>") + 2);
                    currentDoc = currentDoc.Trim((char)65279);
                    if (currentDoc.Contains("<"))
                    {
                        currentDoc = currentDoc.Substring(currentDoc.IndexOf("<"));
                    }
                    if (!String.IsNullOrEmpty(currentDoc))
                    {
                        try
                        {
                            doc = new XmlDocument();
                            doc.LoadXml(currentDoc);
                            MemoryStream wms = new MemoryStream();
                            XmlTextWriter writer = new XmlTextWriter(wms, (new UTF8Encoding(false)));
                            writer.Formatting = Formatting.Indented;
                            doc.WriteContentTo(writer);
                            writer.Flush();
                            newFileContents = (new UTF8Encoding(false)).GetString(wms.GetBuffer(), 0, (int)writer.BaseStream.Length).Trim();
                            wms.Close();
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                    }
                }
                finally
                {
                    ms.Close();
                }

                var fileSet = newFileContents.ToFileSet();
                this.AddFileSetToOutput(fileSet);
                this.Xslt = String.Empty;
                this.Xml = String.Empty;

                return fileSet;
            }
        }

        public ExsltTransform PrepareTransformationObject(String rootPath, string xsltText)
        {
            ExsltTransform xslt = new ExsltTransform();
            MemoryStream ms = new MemoryStream((new UTF8Encoding(false)).GetBytes(xsltText));
            try
            {
                XmlTextReader xmltr = new XmlTextReader(ms);
                xmltr.WhitespaceHandling = WhitespaceHandling.Significant;
                xslt.Load(xmltr, new MyResolver(rootPath));
            }
            finally
            {
                ms.Close();
            }
            return xslt;
        }

        private class MyResolver : XmlResolver
        {
            public MyResolver(String rootPath)
            {
                this.RootPath = rootPath;
            }

            public override System.Net.ICredentials Credentials
            {
                set { throw new NotImplementedException(); }
            }

            public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
            {
                var fi = new FileInfo(this.RootPath.FindClosest(absoluteUri.LocalPath));
                if (fi.Exists) return new FileStream(fi.FullName, FileMode.Open);
                else
                {
                    var contents = this.GetType().Assembly.GetTextResourceContents(fi.Name);
                    if (!String.IsNullOrEmpty(contents))
                    {
                        return new MemoryStream(Encoding.UTF8.GetBytes(contents));
                    }
                    else throw new Exception("Can't find resource: " + fi.Name);
                }
            }

            public string RootPath { get; set; }
        }

        public String GetFirstTextFileSetFile()
        {
            if (!this.InputFileSet.FileSetFiles.Any()) throw new Exception("No input files provided to transpiler.");
            else return this.InputFileSet.FileSetFiles.First().GetFileSetFileContents();
        }

        public string DownloadFirstParamAsUrl()
        {
            var contents = this.GetSingleTextFileContents();

            if (String.IsNullOrEmpty(contents))
            {
                var url = this.Payload.GetParameterByName("param1");
                if (url.StartsWith("http"))
                {
                    contents = new WebClient().DownloadString(url);
                }
            }
            return contents;
        }

        public string GetParameter(int parameterIndex)
        {
            var parameterValue = this.Payload
                                     .CLIParams
                                     .Skip(parameterIndex)
                                     .FirstOrDefault()
                                     .SafeToString();
            if (parameterValue.Contains("="))
            {
                parameterValue = parameterValue.Substring(parameterValue.IndexOf("=") + 1);
            }

            return parameterValue;
        }
        private SSOTMEPayload Payload { get; set; }
        public string Xml { get; set; }
        public string Xslt { get; set; }
        public FileSet InputFileSet { get; private set; }
        public FileSet OutputFileSet { get; private set; }
        public string InputFileName { get; private set; }
        private string _outputFileName;
        public string OutputFileName
        {
            get { return _outputFileName; }
            set
            {
                _outputFileName = value;
                this.AddParameter("output-filename", value);
            }
        }
        public string ScreenName { get; private set; }

        public void TranspileWithXslt(string inputXml, string xsltPartialName)
        {
            // Transpile a thing
            this.LoadXsltFromPartialResourceName(xsltPartialName);
            this.Xml = inputXml;
            this.ProcessXslt();
        }



        public FileSetFile GetFileSetFileByExtension(string fileExtension, bool isOptionalFile = true)
        {

            var firstMatch = this.InputFileSet.FileSetFiles.FirstOrDefault(fodFile => String.Equals(Path.GetExtension(fodFile.RelativePath), fileExtension, StringComparison.OrdinalIgnoreCase));
            if (ReferenceEquals(firstMatch, null))
            {
                if (isOptionalFile) return default(FileSetFile);
                else throw new Exception(String.Format("Can't find a file with the extension {0}", fileExtension));
            }
            else return firstMatch;
        }

        public string InputFilePlus(string additionalExtension)
        {
            if (ReferenceEquals(this.Payload.CLIInput, null))
            {
                this.Payload.CLIInput = new List<string>();
                if (!ReferenceEquals(this.InputFileSet, null) && this.InputFileSet.FileSetFiles.Any())
                {
                    this.Payload.CLIInput.Add(this.InputFileSet.FileSetFiles.First().RelativePath);
                }
                else this.Payload.CLIInput.Add("input.txt");
            }

            var fileName = this.Payload.CLIInput.FirstOrDefault();
            if (String.IsNullOrEmpty(fileName))
            {
                var fsf = this.InputFileSet.FileSetFiles.FirstOrDefault();
                if (!ReferenceEquals(fsf, null))
                {
                    fileName = Path.GetFileName(fsf.RelativePath);
                }
            }
            if (String.IsNullOrEmpty(fileName)) throw new Exception("Input file missing - can't calculate output filename");
            else return String.Format("{0}{1}", fileName, additionalExtension);
        }

        public abstract void Transpile();

        public string FirstString()
        {
            string firstString = this.GetSingleTextFileContents();
            if (String.IsNullOrEmpty(firstString))
            {
                firstString = this.GetParameterByName("param1");
                if (String.IsNullOrEmpty(firstString))
                {
                    firstString = this.Payload.CLIParams.FirstOrDefault().StripParamNumber();
                }
            }
            return firstString;
        }

        public string GetSingleTextFileContents()
        {
            if (this.InputFileSet.FileSetFiles.Any())
            {
                return this.InputFileSet.FileSetFiles[0].GetFileSetFileContents();
            }
            else return String.Empty;
        }

        public Uri GetFirstUri()
        {
            string url = this.GetSingleTextFileContents();
            if (String.IsNullOrEmpty(url))
            {
                url = this.Parameters
                          .Values
                          .FirstOrDefault(fod => fod.StartsWith("http"));

            }
            if (String.IsNullOrEmpty(url))
            {
                url = this.Parameters
                          .Keys
                          .FirstOrDefault(fod => fod.StartsWith("http"));
                var value = this.Parameters[url];
                if (!String.IsNullOrEmpty(value)) url = String.Format("{0}={1}", url, value);
            }
            var uri = default(Uri);
            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri))
            {
            }
            return uri;

        }
    }
}

