using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using Nekoyume.Model.State;
using Serilog;
using BxDictionary = Bencodex.Types.Dictionary;
using BxList = Bencodex.Types.List;
using BxText = Bencodex.Types.Text;

namespace Nekoyume.Model.Stat
{
    /// <summary>
    /// 장비의 스탯을 관리한다.
    /// 스탯은 장비의 기본 스탯인 _baseStat과
    /// 제작과 강화에 의한 _optionStats
    /// 마지막으로 모든 스탯을 합한 EquipmentStats 순서로 계산한다.
    /// </summary>
    [Serializable]
    public class EquipmentStats : IBaseAndAdditionalStats
    {
        [Serializable]
        public class GradeAndStat : IState
        {
            public readonly int Grade;
            public readonly EnhancedDecimalStat EnhancedStat;

            public GradeAndStat(int grade, EnhancedDecimalStat enhancedStat)
            {
                Grade = grade;
                EnhancedStat = enhancedStat;
            }

            public GradeAndStat(BxDictionary serialized)
            {
                try
                {
                    Grade = serialized["grade"].ToInteger();
                    EnhancedStat = serialized["enhanced-stat"].ToEnhancedDecimalStat();
                }
                catch (InvalidCastException e)
                {
                    Log.Error("{Exception}", e.ToString());
                    throw;
                }
                catch (KeyNotFoundException e)
                {
                    Log.Error("{Exception}", e.ToString());
                    throw;
                }
            }

            #region IState

            public IValue Serialize() => new BxDictionary()
                .SetItem("grade", Grade.Serialize())
                .SetItem("enhanced-stat", EnhancedStat.Serialize());

            #endregion

            protected bool Equals(GradeAndStat other)
            {
                return Grade == other.Grade && Equals(EnhancedStat, other.EnhancedStat);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((GradeAndStat) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Grade * 397) ^ (EnhancedStat != null ? EnhancedStat.GetHashCode() : 0);
                }
            }
        }

        private readonly EnhancedDecimalStat _baseStat;

        private readonly List<GradeAndStat> _optionStats = new List<GradeAndStat>();
        public IReadOnlyList<GradeAndStat> OptionStats => _optionStats;

        [NonSerialized]
        private readonly List<EnhancedDecimalStat> _allStats = new List<EnhancedDecimalStat>();
        
        [NonSerialized]
        private readonly DecimalStat _hp = new DecimalStat(StatType.HP);
        
        [NonSerialized]
        private readonly DecimalStat _atk = new DecimalStat(StatType.ATK);
        
        [NonSerialized]
        private readonly DecimalStat _def = new DecimalStat(StatType.DEF);
        
        [NonSerialized]
        private readonly DecimalStat _cri = new DecimalStat(StatType.CRI);
        
        [NonSerialized]
        private readonly DecimalStat _hit = new DecimalStat(StatType.HIT);
        
        [NonSerialized]
        private readonly DecimalStat _spd = new DecimalStat(StatType.SPD);

        public StatType BaseStatType => _baseStat.Type;

        public decimal BaseStatValue => _baseStat.Value;

        public EquipmentStats(StatType statType, decimal statValue)
        {
            _baseStat = new EnhancedDecimalStat(statType, statValue);
            _allStats.Add(_baseStat);
            UpdateTotalStats();
        }

        public EquipmentStats(int enhancementLevel, StatType baseStatType, StatsMap statsMap)
        {
            if (enhancementLevel < 0)
            {
                throw new ArgumentException($"{nameof(enhancementLevel)} should greater than or equal to 0");
            }

            if (statsMap is null)
            {
                throw new ArgumentNullException(nameof(statsMap));
            }

            (decimal, decimal) SeparateValue(decimal value, bool isBaseStat = default)
            {
                if (enhancementLevel == 0)
                {
                    return (value, 0m);
                }
                
                var level = enhancementLevel;
                var baseValue = 0m;
                while (level > 0)
                {
                    baseValue = value / (isBaseStat ? 110m : 130m) * 100m;
                    level--;
                }
                
                return (baseValue, value - baseValue);
            }
            
            var statMaps = statsMap.GetStats();
            foreach (var statMapEx in statMaps)
            {
                EnhancedDecimalStat optionStat;
                if (statMapEx.StatType == baseStatType)
                {
                    var (baseValue, enhancedValue) = SeparateValue(statMapEx.Value, true);
                    _baseStat = new EnhancedDecimalStat(
                        statMapEx.StatType,
                        baseValue,
                        enhancedValue);
                    _allStats.Add(_baseStat);
                    
                    (baseValue, enhancedValue) = SeparateValue(statMapEx.AdditionalValue);
                    if (baseValue == 0m && enhancedValue == 0m)
                    {
                        continue;
                    }
                    
                    optionStat = new EnhancedDecimalStat(
                        statMapEx.StatType,
                        baseValue,
                        enhancedValue);
                }
                else
                {
                    var (baseValue, enhancedValue) = SeparateValue(
                        statMapEx.Value + statMapEx.AdditionalValue);
                    optionStat = new EnhancedDecimalStat(
                        statMapEx.StatType,
                        baseValue,
                        enhancedValue);
                }
                
                // NOTE: Set option grade to `1` because `StatsMap` has no option level.
                _optionStats.Add(new GradeAndStat(1, optionStat));
                _allStats.Add(optionStat);
            }
            
            UpdateTotalStats();
        }
        
        public EquipmentStats(BxDictionary serialized)
        {
            try
            {
                _baseStat = serialized["base-stat"].ToEnhancedDecimalStat();
                _optionStats = serialized["option-stats"]
                    .ToList(e => new GradeAndStat((BxDictionary) e));
            }
            catch (InvalidCastException e)
            {
                Log.Error("{Exception}", e.ToString());
                throw;
            }
            catch (KeyNotFoundException e)
            {
                Log.Error("{Exception}", e.ToString());
                throw;
            }

            _allStats.Add(_baseStat);
            for (var i = _optionStats.Count; i > 0; i--)
            {
                _allStats.Add(_optionStats[i - 1].EnhancedStat);
            }

            UpdateTotalStats();
        }

        protected bool Equals(EquipmentStats other)
        {
            if (_optionStats is null || other._optionStats is null)
            {
                return false;
            }
            
            return Equals(_baseStat, other._baseStat) &&
                   _optionStats.SequenceEqual(other._optionStats);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EquipmentStats) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_baseStat != null ? _baseStat.GetHashCode() : 0) * 397) ^ (_optionStats != null ? _optionStats.GetHashCode() : 0);
            }
        }

        #region IState

        public IValue Serialize() => BxDictionary.Empty
            .SetItem("base-stat", _baseStat.Serialize())
            .SetItem("option-stats", _optionStats.Select(e => e.Serialize()).Serialize());

        #endregion
        
        #region IBaseAndAdditionalStats

        public int HP => _hp.ValueAsInt;
        public decimal HPAsDecimal => _hp.Value;
        public int ATK => _atk.ValueAsInt;
        public decimal ATKAsDecimal => _atk.Value;
        public int DEF => _def.ValueAsInt;
        public decimal DEFAsDecimal => _def.Value;
        public int CRI => _cri.ValueAsInt;
        public decimal CRIAsDecimal => _cri.Value;
        public int HIT => _hit.ValueAsInt;
        public decimal HITAsDecimal => _hit.Value;
        public int SPD => _spd.ValueAsInt;
        public decimal SPDAsDecimal => _spd.Value;

        public bool HasHP => HPAsDecimal > 0m;
        public bool HasATK => ATKAsDecimal > 0m;
        public bool HasDEF => DEFAsDecimal > 0m;
        public bool HasCRI => CRIAsDecimal > 0m;
        public bool HasHIT => HITAsDecimal > 0m;
        public bool HasSPD => SPDAsDecimal > 0m;
        
        public int BaseHP => _baseStat.Type == StatType.HP ? _baseStat.ValueAsInt : 0;
        public decimal BaseHPAsDecimal => _baseStat.Type == StatType.HP ? _baseStat.Value : 0m;
        public int BaseATK => _baseStat.Type == StatType.ATK ? _baseStat.ValueAsInt : 0;
        public decimal BaseATKAsDecimal => _baseStat.Type == StatType.ATK ? _baseStat.Value : 0m;
        public int BaseDEF => _baseStat.Type == StatType.DEF ? _baseStat.ValueAsInt : 0;
        public decimal BaseDEFAsDecimal => _baseStat.Type == StatType.DEF ? _baseStat.Value : 0m;
        public int BaseCRI => _baseStat.Type == StatType.CRI ? _baseStat.ValueAsInt : 0;
        public decimal BaseCRIAsDecimal => _baseStat.Type == StatType.CRI ? _baseStat.Value : 0m;
        public int BaseHIT => _baseStat.Type == StatType.HIT ? _baseStat.ValueAsInt : 0;
        public decimal BaseHITAsDecimal => _baseStat.Type == StatType.HIT ? _baseStat.Value : 0m;
        public int BaseSPD => _baseStat.Type == StatType.SPD ? _baseStat.ValueAsInt : 0;
        public decimal BaseSPDAsDecimal => _baseStat.Type == StatType.SPD ? _baseStat.Value : 0m;

        public bool HasBaseHP => _baseStat.Type == StatType.HP && _baseStat.Value > 0m;
        public bool HasBaseATK => _baseStat.Type == StatType.ATK && _baseStat.Value > 0m;
        public bool HasBaseDEF => _baseStat.Type == StatType.DEF && _baseStat.Value > 0m;
        public bool HasBaseCRI => _baseStat.Type == StatType.CRI && _baseStat.Value > 0m;
        public bool HasBaseHIT => _baseStat.Type == StatType.HIT && _baseStat.Value > 0m;
        public bool HasBaseSPD => _baseStat.Type == StatType.SPD && _baseStat.Value > 0m;

        public int AdditionalHP => _baseStat.Type == StatType.HP
            ? HP - _baseStat.ValueAsInt
            : HP;
        public decimal AdditionalHPAsDecimal => _baseStat.Type == StatType.HP
            ? HPAsDecimal - _baseStat.Value
            : HPAsDecimal;
        public int AdditionalATK => _baseStat.Type == StatType.ATK
            ? ATK - _baseStat.ValueAsInt
            : ATK;
        public decimal AdditionalATKAsDecimal => _baseStat.Type == StatType.ATK
            ? ATKAsDecimal - _baseStat.Value
            : ATKAsDecimal;
        public int AdditionalDEF => _baseStat.Type == StatType.DEF
            ? DEF - _baseStat.ValueAsInt
            : DEF;
        public decimal AdditionalDEFAsDecimal => _baseStat.Type == StatType.DEF
            ? DEFAsDecimal - _baseStat.Value
            : DEFAsDecimal;
        public int AdditionalCRI => _baseStat.Type == StatType.CRI
            ? CRI - _baseStat.ValueAsInt
            : CRI;
        public decimal AdditionalCRIAsDecimal => _baseStat.Type == StatType.CRI
            ? CRIAsDecimal - _baseStat.Value
            : CRIAsDecimal;
        public int AdditionalHIT => _baseStat.Type == StatType.HIT
            ? HIT - _baseStat.ValueAsInt
            : HIT;
        public decimal AdditionalHITAsDecimal => _baseStat.Type == StatType.HIT
            ? HITAsDecimal - _baseStat.Value
            : HITAsDecimal;
        public int AdditionalSPD => _baseStat.Type == StatType.SPD
            ? SPD - _baseStat.ValueAsInt
            : SPD;
        public decimal AdditionalSPDAsDecimal => _baseStat.Type == StatType.SPD
            ? SPDAsDecimal - _baseStat.Value
            : SPDAsDecimal;

        public bool HasAdditionalHP => AdditionalHPAsDecimal > 0m;
        public bool HasAdditionalATK => AdditionalATKAsDecimal > 0m;
        public bool HasAdditionalDEF => AdditionalDEFAsDecimal > 0m;
        public bool HasAdditionalCRI => AdditionalCRIAsDecimal > 0m;
        public bool HasAdditionalHIT => AdditionalHITAsDecimal > 0m;
        public bool HasAdditionalSPD => AdditionalSPDAsDecimal > 0m;

        public bool HasAdditionalStats => HasAdditionalHP ||
                                          HasAdditionalATK ||
                                          HasAdditionalDEF ||
                                          HasAdditionalCRI ||
                                          HasAdditionalHIT ||
                                          HasAdditionalSPD;

        public IEnumerable<(StatType statType, int value)> GetStats(bool ignoreZero = default)
        {
            if (ignoreZero)
            {
                if (HasHP)
                {
                    yield return (StatType.HP, HP);
                }

                if (HasATK)
                {
                    yield return (StatType.ATK, ATK);
                }

                if (HasDEF)
                {
                    yield return (StatType.DEF, DEF);
                }

                if (HasCRI)
                {
                    yield return (StatType.CRI, CRI);
                }

                if (HasHIT)
                {
                    yield return (StatType.HIT, HIT);
                }

                if (HasSPD)
                {
                    yield return (StatType.SPD, SPD);
                }
            }
            else
            {
                yield return (StatType.HP, HP);
                yield return (StatType.ATK, ATK);
                yield return (StatType.DEF, DEF);
                yield return (StatType.CRI, CRI);
                yield return (StatType.HIT, HIT);
                yield return (StatType.SPD, SPD);
            }
        }

        public IEnumerable<(StatType statType, decimal value)> GetRawStats(bool ignoreZero = default)
        {
            if (ignoreZero)
            {
                if (HasHP)
                {
                    yield return (StatType.HP, HPAsDecimal);
                }

                if (HasATK)
                {
                    yield return (StatType.ATK, ATKAsDecimal);
                }

                if (HasDEF)
                {
                    yield return (StatType.DEF, DEFAsDecimal);
                }

                if (HasCRI)
                {
                    yield return (StatType.CRI, CRIAsDecimal);
                }

                if (HasHIT)
                {
                    yield return (StatType.HIT, HITAsDecimal);
                }

                if (HasSPD)
                {
                    yield return (StatType.SPD, SPDAsDecimal);
                }
            }
            else
            {
                yield return (StatType.HP, HPAsDecimal);
                yield return (StatType.ATK, ATKAsDecimal);
                yield return (StatType.DEF, DEFAsDecimal);
                yield return (StatType.CRI, CRIAsDecimal);
                yield return (StatType.HIT, HITAsDecimal);
                yield return (StatType.SPD, SPDAsDecimal);
            }
        }
        
        public IEnumerable<(StatType statType, int baseValue)> GetBaseStats(bool ignoreZero = default)
        {
            if (ignoreZero)
            {
                if (HasBaseHP)
                {
                    yield return (StatType.HP, BaseHP);
                }

                if (HasBaseATK)
                {
                    yield return (StatType.ATK, BaseATK);
                }

                if (HasBaseDEF)
                {
                    yield return (StatType.DEF, BaseDEF);
                }

                if (HasBaseCRI)
                {
                    yield return (StatType.CRI, BaseCRI);
                }

                if (HasBaseHIT)
                {
                    yield return (StatType.HIT, BaseHIT);
                }

                if (HasBaseSPD)
                {
                    yield return (StatType.SPD, BaseSPD);
                }
            }
            else
            {
                yield return (StatType.HP, BaseHP);
                yield return (StatType.ATK, BaseATK);
                yield return (StatType.DEF, BaseDEF);
                yield return (StatType.CRI, BaseCRI);
                yield return (StatType.HIT, BaseHIT);
                yield return (StatType.SPD, BaseSPD);
            }
        }

        public IEnumerable<(StatType statType, int additionalValue)> GetAdditionalStats(bool ignoreZero = default)
        {
            if (ignoreZero)
            {
                if (HasAdditionalHP)
                {
                    yield return (StatType.HP, AdditionalHP);
                }

                if (HasAdditionalATK)
                {
                    yield return (StatType.ATK, AdditionalATK);
                }

                if (HasAdditionalDEF)
                {
                    yield return (StatType.DEF, AdditionalDEF);
                }

                if (HasAdditionalCRI)
                {
                    yield return (StatType.CRI, AdditionalCRI);
                }

                if (HasAdditionalHIT)
                {
                    yield return (StatType.HIT, AdditionalHIT);
                }

                if (HasAdditionalSPD)
                {
                    yield return (StatType.SPD, AdditionalSPD);
                }
            }
            else
            {
                yield return (StatType.HP, AdditionalHP);
                yield return (StatType.ATK, AdditionalATK);
                yield return (StatType.DEF, AdditionalDEF);
                yield return (StatType.CRI, AdditionalCRI);
                yield return (StatType.HIT, AdditionalHIT);
                yield return (StatType.SPD, AdditionalSPD);
            }
        }

        public IEnumerable<(StatType statType, int baseValue, int additionalValue)> GetBaseAndAdditionalStats(
            bool ignoreZero = default)
        {
            if (ignoreZero)
            {
                if (HasBaseHP || HasAdditionalHP)
                {
                    yield return (StatType.HP, BaseHP, AdditionalHP);
                }

                if (HasBaseATK || HasAdditionalATK)
                {
                    yield return (StatType.ATK, BaseATK, AdditionalATK);
                }

                if (HasBaseDEF || HasAdditionalDEF)
                {
                    yield return (StatType.DEF, BaseDEF, AdditionalDEF);
                }

                if (HasBaseCRI || HasAdditionalCRI)
                {
                    yield return (StatType.CRI, BaseCRI, AdditionalCRI);
                }

                if (HasBaseHIT || HasAdditionalHIT)
                {
                    yield return (StatType.HIT, BaseHIT, AdditionalHIT);
                }

                if (HasBaseSPD || HasAdditionalSPD)
                {
                    yield return (StatType.SPD, BaseSPD, AdditionalSPD);
                }
            }
            else
            {
                yield return (StatType.HP, BaseHP, AdditionalHP);
                yield return (StatType.ATK, BaseATK, AdditionalATK);
                yield return (StatType.DEF, BaseDEF, AdditionalDEF);
                yield return (StatType.CRI, BaseCRI, AdditionalCRI);
                yield return (StatType.HIT, BaseHIT, AdditionalHIT);
                yield return (StatType.SPD, BaseSPD, AdditionalSPD);
            }
        }

        public IEnumerable<(StatType statType, decimal baseValue, decimal additionalValue)> GetBaseAndAdditionalRawStats(
            bool ignoreZero = default)
        {
            if (ignoreZero)
            {
                if (HasHP)
                {
                    yield return (StatType.HP, BaseHPAsDecimal, AdditionalHPAsDecimal);
                }

                if (HasATK)
                {
                    yield return (StatType.ATK, BaseATKAsDecimal, AdditionalATKAsDecimal);
                }

                if (HasDEF)
                {
                    yield return (StatType.DEF, BaseDEFAsDecimal, AdditionalDEFAsDecimal);
                }

                if (HasCRI)
                {
                    yield return (StatType.CRI, BaseCRIAsDecimal, AdditionalCRIAsDecimal);
                }

                if (HasHIT)
                {
                    yield return (StatType.HIT, BaseHITAsDecimal, AdditionalHITAsDecimal);
                }

                if (HasSPD)
                {
                    yield return (StatType.SPD, BaseSPDAsDecimal, AdditionalSPDAsDecimal);
                }
            }
            else
            {
                yield return (StatType.HP, BaseHPAsDecimal, AdditionalHPAsDecimal);
                yield return (StatType.ATK, BaseATKAsDecimal, AdditionalATKAsDecimal);
                yield return (StatType.DEF, BaseDEFAsDecimal, AdditionalDEFAsDecimal);
                yield return (StatType.CRI, BaseCRIAsDecimal, AdditionalCRIAsDecimal);
                yield return (StatType.HIT, BaseHITAsDecimal, AdditionalHITAsDecimal);
                yield return (StatType.SPD, BaseSPDAsDecimal, AdditionalSPDAsDecimal);
            }
        }

        #endregion
        
        public void AddOptionStat(StatType statType, decimal statValue, int optionGrade)
        {
            var stat = new EnhancedDecimalStat(statType, statValue);
            _optionStats.Add(new GradeAndStat(optionGrade, stat));
            _allStats.Add(stat);
            UpdateTotalStats();
        }

        // TODO: Implement new enhancement logic
        public void Enhance()
        {
            _baseStat.enhancedValue *= 1.3m;

            for (var i = _optionStats.Count; i > 0; i--)
            {
                var optionStat = _optionStats[i - 1];
                optionStat.EnhancedStat.enhancedValue *= 1.3m;
            }

            UpdateTotalStats();
        }

        private void UpdateTotalStats()
        {
            Reset();

            for (var i = _allStats.Count; i > 0; i--)
            {
                var stat = _allStats[i - 1];
                switch (stat.Type)
                {
                    case StatType.NONE:
                        break;
                    case StatType.HP:
                        _hp.AddValue(Math.Max(0, stat.Value));
                        break;
                    case StatType.ATK:
                        _atk.AddValue(Math.Max(0, stat.Value));
                        break;
                    case StatType.DEF:
                        _def.AddValue(Math.Max(0, stat.Value));
                        break;
                    case StatType.CRI:
                        _cri.AddValue(Math.Max(0, stat.Value));
                        break;
                    case StatType.HIT:
                        _hit.AddValue(Math.Max(0, stat.Value));
                        break;
                    case StatType.SPD:
                        _spd.AddValue(Math.Max(0, stat.Value));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        
        private void Reset()
        {
            _hp.Reset();
            _atk.Reset();
            _def.Reset();
            _cri.Reset();
            _hit.Reset();
            _spd.Reset();
        }
    }
}
