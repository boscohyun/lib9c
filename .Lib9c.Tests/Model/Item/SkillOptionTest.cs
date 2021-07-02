namespace Lib9c.Tests.Model.Item
{
    using System;
    using System.Linq;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.Stat;
    using Nekoyume.TableData;
    using Xunit;

    public class SkillOptionTest
    {
        private static readonly StatType[] StatTypes = Enum.GetValues(typeof(StatType))
            .Cast<StatType>()
            .Where(e => e != StatType.NONE)
            .ToArray();

        private readonly SkillSheet _skillSheet;

        public SkillOptionTest()
        {
            var csv = TableSheetsImporter.ImportSheets()[nameof(SkillSheet)];
            _skillSheet = new SkillSheet();
            _skillSheet.Set(csv);
        }

        [Fact]
        public void Serialize()
        {
            var random = new Random(DateTime.UtcNow.Millisecond);
            foreach (var grade in Enumerable.Range(1, 10))
            {
                foreach (var row in _skillSheet)
                {
                    var damage = random.Next();
                    var chance = random.Next();
                    var option = new SkillOption(grade, row, damage, chance);
                    var serialized = option.Serialize();
                    var deserialized = new SkillOption(serialized);
                    Assert.Equal(option.Grade, deserialized.Grade);
                    Assert.Equal(option.SkillRow, deserialized.SkillRow);
                    Assert.Equal(option.power, deserialized.power);
                    Assert.Equal(option.chance, deserialized.chance);
                }
            }
        }

        [Theory]
        [InlineData(.1, 1, 1)]
        [InlineData(.9, 1, 1)]
        [InlineData(1.1, 1, 2)]
        [InlineData(1.9, 1, 2)]
        [InlineData(.1, 1000, 1100)]
        [InlineData(.9, 1000, 1900)]
        [InlineData(-.1, 1, 0)]
        [InlineData(-.9, 1, 0)]
        [InlineData(-1.1, 1, 0)]
        [InlineData(-1.9, 1, 0)]
        [InlineData(-2.1, 1, -1)]
        [InlineData(-2.9, 1, -1)]
        [InlineData(-.1, 1000, 900)]
        [InlineData(-.9, 1000, 100)]
        public void Enhance(decimal ratio, int from, int to)
        {
            var option = new SkillOption(default, default, from, from);
            option.Enhance(ratio);
            Assert.Equal(to, option.power);
            Assert.Equal(to, option.chance);
        }
    }
}
