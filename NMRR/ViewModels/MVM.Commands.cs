using NMRR.Helpers;
using ScottPlot.Colormaps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NMRR.ViewModels
{
    internal partial class MainViewModel
    {
        private async void StartReceiving()
        {
            tTqCsv.Clear();
            tPosCsv.Clear();
            TqCsv.Clear();
            PosCsv.Clear();
            //showFeedback = true;
            //CollectData = true;

            await _serialPortService.StartReceivingAsync();
        }

        private async void StopReceiving()
        {
            await _serialPortService.StopReceivingAsync();
        }

        private async void SendCommandToDevice()
        {
            if (!string.IsNullOrEmpty(CommandToSend))
            {
                await _serialPortService.SendDataAsync(CommandToSend);
            }
        }

        private void send_pattern(List<float> pattern)
        {
            if (pattern.Count < MainViewModel.DAC_BULK_SIZE / sizeof(float))
            {
                SendBulk("pattern_init", pattern.Count, pattern.ToArray());
                return;
            }
            else
            {
                int DAC_idx = (MainViewModel.DAC_BULK_SIZE) / sizeof(float);
                SendBulk("pattern_init", pattern.Count, pattern.GetRange(0, DAC_idx).ToArray());
            }
        }

        public async void SendBulk(string message, int length, float[] pattern_bulk)
        {
            string init_message = "{"+ message + "," + length + ",";
            string end_message = "}\r\n";
            byte[] buffer = new byte[init_message.Length + pattern_bulk.Length * sizeof(float) + end_message.Length];
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(init_message), 0, buffer, 0, init_message.Length);
            Buffer.BlockCopy(pattern_bulk, 0, buffer, init_message.Length, pattern_bulk.Length * sizeof(float));
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(end_message), 0, buffer, init_message.Length + pattern_bulk.Length * sizeof(float), end_message.Length);

            await _serialPortService.SendDataAsync(buffer);
        }

        private void SaveToCsv()
        {
            string basePath = @"D:\NMRR_DATA";
            if (string.IsNullOrEmpty(sessionFolderPath) || !Directory.Exists(sessionFolderPath))
            {
                string sessionName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                sessionFolderPath = Path.Combine(basePath, sessionName);
                Directory.CreateDirectory(sessionFolderPath);
            }

            string fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            string filePath = Path.Combine(sessionFolderPath, fileName);
            StringBuilder csvContent = new();

            csvContent.AppendLine("Pos_t,Ref Pos,Pos Value,Pos_encoder,Tq Value");

            for (int i = 0; i < tPosCsv.Count; i++)
            {
                if(i < CommandPattern.Count)
                    csvContent.AppendLine($"{tPosCsv[i]},{CommandPattern[i]},{PosCsv[i]},{tTqCsv[i]},{TqCsv[i]}");
                else
                    csvContent.AppendLine($"{tPosCsv[i]},0,{PosCsv[i]},{tTqCsv[i]},{TqCsv[i]}");
            }

            try
            {
                File.WriteAllText(filePath, csvContent.ToString());
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void onStartBtn()
        {
            tTqCsv.Clear();
            tPosCsv.Clear();
            TqCsv.Clear();
            PosCsv.Clear();
            //CollectData = true;

            try
            {
                //_serialPortService.SendData("{dac_init,end}\r\n");
                PatternTabSelectedIndex = 1;
                OnPropertyChanged(nameof(PatternTabSelectedIndex));
                
                send_pattern(CommandPattern);
                //showFeedback = true;
                
                SetStatus("Loading Pattern", "warning");
            }
            catch (Exception ex)
            {  MessageBox.Show(ex.Message); }
        }

        private void onStopBtn()
        {
            Task.Run(() => _serialPortService.EmergencyStopBtnAsync());
        }

        private void GoTo()
        {
            List<float> pattern = PatternViewModel.Ramp(0, float.Parse(GoToTextBox) - float.Parse(MotorPos), 5);
            CommandPattern = pattern;
            for (int i = 0; i < pattern.Count; i++)
            {
                CommandPattern[i] /= 12;
            }
            send_pattern(CommandPattern);
        }

        private void onVoluntaryBtn()
        {
            _serialPortService = new TcpClientService("192.168.5.250", 7);
            Task.Run(() => InitializeCommunication());
        }
    }
}
