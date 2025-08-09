using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class MainMenu_LoadController : MonoBehaviour
{
    [Header("Referencias de UI")]
    public GameObject slotPrefab;
    public Transform slotContentContainer;
    public Text titleText;
    public MenuNavegacion menuNavegacion;

    [Header("Opciones Navegables")]
    public CanvasGroup volverButtonCanvasGroup;

    [Header("Configuración del Scroll")]
    public float slotHeight = 48f;
    public int maxVisibleSlots = 3;

    [Header("Paneles de Confirmación")]
    public GameObject confirmarBorradoPanel;
    public CanvasGroup confirmarBorradoCanvasGroup;
    public CanvasGroup confirmarBorradoSiCanvasGroup;
    public CanvasGroup confirmarBorradoNoCanvasGroup;
    
    [Header("Transición de Carga")]
    public Image fadeToBlackImageUpper;
    public Image fadeToBlackImageLower;
    public float fadeToBlackDuration = 1.0f;
    public BackgroundMusicPlayer bgmPlayer;

    // --- Variables Internas ---
    private List<GameObject> instantiatedSlots = new List<GameObject>();
    private List<CanvasGroup> navigableElements = new List<CanvasGroup>();
    private int currentSelectionIndex = 0;
    private bool navigationEnabled = false;
    private int lastSelectedSlotIndex = 0;
    private RectTransform slotsContainerRect;
    private int topVisibleIndex = 0;

    private bool isConfirmingDelete = false;
    private int slotIndexToDelete = -1;
    private int confirmationChoice = 0;

    void Start()
    {
        if (slotContentContainer != null)
        {
            slotsContainerRect = slotContentContainer.GetComponent<RectTransform>();
        }
        if (confirmarBorradoPanel != null)
        {
            confirmarBorradoPanel.SetActive(false);
        }

        if (GetComponent<CanvasGroup>() != null)
        {
            GetComponent<CanvasGroup>().alpha = 0f;
        }
        gameObject.SetActive(true);

        RefrescarListaDeSlots();
    }

    void Update()
    {
        if (isConfirmingDelete)
        {
            HandleDeleteConfirmationInput();
            return;
        }

        if (!navigationEnabled || navigableElements.Count == 0) return;

        HandleMainNavigationInput();
    }

    public void AbrirMenuCarga()
    {
        gameObject.SetActive(true);

        if (slotsContainerRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotsContainerRect);
        }

        navigationEnabled = false; 
        StartCoroutine(FadeCanvasGroup(GetComponent<CanvasGroup>(), 1f, 0.3f, () => {
            // El callback ahora solo inicia la corrutina de espera.
            StartCoroutine(EnableNavigationAfterFrame());
        }));
    }
    private IEnumerator EnableNavigationAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        navigationEnabled = true;
    }
    public void CerrarMenuCarga()
    {
        navigationEnabled = false;
        StartCoroutine(FadeCanvasGroup(GetComponent<CanvasGroup>(), 0f, 0.3f, () => {
            gameObject.SetActive(true);
            if (menuNavegacion != null)
            {
                menuNavegacion.OnSubMenuClosed();
            }
        }));
    }

    // --- LÓGICA DE NAVEGACIÓN Y ACCIONES ---
	private void HandleMainNavigationInput()
	{
		int previousSelection = currentSelectionIndex;
		int slotCount = instantiatedSlots.Count;
		bool isSelectingSlot = currentSelectionIndex < slotCount;

		if (Input.GetKeyDown(KeyCode.UpArrow))
		{
			if (isSelectingSlot) // Si estamos en un slot
			{
				if (currentSelectionIndex == 0)
					currentSelectionIndex = slotCount; // Del primer slot, vamos a "Volver"
				else
					currentSelectionIndex--;
			}
			else // Si estamos en "Volver"
			{
				if (slotCount > 0)
				{
					currentSelectionIndex = lastSelectedSlotIndex;
				}
			}
		}
		else if (Input.GetKeyDown(KeyCode.DownArrow))
		{
			if (isSelectingSlot) // Si estamos en un slot
			{
				if (currentSelectionIndex == slotCount - 1)
					currentSelectionIndex = slotCount; // Del último slot, vamos a "Volver"
				else
					currentSelectionIndex++;
			}
			else // Si estamos en "Volver"
			{
				// Volvemos al primer slot de la lista.
				if (slotCount > 0)
					currentSelectionIndex = 0;
			}
		}
		else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
		{
			if (isSelectingSlot)
			{
				// Desde cualquier slot, saltamos a "Volver".
				currentSelectionIndex = slotCount;
			}
		}

        // Guardamos el índice del slot solo si la selección actual es un slot.
        // Intento 66: usando "<" XD csm
        if (currentSelectionIndex < slotCount)
        {
            lastSelectedSlotIndex = currentSelectionIndex;
            //funcionó :D
        }

		if (previousSelection != currentSelectionIndex)
		{
			UpdateHighlightAndScroll();
		}

		if (Input.GetKeyDown(KeyCode.X) && isSelectingSlot)
		{
			GameObject selectedSlotObject = instantiatedSlots[currentSelectionIndex];
			SaveSlotHelper helper = selectedSlotObject.GetComponent<SaveSlotHelper>();
			slotIndexToDelete = helper.originalSlotIndex;
			isConfirmingDelete = true;
			navigationEnabled = false;
			confirmationChoice = -1;
			StartCoroutine(CrossfadeMenus(GetComponent<CanvasGroup>(), confirmarBorradoCanvasGroup, 0.3f));
		}

		if (Input.GetKeyDown(KeyCode.A))
		{
			if (isSelectingSlot)
			{
				GameObject selectedSlotObject = instantiatedSlots[currentSelectionIndex];
				SaveSlotHelper helper = selectedSlotObject.GetComponent<SaveSlotHelper>();
				StartCoroutine(LoadGameSequence(helper.originalSlotIndex));
			}
			else // Es el botón "Volver"
			{
				CerrarMenuCarga();
			}
		}
	}
    private void HandleDeleteConfirmationInput()
    {
        if (confirmationChoice == -1)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) confirmationChoice = 0;
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow)) confirmationChoice = 1;
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) confirmationChoice = 0;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) confirmationChoice = 1;
        }
        //Esto lo hizo Gemini. ¿Qué hace? ni idea XD pero funciona \:D/
        if (confirmarBorradoSiCanvasGroup != null) confirmarBorradoSiCanvasGroup.alpha = (confirmationChoice == 0) ? 1f : 0.5f;
        if (confirmarBorradoNoCanvasGroup != null) confirmarBorradoNoCanvasGroup.alpha = (confirmationChoice == 1) ? 1f : 0.5f;
        if (confirmationChoice == -1)
        {
            if (confirmarBorradoSiCanvasGroup != null) confirmarBorradoSiCanvasGroup.alpha = 0.5f;
            if (confirmarBorradoNoCanvasGroup != null) confirmarBorradoNoCanvasGroup.alpha = 0.5f;
        }

        if (Input.GetKeyDown(KeyCode.A) && confirmationChoice != -1)
        {
            if (confirmationChoice == 0) // Sí
            {
                StartCoroutine(DeleteAndReturn());
            }
            else // No
            {
                StartCoroutine(CrossfadeMenus(confirmarBorradoCanvasGroup, GetComponent<CanvasGroup>(), 0.3f, () => {
                    isConfirmingDelete = false;
                    navigationEnabled = true;
                }));
            }
        }
    }

    private IEnumerator LoadGameSequence(int slotIndex)
    {
        navigationEnabled = false;
        
        if (bgmPlayer != null) bgmPlayer.StartFadeOut();
        StartCoroutine(FadeImage(fadeToBlackImageUpper, 1f, fadeToBlackDuration));
        StartCoroutine(FadeImage(fadeToBlackImageLower, 1f, fadeToBlackDuration));

        yield return new WaitForSeconds(fadeToBlackDuration);

        SaveLoadManager.Instance.LoadGame(slotIndex);
    }
    
    private IEnumerator DeleteAndReturn()
    {
        SaveLoadManager.Instance.DeleteSaveSlot(slotIndexToDelete);
        
        yield return StartCoroutine(CrossfadeMenus(confirmarBorradoCanvasGroup, GetComponent<CanvasGroup>(), 0.3f));
        
        RefrescarListaDeSlots();

        if (instantiatedSlots.Count > 0)
        {
            currentSelectionIndex = Mathf.Min(lastSelectedSlotIndex, instantiatedSlots.Count - 1);
        }
        else
        {
            currentSelectionIndex = 0;
        }
        UpdateHighlightAndScroll();

        isConfirmingDelete = false;
        navigationEnabled = true;
        slotIndexToDelete = -1;
    }


    // --- MÉTODOS DE AYUDA (UI Y DATOS) ---
    private void RefrescarListaDeSlots()
    {
        if (SaveLoadManager.Instance == null) return;

        foreach (GameObject slot in instantiatedSlots)
        {
            RawImage capturaImage = slot.transform.Find("Captura").GetComponent<RawImage>();
            if (capturaImage != null && capturaImage.texture != null)
            {
                Destroy(capturaImage.texture); // Liberar la memoria de la textura antigua
            }
            Destroy(slot); // Ahora destruimos el objeto
        }
        //Creo que usar Destroy mucho causa el lag
        //Pero está solucionado, espero
        instantiatedSlots.Clear();
        navigableElements.Clear();

        List<SaveSlotData> slotsGuardados = SaveLoadManager.Instance.GetSaveSlots();
        if (slotsGuardados == null) return;
        //Cualquier cosa volver aquí XD

        for (int i = 0; i < slotsGuardados.Count; i++)
        {
            if (slotsGuardados[i].isSlotInUse)
            {
                GameObject newSlot = Instantiate(slotPrefab, slotContentContainer);
                SaveSlotHelper helper = newSlot.GetComponent<SaveSlotHelper>();
                if (helper != null)
                {
                    helper.originalSlotIndex = i;
                }
                PopulateSlotUI(newSlot, slotsGuardados[i], i + 1);
                instantiatedSlots.Add(newSlot);
                navigableElements.Add(newSlot.GetComponent<CanvasGroup>());
            }
        }

        if (volverButtonCanvasGroup != null) navigableElements.Add(volverButtonCanvasGroup);

        float totalContentHeight = instantiatedSlots.Count * slotHeight;
        if (slotsContainerRect != null)
        {
            slotsContainerRect.sizeDelta = new Vector2(slotsContainerRect.sizeDelta.x, totalContentHeight);
        }
		// Regla 1: Selección inicial por defecto
		if (instantiatedSlots.Count > 0)
		{
			currentSelectionIndex = 0; // Si hay saves, seleccionar el primero.
		}
		else
		{
			// Si no hay saves, el único elemento es "Volver", que estará en el índice 0.
			currentSelectionIndex = 0;
		}
		lastSelectedSlotIndex = 0;

        currentSelectionIndex = 0;
        lastSelectedSlotIndex = 0;
        topVisibleIndex = 0;
        UpdateHighlightAndScroll();
    }
	
    private void PopulateSlotUI(GameObject slotInstance, SaveSlotData data, int slotNumber)
    {
        Text mesText = slotInstance.transform.Find("Mes").GetComponent<Text>();
        Text horasText = slotInstance.transform.Find("Horas").GetComponent<Text>();
        GameObject tiempoTextObject = slotInstance.transform.Find("TiempoText").gameObject;
        
        Text escenaText = slotInstance.transform.Find("Escena").GetComponent<Text>();
        Image capturaImage = slotInstance.transform.Find("Captura").GetComponent<Image>();

        if (data.isSlotInUse)
        {
            if(tiempoTextObject != null) tiempoTextObject.SetActive(true);
            if(horasText != null) horasText.gameObject.SetActive(true);

            string playtime = FormatPlaytime(data.playtimeInSeconds);

            if (mesText != null) mesText.text = data.realWorldDateTime + " // ";
            if (horasText != null) horasText.text = playtime;
            if (escenaText != null) escenaText.text = data.sceneDescription;

            if (capturaImage != null && !string.IsNullOrEmpty(data.backgroundPath))
            {
                Sprite backgroundSprite = Resources.Load<Sprite>("images/" + data.backgroundPath);

                if (backgroundSprite != null)
                {
                    capturaImage.sprite = backgroundSprite;
                    capturaImage.color = Color.white; 
                }
                else
                {
                    // Si no se encuentra el sprite, dejamos la imagen en blanco.
                    capturaImage.sprite = null;
                    capturaImage.color = Color.clear; // Totalmente transparente
                    Debug.LogWarning("No se pudo cargar el sprite de vista previa para la ruta: images/" + data.backgroundPath);
                }
            }
        }
        else // Si el slot está vacío
        {
            // Ocultamos los textos de tiempo que no necesitamos.
            if(tiempoTextObject != null) tiempoTextObject.SetActive(false);
            if(horasText != null) horasText.gameObject.SetActive(false);

            // Mostramos el texto de slot vacío.
            if (mesText != null) mesText.text = string.Format("Slot {0}", slotNumber);
            if (escenaText != null) escenaText.text = "Vacío";

            if (capturaImage != null)
            {
                capturaImage.sprite = null;
                capturaImage.color = new Color(1, 1, 1, 0);
            }
        }
    }
    private string FormatPlaytime(float totalSeconds)
    {
        int hours = (int)(totalSeconds / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        return string.Format("{0:D2}:{1:D2}", hours, minutes);
    }

    private void UpdateHighlightAndScroll()
    {
        for (int i = 0; i < navigableElements.Count; i++)
        {
            if (navigableElements[i] != null)
            {
                navigableElements[i].alpha = (i == currentSelectionIndex) ? 1.0f : 0.5f;
            }
        }

        int slotCount = instantiatedSlots.Count;
        bool isSelectingSlot = currentSelectionIndex < slotCount;

        if (isSelectingSlot)
        {
            if (currentSelectionIndex >= topVisibleIndex + maxVisibleSlots)
            {
                topVisibleIndex = currentSelectionIndex - maxVisibleSlots + 1;
            }
            else if (currentSelectionIndex < topVisibleIndex)
            {
                topVisibleIndex = currentSelectionIndex;
            }
        }

		if (slotsContainerRect != null)
		{
			float basePositionY = -(slotsContainerRect.sizeDelta.y / 2f);
			float scrollOffsetY = topVisibleIndex * slotHeight;
			float targetY = basePositionY + scrollOffsetY;
			
			slotsContainerRect.anchoredPosition = new Vector2(slotsContainerRect.anchoredPosition.x, targetY);
		}
    }

    // --- HELPERS DE FADES Y OTROS ---
    private void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
    
    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration, System.Action onComplete = null)
    {
        if (cg == null) 
        {
            if(onComplete != null) onComplete();
            yield break;
        }
        float startAlpha = cg.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            yield return null;
        }
        cg.alpha = targetAlpha;
        if(onComplete != null) onComplete();
    }
    
    private IEnumerator FadeImage(Image image, float targetAlpha, float duration)
    {
        if (image == null) yield break;
        
        image.gameObject.SetActive(true);
        float startAlpha = image.color.a;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            SetImageAlpha(image, Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration));
            yield return null;
        }
        SetImageAlpha(image, targetAlpha);
    }

    private IEnumerator CrossfadeMenus(CanvasGroup groupToFadeOut, CanvasGroup groupToFadeIn, float duration, System.Action onComplete = null)
    {
        groupToFadeIn.gameObject.SetActive(true);
        groupToFadeIn.alpha = 0f;

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float progress = time / duration;
            if (groupToFadeOut != null) groupToFadeOut.alpha = 1f - progress;
            if (groupToFadeIn != null) groupToFadeIn.alpha = progress;
            yield return null;
        }

        if (groupToFadeOut != null) groupToFadeOut.alpha = 0f;
        if (groupToFadeIn != null) groupToFadeIn.alpha = 1f;
        
        if (onComplete != null) onComplete();
    }
}