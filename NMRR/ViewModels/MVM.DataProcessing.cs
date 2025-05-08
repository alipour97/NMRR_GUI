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
            if (command == "inf" | command == "err")
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
                {
                    tTqCsv.Clear();
                    tPosCsv.Clear();
                    TqCsv.Clear();
                    PosCsv.Clear();
                    showFeedback = true;
                    CollectData = true;
                    //await _serialPortService.SendDataAsync("{start,1,end}\r\n");
                    SetStatus("Loading Complete", "success");
                    //MessageBox.Show("Done");
                    return;
                }
                else
                {
                    int length = (CommandPattern.Count < DAC_idx + DAC_BULK_SIZE / sizeof(float)) ? CommandPattern.Count - DAC_idx : DAC_BULK_SIZE / sizeof(float);
                    SendBulk("pattern_bulk", length, CommandPattern.GetRange(DAC_idx, length).ToArray());
                }

            }
            else if (command == "cmd")
            {
                string msgString = Encoding.ASCII.GetString(data);

                if (msgString == "end_pattern")
                {
                    showFeedback = false;
                    SetStatus("Pattern Finished", "success");
                    PlotResults();
                }
            }
        }

        private void ProcessFeedbackData(byte[] data)
        {
            var tPosBatch = new List<float>();
            var tTqBatch = new List<float>();
            var posBatch = new List<float>();
            var tqBatch = new List<float>();
            var tFeedback = new List<float>();

            


            for (int i = 0; i < ADC_BUFFER_LENGTH; i++)
            {
                uint pos_val = BitConverter.ToUInt32(data, ADC_BUFFER_LENGTH * sizeof(uint) + i * sizeof(uint));
                uint tq_val = BitConverter.ToUInt32(data, 3 * ADC_BUFFER_LENGTH * sizeof(uint) + i * sizeof(uint));

                float posValue = ((float)pos_val / (1 << 23) - 1) * 25;
                float tqValue = ((float)tq_val / (1 << 23) - 1) * 25;
                tPosBatch.Add((float)(BitConverter.ToUInt32(data, i * sizeof(uint))) / 1000);
                tTqBatch.Add((float)(BitConverter.ToUInt32(data, 2 * ADC_BUFFER_LENGTH * sizeof(uint) + i * sizeof(uint))));
                posBatch.Add(posValue * PosGain + PosOffset);
                tqBatch.Add(tqValue * TqGain +TqOffset);
                tFeedback.Add((float)i);
            }

            if (CollectData)
            {
                tPosCsv.AddRange(tPosBatch);
                tTqCsv.AddRange(tTqBatch);
                PosCsv.AddRange(posBatch);
                TqCsv.AddRange(tqBatch);
            }

            // Update Motor Position
            App.Current.Dispatcher.Invoke(() =>
            {
                MotorPos = $"{posBatch.Last():F1}";
                int minute = (int)(tPosBatch.Last() / 60000);
                int second = (int)(tPosBatch.Last() / 1000 % 60);
                TimeStr = $"{minute:D2}:{second:D2}";
                OnPropertyChanged(nameof(MotorPos));
                OnPropertyChanged(nameof(TimeStr));
            });

            // if operation mode is set to show feedback, show feedback
            if (showFeedback)
                UpdateFeedbackPlot(tFeedback, tqBatch, posBatch);
            if (CollectData && TqCsv.Count >= CommandPattern.Count)
            {
                CollectData = false;
                showFeedback = false;
                SetStatus("Pattern Finished", "success");
                PlotResults();
            }


        }
    }
}
