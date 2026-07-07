using HarmonyLib;
using StardewValley;
using SObject = StardewValley.Object;

namespace ActivateSprinklersMobile.Patches
{
    /// <summary>
    /// Patches the game's own interaction entry point for placed objects.
    /// This method is invoked by the game for EVERY input method that performs
    /// an "action" on a tile — mouse right-click, controller button, and
    /// Android touchscreen taps alike — so no platform-specific input code
    /// is needed anywhere in this mod.
    /// </summary>
    [HarmonyPatch(typeof(SObject), nameof(SObject.checkForAction))]
    internal static class ObjectCheckForActionPatch
    {
        /// <summary>Runs after the vanilla method. Only acts if vanilla found nothing to do.</summary>
        private static void Postfix(SObject __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
        {
            // justCheckingForActivity is used by the game just to decide cursor icons; ignore it.
            if (justCheckingForActivity)
                return;

            // If vanilla already handled the interaction (e.g. attaching a Pressure Nozzle
            // or Enricher to the sprinkler), don't override that behavior.
            if (__result)
                return;

            // Only handle actual sprinklers (Basic/Quality/Iridium + any enricher attachment).
            if (!__instance.IsSprinkler())
                return;

            ModEntry.Instance?.ActivateSprinkler(__instance, who);

            // Mark the interaction as handled so nothing else tries to process it.
            __result = true;
        }
    }
}
