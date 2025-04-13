using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace Scripts.Services
{
    public class GamepadInputManagerService : InputManagerService
    {       
        private GamepadInput.GamepadControlActions _gamepadActions;

        public GamepadInputManagerService()
        {
            var gameInput = new GamepadInput();
            _gamepadActions = gameInput.GamepadControl;
            gameInput.Enable();
        }

        public override void Update()
        {
            UpdateLeftStickValue(_gamepadActions.LeftStickMove.ReadValue<Vector2>());          

            UpdateRightStickValue(_gamepadActions.RightStickMove.ReadValue<Vector2>());            

            UpdateSpeedUpPressed(_gamepadActions.SpeedUp.IsPressed());           

            UpdateSpeedDownPressed(_gamepadActions.SpeedDown.IsPressed());           

            UpdateOptionsPressed(_gamepadActions.Settings.IsPressed());          

            UpdateDpadValue(_gamepadActions.CameraLeft.IsPressed() ? -1f : _gamepadActions.CameraRight.IsPressed() ? 1f : 0f);
            
        }
    }
} 