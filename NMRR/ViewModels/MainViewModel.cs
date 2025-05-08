using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using NMRR.Models;
using System.Windows.Input;
using NMRR.Helpers;
using System.IO;
using System.Linq;
using ScottPlot;
using System.Windows.Data;
using System.Security.Policy;
using System.Windows;
using System.Drawing;
using System.Windows.Threading;
using ScottPlot.WPF;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NMRR.ViewModels
{
    internal partial class MainViewModel : BaseViewModel
    {
        public static MainViewModel Instance { get; private set; } = new MainViewModel();  // Instance to use as Singlton model in other views

        private TcpClientService _serialPortService; // Serial Port Service to handle serial communication
        private const float PosGain = 12; // Gain for Position (+-10V --> +-120 degree)
        private const float PosOffset = (float)-0.25; // Offset for Position (used for calibration)
        private const float TqGain = 10; // Gain for Torque (+-10V --> +-100 N.m)
        private const float TqOffset = 0; // Offset for Torque (used for calibration)

        public const int ADC_CHANNELS = 2; // Number of ADC channels
        public const int ADC_BUFFER_LENGTH = 50; // Number of samples per ADC channel buffer for feedback packets
        public const float Ts = 0.001F; // Sampling time for feedback data
        public const int DAC_BULK_SIZE = 256; // Bulk size of DAC for lower UART buffer in MCU

        private string sessionFolderPath;
        private List<float> tPosCsv; // Time for Position data
        private List<float> tTqCsv; // Time for Torque data
        private List<float> PosCsv; // Position data
        private List<float> TqCsv; // Torque data

        // Load Final Pattern to a specific variable so it can be download to MCU
        private List<float> CommandPattern;

        private bool showFeedback { get; set; } = false; // Flag to show feedback data in FeedbackPlot or not
        private bool CollectData { get; set; } = false; // Flag to Collect data to CSV buffers

        // ScottPlot Plot object to hold the data
        public Plot PatternPlot { get; } = new(); // Pattern Plot used in View
        public Plot FeedbackPlot { get; } = new(); // Feedback Plot used in View        public Multiplot ResultMultiPlot { get; } = new(); // Multiplot to hold Result Plot Position and Torque
        public event EventHandler PatternPlotUpdated; // Event handler to update Pattern Plot in View
        public event EventHandler ResultPlotHandler; // Event handler to update Result Plot in View
        public int PatternTabSelectedIndex { get; set; } = 0; // Index of the selected tab in Pattern Plot (Pattern, Feedback, Result)
        public string MotorPos { get; set; } = string.Empty; // Motor Position to show in View
        public string TimeStr { get; set; } = string.Empty; // Time of Feedback (to check if data is comming or not)
        public string CommandToSend { get; set; } // Command to send to MCU
        public string SerialLog { get; set; } = string.Empty; // Serial Log to show in View
        public string GoToTextBox { get; set; } = string.Empty; // Go To Pos Text Box
        public string StatusMessage { get; set; } = "IDLE"; // Status Bar Message Label
        public System.Windows.Media.Brush StatusBGColor { get; set; } = System.Windows.Media.Brushes.LightGray; // StatusBar Background
        private DispatcherTimer StatusTimer; // a Timer to get back status bar to IDLE

        public ICommand StartCommand { get; } // Button to start receiving data from MCU
        public ICommand StopCommand { get; } // Button to stop receiving data from MCU
        public ICommand SendCommand { get; } // Button to send command to MCU
        public ICommand SaveToCsvCommand { get; } // Button to save data to CSV file
        public ICommand SaveBtn { get; } // Button to save data to CSV file
        public ICommand StartBtnCommand { get; } // Button to write pattern to MCU
        public ICommand StopBtnCommand { get; } // Button to Stop
        public ICommand GotoBtn { get; } // Button to start Goto
        public ICommand VoluntaryBtn { get; } // Button to start Goto
        


        public MainViewModel()
        {
            _serialPortService = new TcpClientService("192.168.5.250", 7);
            Task.Run(() => InitializeCommunication());
            StartCommand = new RelayCommand(StartReceiving);
            StopCommand = new RelayCommand(StopReceiving);
            SendCommand = new RelayCommand(SendCommandToDevice);
            SaveToCsvCommand = new RelayCommand(SaveToCsv);
            SaveBtn = new RelayCommand(SaveToCsv);
            StartBtnCommand = new RelayCommand(onStartBtn);
            StopBtnCommand = new RelayCommand(onStopBtn);
            GotoBtn = new RelayCommand(GoTo);
            VoluntaryBtn = new RelayCommand(onVoluntaryBtn);


            StatusTimer = new DispatcherTimer();
            StatusTimer.Interval = TimeSpan.FromSeconds(1); // Default 1s For Status Timer
            StatusTimer.Tick += StatusTimer_Tick; // Attach the Tick event

            tPosCsv = [];
            PosCsv = [];
            tTqCsv = [];
            TqCsv = [];

            CommandPattern = [];

            InitializePlots();

            // Initialize MotorPos
            MotorPos = "0.0";
            TimeStr = "00:00";
        }

        // Load Final Pattern to a specific variable so it can be download to MCU
        public void LoadFinalPattern(List<float> pattern)
        {
            CommandPattern = pattern;
            for (int i = 0; i < pattern.Count; i++)
            {
                CommandPattern[i] /= 12;
            }
        }

        private async Task InitializeCommunication()
        {
            _serialPortService.DataReceived += OnDataReceived;
            bool connected = await _serialPortService.ConnectAsync();
            if (!connected)
            {
                // Handle connection failure (e.g., show error message)
                Debug.WriteLine("Failed to connect to the TCP server.");
            }
            else
            {
                Debug.WriteLine("Successfully connected to the TCP server.");
                // Optionally send an initial command after connecting
                // await _tcpService.StartReceivingAsync();
            }
        }
    }

}
