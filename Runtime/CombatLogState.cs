using UnityEngine;

namespace CombatLog.Runtime
{
    public static class CombatLogState
    {
        public static Rect LastPanelRect;

        public static bool MouseInPanel()
        {
            if (LastPanelRect.width <= 0f || LastPanelRect.height <= 0f)
                return false;
            var mp = UnityEngine.Input.mousePosition;
            var pt = new Vector2(mp.x, Screen.height - mp.y);
            return LastPanelRect.Contains(pt);
        }
    }
}
