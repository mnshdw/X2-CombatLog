using System.Reflection;
using Artitas.Utils;
using log4net;

namespace CombatLog
{
    public static class ModConstants
    {
        public const string LogPrefix = "[CombatLog]";

        public const string PlayerColor = "#7ed957";
        public const string EnemyColor = "#ff6b6b";
        public const string NeutralColor = "#c0c0c0";
        public const string MetaColor = "#a0a0a0";

        public const string AbilityHitColor = "#5cb85c";
        public const string AbilityMissColor = "#888888";

        public const string BodyColor = "#d92e2e";
        public const string ArmourColor = "#9c9c9c";
        public const string StunColor = "#9b59b6";

        public const string DeathColor = "#8e44ad";

        public static readonly ILog Log = ArtitasLogger.GetLogger(
            MethodBase.GetCurrentMethod()!.DeclaringType
        );
    }
}
