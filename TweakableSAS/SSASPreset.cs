using System;
using System.Collections.Generic;

namespace TweakableSAS
{
    public class SSASPreset
    {
        public string name;
        public double[,] PIDGains = new double[3, 4];

        public SSASPreset(List<PIDErrorController> controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;

            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = controllers[(int)s].PGain;
                PIDGains[(int)s, 1] = controllers[(int)s].IGain;
                PIDGains[(int)s, 2] = controllers[(int)s].DGain;
                PIDGains[(int)s, 3] = controllers[(int)s].Scalar;
            }
        }

        public SSASPreset(PIDErrorController[] controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;
            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = controllers[(int)s].PGain;
                PIDGains[(int)s, 1] = controllers[(int)s].IGain;
                PIDGains[(int)s, 2] = controllers[(int)s].DGain;
                PIDGains[(int)s, 3] = controllers[(int)s].Scalar;
            }
        }

        public SSASPreset(List<double[]> gains, string Name) // used for loading presets from file
        {
            name = Name;
            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = gains[(int)s][0];
                PIDGains[(int)s, 1] = gains[(int)s][1];
                PIDGains[(int)s, 2] = gains[(int)s][2];
                PIDGains[(int)s, 3] = gains[(int)s][3];
            }
        }

        public void Update(List<PIDErrorController> controllers)
        {
            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = controllers[(int)s].PGain;
                PIDGains[(int)s, 1] = controllers[(int)s].IGain;
                PIDGains[(int)s, 2] = controllers[(int)s].DGain;
                PIDGains[(int)s, 3] = controllers[(int)s].Scalar;
            }
        }

        public void Update(PIDErrorController[] controllers)
        {
            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = controllers[(int)s].PGain;
                PIDGains[(int)s, 1] = controllers[(int)s].IGain;
                PIDGains[(int)s, 2] = controllers[(int)s].DGain;
                PIDGains[(int)s, 3] = controllers[(int)s].Scalar;
            }
        }
    }
}
