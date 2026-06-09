using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace XLib.XLeveling
{
    /// <summary>
    /// A book that can be used to gain some experience for a specific skill.
    /// Uses the attributes "skill" and "experience".
    /// </summary>
    public class ItemSkillBook : Item
    {
        /// <summary>
        /// XLeveling mod system
        /// </summary>
        protected XLeveling system;

        /// <summary>
        /// When the player has begun using this item for attacking (left mouse click).
        /// Sets the attributes "skill" and "experience" randomly if not set yet.
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="byEntity"></param>
        /// <param name="blockSel"></param>
        /// <param name="entitySel"></param>
        /// <param name="handling"></param>
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
            ItemStack stack = slot.Itemstack;

            string skillName = stack?.Attributes.GetString("skill");
            if (skillName != null) return;
            handling = EnumHandHandling.Handled;

            if (api is ServerCoreAPI)
            {
                Skill skill = system.SkillSetTemplate[byEntity.World.Rand.Next(0, system.SkillSetTemplate.Count)];
                float exp = skill.ExpBase * 0.5f;
                stack.Attributes.SetString("skill", skill.Name);
                stack.Attributes.SetFloat("experience", exp);
                slot.MarkDirty();
            }
        }

        /// <summary>
        /// Server Side: Called one the collectible has been registered Client Side: Called
        /// once the collectible has been loaded from server packet
        /// </summary>
        /// <param name="api"></param>
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            system = XLeveling.Instance(api);
        }

        /// <summary>
        /// Called by the inventory system when you hover over an item stack. This is the item stack name that is getting displayed.
        /// </summary>
        /// <param name="itemStack"></param>
        /// <returns></returns>
        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (itemStack?.Attributes == null)
            {
                return Lang.Get("game:item-" + this.Code?.Path?.Replace("skill", ""));
            }
            string skillName = itemStack.Attributes.GetString("skill");
            float exp = (float)itemStack.Attributes.GetDecimal("experience");
            string knowledge = itemStack.Attributes.GetString("knowledge");

            if (knowledge != null)
            {
                string[] strings = knowledge.Split(':');
                string name;
                if (strings.Length == 2)
                    name = Lang.GetIfExists(strings[0] + ":book-" + strings[1]);
                else
                    name = Lang.GetIfExists("book-" + knowledge);
                if (name != null) return name;
            }

            Skill skill = system.GetSkill(skillName);
            if (skill == null)
                return Lang.Get("game:item-" + this.Code.Path.Replace("skill", ""));
            return skill.DisplayName + ": " + exp.ToString("0.00");
        }

        /// <summary>
        /// Called by the inventory system when you hover over an item stack. This is the text that is getting displayed.
        /// </summary>
        /// <param name="inSlot"></param>
        /// <param name="dsc"></param>
        /// <param name="world"></param>
        /// <param name="withDebugInfo"></param>
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Itemstack?.Attributes == null) return;

            string skillName = inSlot.Itemstack.Attributes.GetString("skill");
            float exp = (float)inSlot.Itemstack.Attributes.GetDecimal("experience");
            string knowledge = inSlot.Itemstack.Attributes.GetString("knowledge");

            bool isStudied = inSlot.Itemstack.Attributes.GetBool("studied", false);
            bool isPreserved = inSlot.Itemstack.Attributes.GetBool("preserved", false);
            string readBy = inSlot.Itemstack.Attributes.GetString("readBy");

            Skill skill = system.GetSkill(skillName);

            if (skill != null && exp != 0.0f)
                dsc.AppendLine(Lang.Get("xlib:skillbook-dsc", skill.DisplayName, exp));
            if (knowledge != null)
                dsc.AppendLine(Lang.Get("xlib:skillbook-dsc2", Lang.Get(knowledge)));

            // Показываем ники ВСЕХ, кто читал книгу (если такие есть)
            if (!string.IsNullOrEmpty(readBy))
            {
                dsc.AppendLine("<font color=\"#ff9999\">" + Lang.Get("xlib:skillbook-studied-by", readBy) + "</font>");
            }
            else if (isStudied) // Фоллбэк для старых книг без ников
            {
                dsc.AppendLine("<font color=\"#ff9999\">" + Lang.Get("xlib:skillbook-studied") + "</font>");
            }

            // Показываем состояние самой книги
            if (isStudied)
            {
                // Текст о том, что книга ветхая и повреждена
                dsc.AppendLine("<font color=\"#ff9999\">" + Lang.Get("xlib:skillbook-damaged") + "</font>");
            }
            else if (isPreserved)
            {
                // Текст о том, что с книгой обращались аккуратно (зелёным цветом)
                dsc.AppendLine("<font color=\"#99ff99\">" + Lang.Get("xlib:skillbook-preserved") + "</font>");
            }
        }

        /// <summary>
        /// Called when the player right clicks while holding this block/item in his hands
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="byEntity"></param>
        /// <param name="blockSel"></param>
        /// <param name="entitySel"></param>
        /// <param name="firstEvent">
        /// True when the player pressed the right mouse button on this block. 
        /// Every subsequent call, while the player holds right mouse down will be false, 
        /// it gets called every second while right mouse is down</param>
        /// <param name="handling">Whether or not to do any subsequent actions. If not set or set to NotHandled, the action will not called on the server.</param>
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

            if (slot.Empty || !firstEvent) return;

            if (slot.Itemstack.Attributes?.GetBool("studied", false) == true)
            {
                return;
            }

            handling = EnumHandHandling.PreventDefault;

            IPlayer player = (byEntity as EntityPlayer)?.Player;

            byEntity.World.PlaySoundAt(new AssetLocation("xlib:sounds/knowledge_consuming"), byEntity, player, true, 16, 1f);

            if (byEntity.World is IServerWorldAccessor)
            {
                if (player == null) return;

                ItemStack readBook = slot.TakeOut(1);

                string skillName = readBook.Attributes.GetString("skill");
                float exp = (float)readBook.Attributes.GetDecimal("experience");
                string knowledge = readBook.Attributes.GetString("knowledge");

                XLeveling system = XLeveling.Instance(api);
                Skill skill = system?.GetSkill(skillName);

                PlayerSkillSet skillSet = byEntity.GetBehavior<PlayerSkillSet>();
                PlayerSkill playerSkill = skill != null ? skillSet?[skill.Id] : null;

                if (exp != 0.0f) playerSkill?.AddExperience(exp, false);

                if (knowledge != null)
                {
                    skillSet.Knowledge.TryGetValue(knowledge, out int value);
                    (XLeveling.Instance(api)?.IXLevelingAPI as XLevelingServer)?.SetPlayerKnowledge(player, knowledge, value + 1);
                }

                bool shouldMarkStudied = true;
                bool perkTriggered = false;

                // Проверяем навык выживания на наличие перка "Аккуратный чтец"
                Skill survivalSkill = system?.GetSkill("survival");
                if (survivalSkill != null)
                {
                    PlayerSkill playerSurvival = skillSet[survivalSkill.Id];

                    Ability carefulReaderAbilityInfo = null;
                    if (survivalSkill.Abilities != null)
                    {
                        foreach (Ability ability in survivalSkill.Abilities)
                        {
                            if (ability.Name.EndsWith("carefulreader"))
                            {
                                carefulReaderAbilityInfo = ability;
                                break;
                            }
                        }
                    }

                    if (carefulReaderAbilityInfo != null && playerSurvival != null)
                    {
                        PlayerAbility carefulReader = playerSurvival[carefulReaderAbilityInfo.Id];

                        if (carefulReader != null && carefulReader.Tier > 0)
                        {
                            float saveChance = carefulReader.Value(0) * 0.01f;

                            if (byEntity.World.Rand.NextDouble() < saveChance)
                            {
                                shouldMarkStudied = false;
                                perkTriggered = true;
                            }
                        }
                    }
                }

                // --- ЛОГИКА СОХРАНЕНИЯ НИКОВ ---
                string existingReaders = readBook.Attributes.GetString("readBy");

                if (string.IsNullOrEmpty(existingReaders))
                {
                    // Если книгу еще никто не читал
                    readBook.Attributes.SetString("readBy", player.PlayerName);
                }
                else
                {
                    // Разбиваем строку на массив ников
                    string[] readers = existingReaders.Split(new string[] { ", " }, StringSplitOptions.None);

                    // Ищем ник текущего игрока. Если его там нет (вернулся -1) — добавляем
                    if (Array.IndexOf(readers, player.PlayerName) == -1)
                    {
                        readBook.Attributes.SetString("readBy", existingReaders + ", " + player.PlayerName);
                    }
                }

                // --- ЛОГИКА СОХРАНЕНИЯ СОСТОЯНИЯ КНИГИ ---
                if (shouldMarkStudied)
                {
                    readBook.Attributes.SetBool("studied", true);
                    readBook.Attributes.RemoveAttribute("preserved"); // Убираем статус сохранённой, если она в итоге сломалась
                }
                else if (perkTriggered)
                {
                    readBook.Attributes.SetBool("preserved", true);
                }

                bool consume = (system?.IXLevelingAPI as XLevelingServer)?.Config?.consumeSkillBookOnStudy ?? false;
                if (!consume)
                {
                    if (!player.InventoryManager.TryGiveItemstack(readBook))
                    {
                        byEntity.World.SpawnItemEntity(readBook, byEntity.Pos.XYZ);
                    }
                }

                slot.MarkDirty();
            }
        }
    }
}