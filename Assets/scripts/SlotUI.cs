using UnityEngine;
using UnityEngine.UI;

public class SlotUI : MonoBehaviour
{
    public Text dateText;
    public Text timeLabel; // "Tiempo de juego:"
    public Text timeText;  // "12:55" (Valor numérico)
    public Text sceneText;
    public Image screenshotImage;
    public CanvasGroup cg;

    private SaveHeader currentHeader; // Cachimng the reference to eliminate 3DS GC spikes

    public void SetData(SaveHeader header, Sprite bgSprite)
    {
        if (currentHeader.slotIndex == header.slotIndex && 
            currentHeader.playTime == header.playTime && 
            currentHeader.date == header.date) return; // Evitar Garbage Collection comparando campos base en lugar de ==
        
        currentHeader = header;

        dateText.text = header.date;

        // Etiqueta "Tiempo de juego" — usa el idioma actual del juego
        if (timeLabel != null)
        {
            timeLabel.text = (LocalizationManager.Instance != null) ? LocalizationManager.Instance.Get("tiempo_jugado") : "Time:";
        }

        // Valor numérico (SIN PREFIJOS)
        if (timeText != null)
        {
            timeText.text = FormatTime(header.playTime);
        }

        if (LocalizationManager.Instance != null)
            sceneText.text = LocalizationManager.Instance.GetSceneName(header.actName, header.lastSceneHash, header.sceneName);
        else
            sceneText.text = header.sceneName;
        if (screenshotImage != null)
        {
            screenshotImage.sprite = bgSprite;
            screenshotImage.color = (bgSprite != null) ? Color.white : new Color(0, 0, 0, 0);
        }
    }

    public void SetHighlight(bool isSelected)
    {
        cg.alpha = isSelected ? 1.0f : 0.5f;
    }

    private string FormatTime(float seconds)
    {
        int h = (int)(seconds / 3600);
        int m = (int)((seconds % 3600) / 60);
        return string.Format("{0:00}:{1:00}", h, m);
    }
}