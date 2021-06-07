namespace Lib9c.Tests.Action
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Bencodex.Types;
    using Libplanet;
    using Libplanet.Action;
    using Libplanet.Assets;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Model.State;
    using Nekoyume.TableData;
    using Xunit;

    public class MonsterCollectTest
    {
        private readonly TableSheets _tableSheets;
        private readonly Address _signer;
        private IAccountStateDelta _initialState;

        public MonsterCollectTest()
        {
            Dictionary<string, string> sheets = TableSheetsImporter.ImportSheets();
            _tableSheets = new TableSheets(sheets);
            _signer = default;
            var currency = new Currency("NCG", 2, minters: null);
            var goldCurrencyState = new GoldCurrencyState(currency);
            _initialState = new State()
                .SetState(Addresses.GoldCurrency, goldCurrencyState.Serialize());
            foreach ((string key, string value) in sheets)
            {
                _initialState = _initialState
                    .SetState(Addresses.TableSheet.Derive(key), value.Serialize())
                    .SetState(_signer, new AgentState(_signer).Serialize());
            }
        }

        [Theory]
        [InlineData(true, 2, 1, MonsterCollectionState.LockUpInterval)]
        [InlineData(true, 5, 2, MonsterCollectionState.LockUpInterval + 1)]
        [InlineData(false, 1, 3, 1)]
        [InlineData(false, 3, 4, MonsterCollectionState.RewardInterval * 3)]
        public void Execute(bool exist, int level, int prevLevel, long blockIndex)
        {
            Address monsterCollectionAddress = MonsterCollectionState.DeriveAddress(_signer, 0);
            Currency currency = _initialState.GetGoldCurrency();
            FungibleAssetValue balance = 0 * currency;
            FungibleAssetValue requiredGold = 0 * currency;
            if (exist)
            {
                List<MonsterCollectionRewardSheet.RewardInfo> rewards = _tableSheets.MonsterCollectionRewardSheet[prevLevel].Rewards;
                MonsterCollectionState prevMonsterCollectionState = new MonsterCollectionState(monsterCollectionAddress, prevLevel, 0, _tableSheets.MonsterCollectionRewardSheet);
                _initialState = _initialState.SetState(monsterCollectionAddress, prevMonsterCollectionState.Serialize());
                for (int i = 0; i < prevLevel; i++)
                {
                    MonsterCollectionSheet.Row row = _tableSheets.MonsterCollectionSheet[i + 1];
                    balance += row.RequiredGold * currency;
                    _initialState = _initialState.MintAsset(monsterCollectionAddress, row.RequiredGold * currency);
                }

                Assert.All(prevMonsterCollectionState.RewardLevelMap, kv => Assert.Equal(rewards, kv.Value));
            }

            for (int i = 1; i < level + 1; i++)
            {
                MonsterCollectionSheet.Row row = _tableSheets.MonsterCollectionSheet[i];
                requiredGold += row.RequiredGold * currency;
                _initialState = _initialState.MintAsset(_signer, row.RequiredGold * currency);
            }

            MonsterCollect action = new MonsterCollect
            {
                level = level,
            };

            IAccountStateDelta nextState = action.Execute(new ActionContext
            {
                PreviousStates = _initialState,
                Signer = _signer,
                BlockIndex = blockIndex,
            });

            MonsterCollectionState nextMonsterCollectionState = new MonsterCollectionState((Dictionary)nextState.GetState(monsterCollectionAddress));
            AgentState nextAgentState = nextState.GetAgentState(_signer);
            Assert.Equal(level, nextMonsterCollectionState.Level);
            Assert.Equal(blockIndex, nextMonsterCollectionState.StartedBlockIndex);
            Assert.Equal(0, nextMonsterCollectionState.ReceivedBlockIndex);
            Assert.Equal(0, nextMonsterCollectionState.ExpiredBlockIndex);
            Assert.Equal(requiredGold, nextState.GetBalance(monsterCollectionAddress, currency));
            Assert.Equal(balance, nextState.GetBalance(_signer, currency));
            Assert.Equal(0, nextAgentState.MonsterCollectionRound);
        }

        [Fact]
        public void Execute_Throw_FailedLoadStateException()
        {
            MonsterCollect action = new MonsterCollect
            {
                level = 1,
            };

            Assert.Throws<FailedLoadStateException>(() => action.Execute(new ActionContext
            {
                PreviousStates = new State(),
                Signer = _signer,
                BlockIndex = 1,
            }));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(100)]
        public void Execute_Throw_SheetRowNotFoundException(int level)
        {
            Assert.False(_tableSheets.MonsterCollectionSheet.Keys.Contains(level));

            MonsterCollect action = new MonsterCollect
            {
                level = level,
            };

            Assert.Throws<SheetRowNotFoundException>(() => action.Execute(new ActionContext
            {
                PreviousStates = _initialState,
                Signer = _signer,
                BlockIndex = 1,
            }));
        }

        [Fact]
        public void Execute_Throw_InsufficientBalanceException()
        {
            MonsterCollect action = new MonsterCollect
            {
                level = 1,
            };

            Assert.Throws<InsufficientBalanceException>(() => action.Execute(new ActionContext
            {
                PreviousStates = _initialState,
                Signer = _signer,
                BlockIndex = 1,
            }));
        }

        [Theory]
        [InlineData(2, 1, 1)]
        [InlineData(3, 0, MonsterCollectionState.ExpirationIndex - 1)]
        public void Execute_Throw_RequiredBlockIndexException(int prevLevel, int level, long blockIndex)
        {
            Address collectionAddress = MonsterCollectionState.DeriveAddress(_signer, 0);
            MonsterCollectionState prevMonsterCollectionState = new MonsterCollectionState(collectionAddress, prevLevel, 0, _tableSheets.MonsterCollectionRewardSheet);
            _initialState = _initialState.SetState(collectionAddress, prevMonsterCollectionState.Serialize());

            MonsterCollect action = new MonsterCollect
            {
                level = level,
            };

            Assert.Throws<RequiredBlockIndexException>(() => action.Execute(new ActionContext
            {
                PreviousStates = _initialState,
                Signer = _signer,
                BlockIndex = blockIndex,
            }));
        }

        [Fact]
        public void Rehearsal()
        {
            Address collectionAddress = MonsterCollectionState.DeriveAddress(_signer, 0);
            MonsterCollect action = new MonsterCollect
            {
                level = 1,
            };
            IAccountStateDelta nextState = action.Execute(new ActionContext
            {
                PreviousStates = new State(),
                Signer = _signer,
                Rehearsal = true,
            });

            List<Address> updatedAddresses = new List<Address>()
            {
                _signer,
                collectionAddress,
            };

            Assert.Equal(updatedAddresses.ToImmutableHashSet(), nextState.UpdatedAddresses);
        }
    }
}
