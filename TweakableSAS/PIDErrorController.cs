﻿using System;
using UnityEngine;

namespace TweakableSAS
{
    public enum PIDmode
    {
        PID,
        PD,
        D
    }

    public class PIDErrorController
    {
        protected double target_setpoint = 0; // target setpoint
        protected double active_setpoint = 0;

        protected double k_proportional; // Kp
        protected double k_integral; // Ki
        protected double k_derivative; // Kd

        protected double sum = 0; // integral sum
        protected double previous = 0; // previous value stored for derivative action
        protected double rolling_diff = 0; // used for rolling average difference
        protected double rollingFactor = 0.5; // rolling average proportion. 0 = all new, 1 = never changes
        protected double error = 0; // error of current iteration

        protected double inMin = -1000000000; // Minimum input value
        protected double inMax = 1000000000; // Maximum input value

        protected double outMin; // Minimum output value
        protected double outMax; // Maximum output value

        protected double integralClampUpper; // AIW clamp
        protected double integralClampLower;

        protected double dt = 1; // standardised response for any physics dt

        protected double scale = 1;
        protected double easing = 1;
        protected double increment = 0;

        public double lastOutput { get; protected set; }
        public bool invertInput { get; set; }
        public bool invertOutput { get; set; }
        public bool bShow { get; set; }
        public bool skipDerivative { get; set; }
        public bool isHeadingControl { get; set; }
        public SASList ctrlID { get; protected set; }

        public PIDErrorController(SASList ID, double Kp, double Ki, double Kd, double OutputMin, double OutputMax, double intClampLower, double intClampUpper, double scalar = 1, double easing = 1)
        {
            ctrlID = ID;
            k_proportional = Kp;
            k_integral = Ki;
            k_derivative = Kd;
            outMin = OutputMin;
            outMax = OutputMax;
            integralClampLower = intClampLower;
            integralClampUpper = intClampUpper;
            scale = scalar;
            this.easing = easing;
        }

        public PIDErrorController(SASList ID, double[] gains)
        {
            ctrlID = ID;
            k_proportional = gains[0];
            k_integral = gains[1];
            k_derivative = gains[2];
            outMin = gains[3];
            outMax = gains[4];
            integralClampLower = gains[5];
            integralClampUpper = gains[6];
            scale = gains[7];
            easing = gains[8];
        }

        public virtual double ResponseD(double error, double rate, PIDmode mode)
        {
            if (invertInput)
            {
                error *= -1;
                rate *= -1;
            }

            double res_d = 0, res_i = 0, res_p = 0;
            res_d = derivativeError(rate);
            if (mode == PIDmode.PID)
                res_i = integralError(error, true);
            if (mode == PIDmode.PD || mode == PIDmode.PID)
                res_p = proportionalError(error);

            lastOutput = (invertOutput ? -1 : 1) * Utils.Clamp(res_p + res_i + res_d, OutMin, OutMax);
            return lastOutput;
        }

        public virtual float ResponseF(double error, double rate, PIDmode mode)
        {
            return (float)ResponseD(error, rate, mode);
        }

        protected virtual double proportionalError(double error)
        {
            return error * k_proportional / scale;
        }

        protected virtual double integralError(double error, bool useIntegral)
        {
            if (k_integral == 0 || !useIntegral)
            {
                sum = 0;
                return sum;
            }

            sum += error * dt * k_integral / scale;
            sum = Utils.Clamp(sum, integralClampLower, integralClampUpper); // AIW
            return sum;
        }

        protected virtual double derivativeError(double rate)
        {
            return rate * k_derivative / scale;
        }

        protected virtual double derivativeErrorRate(double rate)
        {
            return rate * k_derivative / scale;
        }

        public virtual void Clear()
        {
            sum = 0;
        }

        public virtual void Preset(bool invert = false)
        {
            sum = lastOutput * (invert ? -1 : 1);
        }

        public virtual void Preset(double target, bool invert = false)
        {
            if (!invert)
                sum = target * (invertOutput ? -1 : 1);
            else
                sum = target * (invertOutput ? 1 : -1);
        }

        #region properties
        public virtual double SetPoint
        {
            get
            {
                return invertInput ? -target_setpoint : target_setpoint;
            }
            set
            {
                active_setpoint = target_setpoint = invertInput ? -value : value;
            }
        }

        /// <summary>
        /// let active setpoint move to match the target to smooth the transition
        /// </summary>
        public virtual double BumplessSetPoint
        {
            get
            {
                return invertInput ? -active_setpoint : active_setpoint;
            }
            set
            {
                target_setpoint = invertInput ? -value : value;
                increment = 0;
            }
        }

        public virtual double PGain
        {
            get
            {
                return k_proportional;
            }
            set
            {
                k_proportional = value;
            }
        }

        public virtual double IGain
        {
            get
            {
                return k_integral;
            }
            set
            {
                k_integral = value;
            }
        }

        public virtual double DGain
        {
            get
            {
                return k_derivative;
            }
            set
            {
                k_derivative = value;
            }
        }

        public virtual double InMin
        {
            set
            {
                inMin = value;
            }
        }

        public virtual double InMax
        {
            set
            {
                inMax = value;
            }
        }

        /// <summary>
        /// Set output minimum to value
        /// </summary>
        public virtual double OutMin
        {
            get
            {
                return outMin;
            }
            set
            {
                outMin = value;
            }
        }

        /// <summary>
        /// Set output maximum to value
        /// </summary>
        public virtual double OutMax
        {
            get
            {
                return outMax;
            }
            set
            {
                outMax = value;
            }
        }

        public virtual double ClampLower
        {
            get
            {
                return integralClampLower;
            }
            set
            {
                integralClampLower = value;
            }
        }

        public virtual double ClampUpper
        {
            get
            {
                return integralClampUpper;
            }
            set
            {
                integralClampUpper = value;
            }
        }

        public virtual double Scalar
        {
            get
            {
                return scale;
            }
            set
            {
                scale = Math.Max(value, 0.01);
            }
        }

        public virtual double Easing
        {
            get
            {
                return easing;
            }
            set
            {
                easing = Math.Max(value, 0.01);
            }
        }
        #endregion
    }
}
