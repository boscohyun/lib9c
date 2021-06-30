namespace Lib9c.Tests.Model.Stat
{
    using System;
    using System.Linq;
    using Nekoyume.Model.Stat;
    using Xunit;

    public class EquipmentStatsTest
    {
        private static readonly StatType[] StatTypes = Enum.GetValues(typeof(StatType))
            .Cast<StatType>()
            .Where(e => e != StatType.NONE)
            .ToArray();

        public static EquipmentStats CreateEquipmentStats(
            StatType? baseStatType = default,
            decimal? baseValue = default,
            int? optionCount = default,
            int? enhancementLevel = default)
        {
            var random = new Random(DateTime.UtcNow.Millisecond);
            baseStatType = baseStatType.HasValue
                ? baseStatType
                : StatTypes[random.Next(0, StatTypes.Length)];
            baseValue = baseValue.HasValue
                ? baseValue
                : (decimal)random.NextDouble() / 1000m;
            var equipmentState = new EquipmentStats(baseStatType.Value, baseValue.Value);

            for (var i = optionCount; i > 0; i--)
            {
                var optionStatType = StatTypes[random.Next(0, StatTypes.Length)];
                var optionValue = (decimal)random.NextDouble() / 1000m;
                equipmentState.AddOptionStat(optionStatType, optionValue, 1);
            }

            for (var i = enhancementLevel; i > 0; i--)
            {
                equipmentState.Enhance();
            }

            return equipmentState;
        }

        [Fact]
        public void Serialize()
        {
            foreach (var baseStatType in StatTypes)
            {
                foreach (var optionCount in Enumerable.Range(0, 10))
                {
                    foreach (var enhancementLevel in Enumerable.Range(0, 11))
                    {
                        var equipmentStats = CreateEquipmentStats(
                            baseStatType,
                            optionCount: optionCount,
                            enhancementLevel: enhancementLevel);
                        var serialized = equipmentStats.Serialize();
                        var deserialized = new EquipmentStats((Bencodex.Types.Dictionary)serialized);
                        Assert.Equal(equipmentStats, deserialized);
                    }
                }
            }
        }

        [Fact]
        public void GetBaseAndAdditionalRawStats()
        {
            var equipmentStats = CreateEquipmentStats(optionCount: 10, enhancementLevel: 10);
            var tuples = equipmentStats.GetBaseAndAdditionalRawStats().ToArray();
            foreach (var (statType, baseValue, additionalValue) in tuples)
            {
                switch (statType)
                {
                    case StatType.HP:
                        Assert.Equal(equipmentStats.HPAsDecimal, baseValue + additionalValue);
                        Assert.Equal(equipmentStats.BaseHPAsDecimal, baseValue);
                        Assert.Equal(equipmentStats.AdditionalHPAsDecimal, additionalValue);
                        break;
                    case StatType.ATK:
                        Assert.Equal(equipmentStats.ATKAsDecimal, baseValue + additionalValue);
                        Assert.Equal(equipmentStats.BaseATKAsDecimal, baseValue);
                        Assert.Equal(equipmentStats.AdditionalATKAsDecimal, additionalValue);
                        break;
                    case StatType.DEF:
                        Assert.Equal(equipmentStats.DEFAsDecimal, baseValue + additionalValue);
                        Assert.Equal(equipmentStats.BaseDEFAsDecimal, baseValue);
                        Assert.Equal(equipmentStats.AdditionalDEFAsDecimal, additionalValue);
                        break;
                    case StatType.CRI:
                        Assert.Equal(equipmentStats.CRIAsDecimal, baseValue + additionalValue);
                        Assert.Equal(equipmentStats.BaseCRIAsDecimal, baseValue);
                        Assert.Equal(equipmentStats.AdditionalCRIAsDecimal, additionalValue);
                        break;
                    case StatType.HIT:
                        Assert.Equal(equipmentStats.HITAsDecimal, baseValue + additionalValue);
                        Assert.Equal(equipmentStats.BaseHITAsDecimal, baseValue);
                        Assert.Equal(equipmentStats.AdditionalHITAsDecimal, additionalValue);
                        break;
                    case StatType.SPD:
                        Assert.Equal(equipmentStats.SPDAsDecimal, baseValue + additionalValue);
                        Assert.Equal(equipmentStats.BaseSPDAsDecimal, baseValue);
                        Assert.Equal(equipmentStats.AdditionalSPDAsDecimal, additionalValue);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StatsMapCompatible(bool addBaseAdditionalValue)
        {
            var random = new Random(DateTime.UtcNow.Millisecond);
            foreach (var baseStatType in StatTypes)
            {
                foreach (var optionCount in Enumerable.Range(0, 3))
                {
                    foreach (var enhancementLevel in Enumerable.Range(0, 11))
                    {
                        // Prepare StatsMap
                        var baseValue = (decimal)random.NextDouble() / 1000m;
                        var statsMap = new StatsMap();
                        statsMap.SetStatValue(baseStatType, baseValue);

                        // Option
                        for (var i = optionCount; i > 0; i--)
                        {
                            var optionStatType = addBaseAdditionalValue && i == optionCount
                                ? baseStatType
                                : StatTypes[random.Next(0, StatTypes.Length)];
                            var optionValue = (decimal)random.NextDouble() / 1000m;
                            statsMap.AddStatAdditionalValue(optionStatType, optionValue);
                        }

                        // Enhancement
                        for (var i = enhancementLevel; i > 0; i--)
                        {
                            // Enhancement. Ref Nekoyume.Model.Item.Equipment.LevelUp()
                            var enhanceValue = Math.Max(1.0m, statsMap.GetStat(baseStatType, true) * 0.1m);
                            statsMap.AddStatValue(baseStatType, enhanceValue);

                            if (i != 3 && i != 7 && i != 10)
                            {
                                continue;
                            }

                            foreach (var additionalStat in statsMap.GetAdditionalStats())
                            {
                                statsMap.SetStatAdditionalValue(
                                    additionalStat.StatType,
                                    additionalStat.AdditionalValue * 1.3m);
                            }
                        }

                        var equipmentStats = new EquipmentStats(enhancementLevel, baseStatType, statsMap);
                        var statsMap2 = new StatsMap(equipmentStats);
                        Assert.Equal(statsMap, statsMap2);
                        var equipmentStats2 = new EquipmentStats(enhancementLevel, baseStatType, statsMap2);
                        Assert.Equal(equipmentStats, equipmentStats2);
                    }
                }
            }
        }
    }
}
