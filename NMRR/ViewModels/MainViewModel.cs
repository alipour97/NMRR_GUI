using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using NMRR.Models;
using System.Windows.Input;
using NMRR.Helpers;
using System.IO;
using System.Linq;
using OxyPlot;
using OxyPlot.Series;
using LineSeries = OxyPlot.Series.LineSeries;
using ScottPlot;
using System.Windows.Data;
using System.Security.Policy;

namespace NMRR.ViewModels
{
    internal class MainViewModel : BaseViewModel
    {

        public static MainViewModel Instance { get; private set; } = new MainViewModel();

        private readonly SerialPortService _serialPortService;


        private static double PosGain { get; set; } = 12;
        private static double PosOffset { get; set; } = 0;
        private static double TqGain { get; set; } = 10;
        private static double TqOffset { get; set; } = 0;

        public static int ADC_CHANNELS { get; set; } = 2;
        public static int ADC_BUFFER_LENGTH { get; set; } = 50;
        public static double Ts {get; set;} = 0.001;

        private List<double> tPosCsv;
        private List<double> tTqCsv;
        private List<double> PosCsv;
        private List<double> TqCsv;

        // Load Final Pattern to a specific variable so it can be download to MCU
        private List<double> CommandPattern;

        private bool showFeedback { get; set; } = false;

        public PlotModel PlotModel { get; set; }


        // ScottPlot Plot object to hold the data
        public Plot PatternPlot { get; } = new();
        public Plot FeedbackPlot { get; } = new();
        public Plot ResultPlot { get; } = new();
        public Multiplot ResultMultiPlot { get; } = new();
        public event EventHandler PatternPlotUpdated;
        public event EventHandler ResultPlotHandler;
        public int PatternTabSelectedIndex { get; set; } = 0;

        private LineSeries PosSeries { get; set; } // Values for PosValue
        private LineSeries TqSeries { get; set; } // Values for TqValue

        public string MotorPos { get; set; } = string.Empty;
        public string CommandToSend { get; set; }
        public string SerialLog { get; set; } = string.Empty;

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SendCommand { get; }
        public ICommand SaveToCsvCommand { get; }



        public MainViewModel()
        {
            _serialPortService = new SerialPortService();

            StartCommand = new RelayCommand(StartReceiving);
            StopCommand = new RelayCommand(StopReceiving);
            SendCommand = new RelayCommand(SendCommandToDevice);
            SaveToCsvCommand = new RelayCommand(SaveToCsv);

            _serialPortService.DataReceived += OnDataReceived;

            tPosCsv = new List<double>();
            PosCsv = new List<double>();
            tTqCsv = new List<double>();
            TqCsv = new List<double>();

            CommandPattern = [];

            PatternPlot.Axes.Left.Label.Text = "Angles (deg)";
            PatternPlot.Axes.Bottom.Label.Text = "Time (s)";
            PatternPlot.Axes.Left.MinorTickStyle.Width = 0;

            FeedbackPlot.Axes.Left.Label.Text = "Torque (N.m)";
            FeedbackPlot.Axes.Bottom.Label.Text = "Time (ms)";
            FeedbackPlot.Axes.Left.MinorTickStyle.Width = 0;
            FeedbackPlot.Axes.Bottom.MinorTickStyle.Width = 0;

            Plot PosResultPlot = new();
            Plot TqResultPlot = new();

            PosResultPlot.Axes.Left.Label.Text = "Angle (deg)";
            TqResultPlot.Axes.Left.Label.Text = "Torque (N.m)";
            TqResultPlot.Axes.Bottom.Label.Text = "Time (s)";

            ResultMultiPlot.AddPlot(PosResultPlot);
            ResultMultiPlot.AddPlot(TqResultPlot);  
            //ResultMultiPlot.Layout = new ScottPlot.MultiplotLayouts.Grid(2, 1);
            //ResultMultiPlot.SetPosition(0, new ScottPlot.SubplotPositions.GridCell(0, 0, 1, 1));
            //ResultMultiPlot.SetPosition(1, new ScottPlot.SubplotPositions.GridCell(1, 0, 1, 1));
            
            // Initialize MotorPos
            MotorPos = "Motor Pos: 0.0";
        }

        private void StartReceiving()
        {
            tTqCsv.Clear();
            tPosCsv.Clear();
            TqCsv.Clear();
            PosCsv.Clear();

            _serialPortService.StartReceiving();
        }

        private void StopReceiving()
        {
            _serialPortService.StopReceiving();
        }

        private void SendCommandToDevice()
        {
            if (!string.IsNullOrEmpty(CommandToSend))
            {
                _serialPortService.SendData(CommandToSend);
            }
        }

        private void SaveToCsv()
        {
            string filePath = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            StringBuilder csvContent = new();

            csvContent.AppendLine("Pos_t,Pos Value,Tq_t,Tq Value");

            for (int i = 0; i < tPosCsv.Count; i++)
            {
                csvContent.AppendLine($"{tPosCsv[i]},{PosCsv[i]},{tTqCsv[i]},{TqCsv[i]}");
            }

            File.WriteAllText(filePath, csvContent.ToString());
        }

        private void OnDataReceived(string command, byte[] data)
        {
            if(command == "inf")
            {
                string infoString = Encoding.ASCII.GetString(data);
                SerialLog += $"{DateTime.Now}: {infoString}\n";
                OnPropertyChanged(nameof(SerialLog));
            }
            else if(command == "fdb")
            {
                // Temporary lists to hold batch updates
                var tPosBatch = new List<double>();
                var tTqBatch = new List<double>();
                var posBatch = new List<double>();
                var tqBatch = new List<double>();

                for (int i = 0; i < ADC_BUFFER_LENGTH; i++)
                {
                    uint pos_val = BitConverter.ToUInt32(data, ADC_BUFFER_LENGTH * sizeof(uint) + i * sizeof(uint));
                    uint tq_val = BitConverter.ToUInt32(data, 3 * ADC_BUFFER_LENGTH * sizeof(uint) + i * sizeof(uint));

                    double posValue = ((double)pos_val / (1 << 23) - 1) * 25;
                    double tqValue = ((double)tq_val / (1 << 23) - 1) * 25;

                    // Add to batch
                    tPosBatch.Add((double)(BitConverter.ToUInt32(data, i * sizeof(uint)))/1000);
                    tTqBatch.Add((double)(BitConverter.ToUInt32(data, 2 * ADC_BUFFER_LENGTH * sizeof(uint) + i * sizeof(uint)))/1000);
                    posBatch.Add((posValue - PosOffset) * PosGain);
                    tqBatch.Add((tqValue - TqOffset) * TqGain);
                }

                tPosCsv.AddRange(tPosBatch);
                tTqCsv.AddRange(tTqBatch);
                PosCsv.AddRange(posBatch);
                TqCsv.AddRange(tqBatch);

                // Refresh the plot
                FeedbackPlot.Clear();
                var scatter = FeedbackPlot.Add.Scatter(tTqBatch, tqBatch);
                scatter.MarkerShape = MarkerShape.None;
                App.Current.Dispatcher.Invoke(() =>
                {
                    // Update GUI
                    PatternPlotUpdated?.Invoke(FeedbackPlot, EventArgs.Empty);
                    // Update Motor Pos
                    MotorPos = $"Motor Pos: {posBatch.Last():F1}";

                    OnPropertyChanged(nameof(MotorPos));
                });
            }
        }
        public void UpdatePlot(List<double> pattern)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                // Generate Time series
                double[] dataX = new double[pattern.Count];
                for (int i = 0; i < pattern.Count; i++)
                    dataX[i] = i * Ts;

                PatternPlot.Clear();
                var scatter = PatternPlot.Add.Scatter(dataX, [.. pattern]);
                scatter.MarkerShape = MarkerShape.None;
                scatter.LineWidth = 1.5f;
                scatter.LineColor = Colors.Blue;
                PatternPlot.Axes.AutoScale();
                PatternPlotUpdated?.Invoke(PatternPlot, EventArgs.Empty);
                ResultPlotHandler?.Invoke(ResultMultiPlot, EventArgs.Empty);
                // Change Pattern tab to Pattern
                PatternTabSelectedIndex = 0;
                OnPropertyChanged(nameof(PatternTabSelectedIndex));
            });
        }

        // Load Final Pattern to a specific variable so it can be download to MCU
        public void LoadFinalPattern(List<double> pattern)
        {
            CommandPattern = pattern;
        }
    }

}
