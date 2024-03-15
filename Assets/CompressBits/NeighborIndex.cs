using System.Runtime.CompilerServices;
using Unity.Burst;

namespace LASK.GoL.CompressBits
{
    [BurstCompile]
    public static class NeighborIndex
    {
        public static readonly int[] NeighborIndexTable = new[]
        {
            0,1,2,3,4,5,6,7,
            8,16,24,32,40,48,
            15,23,31,39,47,55,
            56,57,58,59,60,61,62,63,
        };
        
        public static readonly int[] IndexToNeighborIndexTable = new[]
        {
            0,1,2,3,4,5,6,7,
            8,-1,-1,-1,-1,-1,-1,14,
            9,-1,-1,-1,-1,-1,-1,15,
            10,-1,-1,-1,-1,-1,-1,16,
            11,-1,-1,-1,-1,-1,-1,17,
            12,-1,-1,-1,-1,-1,-1,18,
            13,-1,-1,-1,-1,-1,-1,19,
            20,21,22,23,24,25,26,27,
        };
        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToBitIndex(int neighborIndex)
        {
            return NeighborIndexTable[neighborIndex];
        }
        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexToNeighborIndex(int index)
        {
            return IndexToNeighborIndexTable[index];
        }
    }
}