﻿// (c) Copyright Bailey Ling.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;

namespace Snoop
{
    /// <summary>
    /// Interaction logic for EmbeddedShellView.xaml
    /// </summary>
    public partial class EmbeddedShellView : UserControl
    {
        private readonly Runspace runspace;
        private int historyIndex;

        public EmbeddedShellView()
        {
            InitializeComponent();

            this.Unloaded += delegate { runspace.Dispose(); };

            commandTextBox.PreviewKeyDown += OnCommandTextBoxPreviewKeyDown;

            // ignore execution-policy
            var iis = InitialSessionState.CreateDefault();
            iis.AuthorizationManager = new AuthorizationManager(Guid.NewGuid().ToString());

            this.runspace = RunspaceFactory.CreateRunspace(iis);
            this.runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
            this.runspace.ApartmentState = ApartmentState.STA;
            this.runspace.Open();

            LoadModule();
        }

        private void LoadModule()
        {
            string scriptDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Scripts");
            string modulePath = Path.Combine(scriptDir, "Snoop.psm1");
            SetVariable("modulePath", modulePath);
            using (var pipe = this.runspace.CreatePipeline(string.Format("import-module \"{0}\"", modulePath), false))
                pipe.Invoke();
        }

        private void OnCommandTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    SetCommandTextToHistory(++this.historyIndex);
                    break;
                case Key.Down:
                    if (this.historyIndex - 1 <= 0)
                    {
                        this.commandTextBox.Clear();
                    }
                    else
                    {
                        SetCommandTextToHistory(--this.historyIndex);
                    }
                    break;
                case Key.Return:
                    Invoke(commandTextBox.Text);
                    commandTextBox.Clear();
                    break;
            }
        }

        public void SetVariable(string name, object instance)
        {
            this.runspace.SessionStateProxy.SetVariable(name, instance);
        }

        private void Invoke(string script)
        {
            this.historyIndex = 0;

            outputTextBox.AppendText(Environment.NewLine);
            outputTextBox.AppendText(script);
            outputTextBox.AppendText(Environment.NewLine);

            try
            {
                using (var pipe = this.runspace.CreatePipeline(script, true))
                {
                    var cmd = new Command("Out-String");
                    cmd.Parameters.Add("Width", 500);
                    pipe.Commands.Add(cmd);

                    foreach (var item in pipe.Invoke())
                    {
                        outputTextBox.AppendText(item.ToString());
                    }

                    if (pipe.HadErrors)
                    {
                        foreach (var item in pipe.Error.ReadToEnd())
                        {
                            outputTextBox.AppendText(item.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                outputTextBox.AppendText(ex.Message);
            }

            outputTextBox.ScrollToEnd();
        }

        private void SetCommandTextToHistory(int history)
        {
            var cmd = GetHistoryCommand(history);
            if (cmd != null)
            {
                commandTextBox.Text = cmd;
                commandTextBox.SelectionStart = cmd.Length;
            }
        }

        private string GetHistoryCommand(int history)
        {
            using (var pipe = this.runspace.CreatePipeline("get-history -count " + history))
            {
                var results = pipe.Invoke();
                if (results.Count > 0)
                {
                    dynamic item = results[0];
                    return (string)item.CommandLine;
                }

                return null;
            }
        }
    }
}