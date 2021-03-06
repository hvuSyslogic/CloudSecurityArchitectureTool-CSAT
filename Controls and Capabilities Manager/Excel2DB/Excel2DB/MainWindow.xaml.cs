﻿using CSRC.Models;
using CSRC.Reports;
using Microsoft.Win32;
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using form = System.Windows.Forms;
using Microsoft.VisualBasic;


namespace CSRC
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string ControlFile, CapabilitiesFile, BaselineFile;
        private Context.DataContext dbContent;
        SaveFileDialog saveReportFile = new SaveFileDialog();
        private bool uploadControls = false;
        private string apppath = "";
        private List<Context.Capabilities> caps;
        private List<string> input;

        //set button and run first time set up
        public MainWindow()
        {
            InitializeComponent();

            this.ResizeMode = System.Windows.ResizeMode.CanMinimize;
            TextBlock cap = new TextBlock(){Text="This report allows you to choose what capabilities to create a shortened report of these capabilities.", Width=200,TextWrapping= TextWrapping.Wrap};
            this.capReport.ToolTip = new ToolTip() { Content = cap };
            TextBlock cont = new TextBlock(){Text = "This report allows you to choose what control to list in the report.  For each, there will be a list of all capabilities it is selected for."
                ,Width = 200, TextWrapping = TextWrapping.Wrap};
            this.conReport.ToolTip = new ToolTip() { Content = cont };
            TextBlock tic = new TextBlock() { Text = "This report show every TIC with a list of all capabilities with that marking.", Width = 200, TextWrapping = TextWrapping.Wrap };
            this.TICReport.ToolTip = new ToolTip() { Content = tic };

            //setup

            if (Properties.Settings.Default.FirstRun)
            {
                MessageBox.Show(
                    "Welcome to the Cloud Security Manager. \n\tBefore you can use the program, there is some simple set up to do. \n\n\t  First, you must choose where " +
                "to put the folders that will hold input and output files.  The program will create a containing folder, so just pick a easily accessible place.\n\n "
                + "Then, you may need to set up database connection.");

                Waiting load = new Waiting();
                SetPath();
                load.Topmost = true;
                load.Show();
                load.Topmost = false;
                try
                {
                    
                    Properties.Settings.Default.FirstRun = false;
                    Properties.Settings.Default.Save();
                    DataConnecter.FirstUse();
                    load.Close();  
                    
                }
                catch (Exception e)
                {
                    load.error.Content = e.Message;
                    Console.WriteLine(e.Message);
                }
                  
            }
            else
            {
                apppath = Properties.Settings.Default.appFolders;
            }
            dbContent = new Context.DataContext(DataConnecter.EstablishValidConnection());
            CSRC.Models.Constants.ReadValues();
            
            var con = from p in dbContent.Controls
                      select p;
            if (!con.Any()){
                this.updateCaps.IsEnabled = false;
                this.updateBaselines.IsEnabled = false;
                this.menuUpdateCapabilities.IsEnabled = false;
                this.menuUpdateBaseines.IsEnabled = false;
            }
            
            var ret = from p in dbContent.Capabilities
                      select new { p.Id };
            if (!(ret.Any()))
            {
                ChangeReportStatus(false);
            }

            var retdata = from p in dbContent.BaselineSecurityMappings
                      select new { p.Id };
            if (!retdata.Any())
            {
                ChangeReportStatus(false);
            }
            CSRC.Models.Constants.ReadValues();
            if (CSRC.Models.Constants.capFile3Cols)
            {
                this.cap3col.IsChecked = true;
                this.cap9col.IsChecked = false;
            }
        }

        /// <summary>
        /// set app folder path
        /// </summary>
        public void SetPath()
        {
            
            form.FolderBrowserDialog dialog = new form.FolderBrowserDialog();
            dialog.Description = "Welcome to the Cloud Security Manager.  Select the location to place the application Folder.";
            dialog.ShowDialog();
            apppath = dialog.SelectedPath + @"\Cloud Security Manager";
            Properties.Settings.Default.appFolders = apppath;
            Properties.Settings.Default.Save();
            Directory.CreateDirectory(apppath);
            addFolders();
        }

        /// <summary>
        /// add missing folders
        /// </summary>
        private void addFolders()
        {
            if (!Directory.Exists(apppath + @"\800-53-Controls"))
            {
                Directory.CreateDirectory(apppath + @"\800-53-Controls");
            }
            if (!Directory.Exists(apppath + @"\Reports"))
            {
                Directory.CreateDirectory(apppath + @"\Reports");
            }
            if (!Directory.Exists(apppath + @"\Security Baselines"))
            {
                Directory.CreateDirectory(apppath + @"\Security Baselines");
            }
            if (!Directory.Exists(apppath + @"\Visualizations"))
            {
                Directory.CreateDirectory(apppath + @"\Visualizations");
            }
        }

        /// <summary>
        /// exit
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// change database location
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditDB(object sender, RoutedEventArgs e)
        {
            bool accept = false;
            string prompt = "Enter SQL server Name: ", conectstr = "";
            string CompName = System.Environment.MachineName;
            while (!accept)
            {
                //continue till connection is made
                string server = Interaction.InputBox(prompt);
                if (server == string.Empty)
                    return;
                conectstr = @"Data Source=" + CompName + "\\" + server + @";Initial Catalog=ModelDB;Integrated Security=True;Persist Security Info=True";
                string serversection = @"Data Source=" + CompName + @"\" + server + ";Integrated Security=True;Persist Security Info=True";
                Context.DataContext connection = new Context.DataContext(serversection);
                try
                {
                    connection.Connection.Open();
                    accept = true;
                }
                catch (Exception ex)
                {
                    prompt = "Connection failed.  Enter SQL server name: ";
                }
            }
            Properties.Settings.Default.Connection = conectstr;
            Properties.Settings.Default.Save();

            //set up tables and values 
            Waiting load = new Waiting();
            load.Topmost = true;
            load.Show();
            load.Topmost = false;
            DataConnecter.FirstUse();
            load.Close();
            MessageBox.Show("Connection updated.");
            ChangeReportStatus(true);
        }

        /// <summary>
        /// get new controls file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenControlslFile_Click(object sender, RoutedEventArgs e)
        {
            
            bool result;
            result = OpenFile(ref ControlFile, apppath + @"\800-53-Controls", "Select Controls File to Parse", this.ControlFileName);
            if (result)
            {
                uploadControls = true;
                toggleUpload(false);
                ChangeReportStatus(false);

                DataConnecter.ClearData();
                this.progressBar1.Value = 0;
                this.percentageLabel.Text = 0 + "%";
                
                BackgroundWorker bw = new BackgroundWorker();
                bw.WorkerReportsProgress = true;
                bw.WorkerSupportsCancellation = true;
                bw.DoWork += new DoWorkEventHandler(parseControls);
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(controlsComplete);
                bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
                bw.RunWorkerAsync();
            }

        }

        /// <summary>
        /// get new capabilities file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenCapabilitiesFile_Click(object sender, RoutedEventArgs e)
        {
            bool result;
            result = OpenFile(ref CapabilitiesFile, apppath + @"\Reports", "Select Capabilities File to Parse", this.CapFileName);
            if (result)
            {
                toggleUpload(false);
                ChangeReportStatus(false);
                DataConnecter.wipeCape();

                this.progressBar1.Value = 0;
                this.percentageLabel.Text = 0 + "%";

                BackgroundWorker bw = new BackgroundWorker();
                bw.WorkerReportsProgress = true;
                bw.WorkerSupportsCancellation = true;
                bw.DoWork += new DoWorkEventHandler(parseCaps);
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(capsComplete);
                bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
                bw.RunWorkerAsync();
            }

        }

        private void btnOpenSecurityBaselinesFile_Click(object sender, RoutedEventArgs e)
        {
            bool result = OpenFile(ref BaselineFile, apppath + @"\Security Baselines", "Select a Security Baseline file to parse", this.BaselineFileName);
            if (result)
            {
                toggleUpload(false);
                ChangeReportStatus(false);

                DataConnecter.wipeBaselines();
                this.progressBar1.Value = 0;
                this.percentageLabel.Text = 0 + "%";

                BackgroundWorker bw = new BackgroundWorker();
                bw.WorkerReportsProgress = true;
                bw.WorkerSupportsCancellation = true;
                bw.DoWork += new DoWorkEventHandler(parseBaseliness);
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(baselinesComplete);
                bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
                bw.RunWorkerAsync();
            }
        }

        private void toggleUpload(bool stat)
        {
            this.updatecontrols.IsEnabled = stat;
            this.updateCaps.IsEnabled = stat;
            this.updateBaselines.IsEnabled = stat;
            this.menuUpdateBaseines.IsEnabled = stat;
            this.menuUpdateCapabilities.IsEnabled = stat;
            this.menuUpdateControls.IsEnabled = stat;
        }
        /// <summary>
        /// use a dialog to get file
        /// </summary>
        /// <param name="openFileDialog"></param>
        /// <param name="title"></param>
        /// <param name="initialDirectory"></param>
        /// <param name="prefix"></param>
        /// <param name="theLabel"></param>
        private bool OpenFile(
            ref string fileName,

            string initialDirectory,
            string title,
            Label theLabel =null
        )
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = title;
            openFileDialog.InitialDirectory = initialDirectory;
            if (openFileDialog.ShowDialog() == true)
            {
                if (null != theLabel)
                {
                    theLabel.Content = openFileDialog.SafeFileName;
                }
                fileName = openFileDialog.FileName;
                return true;
            }
            else
            {
                return false;
            }
        }
        
        
        /// <summary>
        /// create new report
        /// </summary>
        /// <param name="saveFileDialog"></param>
        /// <param name="title"></param>
        /// <param name="initialDirectory"></param>
        /// <param name="theLabel"></param>
        /// <returns></returns>
        private string SaveFile(
            SaveFileDialog saveFileDialog,
            string title = "Select Name and Location for the Report File to Create",
            string initialDirectory = "",
            TextBlock theLabel = null
        )
        {
            initialDirectory = apppath + @"\Reports";
            saveFileDialog.Title = title;
            saveFileDialog.InitialDirectory =initialDirectory;
            if (saveFileDialog.ShowDialog() == true)
            {
                if (null != theLabel)
                {
                    theLabel.Text = saveFileDialog.SafeFileName;
                }
                return saveFileDialog.SafeFileName;
            }
            return string.Empty;
        }


        /// <summary>
        /// create capability report
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuCreateReportControlsCap_Click(object sender, RoutedEventArgs e)
        {
            this.report1.Source = null;
            ChangeReportStatus(false);
            toggleUpload(false);
            this.progressBar1.Value = 0;
            this.percentageLabel.Text = 0 + "%";

            CapabilityChooser cc = new CapabilityChooser();
            try
            {
                if (cc.ShowDialog() == true)
                {
                    caps = cc.capsSelected;
                }
                else
                {
                    ChangeReportStatus(true);
                    return;
                }
                string reportFile = SaveFile(saveReportFile);
                if (reportFile == string.Empty)
                {
                    ChangeReportStatus(true);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return;
            }
            BackgroundWorker bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.WorkerSupportsCancellation = true;
            bw.DoWork += new DoWorkEventHandler(capsReport);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(capsReportComplete);
            bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
            bw.RunWorkerAsync();
            // Find out the file-name for output
            // Do the Excel magic
            // Close excel file
            
        }

        /// <summary>
        /// create controls report
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuCreateReportCapControls_Click(object sender, RoutedEventArgs e)
        {
            ChangeReportStatus(false);
            toggleUpload(false);
            this.report2.Source = null;
            this.progressBar1.Value = 0;
            this.percentageLabel.Text = 0 + "%";
            // Find out the file-name for output

            controlSpecChooser CtrlCho = new controlSpecChooser();
            if (CtrlCho.ShowDialog() == true)
            {
                input = CtrlCho.selected;

            }
            else
            {
                ChangeReportStatus(true);
                return; 
            }
            string reportFile = SaveFile(saveReportFile);
            if (reportFile == string.Empty)
            {
                ChangeReportStatus(true);
                return;
            }
            // Do the Excel magic
            BackgroundWorker bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.WorkerSupportsCancellation = true;
            bw.DoWork += new DoWorkEventHandler(controlReport);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(controlsReportComplete);
            bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
            bw.RunWorkerAsync();
        }

        /// <summary>
        /// create tic report
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CreateTICReport(object sender, RoutedEventArgs e)
        {
            toggleUpload(false);
            ChangeReportStatus(false);
            this.progressBar1.Value = 0;
            this.percentageLabel.Text = 0 + "%";
            this.report3.Source = null;
            // Find out the file-name for output
            string reportFile = SaveFile(saveReportFile);
            // Do the Excel magic
            if (reportFile == string.Empty)
            {
                ChangeReportStatus(true);
                return;
            }
            BackgroundWorker bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.WorkerSupportsCancellation = true;
            bw.DoWork += new DoWorkEventHandler(ticReport);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(TICReportComplete);
            bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
            bw.RunWorkerAsync();
        }

        private void CreateBaselineReport(object sender, RoutedEventArgs e)
        {
            toggleUpload(false);
            ChangeReportStatus(false);
            this.progressBar1.Value = 0;
            this.percentageLabel.Text = 0 + "%";
            this.report3.Source = null;
            
            CapabilityChooser cc = new CapabilityChooser();
            try
            {
                if (cc.ShowDialog() == true)
                {
                    caps = cc.capsSelected;
                }
                else
                {
                    ChangeReportStatus(true);
                    return;
                }
                string reportFile = SaveFile(saveReportFile);
                if (reportFile == string.Empty)
                {
                    ChangeReportStatus(true);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return;
            }

            BackgroundWorker bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.WorkerSupportsCancellation = true;
            bw.DoWork += new DoWorkEventHandler(baselineReport);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(baselineReportComplete);
            bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
            bw.RunWorkerAsync();
        }
        
        //*****************************Background workers*****************************************//

        /// <summary>
        /// send parse controls file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void parseControls(object sender, DoWorkEventArgs e)
        {
            // This event handler is where the actual work is done. 
            // This method runs on the background thread. 

            // Get the BackgroundWorker object that raised this event.
            BackgroundWorker work = (BackgroundWorker)sender;
            UpdateControls converter = new UpdateControls(ControlFile);

            converter.ProcessFile(work);
        }

        /// <summary>
        /// send parse capabilities file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void parseCaps(object sender, DoWorkEventArgs e)
        {
            // This event handler is where the actual work is done. 
            // This method runs on the background thread. 

            // Get the BackgroundWorker object that raised this event.
            BackgroundWorker work = (BackgroundWorker)sender;
            UpdateCapabilities converter = new UpdateCapabilities(CapabilitiesFile);

            converter.ProcessFile(work);
        }

        private void parseBaseliness(object sender, DoWorkEventArgs e)
        {
            // This event handler is where the actual work is done. 
            // This method runs on the background thread. 

            // Get the BackgroundWorker object that raised this event.
            BackgroundWorker work = (BackgroundWorker)sender;
            UpdateBaselines converter = new UpdateBaselines(BaselineFile);
            converter.ProcessFile(work);
        }
        /// <summary>
        /// send capability report create
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void capsReport(object sender, DoWorkEventArgs e)
        {
            // This event handler is where the actual work is done. 
            // This method runs on the background thread. 

            // Get the BackgroundWorker object that raised this event.
            BackgroundWorker work = (BackgroundWorker)sender;

            
            CapabilityReport rep = new CapabilityReport();
            rep.CreateReport(saveReportFile.FileName, caps, work);


        }

        private void baselineReport(object sender, DoWorkEventArgs e)
        {
            // This event handler is where the actual work is done. 
            // This method runs on the background thread. 

            // Get the BackgroundWorker object that raised this event.
            BackgroundWorker work = (BackgroundWorker)sender;


            BaselineReport rep = new BaselineReport();
            rep.CreateReport(saveReportFile.FileName, caps, work);
        }

        
        /// <summary>
        /// send controls report create
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void controlReport(object sender, DoWorkEventArgs e)
        {
            // This event handler is where the actual work is done. 
            // This method runs on the background thread. 

            // Get the BackgroundWorker object that raised this event.
            BackgroundWorker work = (BackgroundWorker)sender;


            CapabilitiesByControlsReport report = new CapabilitiesByControlsReport(saveReportFile.FileName);
            report.CreateReport(saveReportFile.FileName,input, work);
        }

        /// <summary>
        /// send tic report create
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ticReport(object sender, DoWorkEventArgs e)
        {

            BackgroundWorker work = (BackgroundWorker)sender;
            TicReport report = new TicReport();
            report.CreateReport(saveReportFile.FileName, work);
        }

        /// <summary>
        /// send visio report create
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void createVisioReport(object sender, DoWorkEventArgs e)
        {
            // This event handler is where the actual work is done. 
            // This method runs on the background thread. 

            // Get the BackgroundWorker object that raised this event.
            BackgroundWorker work = (BackgroundWorker)sender;


            VisioReport report = new VisioReport();
            report.CreateReport(saveReportFile.FileName, input, work);
        }

        /// <summary>
        /// update progress bar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // This method runs on the main thread.
            this.progressBar1.Value = e.ProgressPercentage;
            this.percentageLabel.Text = e.ProgressPercentage.ToString() + "%";
            
        }

        //**************Background completed********************//
        //each switches x to check and enables proper buttons
        /// <summary>
        /// parse controls return
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void controlsComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            // This event handler is called when the background thread finishes. 
            // This method runs on the main thread. 
            if ((e.Cancelled == true))
            {
                this.percentageLabel.Text = "Canceled!";
            }

            else if (!(e.Error == null))
            {
                this.percentageLabel.Text = ("Error!");
                MessageBox.Show("Error: " + e.Error.Message);
            }

            else
            {
                this.upcontrol.Source = new BitmapImage(new Uri(@"\res\check.png", UriKind.Relative));
                this.percentageLabel.Text = "Done!";
                toggleUpload(true);
            }
            
            
        }


        /// <summary>
        /// capabilities parse back
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void capsComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            // This event handler is called when the background thread finishes. 
            // This method runs on the main thread. 
            if ((e.Cancelled == true))
            {
                this.percentageLabel.Text = "Canceled!";
            }

            else if (!(e.Error == null))
            {
                this.percentageLabel.Text = ("Error: " + e.Error.Message);
            }

            else
            {
                this.percentageLabel.Text = "Done!";
            }
            this.upcapability.Source = new BitmapImage(new Uri(@"\res\check.png", UriKind.Relative));
            toggleUpload(true);
            var ret = from p in dbContent.BaselineSecurityMappings
                      select new { p.Id };
            if (ret.Any())
            {
                ChangeReportStatus(true);
            }

        }

        private void baselinesComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            // This event handler is called when the background thread finishes. 
            // This method runs on the main thread. 
            if ((e.Cancelled == true))
            {
                this.percentageLabel.Text = "Canceled!";
            }

            else if (!(e.Error == null))
            {
                this.percentageLabel.Text = ("Error: " + e.Error.Message);
            }

            else
            {
                this.percentageLabel.Text = "Done!";
            }
            this.upbaseline.Source = new BitmapImage(new Uri(@"\res\check.png", UriKind.Relative));
            toggleUpload(true);
            var ret = from p in dbContent.Capabilities
                      select new { p.Id };
            if (ret.Any())
            {
                ChangeReportStatus(true);
            }

        }
        /// <summary>
        /// controls report back
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void controlsReportComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            // This event handler is called when the background thread finishes. 
            // This method runs on the main thread. 
            if ((e.Cancelled == true))
            {
                this.percentageLabel.Text = "Canceled!";
            }

            else if (!(e.Error == null))
            {
                this.percentageLabel.Text = ("Error!");
                MessageBox.Show("Error: " + e.Error.Message);
            }

            else
            {
                this.percentageLabel.Text = "Done!";
                this.report2.Source = new BitmapImage(new Uri(@"\res\check.png", UriKind.Relative));
                ChangeReportStatus(true);
                toggleUpload(true);
            }
            
        }
        
        /// <summary>
        /// capabilities report back
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void capsReportComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            // This event handler is called when the background thread finishes. 
            // This method runs on the main thread. 
            if ((e.Cancelled == true))
            {
                this.percentageLabel.Text = "Canceled!";
            }

            else if (!(e.Error == null))
            {
                this.percentageLabel.Text = ("Error!");
                MessageBox.Show("Error: " + e.Error.Message);
            }

            else
            {
                this.percentageLabel.Text = "Done!";
                this.report1.Source = new BitmapImage(new Uri(@"\res\check.png", UriKind.Relative));
                ChangeReportStatus(true);
                toggleUpload(true);
            }
        }

        /// <summary>
        /// tic report back
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TICReportComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            // This event handler is called when the background thread finishes. 
            // This method runs on the main thread. 
            if ((e.Cancelled == true))
            {
                this.percentageLabel.Text = "Canceled!";
            }

            else if (!(e.Error == null))
            {
                this.percentageLabel.Text = ("Error!");
                MessageBox.Show("Error: " + e.Error.Message);
            }

            else
            {
                this.percentageLabel.Text = "Done!";
                this.report3.Source = new BitmapImage(new Uri(@"\res\check.png", UriKind.Relative));
                ChangeReportStatus(true);
                toggleUpload(true);
            }
            
        }

        private void baselineReportComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            // This event handler is called when the background thread finishes. 
            // This method runs on the main thread. 
            if ((e.Cancelled == true))
            {
                this.percentageLabel.Text = "Canceled!";
            }

            else if (!(e.Error == null))
            {
                this.percentageLabel.Text = ("Error!");
                MessageBox.Show("Error: " + e.Error.Message);
            }

            else
            {
                this.percentageLabel.Text = "Done!";
                this.report4.Source = new BitmapImage(new Uri(@"\res\check.png", UriKind.Relative));
                ChangeReportStatus(true);
                toggleUpload(true);
            }

        }
        


        /// <summary>
        /// show example reports
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
       private void showex(object sender, RoutedEventArgs e)
       {
           Button ob = sender as Button;
           sampler s = new sampler();
           string fnam = "";
           switch ( ob.Name.ToString()){
               case "basedem":
                   fnam = "res\\baselines.png";
                   break;
               case "capabilitydem":
                   fnam = "res\\capability.png";
                   break;
               case "contdem":
                   fnam = "res\\controls.png";
                   break;
               case "ticdem":
                   fnam = "res\\tic.png";
                   break;
           }
           s.demo.Source = new BitmapImage(new Uri("\\" + fnam, UriKind.Relative));
           s.ShowDialog();
       }

       private void visualization_Click(object sender, RoutedEventArgs e)
       {
           VisualizationTool tool = new VisualizationTool();
           tool.ShowDialog();
       }

       private void ChangeReportStatus(bool val)
       {
           this.conReport.IsEnabled = val;
           this.capReport.IsEnabled = val;
           this.TICReport.IsEnabled = val;
           this.BaselineReport.IsEnabled = val;
           this.menucreateBaseline.IsEnabled = val;
           this.menucreatecap.IsEnabled = val;
           this.menucreatecon.IsEnabled = val;
           this.menucreatetic.IsEnabled = val;
           this.visualization.IsEnabled = val;
           this.visMain.IsEnabled = val;
       }

       private void cap9col_Click(object sender, RoutedEventArgs e)
       {
           Properties.Settings.Default.capFile3Cols = false;
           Properties.Settings.Default.Save();
           Models.Constants.ReadValues();
       }

       private void cap3col_Click(object sender, RoutedEventArgs e)
       {
           Properties.Settings.Default.capFile3Cols = true;
           Properties.Settings.Default.Save();
           Models.Constants.ReadValues();
       }

    }
}