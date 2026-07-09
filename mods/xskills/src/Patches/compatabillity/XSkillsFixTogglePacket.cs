using ProtoBuf;

namespace XSkills
{
    // Пакет клиент - сервер: игрок переключил фиксацию доп. слотов Strongback. Хоткей - клиентский ввод, а сортировка StorageTweaks читает IsFixed на сервере, поэтому состояние нужно доставить на серверный экземпляр инвентаря.
    [ProtoContract]
    public class XSkillsFixTogglePacket
    {
        [ProtoMember(1)]
        public bool Fixed;
    }
    [ProtoContract]
    public class XSkillsFixRequestPacket { }
}
