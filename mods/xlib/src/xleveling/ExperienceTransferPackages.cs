using ProtoBuf;

namespace XLib.XLeveling
{
    /// <summary>
    /// Запрос на передачу опыта другому игроку
    /// </summary>
    [ProtoContract]
    public class ExperienceTransferPackage
    {
        /// <summary>
        /// ID передаваемого навыка
        /// </summary>
        [ProtoMember(1)]
        public int skillId;

        /// <summary>
        /// Имя принимающего игрока
        /// </summary>
        [ProtoMember(2)]
        public string targetPlayerName;

        /// <summary>
        /// Количество передаваемых уровней
        /// </summary>
        [ProtoMember(3)]
        public int levels;

        /// <summary>
        /// Количество передаваемого опыта
        /// </summary>
        [ProtoMember(4)]
        public float experience;

        /// <summary>
        /// Создаётся пустой пакет передачи
        /// </summary>
        public ExperienceTransferPackage() { }

        /// <summary>
        /// Создаётся пакет передачи опыта
        /// </summary>
        /// <param name="skillId">ID навыка</param>
        /// <param name="targetPlayerName">Имя игрока</param>
        /// <param name="levels">Количество уровней</param>
        /// <param name="experience">Количество опыта</param>
        public ExperienceTransferPackage(int skillId, string targetPlayerName, int levels, float experience)
        {
            this.skillId = skillId;
            this.targetPlayerName = targetPlayerName;
            this.levels = levels;
            this.experience = experience;
        }
    }

    /// <summary>
    /// Состояние навыка синхронизируется после передачи
    /// </summary>
    [ProtoContract]
    public class ExperienceTransferUpdatePackage
    {
        /// <summary>
        /// ID изменённого навыка
        /// </summary>
        [ProtoMember(1)]
        public int skillId;

        /// <summary>
        /// Итоговый уровень навыка
        /// </summary>
        [ProtoMember(2)]
        public int level;

        /// <summary>
        /// Итоговый опыт навыка
        /// </summary>
        [ProtoMember(3)]
        public float experience;

        /// <summary>
        /// Изменение опыта для уведомления
        /// </summary>
        [ProtoMember(4)]
        public float experienceDelta;

        /// <summary>
        /// Оставшееся время до следующей передачи в секундах
        /// </summary>
        [ProtoMember(5)]
        public float transferCooldown = -1f;

        /// <summary>
        /// Создаётся пустой пакет синхронизации
        /// </summary>
        public ExperienceTransferUpdatePackage() { }

        /// <summary>
        /// Создаётся пакет синхронизации навыка
        /// </summary>
        /// <param name="skillId">ID навыка</param>
        /// <param name="level">Итоговый уровень</param>
        /// <param name="experience">Итоговый опыт</param>
        /// <param name="experienceDelta">Изменение опыта</param>
        public ExperienceTransferUpdatePackage(int skillId, int level, float experience, float experienceDelta)
            : this(skillId, level, experience, experienceDelta, -1f)
        { }

        /// <summary>
        /// Создаётся пакет синхронизации навыка и задержки
        /// </summary>
        /// <param name="skillId">ID навыка</param>
        /// <param name="level">Итоговый уровень</param>
        /// <param name="experience">Итоговый опыт</param>
        /// <param name="experienceDelta">Изменение опыта</param>
        /// <param name="transferCooldown">Оставшееся время до передачи</param>
        public ExperienceTransferUpdatePackage(int skillId, int level, float experience, float experienceDelta, float transferCooldown)
        {
            this.skillId = skillId;
            this.level = level;
            this.experience = experience;
            this.experienceDelta = experienceDelta;
            this.transferCooldown = transferCooldown;
        }
    }
}
