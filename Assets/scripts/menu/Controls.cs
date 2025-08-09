using UnityEngine;
using UnityEngine.UI; 

public class Controls : MonoBehaviour
{
    public Text displayOutputText;

    void Update()
    {
        if (displayOutputText == null)
        {
            Debug.LogError("ERROR: No se ha asignado el objeto UI Text a la variable 'displayOutputText' en el script Controls (GameObject MenuController).");
            return;
        }

        // --- Detección de Botones ---
        string buttonName = null;

        // Botones Principales
        if (Input.GetKeyDown(KeyCode.A)) buttonName = "A";
        if (Input.GetKeyDown(KeyCode.B)) buttonName = "B";
        if (Input.GetKeyDown(KeyCode.X)) buttonName = "X";
        if (Input.GetKeyDown(KeyCode.Y)) buttonName = "Y";

        // Botones Laterales
        if (Input.GetKeyDown(KeyCode.L)) buttonName = "L";
        if (Input.GetKeyDown(KeyCode.R)) buttonName = "R";

        // Botones ZL / ZR
        if (Input.GetKeyDown(KeyCode.LeftAlt)) buttonName = "ZL";
        if (Input.GetKeyDown(KeyCode.RightAlt)) buttonName = "ZR";

        // D-Pad
        if (Input.GetKeyDown(KeyCode.UpArrow)) buttonName = "D-Pad Up";
        if (Input.GetKeyDown(KeyCode.DownArrow)) buttonName = "D-Pad Down";
        if (Input.GetKeyDown(KeyCode.LeftArrow)) buttonName = "D-Pad Left";
        if (Input.GetKeyDown(KeyCode.RightArrow)) buttonName = "D-Pad Right";

        // Start
        if (Input.GetKeyDown(KeyCode.Return)) buttonName = "Start";

        // Circle Pad (Digital Detection)
        if (Input.GetKeyDown(KeyCode.Keypad8)) buttonName = "Circle-Pad Up";
        if (Input.GetKeyDown(KeyCode.Keypad2)) buttonName = "Circle-Pad Down";
        if (Input.GetKeyDown(KeyCode.Keypad4)) buttonName = "Circle-Pad Left";
        if (Input.GetKeyDown(KeyCode.Keypad6)) buttonName = "Circle-Pad Right";

        // Circle Pad Pro (Digital Detection)
        if (Input.GetKeyDown(KeyCode.Alpha1)) buttonName = "C-Stick Up"; // Usamos C-Stick como nombre común
        if (Input.GetKeyDown(KeyCode.Alpha2)) buttonName = "C-Stick Down";
        if (Input.GetKeyDown(KeyCode.Alpha3)) buttonName = "C-Stick Left";
        if (Input.GetKeyDown(KeyCode.Alpha4)) buttonName = "C-Stick Right";


        // --- Actualizar el Texto en Pantalla ---
        if (buttonName != null)
        {
            displayOutputText.text = buttonName;

        }
    }
}