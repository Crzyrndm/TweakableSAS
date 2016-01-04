﻿using UnityEngine;

namespace TweakableSAS
{
    static class AppLauncherFlight
    {
        private static ApplicationLauncherButton btnLauncher;
        private static Texture2D prograde, retrograde;
        public static void Setup()
        {
            prograde = GameDatabase.Instance.GetTexture("TweakableSAS/Icon/Prograde", false);
            retrograde = GameDatabase.Instance.GetTexture("TweakableSAS/Icon/Retrograde", false);
            if (btnLauncher == null)
                btnLauncher = ApplicationLauncher.Instance.AddModApplication(OnToggleTrue, OnToggleFalse, null, null, null, null,
                                        ApplicationLauncher.AppScenes.FLIGHT, prograde);
        }

        private static void OnToggleTrue()
        {
            TweakableSAS.bDisplay = true;
            setBtnState(true);
        }

        private static void OnToggleFalse()
        {
            TweakableSAS.bDisplay = false;
            setBtnState(false);
        }

        public static void setBtnState(bool state, bool click = false)
        {
            if (state)
            {
                btnLauncher.SetTrue(click);
                btnLauncher.SetTexture(GameDatabase.Instance.GetTexture("TweakableSAS/Icon/Retrograde", false));
            }
            else
            {
                btnLauncher.SetFalse(click);
                btnLauncher.SetTexture(GameDatabase.Instance.GetTexture("TweakableSAS/Icon/Prograde", false));
            }
        }
    }
}