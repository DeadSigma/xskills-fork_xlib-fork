using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PandaXPDrops
{
    /// <summary>
    /// Optional bridge to the standalone "xSkills Gilded" mod.
    /// </summary>
    /// <remarks>
    /// Gilded draws its skill window with ImGui (VSImGui), not with the game's dialog system, so it can only be
    /// moved by itself - this class merely forwards the edit hotkey to it. Everything is resolved by reflection:
    /// xlibfork must not gain a hard dependency on a mod that may not be installed.
    /// The whole integration is two members of <c>xSkillGilded.xSkillGraphicalUI</c>: the public field
    /// <c>isOpen</c> and the method <c>ToggleLayoutEdit(string)</c>.
    /// </remarks>
    internal class GildedLayoutBridge
    {
        /// <summary>Full type name of the Gilded mod system.</summary>
        private const string SystemTypeName = "xSkillGilded.xSkillGraphicalUI";

        private readonly ModSystem system;
        private readonly FieldInfo isOpenField;
        private readonly MethodInfo toggleMethod;

        private GildedLayoutBridge(ModSystem system, FieldInfo isOpenField, MethodInfo toggleMethod)
        {
            this.system = system;
            this.isOpenField = isOpenField;
            this.toggleMethod = toggleMethod;
        }

        /// <summary>Whether the Gilded window is currently open, i.e. whether there is anything to position.</summary>
        public bool IsWindowOpen
        {
            get
            {
                try
                {
                    return isOpenField.GetValue(system) is bool open && open;
                }
                catch (Exception ex)
                {
                    PandaXPDropsSystem.Logger?.Warning("[PandaXPDrops] Could not read xSkills Gilded state: {0}", ex);
                    return false;
                }
            }
        }

        /// <summary>Hands the edit hotkey over to Gilded.</summary>
        /// <param name="exitKeyName">Key shown in Gilded's on screen hint.</param>
        /// <returns><c>true</c> when Gilded took over; <c>false</c> means its window is closed.</returns>
        public bool TryToggleLayoutEdit(string exitKeyName)
        {
            try
            {
                return toggleMethod.Invoke(system, new object[] { exitKeyName }) is bool ok && ok;
            }
            catch (Exception ex)
            {
                PandaXPDropsSystem.Logger?.Error("[PandaXPDrops] xSkills Gilded layout edit failed:\n{0}", ex);
                return false;
            }
        }

        /// <summary>
        /// Looks the Gilded mod system up. Called once at start; the mod loader has created every system by then,
        /// regardless of <c>ExecuteOrder</c>.
        /// </summary>
        /// <param name="capi">Client API.</param>
        /// <returns>The bridge, or <c>null</c> when Gilded is missing or too old to support edit mode.</returns>
        public static GildedLayoutBridge TryCreate(ICoreClientAPI capi)
        {
            ModSystem system = null;
            foreach (ModSystem candidate in capi.ModLoader.Systems)
            {
                if (candidate.GetType().FullName == SystemTypeName)
                {
                    system = candidate;
                    break;
                }
            }

            if (system == null) return null;

            Type type = system.GetType();
            FieldInfo isOpenField = AccessTools.Field(type, "isOpen");
            MethodInfo toggleMethod = AccessTools.Method(type, "ToggleLayoutEdit", new[] { typeof(string) });

            if (isOpenField == null || isOpenField.FieldType != typeof(bool) || toggleMethod == null)
            {
                capi.Logger.Notification("[PandaXPDrops] xSkills Gilded found, but without layout edit support - the hotkey stays on the XP drops HUD.");
                return null;
            }

            capi.Logger.Notification("[PandaXPDrops] xSkills Gilded found, the edit hotkey will position its window while it is open.");
            return new GildedLayoutBridge(system, isOpenField, toggleMethod);
        }
    }
}
