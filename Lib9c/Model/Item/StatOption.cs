using System;
using System.Collections.Generic;
using Bencodex.Types;
using Nekoyume.Model.Stat;
using Nekoyume.Model.State;
using Serilog;
using BxBinary = Bencodex.Types.Binary;
using BxDictionary = Bencodex.Types.Dictionary;

namespace Nekoyume.Model.Item
{
    [Serializable]
    public class StatOption : IItemOption
    {
        public readonly StatType StatType;

        public decimal statValue;

        public int Grade { get; }

        public ItemOptionType Type => ItemOptionType.Stat;

        public StatOption(int grade, StatType statType, decimal statValue = default)
        {
            Grade = grade;
            StatType = statType;
            this.statValue = statValue;
        }

        public StatOption(int grade, DecimalStat decimalStat) : this(grade, decimalStat.Type, decimalStat.Value)
        {
        }

        public StatOption(int grade, StatMap statMap) : this(grade, statMap.StatType, statMap.Value)
        {
        }

        public StatOption(IValue serialized)
        {
            try
            {
                var dict = (BxDictionary) serialized;
                Grade = dict["grade"].ToInteger();
                StatType = StatTypeExtension.Deserialize((BxBinary) dict["stat-type"]);
                statValue = dict["stat-value"].ToDecimal();
            }
            catch (Exception e) when (e is InvalidCastException || e is KeyNotFoundException)
            {
                Log.Error("{Exception}", e.ToString());
                throw;
            }
        }

        public static StatOption Deserialize(IValue serialized) => new StatOption(serialized);
        
        public static bool TryDeserialize(IValue serialized, out StatOption statOption)
        {
            statOption = serialized is BxDictionary
                ? Deserialize(serialized)
                : null;
            
            return (statOption is null);
        }

        public IValue Serialize() => BxDictionary.Empty
            .SetItem("grade", Grade.Serialize())
            .SetItem("stat-type", StatType.Serialize())
            .SetItem("stat-value", statValue.Serialize());

        public void Enhance(decimal ratio)
        {
            statValue *= 1m + ratio;
        }
    }
}
