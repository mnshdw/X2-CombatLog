using System.Reflection;
using Artitas.Utils;
using log4net;

namespace CombatLog
{
    public static class ModConstants
    {
        public const string LogPrefix = "[CombatLog]";

        // Color values lifted directly from the game's UI prefabs (generated_prefab_common
        // and generated_prefab_groundcombat):
        //   text_header_blackui font color = #EAAD1C  (header yellow)
        //   combatant_info_bar/hp value    = #EE1C25  (HP red, heart icon)
        //   combatant_info_bar/armor value = #BEBEBE  (armour grey)
        //   crosshair tu_cost_text         = #5CCB00  (TU green)
        //   default text label             = #808080  (greys used for meta info)
        public const string PlayerColor = "#E5E5E5";
        public const string EnemyColor = "#EE6B6B";
        public const string NeutralColor = "#C5C5C5";
        public const string MetaColor = "#808080";

        public const string AbilityHitColor = "#5CCB00";
        public const string AbilityMissColor = "#808080";

        public const string BodyColor = "#EE1C25";
        public const string ArmourColor = "#BEBEBE";
        public const string StunColor = "#7FBAB5";

        public const string DeathColor = "#EAAD1C";

        public const string PanelBorderColor = "#646464";

        public static readonly ILog Log = ArtitasLogger.GetLogger(
            MethodBase.GetCurrentMethod()!.DeclaringType
        );
    }
}
