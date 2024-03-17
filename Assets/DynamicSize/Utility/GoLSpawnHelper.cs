using DynamicSize.Component;
using Unity.Burst;
using Unity.Entities;

namespace DynamicSize.Utility
{
    [BurstCompile]
    public static class GoLSpawnHelper
    {
        [BurstCompile]
        public static void AddCellComponents(ref SystemState state, ref Entity e)
        {
            state.EntityManager.AddComponent<GoLPosition>(e);
            state.EntityManager.AddComponent<CurrentCells>(e);
            state.EntityManager.AddComponent<NextCells>(e);
            state.EntityManager.AddComponent<RenderingHighBits>(e);
            state.EntityManager.AddComponent<RenderingLowBits>(e);
        }
    }
}