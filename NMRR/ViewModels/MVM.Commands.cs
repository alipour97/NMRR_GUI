using ScottPlot.Colormaps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NMRR.ViewModels
{
    internal partial class MainViewModel
    {
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

        private void send_pattern(List<float> pattern)
        {
            if(pattern.Count < MainViewModel.DAC_BULK_SIZE)
                SendBulk("pattern_init", pattern.Count, pattern.ToArray());
            else
                SendBulk("pattern_init", pattern.Count, pattern.GetRange(0,MainViewModel.DAC_BULK_SIZE).ToArray());
        }

        public void SendBulk(string message, int length, float[] pattern_bulk)
        {
            string init_message = "{"+ message + "," + length + ",";
            string end_message = "}\r\n";
            byte[] buffer = new byte[init_message.Length + pattern_bulk.Length * sizeof(float) + end_message.Length];
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(init_message), 0, buffer, 0, init_message.Length);
            Buffer.BlockCopy(pattern_bulk, 0, buffer, init_message.Length, pattern_bulk.Length * sizeof(float));
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(end_message), 0, buffer, init_message.Length + pattern_bulk.Length * sizeof(float), end_message.Length);

            _serialPortService.SendData(buffer);
        }

        private void SaveToCsv()
        {
            string filePath = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            StringBuilder csvContent = new();

            csvContent.AppendLine("Pos_t,Ref Pos,Pos Value,Tq_t,Tq Value");

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

        private void WritePattern()
        {
            tTqCsv.Clear();
            tPosCsv.Clear();
            TqCsv.Clear();
            PosCsv.Clear();
            CollectData = true;

            try
            {
                //_serialPortService.SendData("{dac_init,end}\r\n");
                PatternTabSelectedIndex = 1;
                OnPropertyChanged(nameof(PatternTabSelectedIndex));
                
                send_pattern(CommandPattern);
                showFeedback = true;
                
                SetStatus("Loading Pattern", "warning");
            }
            catch (Exception ex)
            {  MessageBox.Show(ex.Message); }
        }
    }
}
