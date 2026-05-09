using Common;
using Common.Input;
using HarmonyLib;
using UnityEngine;

namespace CombatLog.Runtime
{
    // Suppresses the InputSystem mouse-click and scroll dispatch when the cursor is over
    // the combat-log panel, otherwise clicking the expand/collapse arrow goes through and can
    // issue commands.

    [HarmonyPatch(typeof(InputSystem), "GenerateKeyCodeEvent")]
    internal static class GenerateKeyCodeEventPatch
    {
        private static bool Prefix(KeyCode keyCode)
        {
            if (Constants.MOUSE_KEY_CODES.Contains(keyCode) && CombatLogState.MouseInPanel())
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(InputSystem), "GenerateMouseScrollReport")]
    internal static class GenerateMouseScrollReportPatch
    {
        private static bool Prefix()
        {
            return !CombatLogState.MouseInPanel();
        }
    }
}
