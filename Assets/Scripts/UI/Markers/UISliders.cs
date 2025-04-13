using UnityEngine;
using UnityEngine.UI;

namespace Scripts.UI.Markers
{
    [System.Serializable]
    public class UISliders : MonoBehaviour
    {
        public Slider leftSlider;
        public Slider rightSlider;
        public Slider cameraSlider;

        public Status serverStatus;
        public Status robotStatus;

        public Button settingsButton;
    }
}