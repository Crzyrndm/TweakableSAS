using System;
using System.Collections;
using UnityEngine;

namespace TweakableSAS
{
    public enum SASList
    {
        Pitch,
        Bank,
        Hdg
    }

    enum myStyles
    {
        labelAlert,
        numBoxLabel,
        numBoxText,
        btnPlus,
        btnMinus,
        btnToggle,
        greenTextBox,
        redButtonText,
        lblToggle
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class TweakableSAS : MonoBehaviour
    {
        #region Globals
        public static GUISkin UISkin;
        public static Color stockBackgroundColor;
        public VesselData flightData = new VesselData();
        public Vessel ves;
        public PIDErrorController[] SASControllers = new PIDErrorController[3]; // controller per axis

        public bool bArmed = false; // if armed, SAS toggles activate/deactivate SSAS
        public bool[] bActive = new bool[3]; // activate on per axis basis
        public bool[] bPause = new bool[3]; // pause on a per axis basis

        public string[] targets = { "0.00", "0.00", "0.00" };

        readonly double bankAngleSynch = 5; // the bank angle below which pitch and yaw unlock seperately

        public static Rect SSASwindow = new Rect(10, 505, 100, 30); // gui window rect
        string newPresetName = string.Empty;
        static Rect SSASPresetwindow = new Rect(550, 50, 50, 50);
        static bool bShowSSASPresets = false;
        public static bool bDisplay, bShowTooltips;

        //initialisation and default presets stuff
        // kp, ki, kd, outMin, outMax, iMin, iMax, scalar, easing(unused)
        public readonly static double[] defaultPitchGains = { 0.22, 0.12, 0.3, -1, 1, -1, 1, 1, 200 };
        public readonly static double[] defaultRollGains = { 0.25, 0.1, 0.09, -1, 1, -1, 1, 1, 200 };
        public readonly static double[] defaultHdgGains = { 0.22, 0.12, 0.3, -1, 1, -1, 1, 1, 200 };

        public Quaternion currentTarget = Quaternion.identity;

        VesselAutopilot.AutopilotMode APMode = VesselAutopilot.AutopilotMode.StabilityAssist;
        FlightUIController.SpeedDisplayModes spdMode = FlightUIController.SpeedDisplayModes.Surface;

        public static GUIContent KpLabel = new GUIContent("Kp", "Kp is the proportional response factor. The greater the error between the current state and the target, the greater the impact it has. \r\n\r\nP_res = Kp * error");
        public static GUIContent KiLabel = new GUIContent("Ki", "Ki is the integral response factor. The integral response is the sum of all previous errors and depends on both the magnitude and the duration for which the error remained.\r\n\r\nI_res = Ki * sumOf(error)");
        public static GUIContent KdLabel = new GUIContent("Kd", "Kd is the derivative response factor. The derivative response acts to prevent the output from changing and will dampen out oscillations when used in moderation.\r\n\r\nD_res = Kd * (error - prev_error)");
        public static GUIContent ScalarLabel = new GUIContent("Scalar", "The scalar factor increase/decrease the impact of Kp, Ki, and Kd. This is used to accomodate variations in flight conditions.\r\n\r\nOutput = (P_res + I_res + D_res) / Scalar");
        public static GUIContent IMaxLabel = new GUIContent("I Max", "The maximum value the integral sum can reach. This is mostly used to prevent excessive buildup when the setpoint is changed");
        public static GUIContent IMinLabel = new GUIContent("I Min", "The minimum value the integral sum can reach. This is mostly used to prevent excessive buildup when the setpoint is changed");
        public static GUIContent EasingLabel = new GUIContent("Easing", "The rate of change of the setpoint when a new target is set. Higher gives a faster change, lower gives a smoother change");
        public static GUIContent DelayLabel = new GUIContent("Delay", "The time in ms between there being no input on the axis and the axis attitude being locked");

        #endregion
        public void Awake()
        {
            AppLauncherFlight.Setup();
        }

        public void Start()
        {
            ves = FlightGlobals.ActiveVessel;
            SASControllers[(int)SASList.Pitch] = new PIDErrorController(SASList.Pitch, defaultPitchGains);
            SASControllers[(int)SASList.Bank] = new PIDErrorController(SASList.Bank, defaultRollGains);
            SASControllers[(int)SASList.Hdg] = new PIDErrorController(SASList.Hdg, defaultHdgGains);
            //PresetManager.initDefaultPresets(new SSASPreset(SASControllers, "SSAS"));
            //PresetManager.loadCraftSSASPreset(this);

            GameEvents.onTimeWarpRateChanged.Add(warpHandler);
            GameEvents.onVesselChange.Add(vesselSwitch);
            GameEvents.onHideUI.Add(hideUI);
            GameEvents.onShowUI.Add(showUI);

            ves.OnPostAutopilotUpdate += SASControl;
            tooltip = "";
        }

        public void OnDestroy()
        {
            GameEvents.onTimeWarpRateChanged.Remove(warpHandler);
            GameEvents.onVesselChange.Remove(vesselSwitch);
            GameEvents.onHideUI.Remove(hideUI);
            GameEvents.onShowUI.Remove(showUI);
        }

        public void warpHandler()
        {
            if (TimeWarp.CurrentRateIndex == 0 && TimeWarp.CurrentRate != 1 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
                updateTarget();
        }

        public bool UIVisible = true;
        public void hideUI()
        {
            UIVisible = false;
        }
        public void showUI()
        {
            UIVisible = true;
        }

        public void vesselSwitch(Vessel v)
        {
            ves.OnPostAutopilotUpdate -= SASControl;
            ves = v;
            v.OnPostAutopilotUpdate += SASControl;
        }

        #region Update / Input monitoring
        public void Update()
        {
            if (GameSettings.MODIFIER_KEY.GetKey() && GameSettings.SAS_TOGGLE.GetKeyDown())
                bArmed = !bArmed;
            if (bArmed)
            {
                pauseManager();
                if (GameSettings.SAS_TOGGLE.GetKeyDown())
                    ActivitySwitch(!ActivityCheck());
                if (GameSettings.SAS_HOLD.GetKey())
                    updateTarget();
            }
            if (ves.Autopilot != null)
            {
                if (APMode != ves.Autopilot.Mode && APMode == VesselAutopilot.AutopilotMode.StabilityAssist)
                    updateTarget();
                if (spdMode != FlightUIController.speedDisplayMode)
                {
                    if (spdMode == FlightUIController.SpeedDisplayModes.Surface)
                    {

                    }
                    else
                        orbitalTarget = ves.transform.rotation;
                }
                APMode = ves.Autopilot.Mode;
                spdMode = FlightUIController.speedDisplayMode;
            }
            if (bActive[(int)SASList.Hdg])
                GetSAS(SASList.Hdg).SetPoint = calculateTargetHeading(currentTarget, ves);
        }

        private void pauseManager()
        {
            // if the pitch control is not paused, and there is pitch input or there is yaw input and the bank angle is greater than 5 degrees, pause the pitch lock
            if (!bPause[(int)SASList.Pitch] && (hasPitchInput() || (hasYawInput() && Math.Abs(flightData.bank) > bankAngleSynch)))
                bPause[(int)SASList.Pitch] = true;
            // if the pitch control is paused, and there is no pitch input, and there is no yaw input or the bank angle is less than 5 degrees, unpause the pitch lock
            else if (bPause[(int)SASList.Pitch] && !hasPitchInput() && (!hasYawInput() || Math.Abs(flightData.bank) <= bankAngleSynch))
            {
                bPause[(int)SASList.Pitch] = false;
                if (bActive[(int)SASList.Pitch])
                    StartCoroutine(FadeInAxis(SASList.Pitch));
            }

            // if the heading control is not paused, and there is yaw input input or there is pitch input and the bank angle is greater than 5 degrees, pause the heading lock
            if (!bPause[(int)SASList.Hdg] && (hasYawInput() || (hasPitchInput() && Math.Abs(flightData.bank) > bankAngleSynch)))
                bPause[(int)SASList.Hdg] = true;
            // if the heading control is paused, and there is no yaw input, and there is no pitch input or the bank angle is less than 5 degrees, unpause the heading lock
            else if (bPause[(int)SASList.Hdg] && !hasYawInput() && (!hasPitchInput() || Math.Abs(flightData.bank) <= bankAngleSynch))
            {
                bPause[(int)SASList.Hdg] = false;
                if (bActive[(int)SASList.Hdg])
                    StartCoroutine(FadeInAxis(SASList.Hdg));
            }

            // if the roll control is not paused, and there is roll input or thevessel pitch is > 70 degrees and there is pitch/yaw input
            if (!bPause[(int)SASList.Bank] && (hasRollInput() || (Math.Abs(flightData.pitch) > 70 && (hasPitchInput() || hasYawInput()))))
                bPause[(int)SASList.Bank] = true;
            // if the roll control is paused, and there is not roll input and not any pitch/yaw input if pitch < 60 degrees
            else if (bPause[(int)SASList.Bank] && !(hasRollInput() || (Math.Abs(flightData.pitch) > 60 && (hasPitchInput() || hasYawInput()))))
            {
                bPause[(int)SASList.Bank] = false;
                if (bActive[(int)SASList.Bank])
                    StartCoroutine(FadeInAxis(SASList.Bank));
            }
        }
        #endregion

        #region Fixed Update / Control
        public void SASControl(FlightCtrlState state)
        {
            if (ves.HoldPhysics)
                return;
            flightData.updateAttitude();
            if (!bArmed || !ActivityCheck() || !ves.IsControllable)
                return;

            // facing vectors : vessel (vesRefTrans.up) and target (targetRot * Vector3.forward)
            Transform vesRefTrans = ves.ReferenceTransform.transform;
            Quaternion targetRot = TargetModeSwitch();
            double angleError = Vector3d.Angle(vesRefTrans.up, targetRot * Vector3d.forward);
            //================================
            // pitch / yaw response ratio. Original method from MJ attitude controller
            Vector3d relativeTargetFacing = Quaternion.Inverse(vesRefTrans.rotation) * targetRot * Vector3d.forward;
            Vector2d PYerror = (new Vector2d(relativeTargetFacing.x, -relativeTargetFacing.z)).normalized * angleError;
            //================================
            // roll error is dependant on path taken in pitch/yaw plane. Minimise unnecesary rotation by evaluating the roll error relative to that path
            Vector3d normVec = Vector3d.Cross(targetRot * Vector3d.forward, vesRefTrans.up).normalized; // axis normal to desired plane of travel
            Vector3d rollTargetRight = Quaternion.AngleAxis((float)angleError, normVec) * targetRot * Vector3d.right;
            double rollError = Vector3d.Angle(vesRefTrans.right, rollTargetRight) * Math.Sign(Vector3d.Dot(rollTargetRight, vesRefTrans.forward)); // signed angle difference between vessel.right and rollTargetRot.right
            //================================

            setCtrlState(SASList.Bank, rollError, ves.angularVelocity.y * Mathf.Rad2Deg, ref state.roll);
            setCtrlState(SASList.Pitch, PYerror.y, ves.angularVelocity.x * Mathf.Rad2Deg, ref state.pitch);
            setCtrlState(SASList.Hdg, PYerror.x, ves.angularVelocity.z * Mathf.Rad2Deg, ref state.yaw);
        }

        void setCtrlState(SASList ID, double error, double rate, ref float axisCtrlState)
        {
            PIDmode mode = PIDmode.PID;
            if (!ves.checkLanded() && ves.IsControllable)
                mode = PIDmode.PD; // no integral when it can't do anything useful

            if (allowControl(ID))
                axisCtrlState = GetSAS(ID).ResponseF(error, rate, mode);
            else if (!hasInput(ID))
                axisCtrlState = 0; // kill off stock SAS inputs
            // nothing happens if player input is present
        }

        Quaternion orbitalTarget = Quaternion.identity;
        Quaternion TargetModeSwitch()
        {
            Quaternion target = Quaternion.identity;
            switch (ves.Autopilot.Mode)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                    {
                        float hdgAngle = (float)(bActive[(int)SASList.Hdg] ? GetSAS(SASList.Hdg).SetPoint : flightData.heading);
                        float pitchAngle = (float)(bActive[(int)SASList.Pitch] ? GetSAS(SASList.Pitch).SetPoint : flightData.pitch);

                        target = Quaternion.LookRotation(flightData.planetNorth, flightData.planetUp);
                        target = Quaternion.AngleAxis(hdgAngle, target * Vector3.up) * target; // heading rotation
                        target = Quaternion.AngleAxis(pitchAngle, target * -Vector3.right) * target; // pitch rotation
                    }
                    else
                        return orbitalTarget * Quaternion.Euler(-90, 0, 0);
                    break;
                case VesselAutopilot.AutopilotMode.Prograde:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit)
                        target = Quaternion.LookRotation(ves.obt_velocity, flightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(ves.srf_velocity, flightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(ves.obt_velocity - ves.targetObject.GetVessel().obt_velocity, flightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit)
                        target = Quaternion.LookRotation(-ves.obt_velocity, flightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(ves.srf_velocity, flightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(ves.targetObject.GetVessel().obt_velocity - ves.obt_velocity, flightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(flightData.obtRadial, flightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(flightData.srfRadial, flightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(-flightData.obtRadial, flightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(-flightData.srfRadial, flightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Normal:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(flightData.obtNormal, flightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(flightData.srfNormal, flightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(-flightData.obtNormal, flightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(-flightData.srfNormal, flightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Target:
                    if (!ReferenceEquals(ves.targetObject, null))
                        target = Quaternion.LookRotation(ves.targetObject.GetVessel().GetWorldPos3D() - ves.GetWorldPos3D(), flightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    if (!ReferenceEquals(ves.targetObject, null))
                        target = Quaternion.LookRotation(ves.GetWorldPos3D() - ves.targetObject.GetVessel().GetWorldPos3D(), flightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Maneuver:
                    if (!ReferenceEquals(ves.patchedConicSolver.maneuverNodes, null) && ves.patchedConicSolver.maneuverNodes.Count > 0)
                        target = ves.patchedConicSolver.maneuverNodes[0].nodeRotation;
                    break;
            }
            float rollAngle = (float)(bActive[(int)SASList.Bank] ? GetSAS(SASList.Bank).SetPoint : flightData.bank);
            target = Quaternion.AngleAxis(-rollAngle, target * Vector3.forward) * target; // roll rotation
            return target;
        }

        bool allowControl(SASList ID)
        {
            return bActive[(int)ID] && !bPause[(int)ID];
        }

        private void updateTarget()
        {
            StartCoroutine(FadeInAxis(SASList.Pitch));
            StartCoroutine(FadeInAxis(SASList.Bank));
            StartCoroutine(FadeInAxis(SASList.Hdg));
            orbitalTarget = ves.transform.rotation;
        }

        /// <summary>
        /// wait for rate of rotation to fall below 10 degres / s before locking in the target. Derivative only action until that time
        /// </summary>
        IEnumerator FadeInAxis(SASList axis)
        {
            updateSetpoint(axis, getCurrentVal(axis));
            while (Math.Abs(getCurrentRate(axis, ves) * Mathf.Rad2Deg) > 10)
            {
                updateSetpoint(axis, getCurrentVal(axis));
                yield return null;
            }
            orbitalTarget = ves.transform.rotation;
            if (axis == SASList.Hdg)
                currentTarget = getPlaneRotation(flightData.heading, ves);
        }

        void updateSetpoint(SASList ID, double setpoint)
        {
            GetSAS(ID).SetPoint = setpoint;
            targets[(int)ID] = setpoint.ToString("0.00");
        }
        #endregion

        /// <summary>
        /// Set SSAS mode
        /// </summary>
        /// <param name="enable"></param>
        public void ActivitySwitch(bool enable)
        {
            if (enable)
            {
                bActive[(int)SASList.Pitch] = bActive[(int)SASList.Bank] = bActive[(int)SASList.Hdg] = true;
                updateTarget();
            }
            else
                bActive[(int)SASList.Pitch] = bActive[(int)SASList.Bank] = bActive[(int)SASList.Hdg] = false;
            setStockSAS(enable);
        }

        /// <summary>
        /// returns true if SSAS is active
        /// </summary>
        /// <returns></returns>
        public bool ActivityCheck()
        {
            if (bActive[(int)SASList.Pitch] || bActive[(int)SASList.Bank] || bActive[(int)SASList.Hdg])
                return true;
            else
                return false;
        }

        /// <summary>
        /// set stock SAS state
        /// </summary>
        public void setStockSAS(bool state)
        {
            ves.ActionGroups.SetGroup(KSPActionGroup.SAS, state);
        }

        #region GUI
        public void OnGUI()
        {
            if (ReferenceEquals(UISkin, null))
                customSkin();
            if (!UIVisible)
                return;

            // SAS toggle button
            // is before the bDisplay check so it can be up without the rest of the UI
            if (bArmed && FlightUIModeController.Instance.navBall.StateIndex == 0)
            {
                if (ActivityCheck())
                    GUI.backgroundColor = XKCDColors.BrightOrange;
                else
                    GUI.backgroundColor = XKCDColors.BrightSkyBlue;

                if (GUI.Button(new Rect(Screen.width / 2 + 70 * GameSettings.UI_SCALE, Screen.height - 240 * GameSettings.UI_SCALE, 50, 30), "SSAS"))
                    ActivitySwitch(!ActivityCheck());
                GUI.backgroundColor = stockBackgroundColor;
            }

            // Main and preset window stuff
            if (bDisplay)
            {
                SSASwindow = GUILayout.Window(78934856, SSASwindow, drawSSASWindow, "SSAS", GUILayout.Height(0));

                if (bShowSSASPresets)
                {
                    SSASPresetwindow = GUILayout.Window(78934859, SSASPresetwindow, drawSSASPresetWindow, "SSAS Presets", GUILayout.Height(0));
                    SSASPresetwindow.x = SSASwindow.x + SSASwindow.width;
                    SSASPresetwindow.y = SSASwindow.y;
                }
                if (tooltip != "" && bShowTooltips)
                    GUILayout.Window(34246, new Rect(SSASwindow.x + SSASwindow.width, Screen.height - Input.mousePosition.y, 0, 0), tooltipWindow, "", UISkin.label, GUILayout.Height(0), GUILayout.Width(300));
            }
        }

        private void drawSSASWindow(int id)
        {
            if (GUI.Button(new Rect(SSASwindow.width - 16, 2, 14, 14), ""))
                bDisplay = false;

            //bShowSSASPresets = GUILayout.Toggle(bShowSSASPresets, bShowSSASPresets ? "Hide SAS Presets" : "Show SAS Presets");

            Color tempColor = GUI.backgroundColor;
            GUI.backgroundColor = XKCDColors.BlueBlue;
            if (GUILayout.Button(bArmed ? "Disarm SAS" : "Arm SAS"))
            {
                bArmed = !bArmed;
                ActivitySwitch(ves.ActionGroups[KSPActionGroup.SAS]);
            }
            GUI.backgroundColor = tempColor;

            if (bArmed)
            {
                if (FlightGlobals.speedDisplayMode == FlightGlobals.SpeedDisplayModes.Surface)
                {
                    if (APMode == VesselAutopilot.AutopilotMode.StabilityAssist)
                    {
                        GetSAS(SASList.Pitch).SetPoint = TogPlusNumBox("Pitch:", SASList.Pitch, flightData.pitch, 80, 70);
                        currentTarget = getPlaneRotation(TogPlusNumBox("Heading:", SASList.Hdg, flightData.heading, 80, 70), ves);
                    }
                    GetSAS(SASList.Bank).SetPoint = TogPlusNumBox("Roll:", SASList.Bank, flightData.bank, 80, 70);
                }
                GUILayout.Box("", GUILayout.Height(10)); // seperator

                drawPIDValues(SASList.Pitch, "Pitch");
                drawPIDValues(SASList.Bank, "Roll");
                drawPIDValues(SASList.Hdg, "Yaw");
            }
            GUI.DragWindow();
            tooltip = GUI.tooltip;
        }

        string tooltip = "";
        private void tooltipWindow(int id)
        {
            GUILayout.Label(tooltip, UISkin.textArea);
        }

        private void drawPIDValues(SASList controllerID, string inputName)
        {
            PIDErrorController controller = GetSAS(controllerID);
            controller.bShow = GUILayout.Toggle(controller.bShow, inputName, UISkin.customStyles[(int)myStyles.btnToggle]);

            if (controller.bShow)
            {
                controller.PGain = labPlusNumBox(KpLabel, controller.PGain.ToString("N3"), 45);
                controller.IGain = labPlusNumBox(KiLabel, controller.IGain.ToString("N3"), 45);
                controller.DGain = labPlusNumBox(KdLabel, controller.DGain.ToString("N3"), 45);
                controller.Scalar = labPlusNumBox(ScalarLabel, controller.Scalar.ToString("N3"), 45);
            }
        }

        private void drawSSASPresetWindow(int id)
        {
            if (GUI.Button(new Rect(SSASPresetwindow.width - 16, 2, 14, 14), ""))
                bShowSSASPresets = false;

            if (!ReferenceEquals(PresetManager.Instance.activeSSASPreset, null))
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.Instance.activeSSASPreset.name));
                if (PresetManager.Instance.activeSSASPreset.name != "SSAS")
                {
                    if (GUILayout.Button("Update Preset"))
                        PresetManager.UpdateSSASPreset(this);
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                PresetManager.newSSASPreset(ref newPresetName, SASControllers, ves);
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            //if (GUILayout.Button("Reset to Defaults"))
            //    PresetManager.loadSSASPreset(PresetManager.Instance.craftPresetDict["default"].SSASPreset, this);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (SSASPreset p in PresetManager.Instance.SSASPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadSSASPreset(p, this);
                else if (GUILayout.Button("x", GUILayout.Width(25)))
                    PresetManager.deleteSSASPreset(p);
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Draws a toggle button and text box of specified widths with update button.
        /// </summary>
        /// <param name="toggleText"></param>
        /// <param name="boxVal"></param>
        /// <param name="toggleWidth"></param>
        /// <param name="boxWidth"></param>
        /// <returns></returns>
        public double TogPlusNumBox(string toggleText, SASList controllerID, double currentVal, float toggleWidth, float boxWidth)
        {
            double setPoint = GetSAS(controllerID).SetPoint;

            GUILayout.BeginHorizontal();

            bool tempState = GUILayout.Toggle(bActive[(int)controllerID], toggleText, UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(toggleWidth));
            if (tempState != bActive[(int)controllerID])
            {
                bActive[(int)controllerID] = tempState;
                if (bActive[(int)controllerID])
                {
                    setPoint = currentVal;
                    targets[(int)controllerID] = currentVal.ToString("0.00");
                }
            }

            string tempText = GUILayout.TextField(targets[(int)controllerID], UISkin.customStyles[(int)myStyles.numBoxText], GUILayout.Width(boxWidth));
            targets[(int)controllerID] = tempText;

            if (GUILayout.Button("u"))
            {
                double temp;
                if (double.TryParse(targets[(int)controllerID], out temp))
                    setPoint = temp;

                bActive[(int)controllerID] = true;
            }

            GUILayout.EndHorizontal();
            return setPoint;
        }
        #endregion

        public PIDErrorController GetSAS(SASList id)
        {
            return SASControllers[(int)id];
        }

        /// <summary>
        /// Circular rounding to keep compass measurements within a 360 degree range
        /// maxHeading is the top limit, bottom limit is maxHeading - 360
        /// </summary>
        public static double headingClamp(double valToClamp, double maxHeading, double range = 360)
        {
            double temp = (valToClamp - (maxHeading - range)) % range;
            return (maxHeading - range) + (temp < 0 ? temp + range : temp);
        }

        /// <summary>
        /// Plane normal vector from a given heading (surface right vector)
        /// </summary>
        public static Vector3 vecHeading(double target)
        {
            double angleDiff = target - VesselData.Instance.heading;
            return Quaternion.AngleAxis((float)(angleDiff + 90), (Vector3)VesselData.Instance.planetUp) * VesselData.Instance.surfVesForward;
        }

        /// <summary>
        /// calculate current heading from plane normal vector
        /// </summary>
        public static double calculateTargetHeading(Vector3 direction)
        {
            Vector3 fwd = Vector3.Cross(direction, VesselData.Instance.planetUp);
            double heading = Vector3.Angle(fwd, VesselData.Instance.planetNorth) * Math.Sign(Vector3.Dot(fwd, VesselData.Instance.planetEast));
            return headingClamp(heading, 360);
        }

        /// <summary>
        /// calculate current heading from plane rotation
        /// </summary>
        public static double calculateTargetHeading(Quaternion rotation, Vessel vessel)
        {
            Vector3 fwd = Vector3.Cross(getPlaneNormal(rotation, vessel), VesselData.Instance.planetUp);
            double heading = Vector3.Angle(fwd, VesselData.Instance.planetNorth) * Math.Sign(Vector3.Dot(fwd, VesselData.Instance.planetEast));
            return headingClamp(heading, 360);
        }

        /// <summary>
        /// calculates the angle to feed corrected for 0/360 crossings
        /// eg. if the target is 350 and the current is 10, it will return 370 giving a diff of -20 degrees
        /// else you get +ve 340 and the turn is in the wrong direction
        /// </summary>
        public static double CurrentAngleTargetRel(double current, double target, double maxAngle)
        {
            double diff = target - current;
            if (diff < maxAngle - 360)
                return current - 360;
            else if (diff > maxAngle)
                return current + 360;
            else
                return current;
        }

        /// <summary>
        /// calculate the planet relative rotation from the plane normal vector
        /// </summary>
        public static Quaternion getPlaneRotation(Vector3 planeNormal, Vessel vessel)
        {
            return Quaternion.FromToRotation(vessel.mainBody.transform.right, planeNormal);
        }

        public static Quaternion getPlaneRotation(double heading, Vessel vessel)
        {
            Vector3 planeNormal = vecHeading(heading);
            return getPlaneRotation(planeNormal, vessel);
        }

        public static Vector3 getPlaneNormal(Quaternion rotation, Vessel vessel)
        {
            return rotation * vessel.mainBody.transform.right;
        }

        public static bool IsNeutral(AxisBinding axis)
        {
            return axis.IsNeutral() && Math.Abs(axis.GetAxis()) < 0.00001;
        }

        public static bool hasInput(SASList ID)
        {
            switch (ID)
            {
                case SASList.Bank:
                    return hasRollInput();
                case SASList.Hdg:
                    return hasYawInput();
                case SASList.Pitch:
                default:
                    return hasPitchInput();
            }
        }

        public static bool hasYawInput()
        {
            return GameSettings.YAW_LEFT.GetKey() || GameSettings.YAW_RIGHT.GetKey() || !IsNeutral(GameSettings.AXIS_YAW);
        }

        public static bool hasPitchInput()
        {
            return GameSettings.PITCH_DOWN.GetKey() || GameSettings.PITCH_UP.GetKey() || !IsNeutral(GameSettings.AXIS_PITCH);
        }

        public static bool hasRollInput()
        {
            return GameSettings.ROLL_LEFT.GetKey() || GameSettings.ROLL_RIGHT.GetKey() || !IsNeutral(GameSettings.AXIS_ROLL);
        }

        public static bool hasThrottleInput()
        {
            return GameSettings.THROTTLE_UP.GetKey() || GameSettings.THROTTLE_DOWN.GetKey() || (GameSettings.THROTTLE_CUTOFF.GetKeyDown() && !GameSettings.MODIFIER_KEY.GetKey()) || GameSettings.THROTTLE_FULL.GetKeyDown();
        }

        public static double getCurrentVal(SASList ID)
        {
            switch (ID)
            {
                case SASList.Bank:
                    return VesselData.Instance.bank;
                case SASList.Hdg:
                    return VesselData.Instance.heading;
                case SASList.Pitch:
                default:
                    return VesselData.Instance.pitch;
            }
        }

        public static double getCurrentRate(SASList ID, Vessel v)
        {
            switch (ID)
            {
                case SASList.Bank:
                    return v.angularVelocity.y;
                case SASList.Hdg:
                    return v.angularVelocity.z;
                case SASList.Pitch:
                default:
                    return v.angularVelocity.x;
            }
        }

        public static void customSkin()
        {
            UISkin = (GUISkin)MonoBehaviour.Instantiate(UnityEngine.GUI.skin);
            UISkin.customStyles = new GUIStyle[Enum.GetValues(typeof(myStyles)).GetLength(0)];
            stockBackgroundColor = GUI.backgroundColor;

            // style for the paused message (big, bold, and red)
            UISkin.customStyles[(int)myStyles.labelAlert] = new GUIStyle(GUI.skin.box);
            UISkin.customStyles[(int)myStyles.labelAlert].normal.textColor = XKCDColors.Red;
            UISkin.customStyles[(int)myStyles.labelAlert].fontSize = 21;
            UISkin.customStyles[(int)myStyles.labelAlert].fontStyle = FontStyle.Bold;
            UISkin.customStyles[(int)myStyles.labelAlert].alignment = TextAnchor.MiddleCenter;

            // style for label to align with increment buttons
            UISkin.customStyles[(int)myStyles.numBoxLabel] = new GUIStyle(UISkin.label);
            UISkin.customStyles[(int)myStyles.numBoxLabel].alignment = TextAnchor.MiddleLeft;
            UISkin.customStyles[(int)myStyles.numBoxLabel].margin = new RectOffset(4, 4, 5, 3);

            // style for text box to align with increment buttons better
            UISkin.customStyles[(int)myStyles.numBoxText] = new GUIStyle(UISkin.textField);
            UISkin.customStyles[(int)myStyles.numBoxText].alignment = TextAnchor.MiddleLeft;
            UISkin.customStyles[(int)myStyles.numBoxText].margin = new RectOffset(4, 0, 5, 3);

            // style for increment button
            UISkin.customStyles[(int)myStyles.btnPlus] = new GUIStyle(UISkin.button);
            UISkin.customStyles[(int)myStyles.btnPlus].margin = new RectOffset(0, 4, 2, 0);
            UISkin.customStyles[(int)myStyles.btnPlus].hover.textColor = Color.yellow;
            UISkin.customStyles[(int)myStyles.btnPlus].onActive.textColor = Color.green;

            // style for derement button
            UISkin.customStyles[(int)myStyles.btnMinus] = new GUIStyle(UISkin.button);
            UISkin.customStyles[(int)myStyles.btnMinus].margin = new RectOffset(0, 4, 0, 2);
            UISkin.customStyles[(int)myStyles.btnMinus].hover.textColor = Color.yellow;
            UISkin.customStyles[(int)myStyles.btnMinus].onActive.textColor = Color.green;

            // A toggle that looks like a button
            UISkin.customStyles[(int)myStyles.btnToggle] = new GUIStyle(UISkin.button);
            UISkin.customStyles[(int)myStyles.btnToggle].normal.textColor = UISkin.customStyles[(int)myStyles.btnToggle].focused.textColor = Color.white;
            UISkin.customStyles[(int)myStyles.btnToggle].onNormal.textColor = UISkin.customStyles[(int)myStyles.btnToggle].onFocused.textColor = UISkin.customStyles[(int)myStyles.btnToggle].onHover.textColor
                = UISkin.customStyles[(int)myStyles.btnToggle].active.textColor = UISkin.customStyles[(int)myStyles.btnToggle].hover.textColor = UISkin.customStyles[(int)myStyles.btnToggle].onActive.textColor = Color.green;
            UISkin.customStyles[(int)myStyles.btnToggle].onNormal.background = UISkin.customStyles[(int)myStyles.btnToggle].onHover.background = UISkin.customStyles[(int)myStyles.btnToggle].onActive.background
                = UISkin.customStyles[(int)myStyles.btnToggle].active.background = HighLogic.Skin.button.onNormal.background;
            UISkin.customStyles[(int)myStyles.btnToggle].hover.background = UISkin.customStyles[(int)myStyles.btnToggle].normal.background;

            UISkin.customStyles[(int)myStyles.lblToggle] = new GUIStyle(UISkin.customStyles[(int)myStyles.btnToggle]);

            UISkin.customStyles[(int)myStyles.greenTextBox] = new GUIStyle(UISkin.textArea);
            UISkin.customStyles[(int)myStyles.greenTextBox].active.textColor = UISkin.customStyles[(int)myStyles.greenTextBox].hover.textColor = UISkin.customStyles[(int)myStyles.greenTextBox].focused.textColor = UISkin.customStyles[(int)myStyles.greenTextBox].normal.textColor
                = UISkin.customStyles[(int)myStyles.greenTextBox].onActive.textColor = UISkin.customStyles[(int)myStyles.greenTextBox].onHover.textColor = UISkin.customStyles[(int)myStyles.greenTextBox].onFocused.textColor = UISkin.customStyles[(int)myStyles.greenTextBox].onNormal.textColor = XKCDColors.Green;

            UISkin.customStyles[(int)myStyles.redButtonText] = new GUIStyle(UISkin.button);
            UISkin.customStyles[(int)myStyles.redButtonText].active.textColor = UISkin.customStyles[(int)myStyles.redButtonText].hover.textColor = UISkin.customStyles[(int)myStyles.redButtonText].focused.textColor = UISkin.customStyles[(int)myStyles.redButtonText].normal.textColor
                = UISkin.customStyles[(int)myStyles.redButtonText].onActive.textColor = UISkin.customStyles[(int)myStyles.redButtonText].onHover.textColor = UISkin.customStyles[(int)myStyles.redButtonText].onFocused.textColor = UISkin.customStyles[(int)myStyles.redButtonText].onNormal.textColor = XKCDColors.Red;

            UISkin.box.onActive.background = UISkin.box.onFocused.background = UISkin.box.onHover.background = UISkin.box.onNormal.background =
                UISkin.box.active.background = UISkin.box.focused.background = UISkin.box.hover.background = UISkin.box.normal.background = UISkin.window.normal.background;
        }

        /// <summary>
        /// Draws a label and text box of specified widths with +/- 10% increment buttons. Returns the numeric value of the text box
        /// </summary>
        /// <param name="labelText">text for the label</param>
        /// <param name="boxText">number to display in text box</param>
        /// <param name="labelWidth"></param>
        /// <param name="boxWidth"></param>
        /// <returns>edited value of the text box</returns>
        public double labPlusNumBox(string labelText, string boxText, float labelWidth = 100, float boxWidth = 60)
        {
            double val;
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelText, UISkin.customStyles[(int)myStyles.numBoxLabel], GUILayout.Width(labelWidth));
            val = double.Parse(boxText);
            boxText = val.ToString(",0.0#####");
            string text = GUILayout.TextField(boxText, UISkin.customStyles[(int)myStyles.numBoxText], GUILayout.Width(boxWidth));
            //
            try
            {
                val = double.Parse(text);
            }
            catch
            {
                val = double.Parse(boxText);
            }
            //
            GUILayout.BeginVertical();
            if (GUILayout.Button("+", UISkin.customStyles[(int)myStyles.btnPlus], GUILayout.Width(20), GUILayout.Height(13)))
            {
                if (val != 0)
                    val *= 1.1;
                else
                    val = 0.01;
            }
            if (GUILayout.Button("-", UISkin.customStyles[(int)myStyles.btnMinus], GUILayout.Width(20), GUILayout.Height(13)))
            {
                val /= 1.1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return val;
        }

        public double labPlusNumBox(GUIContent labelText, string boxText, float labelWidth = 100, float boxWidth = 60)
        {
            double val;
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelText, UISkin.customStyles[(int)myStyles.numBoxLabel], GUILayout.Width(labelWidth));
            val = double.Parse(boxText);
            boxText = val.ToString(",0.0#####");
            string text = GUILayout.TextField(boxText, UISkin.customStyles[(int)myStyles.numBoxText], GUILayout.Width(boxWidth));
            //
            try
            {
                val = double.Parse(text);
            }
            catch
            {
                val = double.Parse(boxText);
            }
            //
            GUILayout.BeginVertical();
            if (GUILayout.Button("+", UISkin.customStyles[(int)myStyles.btnPlus], GUILayout.Width(20), GUILayout.Height(13)))
            {
                if (val != 0)
                    val *= 1.1;
                else
                    val = 0.01;
            }
            if (GUILayout.Button("-", UISkin.customStyles[(int)myStyles.btnMinus], GUILayout.Width(20), GUILayout.Height(13)))
            {
                val /= 1.1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return val;
        }
    }
}
