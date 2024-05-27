﻿using System.ComponentModel;
using System.Windows;
using Twitch;

namespace StreamCommandBooper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// The current namespace
        /// </summary>
        protected const string Namespace = "StreamCommandBooper.Windows.MainWindow";
        #region Binding Helper
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }

        }
        #endregion
        /// <summary>
        /// The Twitch Client
        /// </summary>
        public Twitch.Config TwitchConfig { get; set; } = new();
        /// <summary>
        /// The currently selected channel
        /// </summary>
        public string? CurrentChannel { get { return _CurrentChannel; } set { _CurrentChannel = value; OnPropertyChanged(nameof(CurrentChannel)); } }
        private string? _CurrentChannel;
        /// <summary>
        /// The commands to be processed
        /// </summary>
        public string CommandLines { get { return _CommandLines; } set { _CommandLines = value; OnPropertyChanged(nameof(CommandLines)); } }
        private string _CommandLines = string.Empty;
        /// <summary>
        /// Is the User logged in
        /// </summary>
        public bool isLoggedIn { get { return _isLoggedIn; } set { _isLoggedIn = value; OnPropertyChanged(nameof(isLoggedIn)); } }
        bool _isLoggedIn = false;
        /// <summary>
        /// Is the command list processing
        /// </summary>
        public bool isProcessing { get { return _isProcessing; } set { _isProcessing = value; OnPropertyChanged(nameof(isProcessing)); } }
        bool _isProcessing = false;
        /// <summary>
        /// The delay between commands, too fast and Twitch will surpess the message as spam
        /// </summary>
        public Int32 Delay { get { return _Delay; } set { _Delay = value; OnPropertyChanged(nameof(Delay)); } }
        Int32 _Delay = 2000;
        /// <summary>
        /// The number of commands processed
        /// </summary>
        public Int32 Stat_Processed { get { return _Stat_Processed; } set { _Stat_Processed = value; OnPropertyChanged(nameof(Stat_Processed)); } }
        Int32 _Stat_Processed = 0;
        /// <summary>
        /// The remaining number of commands to process
        /// </summary>
        public Int32 Stat_Remaining { get { return _Stat_Remaining; } set { _Stat_Remaining = value; OnPropertyChanged(nameof(Stat_Remaining)); } }
        Int32 _Stat_Remaining = 0;
        /// <summary>
        /// A list of channels the usr is a moderator for, this list is used to select the channel to process command for
        /// </summary>
        public List<String> Channels { get { return _Channels; } set { _Channels = value; OnPropertyChanged(nameof(Channels)); } }
        List<String> _Channels { get; set; } = new();
        /// <summary>
        /// Abort processing the list if True
        /// </summary>
        protected bool AbortProcessing = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Setup the app for use
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Config.Load();
                if (string.IsNullOrWhiteSpace(Twitch.Config.ClientID)) { Twitch.Config.ClientID = "0x51241kq9zvluxfa3mgzi43v2b3l0"; } //Set Default Client ID
                this.CurrentChannel = Twitch.Config.ChannelName;
                this.ConnectToTwitch();
                await this.GetChannels();
            }
            catch (Exception ex) { Helpers.MessageBox2.ShowDialog(ex, $"{Namespace}.Window_Loaded"); }
            this.DataContext = this;
        }

        /// <summary>
        /// Get a list of channels the user is a moderator for
        /// </summary>
        protected async Task GetChannels()
        {
            var Mod = await Twitch.APIs.Users.GetUsersAsync(null, new List<string> { Twitch.Config.ChannelName });

            if (Mod.Data.Count > 0)
            {
                this.Channels.Clear();

                var Channels = await Twitch.APIs.Users.GetModerationChannelsAsync(Mod.Data[0].ID);
                if (Channels.Data == null) { return; }

                this.Channels.Add(Twitch.Config.ChannelName);
                foreach (var user in Channels.Data.OrderBy(channel => channel.Broadcaster_Login))
                {
                    this.Channels.Add(user.Broadcaster_Login);
                }
                this.CurrentChannel = this.Channels.FirstOrDefault();
            }

        }

        /// <summary>
        /// Connect to the Twitch servers
        /// </summary>
        private void ConnectToTwitch()
        {
            if (this.CurrentChannel == null) { return; }
            var userDetails = Twitch.APIs.Users.GetUsersAsync(null, new List<string>() { Twitch.Config.ChannelName });
            this.isLoggedIn = (userDetails != null);

            if (!this.isLoggedIn)
            { // If connection fails, open the settings page
                Windows.Authentication AuthWindow = new Windows.Authentication();
                AuthWindow.ShowDialog();
            }
        }

        /// <summary>
        /// Begin processing the command list
        /// </summary>
        private async void btnProcessCommands_Clicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(this.CurrentChannel)) { return; }
                if (string.IsNullOrWhiteSpace(TwitchConfig.oAuthToken)) { return; }
                if (string.IsNullOrWhiteSpace(TwitchConfig.channelName)) { return; }
                if (string.IsNullOrWhiteSpace(TwitchConfig.channelID)) { return; }
                if (string.IsNullOrWhiteSpace(this.CommandLines)) { return; }
                this.isProcessing = true;
                this.ConnectToTwitch();

                await processCommands();
            }
            catch (Exception ex) { Helpers.MessageBox2.ShowDialog(ex, $"{Namespace}.btnProcessCommands_Clicked"); }

            this.isProcessing = false;
        }

        /// <summary>
        /// Process all commands
        /// </summary>
        protected async Task processCommands()
        {
            if (string.IsNullOrWhiteSpace(this.CurrentChannel)) { return; }
            if (string.IsNullOrWhiteSpace(Twitch.Config.ChannelName)) { return; }
            this.AbortProcessing = false;

            var Channel = await Twitch.APIs.Users.GetUsersAsync(null, new List<string> { this.CurrentChannel }); // Get the Channel Account
            var ModID = await Twitch.APIs.Users.GetUsersAsync(null, new List<string> { Twitch.Config.ChannelName }); // Get the Mod Account

            string[] commandLines = this.CommandLines.Split(Environment.NewLine);

            // Update Statistics
            this.Stat_Remaining = commandLines.Length;
            this.Stat_Processed = 0;
            // Update Statistics

            await Twitch.APIs.Chat.SendMessageAsync(Channel.Data[0].ID, $"Started: {DateTime.Now.ToString("HH:mm:ss")}");
            foreach (string line in commandLines)
            {
                try
                {
                    if (this.AbortProcessing) { return; } // Stop processing if true
                    await Task.Delay(this.Delay); // Delay added to stop from spamming the Twitch servers
                    if (this.AbortProcessing) { return; } // Stop processing if true

                    // Update Statistics
                    this.Stat_Remaining -= 1;
                    this.Stat_Processed += 1;
                    // Update Statistics

                    // BAN A VIEWER
                    if (line.StartsWith("/ban "))
                    {
                        await this.BanUser(Channel.Data[0].ID, line);
                        continue;
                    }
                    // BAN A VIEWER
                    // ADD A BLOCKED TERM
                    if (line.StartsWith("/add_blocked_term "))
                    {
                        await this.AddBlockedTerm(Channel.Data[0].ID, line);
                        continue;
                    }
                    // ADD A BLOCKED TERM

                    // SEND A MESSAGE IN CHAT
                    await Twitch.APIs.Chat.SendMessageAsync(Channel.Data[0].ID, line);
                    // SEND A MESSAGE IN CHAT

                    this.CommandLines = this.CommandLines.Replace($"{line}\r\n", string.Empty); // Remove the processed item from the list
                }
                catch (Exception ex) { Helpers.MessageBox2.ShowDialog(ex, $"{Namespace}.processCommands.ForLoop"); }
            }

            await Twitch.APIs.Chat.SendMessageAsync(Channel.Data[0].ID, $"Completed: {DateTime.Now.ToString("HH:mm:ss")}");
            this.CommandLines = string.Empty;
        }

        /// <summary>
        /// Add blocked term
        /// </summary>
        /// <param name="ChannelID">The channel to ban the phrase in</param>
        /// <param name="line">The command line to process</param>
        /// <returns></returns>
        private async Task AddBlockedTerm(string ChannelID, string line)
        {
            string[] command = line.Split(" ");
            string viewer = string.Empty;
            string blocked_term = string.Empty;
            blocked_term = line.Replace($"/add_blocked_term ", string.Empty);
            if (string.IsNullOrWhiteSpace(blocked_term)) { return; }

            try
            {
                await Twitch.APIs.Blocked_Terms.BlockTermAsync(ChannelID, blocked_term);
            }
            catch { }

            this.CommandLines = this.CommandLines.Replace($"{line}\r\n", string.Empty);
        }

        /// <summary>
        /// Ban the user
        /// </summary>
        /// <param name="ChannelID">The channel to ban the viewer from</param>
        /// <param name="line">The command line to process</param>
        /// <returns></returns>
        private async Task BanUser(string ChannelID, string line)
        {
            string[] command = line.Split(" ");
            string viewer = string.Empty;
            string reason = string.Empty;
            if (command.Length >= 2) { viewer = command[1]; } else { return; }
            if (command.Length >= 3) { reason = line.Replace($"/ban {viewer} ", string.Empty); }
            var UserIDs = await Twitch.APIs.Users.GetUsersAsync(null, new List<string> { viewer });
            if (UserIDs == null || UserIDs.Data == null || UserIDs.Data.Count() == 0) { return; }
            viewer = UserIDs.Data[0].ID;

            try
            {
                await Twitch.APIs.Users.BanUserAsync(ChannelID, UserIDs.Data[0].ID, reason);
            }
            catch { }

            this.CommandLines = this.CommandLines.Replace($"{line}\r\n", string.Empty);
        }

        /// <summary>
        /// Open the login screen
        /// </summary>
        private void btnLogIn_Clicked(object sender, RoutedEventArgs e)
        {
            Windows.Authentication auth = new Windows.Authentication();
            auth.ShowDialog();
        }

        /// <summary>
        /// Stop processing the list
        /// </summary>
        private void btnStopProcessCommands_Clicked(object sender, RoutedEventArgs e)
        {
            this.AbortProcessing = true;
        }
    }
}