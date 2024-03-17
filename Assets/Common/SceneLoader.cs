using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    void Awake()
    {
        if (FindObjectsByType<SceneLoader>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID).Length > 1)
        {
            Destroy(gameObject);
        }
        
        DontDestroyOnLoad(gameObject);
    }
    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene("CompressedBits");
            return;
        }
        if(Keyboard.current.dKey.wasPressedThisFrame)
        {
            //Just respawn whole world, since I have entity clean issue. Maybe not the best way to do it
            var oldWorld = World.DefaultGameObjectInjectionWorld;
            oldWorld.Dispose();
            var world = new World("Custom world");
            World.DefaultGameObjectInjectionWorld = world;
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
 
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<FixedStepSimulationSystemGroup>();
            
            SceneManager.LoadScene("DynamicSize");
            return;
        }
    }
}
