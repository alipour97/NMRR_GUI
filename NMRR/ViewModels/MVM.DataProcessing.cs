using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMRR.ViewModels
{
    internal partial class MainViewModel
    {
        private void OnDataReceived(string command, byte[] data)
        {
            if (command == "inf")
            {
                string infoString = Encoding.ASCII.GetString(data);
                SerialLog += $"{DateTime.Now}: {infoString}\n";
                OnPropertyChanged(nameof(SerialLog));
            }
            else if (command == "fdb")
            {
                ProcessFeedbackData(data);
            }
        }

        private void ProcessFeedbackData(byte[] data)
        {
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

                tPosBatch.Add((double)(BitConverter.ToUInt32(data, i * sizeof(uint))) / 1000);
                tTqBatch.Add((double)(BitConverter.ToUInt32(data, 2 * ADC_BUFFER_LENGTH * sizeof(uint) + i * sizeof(uint))) / 1000);
                posBatch.Add((posValue - PosOffset) * PosGain);
                tqBatch.Add((tqValue - TqOffset) * TqGain);
            }

            tPosCsv.AddRange(tPosBatch);
            tTqCsv.AddRange(tTqBatch);
            PosCsv.AddRange(posBatch);
            TqCsv.AddRange(tqBatch);

            // Update Motor Position
            App.Current.Dispatcher.Invoke(() =>
            {
                MotorPos = $"Motor Pos: {posBatch.Last():F1}";
                OnPropertyChanged(nameof(MotorPos));
            });

            // if operation mode is set to show feedback, show feedback
            if (showFeedback)
                UpdateFeedbackPlot(tTqBatch, tqBatch, posBatch);
        }
    }
}
