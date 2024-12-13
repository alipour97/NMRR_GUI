using System;
using System.Collections.Generic;
using System.Text;

namespace NMRR.Models
{
    internal class DeviceModel
    {
        public uint Time_us { get; set; }
        public double PosValue { get; set; }

        public double Tq_t { get; set; }
        public double TqValue { get; set; }
    }
}
