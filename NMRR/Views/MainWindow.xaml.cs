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
            ResultPlot.Refresh();

            MainViewModel.Instance.ResultPlotHandler += onResultPlotHandler;
            MainViewModel.Instance.PatternPlotUpdated += OnPlotUpdated;
        }

        private void OnPlotUpdated(object sender, EventArgs e)
        {
            // Refresh the WpfPlot control whenever the plot is updated
            if(sender == PatternPlot)
                PatternPlot.Refresh();
            else if(sender == FeedbackPlot)
                FeedbackPlot.Refresh();
            else if (sender == ResultPlot)
                ResultPlot.Refresh();

        }

        private void onResultPlotHandler(object sender, EventArgs e)
        {
            if (sender is Multiplot multiplot)
            {
                for (int i = 0; i < multiplot.Count; i++)
                {
                    ResultPlot.Multiplot.AddPlot(multiplot.GetPlot(i));
                }
                ResultPlot.Refresh();
            }
        }
    }
}
