using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TweakableSAS
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class PresetManager : MonoBehaviour
    {
        private static PresetManager instance;
        public static PresetManager Instance
        {
            get
            {
                return instance;
            }
        }

        public List<SSASPreset> SSASPresetList = new List<SSASPreset>();
        public SSASPreset activeSSASPreset = null;

        const string presetsPath = "GameData/Pilot Assistant/Presets.cfg";
        const string defaultsPath = "GameData/Pilot Assistant/Defaults.cfg";

        const string craftDefaultName = "default";
        const string asstDefaultName = "default";
        const string ssasDefaultName = "SSAS";
        const string SASDefaultName = "stock";
        const string RSASDefaultName = "RSAS";

        const string craftPresetNodeName = "CraftPreset";
        const string asstPresetNodeName = "PIDPreset";
        const string sasPresetNodeName = "SASPreset";
        const string rsasPresetNodeName = "RSASPreset";
        const string ssasPresetNodeName = "SSASPreset";

        const string craftAsstKey = "pilot";
        const string craftSSASKey = "ssas";
        const string craftSASKey = "stock";
        const string craftRSASKey = "rsas";

        const string hdgCtrlr = "HdgBankController";
        const string yawCtrlr = "HdgYawController";
        const string aileronCtrlr = "AileronController";
        const string rudderCtrlr = "RudderController";
        const string altCtrlr = "AltitudeController";
        const string vertCtrlr = "AoAController";
        const string elevCtrlr = "ElevatorController";
        const string speedCtrlr = "SpeedController";
        const string accelCtrlr = "AccelController";

        const string pGain = "PGain";
        const string iGain = "IGain";
        const string dGain = "DGain";
        const string min = "MinOut";
        const string max = "MaxOut";
        const string iLower = "ClampLower";
        const string iUpper = "ClampUpper";
        const string scalar = "Scalar";
        const string ease = "Ease";
        const string delay = "Delay";

        double[] defaultPresetPitchGains = { 0.15, 0.0, 0.06, 3, 20 }; // Kp/i/d, scalar, delay
        double[] defaultPresetRollGains = { 0.1, 0.0, 0.06, 3, 20 };
        double[] defaultPresetHdgGains = { 0.15, 0.0, 0.06, 3, 20 };

        public void Start()
        {
            instance = this;

            loadPresetsFromFile();
            DontDestroyOnLoad(this);
        }

        public void OnDestroy()
        {
            saveToFile();
        }

        public static void loadPresetsFromFile()
        {
            SSASPreset SSASDefault = null;

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(ssasPresetNodeName))
            {
                if (ReferenceEquals(node, null))
                    continue;

                List<double[]> gains = new List<double[]>();
                gains.Add(controllerSASGains(node.GetNode(elevCtrlr), SASList.Pitch));
                gains.Add(controllerSASGains(node.GetNode(aileronCtrlr), SASList.Bank));
                gains.Add(controllerSASGains(node.GetNode(rudderCtrlr), SASList.Hdg));

                string name = node.GetValue("name");
                if (name == ssasDefaultName)
                    SSASDefault = new SSASPreset(gains, name);
                else if (!instance.SSASPresetList.Any(p => p.name == name))
                    instance.SSASPresetList.Add(new SSASPreset(gains, name));
            }
        }

        public static void saveToFile()
        {
            ConfigNode node = new ConfigNode();
            node.AddValue("dummy", "do not delete me");
            foreach (SSASPreset p in instance.SSASPresetList)
            {
                node.AddNode(SSASPresetNode(p));
            }
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + presetsPath);
        }

        //public static void saveDefaults()
        //{
        //    ConfigNode node = new ConfigNode();
        //    if (!ReferenceEquals(SSASPreset, null))
        //        node.AddNode(SSASPresetNode(cP.SSASPreset));

        //    node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + defaultsPath);
        //}

        //public static void updateDefaults()
        //{
        //    instance.craftPresetDict[craftDefaultName].AsstPreset.PIDGains = instance.activeAsstPreset.PIDGains;
        //    instance.craftPresetDict[craftDefaultName].SSASPreset.PIDGains = instance.activeSSASPreset.PIDGains;
        //    instance.craftPresetDict[craftDefaultName].SASPreset.PIDGains = instance.activeSASPreset.PIDGains;
        //    instance.craftPresetDict[craftDefaultName].RSASPreset.PIDGains = instance.activeRSASPreset.PIDGains;

        //    saveDefaults();
        //}

        

        public static double[] controllerSASGains(ConfigNode node, SASList type)
        {
            double[] gains = new double[5];

            if (ReferenceEquals(node, null))
                return defaultControllerGains(type);

            double.TryParse(node.GetValue(pGain), out gains[0]);
            double.TryParse(node.GetValue(iGain), out gains[1]);
            double.TryParse(node.GetValue(dGain), out gains[2]);
            double.TryParse(node.GetValue(scalar), out gains[3]);
            double.TryParse(node.GetValue(delay), out gains[4]);

            return gains;
        }

        public static double[] defaultControllerGains(SASList type)
        {
            switch (type)
            {
                case SASList.Pitch:
                    return Instance.defaultPresetPitchGains;
                case SASList.Bank:
                    return Instance.defaultPresetRollGains;
                case SASList.Hdg:
                    return Instance.defaultPresetHdgGains;
                default:
                    return Instance.defaultPresetPitchGains;
            }
        }

        

        public static ConfigNode SSASPresetNode(SSASPreset preset)
        {
            ConfigNode node = new ConfigNode(ssasPresetNodeName);
            node.AddValue("name", preset.name);
            node.AddNode(PIDnode(aileronCtrlr, (int)SASList.Bank, preset));
            node.AddNode(PIDnode(rudderCtrlr, (int)SASList.Hdg, preset));
            node.AddNode(PIDnode(elevCtrlr, (int)SASList.Pitch, preset));

            return node;
        }

        

        public static ConfigNode PIDnode(string name, int index, SSASPreset preset)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue(pGain, preset.PIDGains[index, 0]);
            node.AddValue(iGain, preset.PIDGains[index, 1]);
            node.AddValue(dGain, preset.PIDGains[index, 2]);
            node.AddValue(scalar, preset.PIDGains[index, 3]);
            return node;
        }

        #region SSAS Preset
        public static void newSSASPreset(ref string name, PIDErrorController[] controllers, Vessel v)
        {
            if (string.IsNullOrEmpty(name))
                return;

            string nameTest = name;
            if (Instance.SSASPresetList.Any(p => p.name == nameTest))
                return;

            SSASPreset newPreset = new SSASPreset(controllers, name);
            Instance.SSASPresetList.Add(newPreset);
            Instance.activeSSASPreset = Instance.SSASPresetList.Last();
            saveToFile();
            name = "";
        }

        public static void loadSSASPreset(SSASPreset p, TweakableSAS instance)
        {
            PIDErrorController[] c = instance.SASControllers;

            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                c[(int)s].PGain = p.PIDGains[(int)s, 0];
                c[(int)s].IGain = p.PIDGains[(int)s, 1];
                c[(int)s].DGain = p.PIDGains[(int)s, 2];
                c[(int)s].Scalar = p.PIDGains[(int)s, 3];
            }

            Instance.activeSSASPreset = p;
            saveToFile();
        }

        public static void UpdateSSASPreset(TweakableSAS instance)
        {
            Instance.activeSSASPreset.Update(instance.SASControllers);
            saveToFile();
        }

        public static void deleteSSASPreset(SSASPreset p)
        {
            if (Instance.activeSSASPreset == p)
                Instance.activeSSASPreset = null;
            Instance.SSASPresetList.Remove(p);
            p = null;
            saveToFile();
        }
        #endregion
    }
}
