using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace LASK.GoL.CompressBits
{
    [BurstCompile]
    public partial struct SecondAttemptJob : IJobParallelForBatch
    {
        [NativeDisableUnsafePtrRestriction] [ReadOnly]
        public NativeArray<GoLCells> grid;

        [WriteOnly] public NativeArray<GoLCells> nextGrid;
        [ReadOnly] public uint2 gridSize;
        [ReadOnly] public uint2 gridSizeInCompressed;

        public ProfilerMarker neighborMarker;
        public ProfilerMarker aliveMarker;

        private bool IsLogIndex(int index)
        {
            return index is >= 16 * 512 + 7 and <= 16 * 512 + 9 ||
                   index is >= 16 * 513 + 7 and <= 16 * 513 + 9 ||
                   index is >= 16 * 514 + 7 and <= 16 * 514 + 9;
        }

        [BurstCompile]
        public unsafe void Execute(int startIndex, int count)
        {
            const ulong maskNs = 0x7;
            const ulong maskCell = 0x5;
            
            //Neighbor array helps vectorized counting neighbors
            //this way is much fast then in-place alive check
            //inspired from FoneE's implementation
            var neighbors = stackalloc byte[64];
            if (!X86.Popcnt.IsPopcntSupported) return;
            for (int idx = 0; idx < count; idx++)
            {
                var val = 0ul;
                var index = startIndex + idx;

                var cell = grid[index].cells;

                //CPU without SSE4 support will not be able to run this job

                //Edge case might not occurs frequently, tag it as unlikely will improve about 5% performance
                //Access memory as sequential as possible
                var w = Hint.Unlikely(index % gridSizeInCompressed.x == 0)
                    ? 0
                    : (grid[index - 1].cells & 0x8000000000000000) >> 63;

                var e = Hint.Unlikely(index % gridSizeInCompressed.x == gridSizeInCompressed.x - 1)
                    ? 0
                    : grid[index + 1].cells & 1;
                
                var nw = Hint.Unlikely(index % gridSizeInCompressed.x == 0 || index - gridSizeInCompressed.x < 0)
                    ? 0
                    : (grid[index - (int)gridSizeInCompressed.x - 1].cells & 0x8000000000000000) >> 63;
                var n = Hint.Unlikely(index - gridSizeInCompressed.x < 0)
                    ? 0
                    : grid[index - (int)gridSizeInCompressed.x].cells;
                var ne = Hint.Unlikely(index % gridSizeInCompressed.x == gridSizeInCompressed.x - 1 ||
                                       index - gridSizeInCompressed.x < 0)
                    ? 0
                    : (grid[index - (int)gridSizeInCompressed.x + 1].cells) & 1;
               
                var sw = Hint.Unlikely(index % gridSizeInCompressed.x == 0 ||
                                       index + gridSizeInCompressed.x >= grid.Length)
                    ? 0
                    : (grid[index + (int)gridSizeInCompressed.x - 1].cells & 0x8000000000000000) >> 63;
                var s = Hint.Unlikely(index + gridSizeInCompressed.x >= grid.Length)
                    ? 0
                    : grid[index + (int)gridSizeInCompressed.x].cells;

                var se = Hint.Unlikely(index % gridSizeInCompressed.x == gridSizeInCompressed.x - 1 ||
                                       index + gridSizeInCompressed.x >= grid.Length)
                    ? 0
                    : grid[index + (int)gridSizeInCompressed.x + 1].cells & 1;
               
                


                //Here is how huge improvement comes from, we try let compiler generate more vectorization code.
                //It's important that access memory as sequential as possible,
                //in previous implementation I process edge case first then process internal cells,
                //but after change order, performance improved about 80%, that's huge,
                //it's seem Burst generate more efficient assembly code, not only the cache friendly access,
                //but I'm not sure about this, need to investigate assembly more.

                for (int i = 0; i < 63; i++)
                {
                    var isFirst = i == 0;
                    
                    if (Hint.Unlikely(isFirst))
                    {
                        neighbors[i] = (byte)(nw + w + sw + (ulong)X86.Popcnt.popcnt_u64(n & 3) +
                                              (ulong)X86.Popcnt.popcnt_u64(s & 3) + ((cell & 2) >> 1));
                    }
                    else
                    {
                        //cool low level intrinsics for counting neighbors
                        neighbors[i] = (byte)(X86.Popcnt.popcnt_u64(n & (maskNs << (i - 1))) +
                                              X86.Popcnt.popcnt_u64(s & (maskNs << (i - 1))) +
                                              X86.Popcnt.popcnt_u64((maskCell << (i - 1)) & cell));
                    }
                }

                neighbors[63] = (byte)(ne + e + se + (ulong)X86.Popcnt.popcnt_u64(n & (maskNs << 62)) +
                                       (ulong)X86.Popcnt.popcnt_u64(s & (maskNs << 62)) +
                                       ((cell & 0x4000000000000000) >> 62));
                /*
                for (int i = 0; i < 63; i++)
                {
                    var isFirst = i == 0;
                    
                    if (Hint.Unlikely(isFirst))
                    {
                        neighbors[i] = (byte)(nw + w + sw + (ulong)math.countbits(n & 3) +
                                              (ulong)math.countbits(s & 3) + ((cell & 2) >> 1));
                    }
                    else
                    {
                        //cool low level intrinsics for counting neighbors
                        neighbors[i] = (byte)(math.countbits(n & (maskNs << (i - 1))) +
                                              math.countbits(s & (maskNs << (i - 1))) +
                                              math.countbits((maskCell << (i - 1)) & cell));
                    }
                }

                neighbors[63] = (byte)(ne + e + se + (ulong)math.countbits(n & (maskNs << 62)) +
                                       (ulong)math.countbits(s & (maskNs << 62)) +
                                       ((cell & 0x4000000000000000) >> 62));*/

                //Almost fully vectorized by Burst. inspired from FoneE's implementation
                for (int i = 0; i < 64; i++)
                {
                    //ulong mask = 1ul << i;
                    bool isSet = (cell & (1ul << i)) > 0;
                    if (isSet)
                    {
                        bool isAlive = neighbors[i] == 2 || neighbors[i] == 3;
                        //bool isAlive = (neighbors[i] & 6) == 2;
                        val |= isAlive ? (1ul << i) : 0;
                    }
                    else
                    {
                        bool isAlive = neighbors[i] == 3;
                        val |= isAlive ? (1ul << i) : 0;
                    }
                }

                nextGrid[index] = new GoLCells { cells = val };
                UnsafeUtility.MemClear(neighbors, 64);
            }
        }
    }
}