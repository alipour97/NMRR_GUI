using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using NMRR.Models;
using System.Windows.Input;
using NMRR.Helpers;
using System.IO;
using LiveCharts;
using System.Linq;

namespace NMRR.ViewModels
{
    internal class MainViewModel : BaseViewModel
    {
        private readonly SerialPortService _serialPortService;

        public const int ADC_CHANNELS = 2;
        public const int ADC_BUFFER_LENGTH = 50;

        public ObservableCollection<DeviceModel> DataPoints { get; set; }
        public double[] ADC_Buffer;
        public uint[] Time_Buffer;

        private List<double> tPosCsv;
        private List<double> tTqCsv;
        private List<double> PosCsv;
        private List<double> TqCsv;

        public ChartValues<double> PosValues { get; set; } // Values for PosValue
        public ChartValues<double> TqValues { get; set; }  // Values for TqValue
        public string CommandToSend { get; set; }
        public string SerialLog { get; set; } = string.Empty;

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SendCommand { get; }
        public ICommand SaveToCsvCommand { get; }



        public MainViewModel()
        {

            _serialPortService = new SerialPortService();
            DataPoints = new ObservableCollection<DeviceModel>();

            PosValues = new ChartValues<double>(); // Initialize PosValue chart
            TqValues = new ChartValues<double>();  // Initialize TqValue chart

            StartCommand = new RelayCommand(StartReceiving);
            StopCommand = new RelayCommand(StopReceiving);
            SendCommand = new RelayCommand(SendCommandToDevice);
            SaveToCsvCommand = new RelayCommand(SaveToCsv);

            _serialPortService.DataReceived += OnDataReceived;
            ADC_Buffer = new double[50];
            Time_Buffer = new uint[50];

            tPosCsv = new List<double>();
            PosCsv = new List<double>();
            tTqCsv = new List<double>();
            TqCsv = new List<double>();
        }

        private void StartReceiving()
        {
            ADC_Buffer = new double[50];
            Time_Buffer = new uint[50];
            PosValues.Clear(); // Clear PosValue chart
            TqValues.Clear();  // Clear TqValue chart
            tTqCsv.Clear();
            tPosCsv.Clear();

            DataPoints.Clear();
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
            StringBuilder csvContent = new StringBuilder();

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
                    posBatch.Add(posValue);
                    tqBatch.Add(tqValue);

                }

                tPosCsv.AddRange(tPosBatch);
                tTqCsv.AddRange(tTqBatch);
                PosCsv.AddRange(posBatch);
                TqCsv.AddRange(tqBatch);

                App.Current.Dispatcher.Invoke(() =>
                {
                    PosValues.Clear();
                    TqValues.Clear();
                    //if (PosValues.Count > 100) PosValues.RemoveAt(0);
                    //if (TqValues.Count > 100) TqValues.RemoveAt(0);
                    // Add batch data to charts
                    PosValues.AddRange(posBatch);
                    TqValues.AddRange(tqBatch);


                    // Notify the UI of property changes
                    OnPropertyChanged(nameof(PosValues));
                    OnPropertyChanged(nameof(TqValues));

                    //DataPoints.Add(new DeviceModel
                    //{
                    //    Time_us = BitConverter.ToUInt32(data, i * sizeof(uint)),
                    //    PosValue = posV,
                    //    Tq_t = BitConverter.ToUInt32(data, 2 * ADC_BUFFER_LENGTH * sizeof(uint) + i * sizeof(uint)),
                    //    TqValue = tqValue
                    //    //ADCValue = (double)val
                    //});

                });
            }
        }
        private void OnDataReceived2(string command, byte[] data)
        {
            App.Current.Dispatcher.Invoke(() =>
            {

                //if (double.TryParse(data, out double adcValue))
                //{
                //    DataPoints.Add(new DeviceModel
                //    {
                //        Timestamp = DateTime.Now,
                //        ADCValue = adcValue
                //    });
                //}\
                string data_string = System.Text.Encoding.ASCII.GetString(data);
                if (data_string.EndsWith(",end}"))
                {
                    if (data_string.StartsWith("{fb,"))
                    {
                        for (int i = 0; i < 50; i++)
                        {
                            uint val = 0;
                            for (int j = sizeof(uint) - 1; j >= 0; j--)
                            {
                                val <<= 8;
                                val += (uint)(data[4 + sizeof(uint) * i + j]);
                            }
                            ADC_Buffer[i] = ((double)val / (1 << 23) - 1) * 25;

                            val = 0;
                            for (int j = 3; j >= 0; j--)
                            {
                                val <<= 8;
                                val += (uint)(data[4 + 50 * sizeof(uint) + sizeof(uint) * i + j]);
                            }
                            Time_Buffer[i] = val;
                            //UInt32 val = (UInt32)(data[4 + 2 << i] + 4<<data[2 << i] + 8<<data[2 << i] + 16<<data[2 << i]);

                            DataPoints.Add(new DeviceModel
                            {
                                Time_us = Time_Buffer[i],
                                PosValue = ADC_Buffer[i]
                            });
                        }
                        
                    }
                    else
                    {
                        SerialLog += $"{DateTime.Now}: {data}\n";
                        OnPropertyChanged(nameof(SerialLog));
                    }
                } 
                else
                {
                    SerialLog += $"{DateTime.Now}: {data}\n";
                    OnPropertyChanged(nameof(SerialLog));
                }
                
            });
            
        }
    }
}
