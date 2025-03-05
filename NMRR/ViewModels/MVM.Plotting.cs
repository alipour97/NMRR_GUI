using ScottPlot;
using System;
using System.Collections.Generic;
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

            ResultMultiPlot.AddPlot(PosResultPlot);
            ResultMultiPlot.AddPlot(TqResultPlot);
        }

        private void UpdateFeedbackPlot(List<float> tTqBatch, List<float> tqBatch, List<float> posBatch)
        {
            FeedbackPlot.Clear();
            var scatter = FeedbackPlot.Add.Scatter(tTqBatch.ToArray(), tqBatch.ToArray());
            scatter.MarkerShape = MarkerShape.None;
            FeedbackPlot.Axes.AutoScale();
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
    }
}
