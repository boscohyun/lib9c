using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using static Lib9c.SerializeKeys;

namespace Nekoyume.Action
{
    [Serializable]
    [ActionType("monster_collect2")]
    public class MonsterCollect : GameAction
    {
        public int level;
        public override IAccountStateDelta Execute(IActionContext context)
        {
            IAccountStateDelta states = context.PreviousStates;
            Address monsterCollectionAddress = MonsterCollectionState.DeriveAddress(context.Signer, 0);
            if (context.Rehearsal)
            {
                return states
                    .SetState(monsterCollectionAddress, MarkChanged)
                    .SetState(context.Signer, MarkChanged)
                    .MarkBalanceChanged(GoldCurrencyMock, context.Signer, monsterCollectionAddress);
            }

            MonsterCollectionSheet monsterCollectionSheet = states.GetSheet<MonsterCollectionSheet>();

            AgentState agentState = states.GetAgentState(context.Signer);
            if (agentState is null)
            {
                throw new FailedLoadStateException("Aborted as the agent state failed to load.");
            }

            if (level < 0 || level > 0 && !monsterCollectionSheet.TryGetValue(level, out MonsterCollectionSheet.Row _))
            {
                throw new SheetRowNotFoundException(nameof(MonsterCollectionSheet), level);
            }

            Currency currency = states.GetGoldCurrency();
            // Set default gold value.
            FungibleAssetValue requiredGold = currency * 0;
            FungibleAssetValue balance = states.GetBalance(context.Signer, states.GetGoldCurrency());

            MonsterCollectionState monsterCollectionState;
            if (states.TryGetState(monsterCollectionAddress, out Dictionary stateDict))
            {
                monsterCollectionState = new MonsterCollectionState(stateDict);
                int currentLevel = monsterCollectionState.Level;
                // 락업 확인
                if (level < currentLevel && monsterCollectionState.IsLock(context.BlockIndex))
                {
                    throw new RequiredBlockIndexException();
                }

                // 언스테이킹
                FungibleAssetValue gold = currency * 0;
                for (int i = currentLevel; i > 0; i--)
                {
                    gold += currency * monsterCollectionSheet[i].RequiredGold;
                }
                states = states.TransferAsset(monsterCollectionAddress, context.Signer, gold);
                Debug.Assert(states.GetBalance(monsterCollectionAddress, currency).Equals(0 * currency));
            }
            monsterCollectionState = new MonsterCollectionState(monsterCollectionAddress, level, context.BlockIndex);

            for (int i = 0; i < level; i++)
            {
                requiredGold += currency * monsterCollectionSheet[i + 1].RequiredGold;
            }

            if (balance < requiredGold)
            {
                throw new InsufficientBalanceException(context.Signer, requiredGold,
                    $"There is no sufficient balance for {context.Signer}: {balance} < {requiredGold}");
            }
            states = states.TransferAsset(context.Signer, monsterCollectionAddress, requiredGold);
            states = states.SetState(monsterCollectionAddress, monsterCollectionState.SerializeV2());
            return states;
        }

        protected override IImmutableDictionary<string, IValue> PlainValueInternal => new Dictionary<string, IValue>
        {
            [LevelKey] = level.Serialize(),
        }.ToImmutableDictionary();

        protected override void LoadPlainValueInternal(IImmutableDictionary<string, IValue> plainValue)
        {
            level = plainValue[LevelKey].ToInteger();
        }
    }
}
