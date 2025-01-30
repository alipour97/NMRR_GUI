using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        private void SaveToCsv()
        {
            string filePath = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            StringBuilder csvContent = new();

            csvContent.AppendLine("Pos_t,Pos Value,Tq_t,Tq Value");

            for (int i = 0; i < tPosCsv.Count; i++)
            {
                csvContent.AppendLine($"{tPosCsv[i]},{PosCsv[i]},{tTqCsv[i]},{TqCsv[i]}");
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
    }
}
