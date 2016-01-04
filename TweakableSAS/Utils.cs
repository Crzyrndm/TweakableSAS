using System;
using System.Linq;
using UnityEngine;

namespace TweakableSAS
{
    public static class Utils
    {
        public static T Clamp<T>(this T val, T min, T max) where T : System.IComparable<T>
        {
            if (val.CompareTo(min) < 0)
                return min;
            else if (val.CompareTo(max) > 0)
                return max;
            else
                return val;
        }

        public static Vector3d projectOnPlane(this Vector3d vector, Vector3d planeNormal)
        {
            return vector - Vector3d.Project(vector, planeNormal);
        }


        public static string TryGetValue(this ConfigNode node, string key, string defaultValue)
        {
            if (node.HasValue(key))
                return node.GetValue(key);
            return defaultValue;
        }

        public static bool TryGetValue(this ConfigNode node, string key, bool defaultValue)
        {
            bool val;
            if (node.HasValue(key) && bool.TryParse(node.GetValue(key), out val))
                return val;
            return defaultValue;
        }

        public static int TryGetValue(this ConfigNode node, string key, int defaultValue)
        {
            int val;
            if (node.HasValue(key) && int.TryParse(node.GetValue(key), out val))
                return val;
            return defaultValue;
        }

        public static float TryGetValue(this ConfigNode node, string key, float defaultValue)
        {
            float val;
            if (node.HasValue(key) && float.TryParse(node.GetValue(key), out val))
                return val;
            return defaultValue;
        }

        public static double TryGetValue(this ConfigNode node, string key, double defaultValue)
        {
            double val;
            if (node.HasValue(key) && double.TryParse(node.GetValue(key), out val))
                return val;
            return defaultValue;
        }

        public static KeyCode TryGetValue(this ConfigNode node, string key, KeyCode defaultValue)
        {
            if (node.HasValue(key))
            {
                try
                {
                    KeyCode val = (KeyCode)System.Enum.Parse(typeof(KeyCode), node.GetValue(key));
                    return val;
                }
                catch { }
            }
            return defaultValue;
        }

        public static Rect TryGetValue(this ConfigNode node, string key, Rect defaultValue)
        {
            if (node.HasValue(key))
            {
                string[] stringVals = node.GetValue(key).Split(',').Select(s => s.Trim(new char[] { ' ', '(', ')' })).ToArray();
                if (stringVals.Length != 4)
                    return defaultValue;
                float x = 0, y = 0, w = 0, h = 0;
                if (!float.TryParse(stringVals[0].Substring(2), out x) || !float.TryParse(stringVals[1].Substring(2), out y) || !float.TryParse(stringVals[2].Substring(6), out w) || !float.TryParse(stringVals[3].Substring(7), out h))
                {
                    Debug.LogError(x + "," + y + "," + w + "," + h);
                    return defaultValue;
                }
                return new Rect(x, y, w, h);
            }
            return defaultValue;
        }
    }
}
