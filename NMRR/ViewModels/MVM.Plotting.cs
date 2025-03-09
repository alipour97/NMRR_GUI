using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace NMRR.ViewModels
{
    internal partial class MainViewModel
    {
        private void InitializePlots()
        {
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

            //ResultMultiPlot.AddPlot(PosResultPlot);
            //ResultMultiPlot.AddPlot(TqResultPlot);
        }

        private void UpdateFeedbackPlot(List<float> tTqBatch, List<float> tqBatch, List<float> posBatch)
        {
            FeedbackPlot.Clear();
            var scatter = FeedbackPlot.Add.Scatter(tTqBatch.ToArray(), tqBatch.ToArray());
            scatter.MarkerShape = MarkerShape.None;
            FeedbackPlot.Axes.SetLimitsX(0, 50);
            FeedbackPlot.Axes.SetLimitsY(-50, 50);
            App.Current.Dispatcher.Invoke(() =>
            {
                PatternPlotUpdated?.Invoke("FeedbackPlot", EventArgs.Empty);
            });
        }

        public void UpdatePatternPlot(List<float> pattern)
        {
            LoadFinalPattern(pattern);
            App.Current.Dispatcher.Invoke(() =>
            {
                float[] dataX = new float[pattern.Count];
                for (int i = 0; i < pattern.Count; i++)
                    dataX[i] = i * Ts;

                PatternPlot.Clear();
                var scatter = PatternPlot.Add.Scatter(dataX, pattern.ToArray());
                scatter.MarkerShape = MarkerShape.None;
                scatter.LineWidth = 1.5f;
                scatter.LineColor = Colors.Blue;
                PatternPlot.Axes.AutoScale();
                PatternPlotUpdated?.Invoke("PatternPlot", EventArgs.Empty);
                PatternTabSelectedIndex = 0;
                OnPropertyChanged(nameof(PatternTabSelectedIndex));
            });
        }

        public void PlotResults()
        {
            App.Current.Dispatcher.Invoke(() =>
            {

                List<List<float>> data = [tPosCsv, PosCsv, tTqCsv, TqCsv];
                //Plot PosResultPlot = ResultPlot.Multiplot.GetPlot(0);
                //Plot TqResultPlot = ResultPlot.Multiplot.GetPlot(1);

                //PosResultPlot.Clear();
                //var PosScatter = PosResultPlot.Add.Scatter(tPosCsv, PosCsv);
                //PosScatter.MarkerShape = MarkerShape.None;
                //PosScatter.LineWidth = 1.5f;
                //PosScatter.LineColor = Colors.Blue;
                //PosResultPlot.Axes.AutoScale();

                //TqResultPlot.Clear();
                //var TqScatter = TqResultPlot.Add.Scatter(tTqCsv, TqCsv);
                //TqScatter.MarkerShape = MarkerShape.None;
                //TqScatter.LineWidth = 1.5f;
                //TqScatter.LineColor = Colors.Blue;

                //PatternPlotUpdated?.Invoke("ResultPlot", EventArgs.Empty);
                ResultPlotHandler?.Invoke(data, EventArgs.Empty);

                //ResultMultiPlot
                PatternTabSelectedIndex = 2;
                OnPropertyChanged(nameof(PatternTabSelectedIndex));
            });
        }
        private void SetStatus(string message, string style)
        {
            App.Current.Dispatcher?.Invoke(() =>
            {
                StatusMessage = message;
                
                if (style == "success")
                {
                    StatusBGColor = System.Windows.Media.Brushes.ForestGreen;
                    StatusTimer.Start();
                }
                else if (style == "warning")
                    StatusBGColor = System.Windows.Media.Brushes.Orange;
                else if (style == "error")
                    StatusBGColor = System.Windows.Media.Brushes.Coral;
                else
                    StatusBGColor = null;

                OnPropertyChanged(nameof(StatusMessage));
                OnPropertyChanged(nameof (StatusBGColor));
            });
        }


        // Event handler for Timer Tick
        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            StatusTimer.Stop(); // Stop the timer to prevent repeating
            StatusTimer.Interval = TimeSpan.FromSeconds(1); // Default 1s For Status Timer

            // Return to IDLE
            App.Current.Dispatcher?.Invoke(() =>
            {
                StatusMessage = "IDLE";
                StatusBGColor = null;

                OnPropertyChanged(nameof(StatusMessage));
                OnPropertyChanged(nameof(StatusBGColor));
            });
        }
    }
}
