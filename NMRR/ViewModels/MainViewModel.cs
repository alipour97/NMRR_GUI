using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using NMRR.Models;
using System.Windows.Input;
using NMRR.Helpers;
using System.IO;

namespace NMRR.ViewModels
{
    internal class MainViewModel : BaseViewModel
    {
        private readonly SerialPortService _serialPortService;

        public ObservableCollection<DeviceModel> DataPoints { get; set; }
        public double[] ADC_Buffer;
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

            StartCommand = new RelayCommand(StartReceiving);
            StopCommand = new RelayCommand(StopReceiving);
            SendCommand = new RelayCommand(SendCommandToDevice);
            SaveToCsvCommand = new RelayCommand(SaveToCsv);

            _serialPortService.DataReceived += OnDataReceived;
            ADC_Buffer = new double[50];
        }

        private void StartReceiving()
        {
            ADC_Buffer = new double[50];
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

            csvContent.AppendLine("Timestamp,ADCValue");

            foreach (var data in DataPoints)
            {
                csvContent.AppendLine($"{data.Timestamp},{data.ADCValue}");
            }

            File.WriteAllText(filePath, csvContent.ToString());
        }

        private void OnDataReceived(string data)
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
                if(data.EndsWith(",end}"))
                {
                    if (data.StartsWith("{fb,"))
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            UInt32 val = 0;
                            for (int j = 3; j >= 0; j--)
                            {
                                val <<= 8;
                                val += (UInt32)(data[4 + 4 * i + j]);
                            }
                            //UInt32 val = (UInt32)(data[4 + 2 << i] + 4<<data[2 << i] + 8<<data[2 << i] + 16<<data[2 << i]);
                            ADC_Buffer[i] = ((double)val/ (1 << 23) - 1) * 25;
                            DataPoints.Add(new DeviceModel
                            {
                                Timestamp = DateTime.Now,
                                ADCValue = ADC_Buffer[i]
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
