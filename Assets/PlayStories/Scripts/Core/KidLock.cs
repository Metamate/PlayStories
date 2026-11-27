using UnityEngine;

namespace PlayStories
{
    /// <summary>
    /// "Locked mode": keeps little hands inside the app.
    ///
    /// Android – full-screen immersive mode + <b>screen pinning</b> (Activity.startLockTask): home,
    ///           recents and back gestures are blocked until the app unpins itself (Close app in
    ///           settings) or a parent does the system unpin gesture. Android asks the parent to
    ///           confirm pinning the first time.
    /// iOS     – Apple does not let apps lock themselves. We defer all edge gestures (a swipe from
    ///           the edge must be done twice) and hide the home indicator. For a real lock, parents
    ///           turn on <b>Guided Access</b> (Settings ▸ Accessibility ▸ Guided Access, then
    ///           triple-click the side button in the app).
    /// All     – the screen does not dim/sleep while a child is looking at a book.
    /// </summary>
    public static class KidLock
    {
        public static bool IsSupportedPinning => Application.platform == RuntimePlatform.Android;
        public static bool IsIOS => Application.platform == RuntimePlatform.IPhonePlayer;

        /// <summary>
        /// Apply the current setting. <paramref name="requestPin"/> only at startup / when the parent
        /// changes the setting: Android shows a confirmation dialog, and re-asking on every focus
        /// change would loop if the parent declines.
        /// </summary>
        public static void Apply(bool requestPin = false) => Set(Settings.KidLock, requestPin);

        public static void Set(bool locked, bool requestPin)
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if UNITY_ANDROID || UNITY_IOS
            Screen.fullScreen = true; // immersive: hides status/navigation bars
#endif
            // Android: runtime check (not #if) so every build compiles the JNI path
#if UNITY_IOS // the iOS API only exists in iOS builds
            UnityEngine.iOS.Device.deferSystemGesturesMode = locked ? UnityEngine.iOS.SystemGestureDeferMode.All
                                                                    : UnityEngine.iOS.SystemGestureDeferMode.None;
            UnityEngine.iOS.Device.hideHomeButton = locked;
#endif
            if (IsSupportedPinning)
            {
                if (!locked) SetAndroidPinned(false);
                else if (requestPin) SetAndroidPinned(true);
            }
        }

        /// <summary>Must be called before quitting so Android doesn't keep the device pinned.</summary>
        public static void Release()
        {
            if (IsSupportedPinning) SetAndroidPinned(false);
        }

        static void SetAndroidPinned(bool pinned)
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    {
                        try { activity.Call(pinned ? "startLockTask" : "stopLockTask"); }
                        catch (System.Exception e) { Debug.LogWarning("[KidLock] " + e.Message); }
                    }));
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[KidLock] " + e.Message); }
        }
    }
}
