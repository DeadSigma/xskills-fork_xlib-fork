using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace XSkills
{
    public class FarmingAutoPatchSystem : ModSystem
    {
        // Загружаемся чуть позже обычного, чтобы перехватить блоки из других модов
        public override double ExecuteOrder() => 0.2;

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            // Вызываем наши аккуратные методы
            int patchedCrops = PatchCrops(api);
            int patchedGrass = PatchGrass(api);

        }

        private int PatchCrops(ICoreAPI api)
        {
            int count = 0;
            JsonObject emptyProps = JsonObject.FromJson("{}");

            foreach (Block block in api.World.Blocks)
            {
                if (block?.Code == null || block.CropProps == null) continue;
                if (block.HasBehavior<XSkillsCropBehavior>()) continue;

                XSkillsCropBehavior customBehavior = new XSkillsCropBehavior(block);
                customBehavior.Initialize(emptyProps);
                customBehavior.OnLoaded(api);

                block.BlockBehaviors = block.BlockBehaviors.Append(customBehavior);
                count++;
            }
            return count;
        }

        private int PatchGrass(ICoreAPI api)
        {
            int count = 0;
            JsonObject grassProps = JsonObject.FromJson("{\"xp\": 0.0}");

            foreach (Block block in api.World.Blocks)
            {
                if (block?.Code == null || !block.Code.Path.StartsWith("tallgrass-")) continue;
                if (block.HasBehavior<XSkillsGrassBehavior>()) continue;

                XSkillsGrassBehavior grassBehavior = new XSkillsGrassBehavior(block);
                grassBehavior.Initialize(grassProps);
                grassBehavior.OnLoaded(api);

                block.BlockBehaviors = block.BlockBehaviors.Append(grassBehavior);
                count++;
            }
            return count;
        }
    }
}