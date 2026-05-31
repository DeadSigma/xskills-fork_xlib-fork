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

            string skillName = inSlot.Itemstack.Attributes.GetString("skill");
            float exp = (float)inSlot.Itemstack.Attributes.GetDecimal("experience");
            string knowledge = inSlot.Itemstack.Attributes.GetString("knowledge");

            // Проверяем, есть ли флаг прочитанной книги
            bool isStudied = inSlot.Itemstack.Attributes.GetBool("studied", false);

            Skill skill = system.GetSkill(skillName);

            if (skill != null && exp != 0.0f)
                dsc.AppendLine(Lang.Get("xlib:skillbook-dsc", skill.DisplayName, exp));
            if (knowledge != null)
                dsc.AppendLine(Lang.Get("xlib:skillbook-dsc2", Lang.Get(knowledge)));

            if (isStudied)
            {
                dsc.AppendLine("<font color=\"#ff9999\">" + Lang.Get("xlib:skillbook-studied") + "</font>");
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

            if (slot.Itemstack.Attributes.GetBool("studied", false))
            {
                return;
            }

            handling = EnumHandHandling.PreventDefault;

            // 1. Сначала получаем объект игрока (работает и на клиенте, и на сервере)
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

                readBook.Attributes.SetBool("studied", true);

                if (!player.InventoryManager.TryGiveItemstack(readBook))
                {
                    byEntity.World.SpawnItemEntity(readBook, byEntity.Pos.XYZ);
                }

                slot.MarkDirty();
            }
        }
    }
}
