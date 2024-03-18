using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DynamicSize.Component
{
    public struct GoLConfig: IComponentData
    {
        public Entity CellsPrefab;
        public int2 InitialGridSize;
    }
    
    public struct GoLState: IComponentData
    {
        public int Generation;
        public bool IsPaused;
        public float TickTime;
    }
    
    //Holds the active cells and the cells to spawn
    
    public struct CellsManagement: IDisposable, ICleanupComponentData
    {
        /*public NativeParallelHashMap<int2, Entity> ActiveCells
        {
            get
            {
                return activeCells;
            }
            set
            {
                activeCells = value;
            }
        }

        public NativeList<int2> CellsToSpawn
        {
            get
            {
                return cellsToSpawn;
            }
            set
            {
                cellsToSpawn = value;
            }
        }

        public int ActiveCellsCapacity
        {
            get
            {
                return activeCells.Capacity;
            }
            set
            {
                activeCells.Capacity = value;
            }
        }
        
        public int CellsToSpawnCapacity
        {
            get
            {
                return cellsToSpawn.Capacity;
            }
            set
            {
                cellsToSpawn.Capacity = value;
            }
        }
        [NonSerialized][NonReorderable]
        private NativeParallelHashMap<int2, Entity> activeCells;
        [NonSerialized][NonReorderable]
        private NativeList<int2> cellsToSpawn;*/
        
        [NonSerialized][NonReorderable]
        public NativeParallelHashMap<int2, Entity> ActiveCells;
        [NonSerialized][NonReorderable]
        public NativeList<int2> CellsToSpawn;
        
         
        public void Dispose()
        {
            ActiveCells.Dispose();
            CellsToSpawn.Dispose();
          
        }
    }
}