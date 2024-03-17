namespace LASK.GoL.CompressBits
{
    using System.Runtime.CompilerServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;

    [BurstCompile]
    public partial struct LiarWrapJob : IJobParallelForBatch
    {
        [NativeDisableUnsafePtrRestriction] [ReadOnly]
        public NativeArray<GoLCells> grid;

        [WriteOnly] public NativeArray<GoLCells> nextGrid;

        public int ArrayWidth;
        public int ArrayHeight;


        /* square bit layout, just show edges to figure out how edge case works
         *
         * 56 57 58 59 60 61 62 63
         * 48                   55
         * 40                   47
         * 32                   39
         * 24                   31
         * 16  ...              23
         * 8                    15
         * 0 1 2 3 4 5 6         7
         */


        /* neighbor layout
         * 20 21 22 23 24 25 26 27
         * 13                   19
         * 12                   18
         * 11                   17
         * 10                   16
         * 9                    15
         * 8                    14
         * 0 1 2 3 4 5 6        7
         */


        [BurstCompile]
        public unsafe void Execute(int startIndex, int count)
        {
            const int neighborCounts = 28;
            var neighbors = stackalloc byte[neighborCounts];

            const ulong northMask = 0x7;
            const ulong southMask = 0x07_00_00_00_00_00_00_00;
            const ulong westMask = 0x808080;
            const ulong eastMask = 0x010101;
            const ulong northWestMask = 0x80;
            const ulong northEastMask = 0x1;
            const ulong southWestMask = 0x80_00_00_00_00_00_00_00;
            const ulong southEastMask = 0x01_00_00_00_00_00_00_00;

            
            //No branch in the loop, Full unroll every loops. Highly vectorized
            for (int idx = 0; idx < count; idx++)
            {
                var index = startIndex + idx;
                var cell = grid[index].cells;

                /* int2 coordinate slower than x,y
                 var xy = new int2(index % gridSizeInCompressed.x, index / gridSizeInCompressed.x);
                
                var northIndex = XYToIndexWrap(xy + new int2(0, 1));
                var southIndex = XYToIndexWrap(xy + new int2(0, -1));
                var westIndex = XYToIndexWrap(xy + new int2(-1, 0));
                var eastIndex = XYToIndexWrap(xy + new int2(1, 0));

                var northWestIndex = XYToIndexWrap(xy + new int2(-1, 1));
                var northEastIndex = XYToIndexWrap(xy + new int2(1, 1));
                var southWestIndex = XYToIndexWrap(xy + new int2(-1, -1));
                var southEastIndex = XYToIndexWrap(xy + new int2(1, -1));*/
                
                 
                int x = index % ArrayWidth;
                int y = index / ArrayWidth;
                var northIndex = XYToIndexWrap(x, y + 1);
                var southIndex = XYToIndexWrap(x, y - 1);
                var westIndex = XYToIndexWrap(x - 1, y);
                var eastIndex = XYToIndexWrap(x + 1, y);

                var northWestIndex = XYToIndexWrap(x - 1, y + 1);
                var northEastIndex = XYToIndexWrap(x + 1, y + 1);
                var southWestIndex = XYToIndexWrap(x - 1, y - 1);
                var southEastIndex = XYToIndexWrap(x + 1, y - 1);

                //In-cell neighbors
                for (int neighborIdx = 0; neighborIdx < neighborCounts; neighborIdx++)
                {
                    neighbors[neighborIdx] =
                        CountBits(cell & Tables.NeighborLookups[NeighborIndex.ToBitIndex(neighborIdx)]);
                }

                
                {
                    var southWest = GetValue(southWestIndex);
                    neighbors[0] += CountBits(southWest & southWestMask);
                }

                
                {
                    var south = GetValue(southIndex);
                    neighbors[0] += (byte)(GetBit(south, 56) + GetBit(south, 57));
                    //general south side 
                    for (int neighborIdx = 1; neighborIdx < 7; neighborIdx++)
                    {
                        neighbors[neighborIdx] += CountBits(south & (southMask << (neighborIdx - 1)));
                    }

                    neighbors[7] += (byte)(GetBit(south, 62) + GetBit(south, 63));
                }

                
                {
                    var southEast = GetValue(southEastIndex);
                    neighbors[7] += CountBits(southEast & southEastMask);
                }

               
                {
                    var west = GetValue(westIndex);
                    neighbors[0] += (byte)(GetBit(west, 7) + GetBit(west, 15));

                    //general east side 
                    for (int neighborIdx = 8; neighborIdx < 14; neighborIdx++)
                    {
                        neighbors[neighborIdx] += CountBits(west & (westMask << (8 * (neighborIdx - 8))));
                    }

                    //bitIndex 56
                    neighbors[20] += (byte)(GetBit(west, 55) + GetBit(west, 63));
                }

                
                {
                    var east = GetValue(eastIndex);
                    neighbors[7] += (byte)(GetBit(east, 0) + GetBit(east, 8));

                    //general west side 
                    for (int neighborIdx = 14; neighborIdx < 20; neighborIdx++)
                    {
                        neighbors[neighborIdx] += CountBits(east & (eastMask << (8 * (neighborIdx - 14))));
                    }

                    //bitIndex 63
                    neighbors[27] += (byte)(GetBit(east, 56) + GetBit(east, 48));
                }

                
                {
                    var northWest = GetValue(northWestIndex);
                    //bitIndex 56
                    neighbors[20] += CountBits(northWest & northWestMask);
                }

                
                {
                    var north = GetValue(northIndex);
                    //bitIndex 56
                    neighbors[20] += (byte)(GetBit(north, 0) + GetBit(north, 1));
                    //general north side 
                    for (int neighborIdx = 21; neighborIdx < 27; neighborIdx++)
                    {
                        var shiftedMask = (northMask << (neighborIdx - 21));
                        var countBits = CountBits(north & shiftedMask);
                        neighbors[neighborIdx] += countBits;
                    }

                    //bitIndex 63
                    neighbors[27] += (byte)(GetBit(north, 6) + GetBit(north, 7));
                }

                
                {
                    var northEast = GetValue(northEastIndex);
                    neighbors[27] += CountBits(northEast & northEastMask);
                }


                var next = 0ul;
                for (int i = 0; i < neighborCounts; i++)
                {
                    var bitIndex = NeighborIndex.ToBitIndex(i);
                    var cellState = GetBit(cell, bitIndex);
                    var is3 = neighbors[i] == 3;
                    var is2 = neighbors[i] == 2;
                    var isAlive = cellState == 1 ? (is2 || is3) : is3;
                    next |= isAlive ? 1ul << bitIndex : 0;
                }

                next |= Liar(cell);
                nextGrid[index] = new GoLCells { cells = next };
                UnsafeUtility.MemClear(neighbors, neighborCounts);
            }
        }


        //https://dotat.at/prog/life/liar2.c
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void REDUCE(out ulong g, out ulong f, ulong a, ulong b, ulong c)
        {
            var d = a ^ b;
            var e = b ^ c;
            f = c ^ d;
            g = d | e;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ulong Liar(ulong bmp)
        {
            ulong tmp, left, right;
            ulong upper1, side1, mid1, lower1;
            ulong upper2, side2, mid2, lower2;
            ulong sum12, sum13, sum24, sum26;
            // this wraps around the left and right edges
            // but incorrect results for the border are ok
            left = bmp << 1;
            right = bmp >> 1;
            // half adder count of cells to either side
            side1 = left ^ right;
            side2 = left & right;
            // full adder count of cells in middle row
            mid1 = side1 ^ bmp;
            mid2 = side1 & bmp;
            mid2 = side2 | mid2;
            // shift middle row count to get upper and lower row counts
            upper1 = mid1 << 8;
            lower1 = mid1 >> 8;
            upper2 = mid2 << 8;
            lower2 = mid2 >> 8;
            // compress vertically
            REDUCE(out sum12, out sum13, upper1, side1, lower1);
            REDUCE(out sum24, out sum26, upper2, side2, lower2);
            // calculate result
            tmp = sum12 ^ sum13;
            bmp = (bmp | sum13) & (tmp ^ sum24) & (tmp ^ sum26);
            // mask out incorrect border cells
            return (bmp & 0x007E7E7E7E7E7E00);
            // total 19 + 7 = 26 operations
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ModPositive(int x, int m)
        {
            /*int r = x % m;
            return r < 0 ? r + m : r;*/
            return (x + m) % m;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int XYToIndexWrap(int x, int y)
        {
            return ModPositive(y, ArrayHeight) * ArrayWidth +
                   ModPositive(x, ArrayWidth);
        }

        

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NorthIndex(int index)
        {
            return index + ArrayWidth;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SouthIndex(int index)
        {
            return index - ArrayWidth;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NorthWestIndex(int index)
        {
            return NorthIndex(index) - 1;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NorthEastIndex(int index)
        {
            return NorthIndex(index) + 1;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SouthWestIndex(int index)
        {
            return SouthIndex(index) - 1;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SouthEastIndex(int index)
        {
            return SouthIndex(index) + 1;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetValue(int index)
        {
            return grid[index].cells;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetValue(uint index)
        {
            return grid[(int)index].cells;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte CountBits(ulong value)
        {
            return (byte)math.countbits(value);
            /*if (!X86.Popcnt.IsPopcntSupported)
                return (byte)math.countbits(value);
            return (byte)X86.Popcnt.popcnt_u64(value);*/
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetBit(ref ulong value, int bit)
        {
            value |= 1ul << bit;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte GetBit(ulong value, int bit)
        {
            return (byte)((value >> bit) & 1);
        }
       
    }
}