using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util; // Обязательно для метода .Append()

namespace XSkills
{
    public class CropAutoPatchSystem : ModSystem
    {
        // Указываем порядок загрузки чуть больше стандартного, 
        // чтобы гарантированно выполниться ПОСЛЕ того, как загрузятся сторонние моды.
        public override double ExecuteOrder() => 0.2; 

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);


            int patchedCount = 0;

            // Проходимся по всем блокам, которые игра загрузила в память
            foreach (Block block in api.World.Blocks)
            {
                if (block?.Code == null) continue;

                // Как отличить урожай от земли/камня? 
                // У любых растений, которые сажаются на грядку и растут по стадиям, есть CropProps.
                if (block.CropProps != null)
                {
                    // Проверяем, не пропатчен ли этот блок уже ванильным crops.json
                    if (block.HasBehavior<XSkillsCropBehavior>()) continue;

                    // Создаем наше поведение для этого блока
                    XSkillsCropBehavior customBehavior = new XSkillsCropBehavior(block);
                    
                    // Создаем пустой JSON-объект. Так как ключа "xp" там нет,
                    // твой код в Initialize() сам выставит xp = -1.0f и рассчитает всё автоматически!
                    JsonObject emptyProps = JsonObject.FromJson("{}");
                    customBehavior.Initialize(emptyProps);
                    
                    // Вручную вызываем OnLoaded, так как обычный этап загрузки поведений уже прошел
                    customBehavior.OnLoaded(api);

                    // Добавляем поведение в массив поведений блока
                    block.BlockBehaviors = block.BlockBehaviors.Append(customBehavior);
                    
                    patchedCount++;
                }
            }

            // Выводим в лог сервера сообщение, чтобы видеть, что всё работает
           // api.Logger.Notification($"[XSkills] Автоматически найдено и пропатчено культур из других модов: {patchedCount}");
        }
    }
}