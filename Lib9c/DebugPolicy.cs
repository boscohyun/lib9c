using System;
using System.Collections.Generic;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Tx;
using Nekoyume.Action;

namespace Lib9c
{
    public class DebugPolicy : IBlockPolicy<PolymorphicAction<ActionBase>>
    {
        public IComparer<IBlockExcerpt> CanonicalChainComparer { get; } = new TotalDifficultyComparer();

        public IAction BlockAction { get; } = new RewardGold();

        public InvalidBlockException ValidateNextBlock(
            BlockChain<PolymorphicAction<ActionBase>> blocks,
            Block<PolymorphicAction<ActionBase>> nextBlock
        )
        {
            return null;
        }

        public long GetNextBlockDifficulty(BlockChain<PolymorphicAction<ActionBase>> blocks)
        {
            return blocks.Tip is null ? 0 : 1;
        }

        public int MaxTransactionsPerBlock { get; } = int.MaxValue;

        public int GetMaxBlockBytes(long index) => int.MaxValue;

        public bool DoesTransactionFollowsPolicy(
            Transaction<PolymorphicAction<ActionBase>> transaction,
            BlockChain<PolymorphicAction<ActionBase>> blockChain
        ) =>
            true;
    }
}
