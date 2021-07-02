using System;
using System.Runtime.Serialization;
using Bencodex.Types;
using Nekoyume.TableData;

namespace Nekoyume.Model.Item
{
    [Serializable]
    public class Weapon : Equipment
    {
        public Weapon(EquipmentItemSheet.Row data, Guid id, long requiredBlockIndex) : base(data, id, requiredBlockIndex)
        {
        }
        
        public Weapon(
            int serializedVersion,
            EquipmentItemSheet.Row data,
            Guid id,
            long requiredBlockIndex,
            int requiredCharacterLevel)
            : base(serializedVersion, data, id, requiredBlockIndex, requiredCharacterLevel)
        {
        }

        public Weapon(Dictionary serialized) : base(serialized)
        {
        }
        
        protected Weapon(SerializationInfo info, StreamingContext _)
            : this((Dictionary) Codec.Decode((byte[]) info.GetValue("serialized", typeof(byte[]))))
        {
        }
    }
}
