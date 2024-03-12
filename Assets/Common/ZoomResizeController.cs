using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Common
{
    public class ZoomResizeController : MonoBehaviour
    {
        public float zoomSpeed = 1;
        private void Update()
        {
            if(Mouse.current.scroll.ReadValue().y != 0)
            {
               transform.localScale = Vector3.Max((1 + Mouse.current.scroll.ReadValue().y * zoomSpeed)*transform.localScale, Vector3.one*0.0001f);
            }
        }
    }
}