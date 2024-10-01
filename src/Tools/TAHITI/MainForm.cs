using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TAHITI_ConnectionTool
{
    public partial class MainForm : Form
    {
        private enum SetupState
        {
            Invalid,
            Start,
            SelectFolder,
            Complete
        }

        private SetupState _state = SetupState.Invalid;

        public MainForm()
        {
            InitializeComponent();
            AdvanceState();
        }

        private void nextButton_Click(object sender, EventArgs e)
        {
            switch (_state)
            {
                case SetupState.Start:
                    AdvanceState();
                    return;

                case SetupState.SelectFolder:
                    SetupResult result = SetupHelper.RunSetup(folderBrowseTextBox.Text);
                    if (result != SetupResult.Success)
                    {
                        string message = SetupHelper.GetResultText(result);
                        MessageBox.Show(message, "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    AdvanceState();
                    return;

                case SetupState.Complete:
                    Application.Exit();
                    return;
            }
        }

        private void folderBrowseButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new())
            {
                DialogResult dialogResult = dialog.ShowDialog(this);
                if (dialogResult != DialogResult.OK) return;
                folderBrowseTextBox.Text = dialog.SelectedPath;
            }
        }

        private void AdvanceState()
        {
            _state++;
            UpdateForm();
        }

        private void UpdateForm()
        {
            switch (_state)
            {
                case SetupState.Start:
                    headerLabel.Text = "Welcome to the TAHITI Connection Tool Setup";
                    bodyLabel.Text = "This program will help you connect to the MHTahiti.com Server.\r\n" +
                                     "\r\n" +
                                     "To continue, click Next.";
                    break;

                case SetupState.SelectFolder:
                    headerLabel.Text = "Marvel Heroes Files";
                    bodyLabel.Text = "MHTahiti.com requires the original Marvel Heroes game client files to work.\r\n" +
                                     "Please choose the game client folder.";

                    folderBrowseTextBox.Visible = true;
                    folderBrowseButton.Visible = true;

                    break;

                case SetupState.Complete:
                    headerLabel.Text = "Setup Complete";
                    bodyLabel.Text = "Setup successful.\r\n" +
                                     "\r\n" +
                                     "Run StartTAHITIServer.bat to launch the game and connect to the TAHITI server.\r\n";
                    nextButton.Text = "Exit";

                    folderBrowseTextBox.Visible = false;
                    folderBrowseButton.Visible = false;

                    break;

                default:
                    headerLabel.Text = "Invalid Setup State";
                    bodyLabel.Text = string.Empty;
                    break;
            }
        }
    }
}
