using ProtoBuf;

namespace XLib.XLeveling
{
    /// <summary>
    /// Пакет запроса передачи опыта другому игроку
    /// </summary>
    [ProtoContract]
    public class ExperienceTransferPackage
    {
        /// <summary>
        /// Навык для передачи опыта
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
        /// Пустой пакет создаётся для сериализации
        /// </summary>
        public ExperienceTransferPackage() { }

        /// <summary>
        /// Пакет заполняется данными передачи
        /// </summary>
        public ExperienceTransferPackage(int skillId, string targetPlayerName, int levels, float experience)
        {
            this.skillId = skillId;
            this.targetPlayerName = targetPlayerName;
            this.levels = levels;
            this.experience = experience;
        }
    }

    /// <summary>
    /// Пакет синхронизации навыка после передачи опыта
    /// </summary>
    [ProtoContract]
    public class ExperienceTransferUpdatePackage
    {
        /// <summary>
        /// Навык для синхронизации
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
        /// Изменение опыта для отображения
        /// </summary>
        [ProtoMember(4)]
        public float experienceDelta;

        /// <summary>
        /// Пустой пакет создаётся для сериализации
        /// </summary>
        public ExperienceTransferUpdatePackage() { }

        /// <summary>
        /// Пакет заполняется итоговым состоянием навыка
        /// </summary>
        public ExperienceTransferUpdatePackage(int skillId, int level, float experience, float experienceDelta)
        {
            this.skillId = skillId;
            this.level = level;
            this.experience = experience;
            this.experienceDelta = experienceDelta;
        }
    }
}
