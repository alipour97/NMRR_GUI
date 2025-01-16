using NMRR.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static System.Runtime.Intrinsics.X86.Avx10v1;

namespace NMRR.ViewModels
{
    class PatternViewModel
    {
        private readonly MainViewModel _mainViewModel;

        // PRBS Params
        public string PRBS_Duration { get; set; } = "10";
        public string PRBS_Amplitude { get; set; } = "2";
        public string PRBS_SR { get; set; } = "20";
        public string PRBS_MaxWidth { get; set; } = "200";
        public string PRBS_Cycles { get; set; } = "1";

        //TV Params

        public string TV_T12 { get; set; } = "1";
        public string TV_T34 { get; set; } = "2";
        public string TV_T56 { get; set; } = "1";

        public string TV_A12 { get; set; } = "0";
        public string TV_A34 { get; set; } = "45";
        public string TV_A56 { get; set; } = "0";

        public string TV_V23 { get; set; } = "5";
        public string TV_V45 { get; set; } = "5";
        public string TV_Reps { get; set; } = "1";
        public bool PRBS_Include { get; set; } = false;

        //Pulse Params
        public string Pulse_Amplitude { get; set; } = "3";
        public string Pulse_Numbers { get; set; } = "1";
        public string Pulse_Width { get; set; } = "100";
        public string Pulse_Period { get; set; } = "300";
        public string Pulse_InitDelay { get; set; } = "200";

        public ICommand PRBS_PlotClick { get; }
        public ICommand Pulse_PlotClick { get; }
        public ICommand TV_PlotClick { get; }
        public PatternViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            PRBS_PlotClick = new RelayCommand(PRBS_PlotClickExecute);
            TV_PlotClick = new RelayCommand(TV_PlotClickExecute);
            Pulse_PlotClick = new RelayCommand(Pulse_PlotClickExecute);
        }
        private void PRBS_PlotClickExecute()
        {
            try
            {
                // Extract values from DynamicResources or other bindings
                //double duration = Convert.ToDouble(PRBS_Duration);
                double duration = Convert.ToDouble(PRBS_Duration);
                double amplitude = Convert.ToDouble(PRBS_Amplitude);
                double switchRate = Convert.ToDouble(PRBS_SR);
                double maxWidth = Convert.ToDouble(PRBS_MaxWidth);
                int cycles = Convert.ToInt16(PRBS_Cycles);

                // Generate PRBS pattern
                List<double> pattern = PRBS(duration, amplitude, switchRate, maxWidth);

                // Pass the pattern to MainViewModel's PlotModel
                MainViewModel.Instance.UpdatePlot(pattern);

                for (int i = 0; i < cycles - 1; i++)
                {
                    pattern.AddRange(PRBS(duration, amplitude, switchRate, maxWidth));
                }

                MainViewModel.Instance.LoadFinalPattern(pattern); // Load Final Pattern to a specific variable so it can be download to MCU
            }
            catch (Exception ex)
            {
                // Handle exceptions
                MessageBox.Show($"Error generating PRBS pattern: {ex.Message}");
            }
        }

        private void TV_PlotClickExecute()
        {
            try
            {
                // Extract values from DynamicResources
                double t12 = Convert.ToDouble(TV_T12);
                double t34 = Convert.ToDouble(TV_T34);
                double t56 = Convert.ToDouble(TV_T56);

                double ang12 = Convert.ToDouble(TV_A12);
                double ang34 = Convert.ToDouble(TV_A34);
                double ang56 = Convert.ToDouble(TV_A56);

                double v23 = Convert.ToDouble(TV_V23);
                double v45 = Convert.ToDouble(TV_V45);

                int repeats = Convert.ToInt16(TV_Reps);

                // Generate the Time Varying Pattern
                List<double> pattern =
                [
                    .. Hold(t12, ang12),
                    .. Ramp(ang12, ang34, v23, 0),
                    .. Hold(t34, ang34),
                    .. Ramp(ang34, ang56, v45, 0),
                    .. Hold(t56, ang56),
                ];

                // Check PRBS inclusion
                bool includePrbs = Convert.ToBoolean(PRBS_Include);
                if (includePrbs)
                {
                    double amp = Convert.ToDouble(PRBS_Amplitude);
                    double sw = Convert.ToDouble(PRBS_SR);
                    double maxwidth = Convert.ToDouble(PRBS_MaxWidth);

                    List<double> prbs = PRBS(pattern.Count * MainViewModel.Ts, amp, sw, maxwidth, 1);
                    for (int t = 0; t < pattern.Count; t++)
                        pattern[t] += prbs[t];
                }

                // Repeat the pattern
                pattern = numCycles(pattern, repeats);

                // Pass the generated pattern to MainViewModel's PlotModel
                _mainViewModel.UpdatePlot(pattern);
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., invalid conversions or logic errors)
                MessageBox.Show($"Error generating TV pattern: {ex.Message}");
            }

        }
        private void Pulse_PlotClickExecute()
        {
            double period = Convert.ToDouble(Pulse_Period);
            double amplitude = Convert.ToDouble(Pulse_Amplitude);
            
            double init_delay = Convert.ToDouble(Pulse_InitDelay);
            double width = Convert.ToDouble(Pulse_Width);
            int numbers = Convert.ToInt16(Pulse_Numbers);
            List<double> pattern = Pulse(period, amplitude, init_delay, width, numbers);

            // Pass the pattern to MainViewModel's PlotModel
            MainViewModel.Instance.UpdatePlot(pattern);
        }

        private static List<double> Ramp(double ang1, double ang2, double velocity, double tHold = 0.5)
        {
            double amplitude = ang2 - ang1;
            // velocity is defined to have same sign as amplitude
            velocity = Math.Sign(amplitude) * Math.Abs(velocity);
            double tRamp = amplitude / velocity;
            List<double> pattern = new List<double>();
            for (int t = 0; t < (int)(tRamp / MainViewModel.Ts); t++)
                pattern.Add(velocity * t * MainViewModel.Ts + ang1);
            for (int t = 0; t < (int)(tHold / MainViewModel.Ts); t++)
                pattern.Add(pattern.Last());
            return pattern;
        }

        private static List<double> Hold(double duration, double amplitude)
        {
            List<double> pattern = new List<double>();
            for (int t = 0; t < duration / MainViewModel.Ts; t++)
                pattern.Add(amplitude);
            return pattern;
        }

        private static List<double> Pulse(double period, double amplitude, double init_delay, double width, int numbers = 1)
        {
            // Change time scalge to seconds
            init_delay /= 1000;
            period /= 1000;
            width /= 1000;

            List<double> pattern = [.. Hold(init_delay, 0)];
            for (int num  = 0; num < numbers; num++)
            {
                pattern.AddRange(Hold(width, amplitude));
                pattern.AddRange(Hold(period-width, 0));
            }

            return pattern;
        }

        private static List<double> PRBS(double duration, double amplitude, double switchRate, double maxWidth, double nCycles = 1)
        {
            List<double> pattern = new List<double>();
            switchRate *= MainViewModel.Ts;
            maxWidth *= MainViewModel.Ts;
            // duration, switchRate, Ts in seconds, amplitude in deg
            int nSwitch = (int)Math.Floor(duration / switchRate); // maximum number of high/low transitions
            int np = (int)Math.Floor(Math.Abs(maxWidth / switchRate)); // maximum width of high/low transitions (per switchRate)

            // generate edges
            // get pulse widths (in number of switchRate units)
            List<int> edges = [];
            int cumsum = 0;
            Random random = new();
            for (int j = 0; j < nSwitch; j++)
            {
                edges.Add((int)Math.Ceiling((decimal)random.Next(1, np))); // get pulse widths (in number of switchRate units)
                cumsum += edges[j];
                if (cumsum > nSwitch) // limit to 'nSwitch' transitions
                    break;
            }

            List<bool> stim = new(nSwitch); // pre-allocate binary sequence
            int hilo = 0;

            // build 'stim' vector with high/low transitions every 'edges' number of points
            foreach (int e in edges)
            {
                int deltaT = (int)Math.Floor(switchRate / MainViewModel.Ts); // number of points in each switching interval
                for (int j = 0; j < e * deltaT; j++)
                    pattern.Add(amplitude * hilo);
                hilo = (hilo == 1) ? 0 : 1; // switch from high to low and vice versa
            }
            // ensure that ends are low
            if (pattern.Count > 0)
            {
                pattern[0] = 0;
                pattern[^1] = 0;
            }

            return pattern;
        }

        private static List<double> numCycles(List<double> pattern, int repeats)
        {
            // Repeat the pattern "repeats" number of times
            List<double> fullPattern = new List<double>();
            for (int i = 0; i < repeats; i++)
            {
                fullPattern.AddRange(pattern);
            }
            return fullPattern;
        }


    }
}
