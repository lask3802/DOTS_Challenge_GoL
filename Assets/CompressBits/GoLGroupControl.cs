using Unity.Jobs;
using UnityEngine;

namespace LASK.GoL.CompressBits
{
    public class GoLGroupControl : MonoBehaviour
    {
        public GoLSimulator[] simulator;
        public int activeCount = 1;

        void Start()
        {
            OnActiveCountChanged(activeCount);
        }
        public void OnActiveCountChanged(int count)
        {
            activeCount = count;
            for (int i = 0; i < simulator.Length; i++)
            {
                simulator[i].gameObject.SetActive(i < count);
            }
        }
        
        public void ChangeImplementation(GoLSimulator.Implementation implementation)
        {
            for (int i = 0; i < simulator.Length; i++)
            {
                simulator[i].currentImplementation = implementation;
            }
        }
        
        public void PauseAll()
        {
            for (int i = 0; i < simulator.Length; i++)
            {
                simulator[i].isPaused = true;
            }
        }
        
        public void ResetGrid(uint size)
        {
            var jobHandle = new Unity.Jobs.JobHandle();
            for (int i = 0; i < simulator.Length; i++)
            {
                jobHandle = JobHandle.CombineDependencies(simulator[i].ResetGrid(size), jobHandle);
            }
            jobHandle.Complete();
        }
    }
}