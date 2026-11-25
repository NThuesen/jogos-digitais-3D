using UnityEngine;

namespace SlipperyZlope.RingToss.Mobile
{
    /// <summary>
    /// Hands a line of text to the platform's share sheet.
    ///
    /// Android only, through the ordinary ACTION_SEND intent wrapped in a chooser, which
    /// needs no permission, no plugin and no manifest entry. Everywhere else — the
    /// editor, and iOS until somebody wires UIActivityViewController — the text goes on
    /// the clipboard and the caller is told, so the button can say so instead of looking
    /// as if it did nothing.
    /// </summary>
    public static class NativeShare
    {
        /// <returns>True if a share sheet was opened; false if the text was copied instead.</returns>
        public static bool Share(string subject, string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
                using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject);
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);

                    using (AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>(
                               "createChooser", intent, "Share Ring Toss"))
                    {
                        UnityEngine.Android.AndroidApplication.currentActivity.Call("startActivity", chooser);
                    }
                }

                return true;
            }
            catch (System.Exception exception)
            {
                // A failed share must never take the title screen down with it. Falls
                // through to the clipboard, which always works.
                Debug.LogWarning($"[Share] The share sheet could not be opened: {exception.Message}");
            }
#endif
            GUIUtility.systemCopyBuffer = text;
            return false;
        }
    }
}
