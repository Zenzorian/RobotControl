using UnityEngine;
using System;

namespace Scripts.Services
{
    public interface IInputManagerService
    {
        event Action OnValueChanged;
        
        Vector2 LeftStickValue { get; }
        Vector2 RightStickValue { get; } 

        bool OptionsPressed { get; }
        float CameraAngle { get; }    
        float SpeedCoefficient { get; set; }
       
        void Update();
        void Reset();
    }
} 