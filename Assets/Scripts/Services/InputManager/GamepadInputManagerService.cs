using UnityEngine;

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

            UpdateSpeedValue(_gamepadActions.SpeedDown.IsPressed() ? -1f : _gamepadActions.SpeedUp.IsPressed() ? 1f : 0f);            
           
            UpdateOptionsPressed(_gamepadActions.Settings.IsPressed());          

            UpdateCameraAngle(_gamepadActions.CameraLeft.ReadValue<float>(), _gamepadActions.CameraRight.ReadValue<float>());            
        }
    }
} 