using ImGuiNET;
using OpenTK.Windowing.Desktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.GameContent;
using VSImGui;
using VSImGui.API;
using XLib.XLeveling;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded
{
    public partial class xSkillGraphicalUI : ModSystem
    {
        public static ModConfig config;
        public const string configFileName = "XLeveling/gui/xSkillsGilded.json";
        private string lastLocale = "";
        internal static ICoreClientAPI api;
        private ImGuiModSystem imguiModSystem;

        XLeveling xLeveling;
        XLevelingClient xLevelingClient;
        Dictionary<string, List<PlayerSkill>> skillGroups;
        List<PlayerSkill> allSkills;
        List<PlayerSkill> currentSkills;
        List<PlayerAbility> specializeGroups;
        PlayerSkill currentPlayerSkill;

        Dictionary<PlayerSkill, int> previousLevels;

        const int checkAPIInterval = 1000;
        const int checkLevelInterval = 100;
        private long checkAPIID, checkLevelID;
        bool isReady = false;

        bool metaPage = false;
        public bool isOpen = false;
        int windowX = 0;
        int windowY = 0;
        int windowBaseWidth = 1800;
        int windowBaseHeight = 1060;
        Stopwatch stopwatch;

        Dictionary<string, AbilityButton> abilityButtons;
        List<float> levelRequirementBars;
        List<DecorationLine> decorationLines;

        float abiliyPageWidth = 0;
        float abiliyPageHeight = 0;
        float buttonWidth = 128;
        float buttonHeight = 100;
        float buttonPad = 16;

        float tooltipWidth = 400;
        float contentPadding = 16;

        string page = "";
        int skillPage = 0;

        string currentTooltip = "";
        List<VTMLblock> tooltipVTML;
        AbilityButton hoveringButton;
        TooltipObject hoveringTooltip = null;
        string hoveringID = null;

        EffectBox effectBox;

        public override bool ShouldLoad(EnumAppSide forSide) { return forSide == EnumAppSide.Client; }
        public override double ExecuteOrder() { return 1; }
        private void UpdateLanguageSettings()
        {
            lastLocale = Lang.CurrentLocale;

            // scarab только для английского - при флаге уходим на ImGui-шрифт даже там
            useInternalTextDrawer = config.ForceDisableScarabFont || lastLocale != "en";

            if (!useInternalTextDrawer)
            {
                fTitle.baseLineHeight = ImGui.GetTextLineHeight();
                fTitleGold.baseLineHeight = ImGui.GetTextLineHeight();
                fSubtitle.baseLineHeight = ImGui.GetTextLineHeight();
                fSubtitleGold.baseLineHeight = ImGui.GetTextLineHeight();
            }

            // Очищаем кэш тултипов, чтобы они тоже обновились
            currentTooltip = "";
            hoveringTooltip = null;

            // Пересобираем текущую страницу для обновления кнопок
            if (!string.IsNullOrEmpty(page))
            {
                setPage(page);
            }
        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            xSkillGraphicalUI.api = api;
            resourceLoader.setApi(api);

            try
            {
                config = api.LoadModConfig<ModConfig>(configFileName);
                if (config == null)
                    config = new ModConfig();

                api.StoreModConfig<ModConfig>(config, configFileName);

            }
            catch (Exception)
            {
                config = new ModConfig();
            }

            api.Input.RegisterHotKey("xSkillGilded_v2", Lang.Get("xskillgilded:hotkey-xSkillGilded_v2"), GlKeys.O, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("xSkillGilded_v2", Toggle);

            imguiModSystem = api.ModLoader.GetModSystem<ImGuiModSystem>();
            imguiModSystem.Draw += Draw;
            imguiModSystem.Closed += Close;

            fTitle = new Font().LoadedTexture(api, Sprite("fonts", "scarab"), FontData.SCARAB).setLetterSpacing(2);
            fTitleGold = new Font().LoadedTexture(api, Sprite("fonts", "scarab_gold"), FontData.SCARAB).setLetterSpacing(2).setFallbackColor(c_gold);
            fSubtitle = new Font().LoadedTexture(api, Sprite("fonts", "scarab_small"), FontData.SCARAB_SMALL).setLetterSpacing(1);
            fSubtitleGold = new Font().LoadedTexture(api, Sprite("fonts", "scarab_small_gold"), FontData.SCARAB_SMALL).setLetterSpacing(1).setFallbackColor(c_gold);

            UpdateLanguageSettings();

            tooltipVTML = new List<VTMLblock>();

            stopwatch = Stopwatch.StartNew();
            checkAPIID = api.Event.RegisterGameTickListener(onCheckAPI, checkAPIInterval);
            checkLevelID = api.Event.RegisterGameTickListener(onCheckLevel, checkLevelInterval);

            effectBox = new(api);

            api.Event.KeyDown += (KeyEvent e) =>
            {
                if (e.KeyCode == (int)GlKeys.Escape && isOpen && !api.Input.KeyboardKeyState[(int)GlKeys.ControlLeft])
                {
                    Close();
                    e.Handled = true;
                }
            };
        }

        public void initFonts(HashSet<string> fonts, HashSet<int> sizes)
        { // UNUSED
            fonts.Add(Path.Combine(GamePaths.AssetsPath, "xskillgilded", "fonts", "scarab.ttf"));
        }

        public void onCheckAPI(float dt)
        {
            if (getSkillData()) isReady = true;
            if (isReady) api.Event.UnregisterGameTickListener(checkAPIID);
        }

        public void onCheckLevel(float dt)
        {
            if (previousLevels == null) return;
            if (!config.lvPopupEnabled) return;

            foreach (PlayerSkill skill in previousLevels.Keys.ToList())
            {
                int currentLevel = skill.Level;

                if (currentLevel > previousLevels[skill])
                {
                    try
                    {
                        LevelPopup levelPopup = new(api, skill);
                        api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/levelup.ogg"), false, .3f);
                        api.Logger.Debug($"{skill.Skill.Name}, {skill.Skill.Id} Level up");
                    }
                    catch (Exception ex)
                    {
                        api.Logger.Error($"[xSkillGilded] Ошибка при создании LevelPopup: {ex}");
                    }
                }

                previousLevels[skill] = currentLevel;
            }
        }

        private bool getSkillData()
        {
            xLeveling = api.ModLoader.GetModSystem<XLeveling>();
            if (xLeveling == null) return false;

            xLevelingClient = xLeveling.IXLevelingAPI as XLevelingClient;
            if (xLevelingClient == null) return false;

            effectBox.xLeveling = xLeveling;
            effectBox.xLevelingClient = xLevelingClient;

            PlayerSkillSet playerSkillSet = xLevelingClient.LocalPlayerSkillSet;
            if (playerSkillSet == null) return false;

            skillGroups = new Dictionary<string, List<PlayerSkill>>();
            previousLevels = new Dictionary<PlayerSkill, int>();
            allSkills = new List<PlayerSkill>();
            specializeGroups = new List<PlayerAbility>();

            bool firstGroup = true;
            foreach (PlayerSkill skill in playerSkillSet.PlayerSkills)
            {
                if (!skill.Skill.Enabled) continue;
                if (skill.Hidden) continue;
                if (skill.PlayerAbilities.Count == 0) continue;

                string groupName = skill.Skill.Group;

                if (!skillGroups.ContainsKey(groupName))
                    skillGroups[groupName] = new List<PlayerSkill>();

                List<PlayerSkill> groupList = skillGroups[groupName];
                groupList.Add(skill);
                allSkills.Add(skill);
                previousLevels[skill] = skill.Level;

                if (firstGroup)
                {
                    setPage(groupName);
                    firstGroup = false;
                }

                foreach (PlayerAbility playerAbility in skill.PlayerAbilities)
                {
                    Ability ability = playerAbility.Ability;
                    foreach (Requirement req in ability.Requirements)
                    {
                        if (IsAbilityLimited(req))
                        {
                            specializeGroups.Add(playerAbility);
                            break;
                        }
                    }

                }
            }

            return true;
        }

        private void setPage(string page)
        {
            if (page == "_Specialize")
            {
                this.page = "_Specialize";
                metaPage = true;

                setPageContentList(specializeGroups);
                return;
            }


            //Вкладка с инфой
            if (page == "_CustomInfo")
            {
                this.page = "_CustomInfo";
                metaPage = true;
                abilityButtons = new Dictionary<string, AbilityButton>();
                if (levelRequirementBars != null) levelRequirementBars.Clear();
                if (decorationLines != null) decorationLines.Clear();
                return;
            }

            if (!skillGroups.ContainsKey(page)) return;

            metaPage = false;
            this.page = page;
            currentSkills = skillGroups[page];
            setSkillPage(0);
        }

        private void setSkillPage(int page)
        {
            if (page < 0 || page >= currentSkills.Count) return;
            skillPage = page;
            currentPlayerSkill = currentSkills[page];

            setPageContent();
        }

        private void setPageContent()
        {
            abilityButtons = new Dictionary<string, AbilityButton>();

            float pad = buttonPad;

            List<int> levelTiers = new List<int>();
            List<int> buttonTiers = new List<int>();

            foreach (PlayerAbility ability in currentPlayerSkill.PlayerAbilities)
            {
                if (!ability.IsVisible()) continue;
                int lv = ability.Ability.RequiredLevel(1);

                while (levelTiers.Count <= lv) levelTiers.Add(0);
                levelTiers[lv]++;
            }

            Dictionary<int, int> levelTierMap = new Dictionary<int, int>();
            for (int i = 0, j = 0; i < levelTiers.Count; i++)
            {
                levelTierMap[i] = j;
                if (levelTiers[i] > 0) j++;
            }

            foreach (PlayerAbility ability in currentPlayerSkill.PlayerAbilities)
            {
                if (!ability.IsVisible()) continue;
                string name = ability.Ability.Name;

                int lv = ability.Ability.RequiredLevel(1);
                int tier = levelTierMap[lv];

                while (buttonTiers.Count <= tier) buttonTiers.Add(0);
                buttonTiers[tier]++;

                AbilityButton button = new AbilityButton(ability);

                button.tier = tier;
                abilityButtons[name] = button;
            }

            Dictionary<int, int> buttonTierMap = new Dictionary<int, int>();
            List<float> tierX = new List<float>();

            for (int i = 0, j = 0; i < buttonTiers.Count; i++)
            {
                buttonTierMap[i] = j;
                if (buttonTiers[i] > 0) j++;
                tierX.Add(0);
            }

            float minx = 99999;
            float miny = 99999;
            float maxx = -99999;
            float maxy = -99999;

            foreach (AbilityButton button in abilityButtons.Values)
            {
                int tier = buttonTierMap[button.tier];
                int roww = buttonTiers[button.tier];

                float _x = tierX[tier] - (roww - 1) / 2 * (buttonWidth + pad);
                float _y = -tier * (buttonHeight + pad);
                tierX[tier] += buttonWidth + pad;

                button.x = _x;
                button.y = _y;

                minx = Math.Min(minx, button.x);
                miny = Math.Min(miny, button.y);

                maxx = Math.Max(maxx, button.x + buttonWidth);
                maxy = Math.Max(maxy, button.y + buttonHeight);
            }

            float cx = (minx + maxx) / 2;
            float cy = (miny + maxy) / 2;

            foreach (AbilityButton button in abilityButtons.Values)
            {
                button.x -= cx;
                button.y -= cy;
            }

            abiliyPageWidth = maxx - minx;
            abiliyPageHeight = maxy - miny;

            levelRequirementBars = new List<float>();
            for (int i = 0; i < levelTiers.Count; i++)
            {
                if (levelTiers[i] > 0)
                    levelRequirementBars.Add(i);
            }

            decorationLines = new List<DecorationLine>();

            foreach (AbilityButton button in abilityButtons.Values)
            {
                float x0 = button.x;
                float y0 = button.y;

                foreach (Requirement req in button.Ability.Ability.Requirements)
                {
                    ExclusiveAbilityRequirement req2 = req as ExclusiveAbilityRequirement;
                    if (req2 != null)
                    {
                        string name = req2.Ability.Name;
                        if (abilityButtons.ContainsKey(name))
                        {
                            AbilityButton _button = abilityButtons[name];
                            float x1 = _button.x;
                            float y1 = _button.y;

                            decorationLines.Add(new(x0, y0, x1, y1, new(165 / 255f, 98 / 255f, 67 / 255f, .5f)));
                        }
                    }
                }
            }
        }

        private void setPageContentList(List<PlayerAbility> abilityList)
        {
            abilityButtons = new Dictionary<string, AbilityButton>();
            levelRequirementBars.Clear();
            decorationLines.Clear();

            float pad = buttonPad;

            int amo = abilityList.Count;
            int col = (int)Math.Floor(Math.Sqrt((double)amo));
            int indx = 0;

            float minx = 99999;
            float miny = 99999;
            float maxx = -99999;
            float maxy = -99999;

            for (int i = 0; i < amo; i++)
            {
                PlayerAbility ability = abilityList[i];
                if (!ability.IsVisible()) continue;

                int c = indx % col;
                int r = indx / col;
                indx++;

                string name = ability.Ability.Name;
                int lv = ability.Ability.RequiredLevel(0);

                AbilityButton button = new AbilityButton(ability);

                button.x = c * (buttonWidth + pad);
                button.y = r * (buttonHeight + pad);

                abilityButtons[name] = button;

                minx = Math.Min(minx, button.x);
                miny = Math.Min(miny, button.y);

                maxx = Math.Max(maxx, button.x + buttonWidth);
                maxy = Math.Max(maxy, button.y + buttonHeight);
            }

            float cx = (minx + maxx) / 2;
            float cy = (miny + maxy) / 2;

            foreach (AbilityButton button in abilityButtons.Values)
            {
                button.x -= cx;
                button.y -= cy;
            }

            abiliyPageWidth = maxx - minx;
            abiliyPageHeight = maxy - miny;
        }

        private bool IsAbilityLimited(Requirement Requirement)
        {
            LimitationRequirement limitation = Requirement as LimitationRequirement;
            if (limitation != null) return true;

            AndRequirement and = Requirement as AndRequirement;
            if (and != null)
            {
                foreach (Requirement req in and.Requirements)
                {
                    if (IsAbilityLimited(req))
                        return true;
                }
            }

            NotRequirement not = Requirement as NotRequirement;
            if (not != null)
            {
                if (IsAbilityLimited(not.Requirement))
                    return true;
            }

            return false;
        }

        private void OnSparringToggle(bool toggle)
        {
            xLevelingClient.LocalPlayerSkillSet.Sparring = toggle;
            CommandPackage package = new CommandPackage(EnumXLevelingCommand.SparringMode, toggle ? 1 : 0);
            xLevelingClient.SendPackage(package);
        }

        private void Open()
        {
            if (isOpen) return;

            if (!isReady)
            {
                onCheckAPI(0);
                if (!isReady) return;
            }

            isOpen = true;
            imguiModSystem.Show();
            api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/open.ogg"), false, .3f);
        }

        private void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            SetLayoutEdit(false);   // закрыли окно - редактор расположения не должен остаться взведённым
            api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/close.ogg"), false, .3f);
        }

        private bool Toggle(KeyCombination _)
        {
            if (isOpen) Close();
            else Open();
            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
            api.Event.UnregisterGameTickListener(checkLevelID);
        }
    }
}