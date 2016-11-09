using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Policy;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

using System.Windows.Threading;

namespace IPUpdate
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DispatcherTimer updateTimer;
        private NotifyIcon nIcon;
        public MainWindow()
        {
            InitializeComponent();
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            updateTimer = new DispatcherTimer {Interval = TimeSpan.FromMinutes(5)};
            updateTimer.Tick += (sender, args) =>
            {
                IPChange();
            };
            updateTimer.Start();
            this.StateChanged += OnStateChanged;


            if (Properties.Settings.Default.username != "")
            {
                userNameTextBox.Text = Properties.Settings.Default.username;
            }
            if (Properties.Settings.Default.password != "")
            {
                passwordTextBox.Text = Properties.Settings.Default.password;
            }
            if (Properties.Settings.Default.ip != "")
            {
                serveripv4Text.Text = Properties.Settings.Default.ip;
            }
            if (Properties.Settings.Default.hostname != "")
            {
                hostnameTextBox.Text = Properties.Settings.Default.hostname;
            }
 
        }

        private void IPChange()
        {
            var client = new WebClient();
            var publicIP = client.DownloadString("http://whatismyip.akamai.com/");
            if (Properties.Settings.Default.myPublicIP != publicIP)
            {
                Properties.Settings.Default.myPublicIP = publicIP;
                Properties.Settings.Default.Save();
                updateIP();
            }
        }

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(IPChange);
        }

        private void OnStateChanged(object sender, EventArgs eventArgs)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                createNotificaitonIcon();
            }


        }

        private void createNotificaitonIcon()
        {
            nIcon?.Dispose();
            nIcon = new NotifyIcon
            {
                Icon = Properties.Resources.NetDrives,
                Visible = true
            };
            // Display for 5 seconds.
            nIcon.DoubleClick += (sender, args) =>
            {
                this.WindowState = WindowState.Normal;
                nIcon.Dispose();
            };
        }

        public void showNotificaiton(string title, string message, ToolTipIcon icon)
        {
            createNotificaitonIcon();
            nIcon.ShowBalloonTip(5, title, message, icon );

            // This will let the balloon close after it's 5 second timeout
            // for demonstration purposes. Comment this out to see what happens
            // when dispose is called while a balloon is still visible.
            Thread.Sleep(5000);

            // The notification should be disposed when you don't need it anymore,
            // but doing so will immediately close the balloon if it's visible.
            if (this.WindowState != WindowState.Minimized)
            {
                nIcon.Dispose();
            }
        }

        public void updateIP()
        {
            var client = new WebClient();
            var user = userNameTextBox.Text;
            if (user == "") return;
            try
            {
                var result = client.DownloadString(
                    "https://ipv4.tunnelbroker.net/nic/update?username=" + user + "&password=" + passwordTextBox.Text +
                    "&hostname=" + hostnameTextBox.Text);
                result = result.TrimEnd('\n');
                var parts = result.Split(' ');
                if (parts[0] == "nochg")
                {
                    Debug.WriteLine("no change detected");
                    // uncomment for debugging
                    showNotificaiton("Success", "No Change, this shouldn't be here", ToolTipIcon.Warning);

                    
                }
                else if (parts.Count() == 2 && parts[0] == "good")
                {
                    var localIP = parts[1];
                    var msg = "IP endpoint changed to " + localIP;
                    var deleteTunnel = ExecuteCommandSync("netsh interface ipv6 delete interface IP6tunnel");
                    var addTunnel =
                        ExecuteCommandSync("netsh interface ipv6 add v6v4tunnel interface=IP6Tunnel localaddress=" +
                                           localIP + " remoteaddress=" + serveripv4Text.Text);
                    if (addTunnel && deleteTunnel)
                    {
                        showNotificaiton("Success", msg, ToolTipIcon.Info);
                    }
                    else
                    {
                        msg = "Unable to update local tunnel.  Are you running as admin?";
                        showNotificaiton("Error", msg, ToolTipIcon.Error);
                    }
                }
            }
            catch (Exception e)
            {
                var msg = "Unable to update the tunnel: "+e.Message;
                showNotificaiton("Unknown Error", msg, ToolTipIcon.Error);
            }
        
        }

        public bool ExecuteCommandSync(object command)
        {
            try
            {
                // create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // Incidentally, /c tells cmd that we want it to execute the command that follows,
                // and then exit.
                var procStartInfo =
                    new ProcessStartInfo("cmd", "/c " + command)
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                // Now we create a process, assign its ProcessStartInfo and start it
                var proc = new Process {StartInfo = procStartInfo};
                proc.Start();
                // Get the output into a string
                var result = proc.StandardOutput.ReadToEnd();
                // Display the command output.

                return result.Trim() == string.Empty;
            }
            catch 
            {
                // Log the exception
                return false;
            }
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            updateIP();
        }

        private void userNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.username = userNameTextBox.Text;
            Properties.Settings.Default.Save();
        }

        private void passwordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.password = passwordTextBox.Text;
            Properties.Settings.Default.Save();
        }

        private void serveripv4Text_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.ip = serveripv4Text.Text;
            Properties.Settings.Default.Save();
        }

        private void hostnameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.hostname = hostnameTextBox.Text;
            Properties.Settings.Default.Save();
        }
    }
}
