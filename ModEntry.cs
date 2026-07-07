using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace ActivateSprinklersMobile
{
    /// <summary>Mod entry point.</summary>
    public class ModEntry : Mod
    {
        /*********
        ** Fields
        *********/
        /// <summary>Tracks sprinklers already activated in the current game tick, to prevent duplicate triggers.</summary>
        private readonly HashSet<string> ActivatedThisTick = new();

        /*********
        ** Accessors
        *********/
        /// <summary>The single mod instance, used by the Harmony patch to call back into mod logic.</summary>
        public static ModEntry Instance { get; private set; }

        /*********
        ** Public methods
        *********/
        /// <inheritdoc/>
        public override void Entry(IModHelper helper)
        {
            Instance = this;

            // Apply Harmony patches.
            Harmony harmony = new(this.ModManifest.UniqueID);
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

            // Clear the "activated this tick" guard every tick.
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }

        /// <summary>
        /// Waters every tile in the sprinkler's normal radius immediately,
        /// exactly as the game does automatically at 6:00 AM.
        /// </summary>
        /// <param name="sprinkler">The sprinkler object that was tapped/activated.</param>
        /// <param name="who">The farmer who triggered the action.</param>
        public void ActivateSprinkler(SObject sprinkler, Farmer who)
        {
            if (sprinkler == null || who == null)
                return;

            GameLocation location = who.currentLocation;
            if (location == null)
                return;

            Vector2 tile = sprinkler.TileLocation;

            // Prevent double-activation within the same tick (e.g. duplicate input events).
            string key = $"{location.NameOrUniqueName}_{tile.X}_{tile.Y}";
            if (this.ActivatedThisTick.Contains(key))
                return;
            this.ActivatedThisTick.Add(key);

            // Get the tiles this exact sprinkler (including any Pressure Nozzle attachment)
            // would water at 6 AM. This uses the game's own vanilla logic, so radius
            // and shape are always correct for Basic/Quality/Iridium + enrichers.
            List<Vector2> tilesToWater;
            try
            {
                tilesToWater = sprinkler.GetSprinklerTiles();
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Could not compute sprinkler tiles at {tile} in {location.Name}: {ex}", LogLevel.Error);
                return;
            }

            if (tilesToWater == null || tilesToWater.Count == 0)
                return;

            int wateredCount = 0;
            foreach (Vector2 wateredTile in tilesToWater)
            {
                // Water regular dirt (crops planted in the ground).
                if (location.terrainFeatures.TryGetValue(wateredTile, out TerrainFeature feature) && feature is HoeDirt hoeDirt)
                {
                    hoeDirt.state.Value = HoeDirt.watered;
                    wateredCount++;
                }

                // Water garden pots (indoor pots) sitting on a covered tile.
                if (location.Objects.TryGetValue(wateredTile, out SObject obj) && obj is IndoorPot pot)
                {
                    if (pot.hoeDirt?.Value != null)
                    {
                        pot.hoeDirt.Value.state.Value = HoeDirt.watered;
                        wateredCount++;
                    }
                }
            }

            // Feedback: sound + HUD message, no energy or water resource consumed.
            location.playSound("wateringCan");
            Game1.addHUDMessage(new HUDMessage("Sprinkler Activated!", HUDMessage.newQuest_type));

            this.Monitor.Log(
                $"Sprinkler at {tile} in '{location.Name}' activated by {who.Name}; watered {wateredCount} tile(s).",
                LogLevel.Trace);
        }

        /*********
        ** Private methods
        *********/
        /// <summary>Reset the duplicate-activation guard every tick.</summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (this.ActivatedThisTick.Count > 0)
                this.ActivatedThisTick.Clear();
        }
    }
}
