using System.ComponentModel;
using System.Runtime.CompilerServices;
using DynamicSize.Component;
using DynamicSize.Utility;
using LASK.GoL.CompressBits;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;


namespace DynamicSize.System
{
    [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))]
    [UpdateAfter(typeof(InitializeGoLSystem))]
    [CreateAfter(typeof(InitializeGoLSystem))]
    public partial struct GoLGenerationSystem : ISystem
    {
        private float tickCountDown;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GoLConfig>();
            state.RequireForUpdate<GoLState>();
            state.RequireForUpdate<CurrentCells>();
            state.RequireForUpdate<GoLPosition>();
            state.RequireForUpdate<CellsManagement>();
          
            tickCountDown = 0f;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var golState = SystemAPI.GetSingleton<GoLState>();
            var golConfig = SystemAPI.GetSingleton<GoLConfig>();
            var management = SystemAPI.GetSingleton<CellsManagement>();
            
            if(golState.IsPaused)
                return;
            
            if(tickCountDown > 0)
            {
                tickCountDown -= SystemAPI.Time.DeltaTime;
                return;
            }
            tickCountDown = golState.TickTime;
            
            // We can't resize in the job, so we do it here and ensure we have enough space.
            if (management.ActiveCells.Count() >= management.ActiveCells.Capacity / 2)
            {
                management.ActiveCells.Capacity *= 2;
                
                Debug.Log($"active cells capacity: {management.ActiveCells.Capacity}");
            }

            
            // worst case scenario, we need to spawn all neighbor cells around the active cells.
            if (management.CellsToSpawn.Capacity < management.ActiveCells.Count() / 2)
            {
                var capacity = math.max(management.CellsToSpawn.Capacity * 2,
                    management.ActiveCells.Count() * 2);
                management.CellsToSpawn.Capacity = math.min(capacity, int.MaxValue);
                Debug.Log($"cells to spawn capacity: {management.CellsToSpawn.Capacity}");
            }
            
            
            
            //Entity disable system got bad performance, just let it growth.
            
            /* var allocPM = new ProfilerMarker("AllocateHashSets");
            allocPM.Begin();
            var cellsToDisable = new NativeParallelHashSet<Entity>(management.ActiveCells.Count(), state.WorldUpdateAllocator);
            var cellsToEnable = new NativeParallelHashSet<Entity>(management.ActiveCells.Count(), state.WorldUpdateAllocator);
            allocPM.End();
            */
           
           
            var job = new LiarJob
            {
                ActiveCells = management.ActiveCells.AsReadOnly(),
                CellsLookup = SystemAPI.GetComponentLookup<CurrentCells>(),
                CellsToSpawn = management.CellsToSpawn.AsParallelWriter(),
            };
            state.Dependency = job.ScheduleParallel(state.Dependency);
            
            // swap next cells
            var swapJob = new SwapNextJob();
            state.Dependency = swapJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();
            /*state.Dependency= new DisableCellsJob
            {
                EntitiesToDisabled = cellsToDisable,
                NextCellLookup = SystemAPI.GetComponentLookup<GoLCells>()
            }.Schedule( state.Dependency);
            state.Dependency = new EnableCellsJob
            {
                EntitiesToEnabled = cellsToEnable,
                NextCellLookup = SystemAPI.GetComponentLookup<GoLCells>()
            }.Schedule( state.Dependency);*/
            
            
            // advance generation
            golState.Generation++;
            SystemAPI.SetSingleton(golState);
            
            
            
        }

    }
    
    

    [BurstCompile]
    public partial struct EnableCellsJob: IJob
    {
        [Unity.Collections.ReadOnly] public NativeParallelHashSet<Entity> EntitiesToEnabled;
        [WriteOnly] public ComponentLookup<CurrentCells> NextCellLookup;
        
        [BurstCompile]
        public void Execute()
        {
            foreach (var entity in EntitiesToEnabled)
            {
                NextCellLookup.SetComponentEnabled(entity, true);
            }
        }
    }
    
    [BurstCompile]
    public partial struct DisableCellsJob: IJob
    {
        [Unity.Collections.ReadOnly] public NativeParallelHashSet<Entity> EntitiesToDisabled;
        [WriteOnly] public ComponentLookup<CurrentCells> NextCellLookup;
    
        
        [BurstCompile]
        public void Execute()
        {
            foreach (var entity in EntitiesToDisabled)
            {
                NextCellLookup.SetComponentEnabled(entity, false);
            }
        }
    }

    [BurstCompile]
    public partial struct SwapNextJob: IJobEntity
    {
        [BurstCompile]
        public void Execute(ref CurrentCells cells, in NextCells nextCells)
        {
            cells.Value = nextCells.Value;
        }
    }
    
   
    
    [BurstCompile]
    public partial struct LiarJob : IJobEntity
    {
        [Unity.Collections.ReadOnly] public NativeParallelHashMap<int2, Entity>.ReadOnly ActiveCells;
        [Unity.Collections.ReadOnly] public ComponentLookup<CurrentCells> CellsLookup;
        
        [WriteOnly] public NativeList<int2>.ParallelWriter CellsToSpawn;
       // [WriteOnly] public NativeParallelHashSet<Entity>.ParallelWriter CellsToDisabled;
       // [WriteOnly] public NativeParallelHashSet<Entity>.ParallelWriter CellsToEnabled;

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


        private struct TouchedNewCells
        {
            public int2 Position;
        }
        
        [BurstCompile]
        public unsafe void Execute(in Entity entity,
            in CurrentCells cells, ref NextCells nextCells,
            in GoLPosition position,
            [WriteOnly] ref RenderingHighBits highBits,
            [WriteOnly] ref RenderingLowBits lowBits
            )
        {
            var neighbors = stackalloc byte[28];
            var newCells = stackalloc TouchedNewCells[8];
            var newCellCnt = 0;
            
            const ulong northMask = 0x7;
            const ulong southMask = 0x07_00_00_00_00_00_00_00;
            const ulong westMask = 0x808080;
            const ulong eastMask = 0x010101;
            const ulong northWestMask = 0x80;
            const ulong northEastMask = 0x1;
            const ulong southWestMask = 0x80_00_00_00_00_00_00_00;
            const ulong southEastMask = 0x01_00_00_00_00_00_00_00;

            var southIndex = SouthIndex(position.Position);
            var westIndex = WestIndex(position.Position);
            var eastIndex = EastIndex(position.Position);
            var northIndex = NorthIndex(position.Position);
            var northWestIndex = NorthWestIndex(position.Position);
            var northEastIndex = NorthEastIndex(position.Position);
            var southWestIndex = SouthWestIndex(position.Position);
            var southEastIndex = SouthEastIndex(position.Position);

            
            for (int neighborIdx = 0; neighborIdx < 28; neighborIdx++)
            {
                neighbors[neighborIdx] =
                    CountBits(cells.Value & Tables.NeighborLookups[NeighborIndex.ToBitIndex(neighborIdx)]);
            }

            
            {
                var southWest = GetValue(southWestIndex, out var isNew);
                if (isNew)
                    PushNewCellIndex(newCells, ref newCellCnt, southWestIndex);
               
                neighbors[0] += CountBits(southWest & southWestMask);
            }

            
            {
                var south = GetValue(southIndex, out var isNew);
                if (isNew)
                    PushNewCellIndex(newCells, ref newCellCnt, southIndex);
                
                neighbors[0] += (byte)(GetBit(south, 56) + GetBit(south, 57));
                //general south side 
                for (int neighborIdx = 1; neighborIdx < 7; neighborIdx++)
                {
                    neighbors[neighborIdx] += CountBits(south & (southMask << (neighborIdx - 1)));
                }

                neighbors[7] += (byte)(GetBit(south, 62) + GetBit(south, 63));
            }


            {
                var southEast = GetValue(southEastIndex, out var isNew);
                if (isNew)
                    PushNewCellIndex(newCells, ref newCellCnt, southEastIndex);
                neighbors[7] += CountBits(southEast & southEastMask);
            }


            {
                var west = GetValue(westIndex, out var isNew);
                if (isNew)
                    PushNewCellIndex(newCells, ref newCellCnt, westIndex);
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
                var east = GetValue(eastIndex, out var isNew);
                if (isNew)
                    PushNewCellIndex(newCells, ref newCellCnt, eastIndex);
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
                var northWest = GetValue(northWestIndex, out var isNew);
                if (isNew)
                    PushNewCellIndex(newCells, ref newCellCnt, northWestIndex);
                //bitIndex 56
                neighbors[20] += CountBits(northWest & northWestMask);
            }


            {
                var north = GetValue(northIndex, out var isNew);
                if (isNew)
                    PushNewCellIndex(newCells, ref newCellCnt, northIndex);
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
                var northEast = GetValue(northEastIndex, out var isNew);
                if (isNew)
                    PushNewCellIndex(newCells, ref newCellCnt, northEastIndex);
                neighbors[27] += CountBits(northEast & northEastMask);
            }

            ulong next = 0ul;
            for (int i = 0; i < 28; i++)
            {
                var bitIndex = NeighborIndex.ToBitIndex(i);
                var cellState = GetBit(cells.Value, bitIndex);
                var is3 = neighbors[i] == 3;
                var is2 = neighbors[i] == 2;
                var isAlive = cellState == 1 ? (is2 || is3) : is3;
                next |= isAlive ? 1ul << bitIndex : 0;
            }

            next |= Liar(cells.Value);
            nextCells = new NextCells { Value = next };
            //Debug.Log($"({position.Position.x},{position.Position.y}) cells: {cells.Value} next: {next}");
            
            highBits.Value = (int)(next >> 32);
            lowBits.Value = (int)next;
            if (next == 0)
            {
                //CellsToDisabled.Add(entity);
                //NextCellLookup.SetComponentEnabled(entity, false);
                //EntitiesToDisabled.AddNoResize(new EntityToDisabled{Position = position.Position, Entity = entity});
            }
            else
            {
                for (int i = 0; i < newCellCnt; i++)
                {
                    if(ActiveCells.ContainsKey(newCells[i].Position))
                        continue;
                    CellsToSpawn.AddNoResize(newCells[i].Position);
                }
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe int PushNewCellIndex(TouchedNewCells* newCells, ref int newCellCnt, int2 position)
        {
            newCells[newCellCnt++] = new TouchedNewCells { Position = position };
            return newCellCnt;
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
        private int2 WestIndex(int2 index)
        {
            return index - new int2(1, 0);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int2 EastIndex(int2 index)
        {
            return index + new int2(1, 0);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int2 NorthIndex(int2 index)
        {
            return index + new int2(0, 1);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int2 SouthIndex(int2 index)
        {
            return index - new int2(0, 1);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int2 NorthWestIndex(int2 index)
        {
            return NorthIndex(index) - new int2(1, 0);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int2 NorthEastIndex(int2 index)
        {
            return NorthIndex(index) + new int2(1, 0);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int2 SouthWestIndex(int2 index)
        {
            return SouthIndex(index) - new int2(1, 0);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int2 SouthEastIndex(int2 index)
        {
            return SouthIndex(index) + new int2(1, 0);
        }


        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetValue(int2 position, out bool isNew)
        {
            if (ActiveCells.TryGetValue(position, out var entity))
            {
                isNew = false;
                /*if(!CellsLookup.IsComponentEnabled(entity))
                    CellsToEnabled.Add(entity);*/
                return CellsLookup[entity].Value;
            }
            isNew = true;
            return 0;
        }


        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte CountBits(ulong value)
        {
            if (!X86.Popcnt.IsPopcntSupported)
                return (byte)math.countbits(value);
            return (byte)X86.Popcnt.popcnt_u64(value);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte GetBit(ulong value, int bit)
        {
            return (byte)((value >> bit) & 1);
        }
    }
}