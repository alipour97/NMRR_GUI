using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NMRR.ViewModels;
using ScottPlot;

namespace NMRR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        public MainWindow()
        {
            InitializeComponent();
            DataContext = MainViewModel.Instance;
            PatternPlot.Reset(MainViewModel.Instance.PatternPlot);
            PatternPlot.Refresh();

            FeedbackPlot.Reset(MainViewModel.Instance.FeedbackPlot);
            FeedbackPlot.Refresh();

            //ResultPlot.Reset(MainViewModel.Instance.ResultPlot);
            OnResultPlotHandler(ResultPlot.Multiplot, EventArgs.Empty);
            ResultPlot.Refresh();

            MainViewModel.Instance.ResultPlotHandler += OnResultPlotHandler;
            MainViewModel.Instance.PatternPlotUpdated += OnPlotUpdated;
        }

        private void OnPlotUpdated(object sender, EventArgs e)
        {
            // Refresh the WpfPlot control whenever the plot is updated
            if((string)sender == "PatternPlot")
                PatternPlot.Refresh();
            else if((string)sender == "FeedbackPlot")
                FeedbackPlot.Refresh();
            else if ((string)sender == "ResultPlot")
                ResultPlot.Refresh();

        }

        private void OnResultPlotHandler(object sender, EventArgs e)
        {
            int count = sender is Multiplot mp ? mp.Count : 0;
            if (sender is Multiplot multiplot)
            {
                //for (int i = 0; i < 2; i++)
                //{
                    ResultPlot.Multiplot.AddPlot();
                //}
            }
            else
            {
                List<List<float>> data = (List<List<float>>)sender;
                Plot PosPlot = ResultPlot.Multiplot.GetPlot(0);
                Plot TqPlot = ResultPlot.Multiplot.GetPlot(1);

                PosPlot.Clear();
                var PosScatter = PosPlot.Add.Scatter(data[0], data[1]);
                PosScatter.MarkerShape = MarkerShape.None;
                PosScatter.LineWidth = 1.5f;
                PosScatter.LineColor = ScottPlot.Colors.Blue;
                PosPlot.Axes.AutoScale();

                TqPlot.Clear();
                var TqScatter = TqPlot.Add.Scatter(data[2], data[3]);
                TqScatter.MarkerShape = MarkerShape.None;
                TqScatter.LineWidth = 1.5f;
                TqScatter.LineColor = ScottPlot.Colors.Blue;
                TqPlot.Axes.AutoScale();
                //ResultPlot.Multiplot.Render();

            }
            ResultPlot.Refresh();
        }
    }
}
