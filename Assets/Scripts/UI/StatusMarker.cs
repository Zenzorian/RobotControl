using UnityEngine;
using UnityEngine.UI;

namespace Scripts.UI
{
    public class StatusMarker : MonoBehaviour
    {
        public Color activeColor = Color.green;
        public Color inactiveColor = Color.red;
       
        public Color errorTextColor = Color.red;
        public Color infoTextColor = Color.black;

        public Image serverStatusImage;
        public Image robotStatusImage;

        public Text debugText;        
    }
}
