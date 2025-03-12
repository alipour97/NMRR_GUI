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
                //float duration = Convert.ToFloat(PRBS_Duration);
                float duration = float.Parse(PRBS_Duration);
                float amplitude = float.Parse(PRBS_Amplitude);
                float switchRate = float.Parse(PRBS_SR);
                float maxWidth = float.Parse(PRBS_MaxWidth);
                int cycles = Convert.ToInt16(PRBS_Cycles);

                // Generate PRBS pattern
                List<float> pattern = PRBS(duration, amplitude, switchRate, maxWidth);

                // Pass the pattern to MainViewModel's PlotModel
                MainViewModel.Instance.UpdatePatternPlot(pattern);

                for (int i = 0; i < cycles - 1; i++)
                {
                    pattern.AddRange(PRBS(duration, amplitude, switchRate, maxWidth));
                }

                //MainViewModel.Instance.LoadFinalPattern(pattern); // Load Final Pattern to a specific variable so it can be download to MCU
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
                float t12 = float.Parse(TV_T12);
                float t34 = float.Parse(TV_T34);
                float t56 = float.Parse(TV_T56);

                float ang12 = float.Parse(TV_A12);
                float ang34 = float.Parse(TV_A34);
                float ang56 = float.Parse(TV_A56);

                float v23 = float.Parse(TV_V23);
                float v45 = float.Parse(TV_V45);

                int repeats = Convert.ToInt16(TV_Reps);

                // Generate the Time Varying Pattern
                List<float> pattern =
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
                    float amp = float.Parse(PRBS_Amplitude);
                    float sw = float.Parse(PRBS_SR);
                    float maxwidth = float.Parse(PRBS_MaxWidth);

                    List<float> prbs = PRBS(pattern.Count * MainViewModel.Ts, amp, sw, maxwidth, 1);
                    for (int t = 0; t < pattern.Count; t++)
                        pattern[t] += prbs[t];
                }
                // Repeat the pattern
                pattern = numCycles(pattern, repeats);
                // Pass the generated pattern to MainViewModel's PlotModel
                _mainViewModel.UpdatePatternPlot(pattern);
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., invalid conversions or logic errors)
                MessageBox.Show($"Error generating TV pattern: {ex.Message}");
            }

        }
        private void Pulse_PlotClickExecute()
        {
            float period = float.Parse(Pulse_Period);
            float amplitude = float.Parse(Pulse_Amplitude);
            
            float init_delay = float.Parse(Pulse_InitDelay);
            float width = float.Parse(Pulse_Width);
            int numbers = Convert.ToInt16(Pulse_Numbers);
            List<float> pattern = Pulse(period, amplitude, init_delay, width, numbers);

            // Pass the pattern to MainViewModel's PlotModel
            MainViewModel.Instance.UpdatePatternPlot(pattern);
        }

        public static List<float> Ramp(float ang1, float ang2, float velocity, float tHold = 0.5F)
        {
            float amplitude = ang2 - ang1;
            // velocity is defined to have same sign as amplitude
            velocity = Math.Sign(amplitude) * Math.Abs(velocity);
            float tRamp = amplitude / velocity;
            List<float> pattern = new List<float>();
            for (int t = 0; t < (int)(tRamp / MainViewModel.Ts); t++)
                pattern.Add(velocity * t * MainViewModel.Ts + ang1);
            for (int t = 0; t < (int)(tHold / MainViewModel.Ts); t++)
                pattern.Add(pattern.Last());
            return pattern;
        }

        private static List<float> Hold(float duration, float amplitude)
        {
            List<float> pattern = new List<float>();
            for (int t = 0; t < duration / MainViewModel.Ts; t++)
                pattern.Add(amplitude);
            return pattern;
        }

        private static List<float> Pulse(float period, float amplitude, float init_delay, float width, int numbers = 1)
        {
            // Change time scalge to seconds
            init_delay /= 1000;
            period /= 1000;
            width /= 1000;

            List<float> pattern = [.. Hold(init_delay, 0)];
            for (int num  = 0; num < numbers; num++)
            {
                pattern.AddRange(Hold(width, amplitude));
                pattern.AddRange(Hold(period-width, 0));
            }

            return pattern;
        }

        private static List<float> PRBS(float duration, float amplitude, float switchRate, float maxWidth, float nCycles = 1)
        {
            List<float> pattern = new();
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

        private static List<float> numCycles(List<float> pattern, int repeats)
        {
            // Repeat the pattern "repeats" number of times
            List<float> fullPattern = new List<float>();
            for (int i = 0; i < repeats; i++)
            {
                fullPattern.AddRange(pattern);
            }
            return fullPattern;
        }


    }
}
