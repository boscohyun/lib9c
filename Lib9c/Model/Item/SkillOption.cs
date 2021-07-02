using System;
using System.Collections.Generic;
using Bencodex.Types;
using Nekoyume.Model.Skill;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Serilog;
using BxDictionary = Bencodex.Types.Dictionary;

namespace Nekoyume.Model.Item
{
    [Serializable]
    public class SkillOption : IItemOption
    {
        public readonly Skill.Skill Skill;

        public int Grade { get; }

        public ItemOptionType Type => ItemOptionType.Skill;

        public SkillOption(int grade, Skill.Skill skill)
        {
            Skill = skill;
            Grade = grade;
            // SkillRow = skillRow;
            // this.power = power;
            // this.chance = chance;
        }

        public SkillOption(IValue serialized)
        {
            try
            {
                var dict = (BxDictionary) serialized;
                Grade = dict["grade"].ToInteger();
                Skill = SkillFactory.Deserialize((BxDictionary) dict["skill"]);
                // SkillRow = SkillSheet.Row.Deserialize((BxDictionary) dict["skill-row"]);
                // power = dict["power"].ToInteger();
                // chance = dict["chance"].ToInteger();
            }
            catch (Exception e) when (e is InvalidCastException || e is KeyNotFoundException)
            {
                Log.Error("{Exception}", e.ToString());
                throw;
            }
        }

        public static SkillOption Deserialize(IValue serialized) => new SkillOption(serialized);

        public IValue Serialize() => BxDictionary.Empty
            .SetItem("grade", Grade.Serialize())
            .SetItem("skill", Skill.Serialize());
            // .SetItem("skill-row", SkillRow.Serialize())
            // .SetItem("power", power.Serialize())
            // .SetItem("chance", chance.Serialize());

        public void Enhance(decimal ratio)
        {
            // power = decimal.ToInt32(power * (1m + ratio));
            // chance = decimal.ToInt32(chance * (1m + ratio));
            Skill.Update(
                decimal.ToInt32(Skill.Chance * (1m + ratio)),
                decimal.ToInt32(Skill.Power * (1m + ratio)));
        }
    }
}
