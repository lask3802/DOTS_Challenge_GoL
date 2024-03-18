using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace LASK.GoL.CompressBits
{
    public class GoLUI : MonoBehaviour
    {
        [FormerlySerializedAs("groups")] [FormerlySerializedAs("simulator")] public GoLGroupControl group;
        public TMP_InputField gridSizeInput;
        public TextMeshProUGUI activeCountText;
        void Start()
        {
            gridSizeInput.text = $"{group.simulator[0].gridSize.x}";
            activeCountText.text = $"X{group.activeCount}";
        }
        
        public void OnEditGridSize(string newSize)
        {
            var size = uint.Parse(newSize);
            var fixedSize = Math.Clamp(size/64*64, 64, 1<<20);
            if(fixedSize != size)
                gridSizeInput.text = $"{fixedSize}";
            
        }
        
        public void OnActiveCountChanged(float count)
        {
            var cntInt = Mathf.RoundToInt(count);
            activeCountText.text = $"X{cntInt}";
            group.OnActiveCountChanged(cntInt);
        }
        
        public void OnApplyGridSize()
        {
            var size = uint.Parse(gridSizeInput.text);
            group.PauseAll();
            group.ResetGrid(size);
        }

        public void OnFirstAttemptCheck()
        {
            group.ChangeImplementation(GoLSimulator.Implementation.FirstAttempt);
        }
        
        public void OnSecondAttemptCheck()
        {
            group.ChangeImplementation(GoLSimulator.Implementation.SecondAttempt);
        }

        public void OnFoneECheck()
        {
            group.ChangeImplementation(GoLSimulator.Implementation.FoneE);
        }
        
        public void OnFoneESquareCheck()
        {
            group.ChangeImplementation(GoLSimulator.Implementation.FoneESquare);
        }
        
        public void SquareLayout()
        {
            group.ChangeImplementation(GoLSimulator.Implementation.Liar);
        }
        
        public void LiarWrap()
        {
            group.ChangeImplementation(GoLSimulator.Implementation.LiarWrap);
        }
    }
}