using DynamicSize.Component;
using DynamicSize.UI;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DynamicSize.System
{
    public partial struct DynamicGoLUISystem : ISystem
    {
       
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GoLConfig>();
            state.RequireForUpdate<GoLState>();
            state.RequireForUpdate<DynamicGoLUI>();
        }

        
        public void OnUpdate(ref SystemState state)
        {
            var ui = SystemAPI.ManagedAPI.GetSingleton<DynamicGoLUI>();
            var golConfig = SystemAPI.GetSingletonRW<GoLConfig>();
            var goLState = SystemAPI.GetSingletonRW<GoLState>();
            if(ui == null)
                return;

            if (!ui.IsInitialized)
            {
                ui.InitGridSize(golConfig.ValueRW.InitialGridSize.x);
            }
            if (ui.IsReSimulate)
            {
                golConfig.ValueRW.InitialGridSize = new int2(ui.GridSize, ui.GridSize);
                goLState.ValueRW.Generation = 0;
                state.EntityManager.CreateSingleton<TriggerGoLInitialize>();
                ui.IsReSimulate = false;
            }
        }

       
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}