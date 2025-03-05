using ScottPlot.Colormaps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
            else if (command == "dac")
            {
                string msgString = Encoding.ASCII.GetString(data);
                int DAC_idx = Convert.ToInt32(msgString);
                if (DAC_idx >= CommandPattern.Count)
                    return;
                else
                {
                    int length = (CommandPattern.Count < DAC_idx + DAC_BULK_SIZE) ? CommandPattern.Count % DAC_BULK_SIZE : DAC_BULK_SIZE;
                    SendBulk("pattern_bulk", length, CommandPattern.GetRange(DAC_idx, length).ToArray());
                }
                
            }
        }

        private void ProcessFeedbackData(byte[] data)
        {
            var tPosBatch = new List<float>();
            var tTqBatch = new List<float>();
            var posBatch = new List<float>();
            var tqBatch = new List<float>();

            for (int i = 0; i < ADC_BUFFER_LENGTH; i++)
            {
                uint pos_val = BitConverter.ToUInt32(data, ADC_BUFFER_LENGTH * sizeof(uint) + i * sizeof(uint));
                uint tq_val = BitConverter.ToUInt32(data, 3 * ADC_BUFFER_LENGTH * sizeof(uint) + i * sizeof(uint));

                float posValue = ((float)pos_val / (1 << 23) - 1) * 25;
                float tqValue = ((float)tq_val / (1 << 23) - 1) * 25;

                tPosBatch.Add((float)(BitConverter.ToUInt32(data, i * sizeof(uint))) / 1000);
                tTqBatch.Add((float)(BitConverter.ToUInt32(data, 2 * ADC_BUFFER_LENGTH * sizeof(uint) + i * sizeof(uint))) / 1000);
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
