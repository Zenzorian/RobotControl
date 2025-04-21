using UnityEngine;
using System;

namespace Scripts.Services
{
    public abstract class InputManagerService : IInputManagerService
    {       
        public event Action OnValueChanged;
        public Vector2 LeftStickValue { get; private set; }
        public Vector2 RightStickValue { get; private set; }       
        public bool OptionsPressed { get; private set; }
        public float CameraAngle { get; private set; }
        public float SpeedCoefficient{ get;  set; } = 1;

        public abstract void Update();      
        
        private float _dpadValue = 0f;  
        
        private float _speedStep = 0.05f;
       
        private float _cameraAngleStep = 0.5f; 
        private double _triggerDeadZone = 0.2f;       

        public virtual void Reset()
        {
            LeftStickValue = Vector2.zero;
            RightStickValue = Vector2.zero;

            OptionsPressed = false;
            CameraAngle = 90f;

            OnValueChanged?.Invoke();            
        }

        protected void UpdateLeftStickValue(Vector2 newLeftStickValue)
        {    
            newLeftStickValue *= SpeedCoefficient;
            if (newLeftStickValue == LeftStickValue)return;
            
            LeftStickValue = newLeftStickValue;
            OnValueChanged?.Invoke();               
        }

        protected void UpdateRightStickValue(Vector2 newRightStickValue)
        {  
            newRightStickValue *= SpeedCoefficient;
            if (newRightStickValue == RightStickValue)return;
            
            RightStickValue = newRightStickValue;
            OnValueChanged?.Invoke();
        }      

        protected void UpdateOptionsPressed(bool newOptionsPressed)
        {
            if (newOptionsPressed == OptionsPressed)return;
            
            OptionsPressed = newOptionsPressed;
            OnValueChanged?.Invoke();           
        }

        protected void UpdateCameraAngle(float leftTrigger, float rightTrigger)
        {                   
            if (leftTrigger > _triggerDeadZone || rightTrigger > _triggerDeadZone)
            {
                CameraAngle -= leftTrigger * _cameraAngleStep;
                CameraAngle += rightTrigger * _cameraAngleStep;

                if (CameraAngle > 180f) CameraAngle = 180f;
                if (CameraAngle < 0) CameraAngle = 0f;

                OnValueChanged?.Invoke();
            }
            else
            {
                if (CameraAngle>91f && CameraAngle<110f)
                {
                    CameraAngle -= _cameraAngleStep/10;
                     OnValueChanged?.Invoke();   
                }
                else if (CameraAngle<89f && CameraAngle>70f)
                {
                    CameraAngle += _cameraAngleStep/10;
                    OnValueChanged?.Invoke();   
                }              
            }
        }

        protected void UpdateSpeedValue(float dpadValue)
        {
            if (dpadValue == _dpadValue)return;

            _dpadValue = dpadValue;

            SpeedCoefficient += _speedStep * dpadValue;

            if (SpeedCoefficient > 1.0f)SpeedCoefficient = 1.0f;
            if (SpeedCoefficient < 0.1f)SpeedCoefficient = 0.1f;  

            OnValueChanged?.Invoke();
        }     
    }
}
