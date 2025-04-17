using UnityEngine;
using System;

namespace Scripts.Services
{
    public abstract class InputManagerService : IInputManagerService
    {       
        public event Action OnValueChanged;
        public Vector2 LeftStickValue { get; private set; }
        public Vector2 RightStickValue { get; private set; }
        public bool SpeedUpPressed { get; private set; }
        public bool SpeedDownPressed { get; private set; }
        public bool OptionsPressed { get; private set; }
        public float DpadValue { get; private set; }
       
        public abstract void Update();      

        public virtual void Reset()
        {
            LeftStickValue = Vector2.zero;
            RightStickValue = Vector2.zero;
            SpeedUpPressed = false;
            SpeedDownPressed = false;
            OptionsPressed = false;
            DpadValue = 0f;

            OnValueChanged?.Invoke();            
        }

        protected void UpdateLeftStickValue(Vector2 newLeftStickValue)
        {           
            if (newLeftStickValue == LeftStickValue)return;
            
            LeftStickValue = newLeftStickValue;
            OnValueChanged?.Invoke();   
            Debug.Log($"UpdateLeftStickValue: {LeftStickValue}");
        }

        protected void UpdateRightStickValue(Vector2 newRightStickValue)
        {  
            if (newRightStickValue == RightStickValue)return;
            
            RightStickValue = newRightStickValue;
            OnValueChanged?.Invoke();
        }

        protected void UpdateSpeedUpPressed(bool newSpeedUpPressed)
        {
            if (newSpeedUpPressed == SpeedUpPressed)return;
            
            SpeedUpPressed = newSpeedUpPressed;
            OnValueChanged?.Invoke();
        }       

        protected void UpdateSpeedDownPressed(bool newSpeedDownPressed)
        {
            if (newSpeedDownPressed == SpeedDownPressed)return;
            
            SpeedDownPressed = newSpeedDownPressed;
            OnValueChanged?.Invoke();
        }

        protected void UpdateOptionsPressed(bool newOptionsPressed)
        {
            if (newOptionsPressed == OptionsPressed)return;
            
            OptionsPressed = newOptionsPressed;
            OnValueChanged?.Invoke();
            Debug.Log($"UpdateOptionsPressed: {OptionsPressed}");
        }

        protected void UpdateDpadValue(float newDpadValue)
        {          
            if (newDpadValue == DpadValue)return;

            DpadValue = newDpadValue;
            OnValueChanged?.Invoke();
        }
        
    }
}
