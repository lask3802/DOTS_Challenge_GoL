using System.Runtime.CompilerServices;
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
    public partial struct BatchGroupingJob : IJobParallelForBatch
    {
        [NativeDisableUnsafePtrRestriction] [ReadOnly]
        public NativeArray<GoLCells> grid;

        [WriteOnly] public NativeArray<GoLCells> nextGrid;
        [ReadOnly] public uint2 gridSize;
        [ReadOnly] public uint2 gridSizeInCompressed;
        [ReadOnly] const int batchCnt = 4;
        public ProfilerMarker neighborMarker;
        public ProfilerMarker aliveMarker;

        private bool IsLogIndex(int index)
        {
            return index is >= 16 * 512 + 7 and <= 16 * 512 + 9 ||
                   index is >= 16 * 513 + 7 and <= 16 * 513 + 9 ||
                   index is >= 16 * 514 + 7 and <= 16 * 514 + 9;
        }

        [BurstCompile]
        public void Execute(int startIndex, int count)
        {
            for (int i = startIndex; i < startIndex + count; i += batchCnt)
            {
                BatchJob(i);
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void BatchJob(int startIndex)
        {
            const ulong maskNs = 0x7;
            const ulong maskCell = 0x5;
            
            //Neighbor array helps vectorized counting neighbors
            //this way is much fast then in-place alive check
            //inspired from FoneE's implementation
            var neighbors = stackalloc byte[64 * batchCnt];
            
            var result = stackalloc ulong[batchCnt];
            var nGroup = stackalloc byte[batchCnt];
            var sGroup = stackalloc byte[batchCnt];
            var eGroup = stackalloc byte[batchCnt];
            var wGroup = stackalloc byte[batchCnt];
            var neGroup = stackalloc byte[batchCnt];
            var nwGroup = stackalloc byte[batchCnt];
            var swGroup = stackalloc byte[batchCnt];
            var seGroup = stackalloc byte[batchCnt];
            var cellGroup = grid.Reinterpret<ulong>().Slice(startIndex, batchCnt);
            var target = nextGrid.GetSubArray(startIndex, batchCnt).Reinterpret<ulong>();
           

            //neighbor loop
            for (var batchIdx = 0; batchIdx < 4; batchIdx++)
            {
                var index = startIndex + batchIdx;
                         
                //Edge case might not occurs frequently, tag it as unlikely will improve about 5% performance
                //Access memory as sequential as possible
                
                wGroup[batchIdx] = (byte)(Hint.Unlikely(index % gridSizeInCompressed.x == 0)
                    ? 0
                    : (grid[index - 1].cells & 0x8000000000000000) >> 63);
                eGroup[batchIdx] = (byte)(Hint.Unlikely(index % gridSizeInCompressed.x == gridSizeInCompressed.x - 1)
                    ? 0
                    : grid[index + 1].cells & 1);
                
                nwGroup[batchIdx] = (byte)(
                    Hint.Unlikely(index % gridSizeInCompressed.x == 0 || index - gridSizeInCompressed.x < 0)
                        ? 0
                        : (grid[index - (int)gridSizeInCompressed.x - 1].cells & 0x8000000000000000) >> 63);
                nGroup[batchIdx] = (byte)(Hint.Unlikely(index - gridSizeInCompressed.x < 0)
                    ? 0
                    : grid[index - (int)gridSizeInCompressed.x].cells);
                neGroup[batchIdx] = (byte)(Hint.Unlikely(index % gridSizeInCompressed.x == gridSizeInCompressed.x - 1 ||
                                                 index - gridSizeInCompressed.x < 0)
                    ? 0
                    : (grid[index - (int)gridSizeInCompressed.x + 1].cells) & 1);

                swGroup[batchIdx] = (byte)(Hint.Unlikely(index % gridSizeInCompressed.x == 0 ||
                                                         index + gridSizeInCompressed.x >= grid.Length)
                    ? 0
                    : (grid[index + (int)gridSizeInCompressed.x - 1].cells & 0x8000000000000000) >> 63);
                sGroup[batchIdx] = (byte)(Hint.Unlikely(index + gridSizeInCompressed.x >= grid.Length)
                    ? 0
                    : grid[index + (int)gridSizeInCompressed.x].cells);
                seGroup[batchIdx] = (byte)(Hint.Unlikely(index % gridSizeInCompressed.x == gridSizeInCompressed.x - 1 ||
                                                 index + gridSizeInCompressed.x >= grid.Length)
                    ? 0
                    : grid[index + (int)gridSizeInCompressed.x + 1].cells & 1);
                
                neighbors[64 * batchIdx] = (byte)(nwGroup[batchIdx] + wGroup[batchIdx] + swGroup[batchIdx] + (byte)math.countbits(nGroup[batchIdx] & 3) +
                                                  (byte)math.countbits(sGroup[batchIdx] & 3) +
                                                  (byte)((cellGroup[batchIdx] & 2) >> 1));
                for (int i = 1; i < 63; i++)
                {
                       
                    neighbors[64 * batchIdx + i] = (byte)(math.countbits(nGroup[batchIdx] & (maskNs << (i - 1))) +
                                                          math.countbits(sGroup[batchIdx] & (maskNs << (i - 1))) +
                                                          math.countbits((maskCell << (i - 1)) & cellGroup[batchIdx]));
                    
                }

                neighbors[64 * batchIdx + 63] =
                    (byte)(neGroup[batchIdx] + eGroup[batchIdx] + seGroup[batchIdx] + (byte)math.countbits(nGroup[batchIdx] & (maskNs << 62)) +
                           (byte)math.countbits(sGroup[batchIdx] & (maskNs << 62)) +
                           (byte)((cellGroup[batchIdx] & 0x4000000000000000) >> 62));
               
            }
           
        
            for (int batches = 0; batches < batchCnt; batches++)
            {
                    for (int i = 0; i < 64*batchCnt; i++)
                    {
                        var batchIdx = i / 64;
                        //ulong mask = 1ul << i;
                        bool isSet = (cellGroup[batchIdx] & (1ul << i)) > 0;
                        if (isSet)
                        {
                            bool isAlive = neighbors[i] == 2 || neighbors[i] == 3;
                            //bool isAlive = (neighbors[i] & 6) == 2;
                            result[batchIdx] |= isAlive ? (1ul << i) : 0;
                        }
                        else
                        {
                            bool isAlive = neighbors[i] == 3;
                            result[batchIdx] |= isAlive ? (1ul << i) : 0;
                        }
                    }
                    neighbors+=64;
            }
            for (int i = 0; i < batchCnt; i++)
            {
                target[i] = result[i];
            }
        }
    }
}