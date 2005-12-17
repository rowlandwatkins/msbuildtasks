#region Copyright � 2005 Paul Welter. All rights reserved.
/*
Copyright � 2005 Paul Welter. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

1. Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.
3. The name of the author may not be used to endorse or promote products
   derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR "AS IS" AND ANY EXPRESS OR
IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE. 
*/
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Win32;

namespace MSBuild.Community.Tasks
{
    /// <summary>
    /// Uses FxCop to analyse managed code assemblies and reports on
    /// their design best-practice compliance.
    /// </summary>
    /// <example>
    /// <para>Shows how to analyse an assembly and use an XSLT stylesheet 
    /// to present the report as an HTML file. If the static anlysis fails,
    /// the build does not stop - this is controlled with the <c>FailOnError</c>
    /// parameter.</para>
    /// <code><![CDATA[<FxCop 
    ///   TargetAssemblies="$(MSBuildCommunityTasksPath)\MSBuild.Community.Tasks.dll"
    ///   RuleLibraries="@(FxCopRuleAssemblies)" 
    ///   AnalysisReportFileName="Test.html"
    ///   DependencyDirectories="$(MSBuildCommunityTasksPath)"
    ///   FailOnError="False"
    ///   ApplyOutXsl="True"
    ///   OutputXslFileName="C:\Program Files\Microsoft FxCop 1.32\Xml\FxCopReport.xsl"
    /// />
    /// ]]></code>
    /// </example>
    /// <remarks>If you include the <c>MSBuild.Community.Tasks.Targets</c> file 
    /// in you build project, the ItemGroup <c>@(FxCopRuleAssemblies)</c> is defined
    /// with the standard FxCop Rules Assemblies.</remarks>
    public class FxCop : ToolTask
    {
        #region Properties
        private bool _applyOutXsl;

        /// <summary>
        /// Applies the XSL transformation specified in /outXsl to the 
        /// analysis report before saving the file.
        /// </summary>
        public bool ApplyOutXsl
        {
            get { return _applyOutXsl; }
            set { _applyOutXsl = value; }
        }

        private bool _directOutputToConsole;

        /// <summary>
        /// Directs analysis output to the console or to the 
        /// Output window in Visual Studio .NET. By default, 
        /// the XSL file FxCopConsoleOutput.xsl is applied to the 
        /// output before it is displayed.
        /// </summary>
        public bool DirectOutputToConsole
        {
            get { return _directOutputToConsole; }
            set { _directOutputToConsole = value; }
        }

        private ITaskItem[] _dependencyDirectories;

        /// <summary>
        /// Specifies additional directories to search for assembly dependencies. 
        /// FxCopCmd always searches the target assembly directory and the current 
        /// working directory.
        /// </summary>
        public ITaskItem[] DependencyDirectories
        {
            get { return _dependencyDirectories; }
            set { _dependencyDirectories = value; }
        }

        private ITaskItem[] _targetAssemblies;

        /// <summary>
        /// Specifies the target assembly to analyze.
        /// </summary>
        [Required]
        public ITaskItem[] TargetAssemblies
        {
            get { return _targetAssemblies; }
            set { _targetAssemblies = value; }
        }

        private string _consoleXslFileName;

        /// <summary>
        /// Specifies the XSL or XSLT file that contains a transformation to 
        /// be applied to the analysis output before it is displayed in the console.
        /// </summary>
        public string ConsoleXslFileName
        {
            get { return _consoleXslFileName; }
            set { _consoleXslFileName = value; }
        }

        private ITaskItem[] _importFiles;

        /// <summary>
        /// Specifies the name of an analysis report or project file to import. 
        /// Any messages in the imported file that are marked as excluded are not 
        /// included in the analysis results.
        /// </summary>
        public ITaskItem[] ImportFiles
        {
            get { return _importFiles; }
            set { _importFiles = value; }
        }

        private ITaskItem[] _ruleLibraries;

        /// <summary>
        /// Specifies the filename(s) of FxCop project file(s).
        /// </summary>
        public ITaskItem[] RuleLibraries
        {
            get { return _ruleLibraries; }
            set { _ruleLibraries = value; }
        }

        private string _analysisReportFileName;

        /// <summary>
        /// Specifies the file name for the analysis report.
        /// </summary>
        public string AnalysisReportFileName
        {
            get { return _analysisReportFileName; }
            set { _analysisReportFileName = value; }
        }

        private string _outputXslFileName;

        /// <summary>
        /// Specifies the XSL or XSLT file that is referenced by the 
        /// xml-stylesheet processing instruction in the analysis report.
        /// </summary>
        public string OutputXslFileName
        {
            get { return _outputXslFileName; }
            set { _outputXslFileName = value; }
        }

        private string _platformDirectory;

        /// <summary>
        /// Specifies the location of the version of Mscorlib.dll 
        /// that was used when building the target assemblies if this 
        /// version is not installed on the computer running FxCopCmd.
        /// </summary>
        public string PlatformDirectory
        {
            get { return _platformDirectory; }
            set { _platformDirectory = value; }
        }

        private string _projectFile;

        /// <summary>
        /// Specifies the filename of FxCop project file.
        /// </summary>
        public string ProjectFile
        {
            get { return _projectFile; }
            set { _projectFile = value; }
        }

        private bool _includeSummaryReport;

        /// <summary>
        /// Includes a summary report with the informational 
        /// messages returned by FxCopCmd.
        /// </summary>
        public bool IncludeSummaryReport
        {
            get { return _includeSummaryReport; }
            set { _includeSummaryReport = value; }
        }

        private string _typeList;

        /// <summary>
        /// Comma-separated list of type names to analyze.  This option disables 
        /// analysis of assemblies, namespaces, and resources; only the specified 
        /// types and their members are included in the analysis.  
        /// Use the wildcard character '*' at the end of the name to select multiple types.
        /// </summary>
        public string TypeList
        {
            get { return _typeList; }
            set { _typeList = value; }
        }

        private bool _saveResults;

        /// <summary>
        /// Saves the results of the analysis in the project file.
        /// </summary>
        public bool SaveResults
        {
            get { return _saveResults; }
            set { _saveResults = value; }
        }

        private string _workingDirectory;

        /// <summary>
        /// Gets or sets the working directory.
        /// </summary>
        /// <value>The working directory.</value>
        /// <returns>
        /// The directory in which to run the executable file, or a null reference (Nothing in Visual Basic) if the executable file should be run in the current directory.
        /// </returns>
        public string WorkingDirectory
        {
            get { return _workingDirectory; }
            set { _workingDirectory = value; }
        }

        private bool _verbose;

        /// <summary>
        /// Gets or sets a value indicating whether the output is verbose.
        /// </summary>
        /// <value><c>true</c> if verbose; otherwise, <c>false</c>.</value>
        public bool Verbose
        {
            get { return _verbose; }
            set { _verbose = value; }
        }

        private bool _failOnError = true;

        /// <summary>
        /// Gets or sets a value indicating whether the build should
        /// fail if static code analysis reports errors. Defaults to 
        /// <c>true</c>.
        /// </summary>
        /// <value><c>true</c> if verbose; otherwise, <c>false</c>.</value>
        public bool FailOnError
        {
            get { return _failOnError; }
            set { _failOnError = value; }
        }

        #endregion

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns><see langword="true"/> if the task ran successfully; 
        /// otherwise <see langword="false"/>.</returns>
        public override bool Execute()
        {
            if (!FailOnError)
            {
                base.Execute();
                return true;
            }
            else
            {
                return base.Execute();
            }
        }

        /// <summary>
        /// Returns the fully qualified path to the executable file.
        /// </summary>
        /// <returns>
        /// The fully qualified path to the executable file.
        /// </returns>
        protected override string GenerateFullPathToTool()
        {
            string fxCopPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            fxCopPath = Path.Combine(fxCopPath, @"Microsoft FxCop 1.32");

            try
            {
                using (RegistryKey buildKey = Registry.ClassesRoot.OpenSubKey(@"FxCopProject\shell\Open\command"))
                {
                    if (buildKey == null)
                    {
                        Log.LogError("Could not find the FxCopProject File command in the registry. Please make sure FxCop is installed.");
                    }
                    else
                    {
                        fxCopPath = buildKey.GetValue(null, fxCopPath).ToString();
                        Regex fxCopRegex = new Regex("(.+)fxcop\\.exe", RegexOptions.IgnoreCase);
                        Match pathMatch = fxCopRegex.Match(fxCopPath);
                        fxCopPath = pathMatch.Groups[1].Value.Replace("\"", "");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
            }

            base.ToolPath = fxCopPath;
            return Path.Combine(ToolPath, ToolName);
        }

        /// <summary>
        /// Returns a string value containing the command line arguments 
        /// to pass directly to the executable file.
        /// </summary>
        /// <returns>
        /// A string value containing the command line arguments to pass 
        /// directly to the executable file.
        /// </returns>
        protected override string GenerateCommandLineCommands()
        {
            GenerateFullPathToTool();
            StringBuilder _programArguments = new StringBuilder();

            if (ApplyOutXsl)
            {
                _programArguments.Append("/aXsl ");
            }

            if (DirectOutputToConsole)
            {
                _programArguments.Append("/c ");
            }

            if (!string.IsNullOrEmpty(ConsoleXslFileName))
            {
                _programArguments.AppendFormat("/cXsl:\"{0}\" ", ConsoleXslFileName);
            }

            if (DependencyDirectories != null)
            {
                foreach (ITaskItem item in DependencyDirectories)
                {
                    _programArguments.AppendFormat("/d:\"{0}\" ", item.ItemSpec);
                }
            }

            foreach (ITaskItem item in TargetAssemblies)
            {
                _programArguments.AppendFormat("/f:\"{0}\" ", item.ItemSpec);
            }

            if (ImportFiles != null)
            {
                foreach (ITaskItem item in ImportFiles)
                {
                    _programArguments.AppendFormat("/i:\"{0}\" ", item.ItemSpec);
                }
            }

            if (!string.IsNullOrEmpty(AnalysisReportFileName))
            {
                _programArguments.AppendFormat("/o:\"{0}\" ", AnalysisReportFileName);
            }

            if (!string.IsNullOrEmpty(OutputXslFileName))
            {
                _programArguments.AppendFormat("/oXsl:\"{0}\" ", OutputXslFileName);
            }

            if (!string.IsNullOrEmpty(PlatformDirectory))
            {
                _programArguments.AppendFormat("/plat:\"{0}\" ", PlatformDirectory);
            }

            if (!string.IsNullOrEmpty(ProjectFile))
            {
                _programArguments.AppendFormat("/p:\"{0}\" ", ProjectFile);
            }

            if (RuleLibraries != null)
            {
                foreach (ITaskItem item in RuleLibraries)
                {
                    _programArguments.AppendFormat("/r:\"{0}\" ", 
                        Path.Combine(Path.Combine(ToolPath, "Rules"), item.ItemSpec));
                }
            }

            if (IncludeSummaryReport)
            {
                _programArguments.Append("/s ");
            }

            if (!string.IsNullOrEmpty(TypeList))
            {
                _programArguments.AppendFormat("/t:{0} ", TypeList);
            }

            if (SaveResults)
            {
                _programArguments.Append("/u ");
            }

            if (Verbose)
            {
                _programArguments.Append("/v ");
            }

            return _programArguments.ToString();
        }

        /// <summary>
        /// Gets the name of the executable file to run.
        /// </summary>
        /// <value></value>
        /// <returns>The name of the executable file to run.</returns>
        protected override string ToolName
        {
            get 
            { 
                return "fxcopcmd.exe"; 
            }
        }

        /// <summary>
        /// Returns the directory in which to run the executable file.
        /// </summary>
        /// <returns>
        /// The directory in which to run the executable file, 
        /// or a null reference (Nothing in Visual Basic) if the executable file 
        /// should be run in the current directory.
        /// </returns>
        protected override string GetWorkingDirectory()
        {
            return string.IsNullOrEmpty(_workingDirectory) ? base.GetWorkingDirectory() : _workingDirectory;
        }
    }
}