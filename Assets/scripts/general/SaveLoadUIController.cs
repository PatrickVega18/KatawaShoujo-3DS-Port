using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public enum SaveLoadMode
{
    Save,
    Load
}

public class SaveLoadUIController : MonoBehaviour
{
    [Header("Referencias de UI")]
    public GameObject slotPrefab;
    public Transform slotContentContainer;
    public RectTransform scrollMaskContainer;
    public Text titleText;
    public MenuController menuController;

    [Header("Opciones Navegables")]
    public Color baseTextColor = Color.black;
    public CanvasGroup crearSaveButtonCanvasGroup;
    public CanvasGroup volverButtonCanvasGroup;

    private List<GameObject> instantiatedSlots = new List<GameObject>();
    private List<CanvasGroup> navigableElements = new List<CanvasGroup>();
    private int currentSelectionIndex = 0;
    private bool navigationEnabled = false;
    private int lastSelectedSlotIndex = 0;

    [Header("Configuración del Scroll")]
    public float slotHeight = 48f;
    public int maxVisibleSlots = 3;
    private int topVisibleIndex = 0;
    private RectTransform slotsContainerRect;

    private SaveLoadMode currentMode;

    [Header("Paneles de Confirmación")]
    public GameObject confirmarBorradoPanel;
    public CanvasGroup confirmarBorradoCanvasGroup;
    public CanvasGroup confirmarBorradoSiCanvasGroup;
    public CanvasGroup confirmarBorradoNoCanvasGroup;

    public GameObject confirmarGuardadoPanel;
    public CanvasGroup confirmarGuardadoCanvasGroup;
    public CanvasGroup confirmarGuardadoSiCanvasGroup;
    public CanvasGroup confirmarGuardadoNoCanvasGroup;

    public GameObject confirmarProcesoPanel;
    public CanvasGroup confirmarProcesoCanvasGroup;
    public CanvasGroup continuarButtonCanvasGroup;

    public GameObject confirmarCargaPanel;
    public CanvasGroup confirmarCargaCanvasGroup;
    public CanvasGroup confirmarCargaSiCanvasGroup;
    public CanvasGroup confirmarCargaNoCanvasGroup;

    private bool isConfirmingDelete = false;
    private int slotIndexToDelete = -1;
    private int slotIndexToSave = -1;
    private int confirmationChoice = 0;
    private bool isConfirmingSave = false;

    private bool isConfirmingLoad = false;
    private int slotIndexToLoad = -1;

    void Start()
    {
        //gameObject.SetActive(false);
        if (slotContentContainer != null)
        {
            slotsContainerRect = slotContentContainer.GetComponent<RectTransform>();
        }
        if (confirmarBorradoPanel != null)
        {
            if (confirmarBorradoCanvasGroup != null) confirmarBorradoCanvasGroup.alpha = 0f;
            confirmarBorradoPanel.SetActive(false);
        }
        if (confirmarGuardadoPanel != null)
        {
            if (confirmarGuardadoCanvasGroup != null) confirmarGuardadoCanvasGroup.alpha = 0f;
            confirmarGuardadoPanel.SetActive(false);
        }
        if (confirmarProcesoPanel != null)
        {
            if (confirmarProcesoCanvasGroup != null) confirmarProcesoCanvasGroup.alpha = 0f;
            confirmarProcesoPanel.SetActive(false);
        }
        if (confirmarCargaPanel != null)
        {
            if (confirmarCargaCanvasGroup != null) confirmarCargaCanvasGroup.alpha = 0f;
            confirmarCargaPanel.SetActive(false);
        }

        PreloadSlots();
        //gameObject.SetActive(false);
    }

    void Update()
    {
        if (isConfirmingLoad)
        {
            if (confirmationChoice == -1)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    confirmationChoice = 0; // "Sí"
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                {
                    confirmationChoice = 1; // "No"
                }
            }
            else // Si ya hay algo seleccionado, permitimos el cambio normal.
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    confirmationChoice = 0; // "Sí"
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    confirmationChoice = 1; // "No"
                }
            }

            // Actualizamos el resaltado de "Sí" y "No"
            if (confirmarCargaSiCanvasGroup != null) confirmarCargaSiCanvasGroup.alpha = (confirmationChoice == 0) ? 1f : 0.5f;
            if (confirmarCargaNoCanvasGroup != null) confirmarCargaNoCanvasGroup.alpha = (confirmationChoice == 1) ? 1f : 0.5f;
            if (confirmationChoice == -1)
            {
                if (confirmarCargaSiCanvasGroup != null) confirmarCargaSiCanvasGroup.alpha = 0.5f;
                if (confirmarCargaNoCanvasGroup != null) confirmarCargaNoCanvasGroup.alpha = 0.5f;
            }
            // --- LÓGICA DE ACCIÓN PARA SÍ/NO ---
            if (Input.GetKeyDown(KeyCode.A))
            {
                if (confirmationChoice == 0) //  "Sí"
                {
                    // Carga el juego y cierra el menú por completo
                    SaveLoadManager.Instance.LoadGame(slotIndexToLoad);
                    if (menuController != null)
                    {
                        menuController.TriggerClosingSequence();
                    }
                }
                else //  "No"
                {
                    navigationEnabled = false;

                    StartCoroutine(FadeOut(confirmarCargaCanvasGroup, 0.3f, () =>
                    {
                        confirmarCargaPanel.SetActive(false);
                    }));

                    // SIMULTÁNEAMENTE, ENCENDEMOS el panel principal (lista de slots).
                    StartCoroutine(FadeIn(GetComponent<CanvasGroup>(), 0.3f, () =>
                    {
                        // Y solo cuando este fundido termine, reseteamos el estado.
                        isConfirmingLoad = false;
                        navigationEnabled = true;
                        slotIndexToLoad = -1;
                    }));
                }
            }
            return;
        }
        if (isConfirmingDelete)
        {
            // --- NAVEGACIÓN DENTRO DEL MENÚ DE CONFIRMACIÓN ---
            if (confirmationChoice == -1)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    confirmationChoice = 0; // "Sí"
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                {
                    confirmationChoice = 1; // "No"
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    confirmationChoice = 0; // "Sí"
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    confirmationChoice = 1; // "No"
                }
            }

            // Actualizamos el resaltado de "Sí" y "No"
            if (confirmarBorradoSiCanvasGroup != null) confirmarBorradoSiCanvasGroup.alpha = (confirmationChoice == 0) ? 1f : 0.5f;
            if (confirmarBorradoNoCanvasGroup != null) confirmarBorradoNoCanvasGroup.alpha = (confirmationChoice == 1) ? 1f : 0.5f;

            // Si no hay nada seleccionado, ambas opciones están atenuadas (esto es de un paso anterior, pero lo mantenemos)
            if (confirmationChoice == -1)
            {
                if (confirmarBorradoSiCanvasGroup != null) confirmarBorradoSiCanvasGroup.alpha = 0.5f;
                if (confirmarBorradoNoCanvasGroup != null) confirmarBorradoNoCanvasGroup.alpha = 0.5f;
            }


            // --- LÓGICA DE ACCIÓN PARA SÍ/NO ---
            if (Input.GetKeyDown(KeyCode.A))
            {
                // Solo actuamos si el jugador ha elegido una opción.
                if (confirmationChoice != -1)
                {
                    if (confirmationChoice == 0) // El jugador eligió "Sí"
                    {
                        StartCoroutine(DeleteAndReturn());
                    }
                    else // El jugador eligió "No"
                    {
                        navigationEnabled = false;

                        StartCoroutine(FadeOut(confirmarBorradoCanvasGroup, 0.3f, () =>
                        {
                            confirmarBorradoPanel.SetActive(false);
                        }));

                        StartCoroutine(FadeIn(GetComponent<CanvasGroup>(), 0.3f, () =>
                        {
                            isConfirmingDelete = false;
                            navigationEnabled = true;
                            slotIndexToDelete = -1;
                        }));
                    }
                }
            }
            return;
        }
        else if (isConfirmingSave)
        {
            if (confirmationChoice == -1)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    confirmationChoice = 0; // "Sí"
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                {
                    confirmationChoice = 1; // "No"
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    confirmationChoice = 0; // "Sí"
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    confirmationChoice = 1; // "No"
                }
            }

            // --- RESALTADO ---
            if (confirmarGuardadoSiCanvasGroup != null) confirmarGuardadoSiCanvasGroup.alpha = (confirmationChoice == 0) ? 1f : 0.5f;
            if (confirmarGuardadoNoCanvasGroup != null) confirmarGuardadoNoCanvasGroup.alpha = (confirmationChoice == 1) ? 1f : 0.5f;
            if (confirmationChoice == -1)
            {
                if (confirmarGuardadoSiCanvasGroup != null) confirmarGuardadoSiCanvasGroup.alpha = 0.5f;
                if (confirmarGuardadoNoCanvasGroup != null) confirmarGuardadoNoCanvasGroup.alpha = 0.5f;
            }

            // --- ACCIÓN ---
            if (Input.GetKeyDown(KeyCode.A))
            {
                if (confirmationChoice != -1)
                {
                    if (confirmationChoice == 0) // Escogimos "Sí"
                    {
                        StartCoroutine(OverwriteAndConfirmSequence(slotIndexToSave));
                    }
                    else // Escogimos "No"
                    {
                        navigationEnabled = false;

                        StartCoroutine(FadeOut(confirmarGuardadoCanvasGroup, 0.3f, () =>
                        {
                            confirmarGuardadoPanel.SetActive(false);
                        }));

                        // SIMULTÁNEAMENTE, ENCENDEMOS el panel principal de guardado.
                        StartCoroutine(FadeIn(GetComponent<CanvasGroup>(), 0.3f, () =>
                        {
                            // Y solo cuando este fundido termine, reseteamos el estado y la navegación.
                            isConfirmingSave = false;
                            navigationEnabled = true;
                        }));
                    }
                }
            }
            return;
        }
        if (confirmarProcesoPanel != null && confirmarProcesoPanel.activeSelf)
        {
            // Resaltamos el botón "Continuar" si se pulsa cualquier dirección.
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (continuarButtonCanvasGroup != null) continuarButtonCanvasGroup.alpha = 1f;
            }

            // Si pulsamos 'A', volvemos al menú principal.
            if (Input.GetKeyDown(KeyCode.A))
            {
                navigationEnabled = false;

                StartCoroutine(FadeOut(confirmarProcesoCanvasGroup, 0.3f, () =>
                {
                    confirmarProcesoPanel.SetActive(false);
                }));

                StartCoroutine(FadeIn(GetComponent<CanvasGroup>(), 0.3f, () =>
                {
                    RefrescarListaDeSlots();
                }));
            }
            return;
        }

        if (!navigationEnabled || navigableElements.Count == 0) return;

        int previousSelection = currentSelectionIndex;
        int slotCount = instantiatedSlots.Count;
        bool isSelectingSlot = currentSelectionIndex < slotCount;

        // --- Navegación Vertical ---
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (isSelectingSlot)
            {
                // Si estamos en el primer slot, vamos al último elemento de la lista (Volver).
                if (currentSelectionIndex == 0)
                    currentSelectionIndex = navigableElements.Count - 1;
                else
                    currentSelectionIndex--;
            }
            else // Estamos en los botones
            {
                currentSelectionIndex = lastSelectedSlotIndex;
            }
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (isSelectingSlot)
            {
                // Si estamos en el último slot, saltamos al primer botón disponible.
                if (currentSelectionIndex == slotCount - 1)
                    currentSelectionIndex = slotCount; // El primer botón siempre está en el índice 'slotCount'
                else
                    currentSelectionIndex++;
            }
            else // Estamos en los botones, vamos al primer slot
            {
                if (slotCount > 0)
                    currentSelectionIndex = 0;
            }
        }

        // --- Navegación Horizontal y Atajos ---
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (isSelectingSlot)
            {
                if (currentMode == SaveLoadMode.Save)
                    currentSelectionIndex = slotCount; // Atajo a "Crear Save"
                else
                    currentSelectionIndex = navigableElements.Count - 1; // Atajo a "Volver" en modo Carga
            }
            else // Navegación entre botones
            {
                // Si estamos en Volver y hay un botón a la izquierda (modo Guardar), vamos a él.
                if (currentSelectionIndex == navigableElements.Count - 1 && currentMode == SaveLoadMode.Save)
                {
                    currentSelectionIndex = slotCount;
                }
            }
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (isSelectingSlot)
            {
                // El atajo a la derecha siempre va al último elemento (Volver).
                currentSelectionIndex = navigableElements.Count - 1;
            }
            else // Navegación entre botones
            {
                // Si estamos en "Crear Save", vamos a "Volver".
                if (currentMode == SaveLoadMode.Save && currentSelectionIndex == slotCount)
                {
                    currentSelectionIndex = navigableElements.Count - 1;
                }
            }
        }
        // --- Lógica para el Botón de Borrado (Tecla 'X') ---
        if (Input.GetKeyDown(KeyCode.X))
        {
            if (isSelectingSlot)
            {
                // Obtenemos el GameObject del slot seleccionado en la UI
                GameObject selectedSlotObject = instantiatedSlots[currentSelectionIndex];
                // Obtenemos su componente Helper
                SaveSlotHelper helper = selectedSlotObject.GetComponent<SaveSlotHelper>();
                // Guardamos el ÍNDICE ORIGINAL para el borrado
                slotIndexToDelete = helper.originalSlotIndex;
                isConfirmingDelete = true;
                navigationEnabled = false;
                confirmationChoice = -1; // Por defecto, empezamos en "Sí"

                // Iniciamos el fundido cruzado
                StartCoroutine(CrossfadeMenus(GetComponent<CanvasGroup>(), confirmarBorradoCanvasGroup, 0.3f));
            }
        }
        // Si la selección ha cambiado, actualizamos la vista.
        if (previousSelection != currentSelectionIndex)
        {
            UpdateHighlightAndScroll();
        }

        // ===== Recordamos el último slot que se ha seleccionado =====
        if (currentSelectionIndex < slotCount)
        {
            lastSelectedSlotIndex = currentSelectionIndex;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            int createButtonIndex = (currentMode == SaveLoadMode.Save) ? instantiatedSlots.Count : -1;
            int backButtonIndex = navigableElements.Count - 1;
            if (isSelectingSlot)
            {
                int originalIndex = instantiatedSlots[currentSelectionIndex].GetComponent<SaveSlotHelper>().originalSlotIndex;

                if (currentMode == SaveLoadMode.Save)
                {
                    slotIndexToSave = originalIndex;

                    isConfirmingSave = true;
                    navigationEnabled = false;
                    confirmationChoice = -1;
                    StartCoroutine(CrossfadeMenus(GetComponent<CanvasGroup>(), confirmarGuardadoCanvasGroup, 0.3f));
                }
                else if (currentMode == SaveLoadMode.Load)
                {
                    // En lugar de cargar directamente, activamos el modo de confirmación.
                    isConfirmingLoad = true;
                    navigationEnabled = false;
                    slotIndexToLoad = originalIndex; // Guardamos el índice REAL que queremos cargar.
                    confirmationChoice = -1;
                    // Hacemos el fundido al panel de confirmar carga.
                    StartCoroutine(CrossfadeMenus(GetComponent<CanvasGroup>(), confirmarCargaCanvasGroup, 0.3f));
                }
            }
            else if (currentSelectionIndex == createButtonIndex)
            {
                StartCoroutine(OnCreateNewSave(-1));
            }
            else if (currentSelectionIndex == backButtonIndex)
            {
                ResetAndClose();
            }
        }
    }

    // MÉTODO DEDICADO A LA PRE-CARGA DE SLOTS
    // Se solucionó el Lag y el volcado de memoria
    // Casi se quema mi 3ds crjo XDDDD
    private void PreloadSlots()
    {
        if (SaveLoadManager.Instance == null) return;
        if (slotPrefab == null || slotContentContainer == null) return;

        // Limpiamos slots antiguos por si acaso
        foreach (GameObject slot in instantiatedSlots)
        {
            RawImage capturaImage = slot.transform.Find("Captura").GetComponent<RawImage>();
            if (capturaImage != null && capturaImage.texture != null)
            {
                Destroy(capturaImage.texture);
            }
            Destroy(slot);
        }
        instantiatedSlots.Clear();

        List<SaveSlotData> slotsGuardados = SaveLoadManager.Instance.GetSaveSlots();
        if (slotsGuardados == null) return;

        // Creamos una instancia de GameObject para cada slot guardado
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
            }
        }

        // Ajustamos la altura del contenedor
        float totalContentHeight = instantiatedSlots.Count * slotHeight;
        if (slotsContainerRect != null)
        {
            slotsContainerRect.sizeDelta = new Vector2(slotsContainerRect.sizeDelta.x, totalContentHeight);
        }
    }


    private IEnumerator OnCreateNewSave(int slotIndexToSave)
    {
        int targetSlot = slotIndexToSave;

        // Si el índice es -1, buscamos un nuevo slot vacío.
        if (targetSlot == -1)
        {
            List<SaveSlotData> slots = SaveLoadManager.Instance.GetSaveSlots();
            int emptySlotIndex = -1;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].isSlotInUse)
                {
                    emptySlotIndex = i;
                    break;
                }
            }
            targetSlot = emptySlotIndex;
        }

        if (targetSlot != -1)
        {
            yield return StartCoroutine(SaveLoadManager.Instance.SaveGame(targetSlot));

            StartCoroutine(CrossfadeMenus(GetComponent<CanvasGroup>(), confirmarProcesoCanvasGroup, 0.3f));

            // Actualizamos la lista de slots en segundo plano para que esté lista cuando volvamos.
            RefrescarListaDeSlots();
        }
        else
        {
            Debug.Log("No hay slots de guardado libres.");
        }
    }

    public void AbrirMenuGuardado(SaveLoadMode mode)
    {
        currentMode = mode;

        if (mode == SaveLoadMode.Load)
        {
            if (crearSaveButtonCanvasGroup != null) crearSaveButtonCanvasGroup.gameObject.SetActive(false);
        }
        else
        {
            if (crearSaveButtonCanvasGroup != null) crearSaveButtonCanvasGroup.gameObject.SetActive(true);
        }

        navigableElements.Clear();

        foreach (GameObject slot in instantiatedSlots)
        {
            navigableElements.Add(slot.GetComponent<CanvasGroup>());
        }

        if (currentMode == SaveLoadMode.Save)
        {
            if (crearSaveButtonCanvasGroup != null) navigableElements.Add(crearSaveButtonCanvasGroup);
        }
        if (volverButtonCanvasGroup != null) navigableElements.Add(volverButtonCanvasGroup);

        // --- LÓGICA DE SELECCIÓN INICIAL ---
        if (instantiatedSlots.Count > 0)
        {
            currentSelectionIndex = 0;
        }
        else
        {
            currentSelectionIndex = 0; // Apuntará al primer botón
        }
        lastSelectedSlotIndex = 0;
        topVisibleIndex = 0;
        if (slotsContainerRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotsContainerRect);
        }
        UpdateHighlightAndScroll();

        navigationEnabled = false;
        StartCoroutine(EnableNavigationAfterFrame());
    }

    private void RefrescarListaDeSlots()
    {
        PreloadSlots();
        AbrirMenuGuardado(currentMode);
    }
    private IEnumerator EnableNavigationAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        navigationEnabled = true;
    }
    private void PopulateSlotUI(GameObject slotInstance, SaveSlotData data, int slotNumber)
    {
        Text mesText = slotInstance.transform.Find("Mes").GetComponent<Text>();
        Text horasText = slotInstance.transform.Find("Horas").GetComponent<Text>();
        GameObject tiempoTextObject = slotInstance.transform.Find("TiempoText").gameObject; // Obtenemos el GameObject para poder ocultarlo

        Text escenaText = slotInstance.transform.Find("Escena").GetComponent<Text>();
        Image capturaImage = slotInstance.transform.Find("Captura").GetComponent<Image>();

        if (data.isSlotInUse)
        {
            if (tiempoTextObject != null) tiempoTextObject.SetActive(true);
            if (horasText != null) horasText.gameObject.SetActive(true);

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
                    capturaImage.color = Color.clear;
                    Debug.LogWarning("No se pudo cargar el sprite de vista previa para la ruta: images/" + data.backgroundPath);
                }
            }
        }
        else
        {
            // Ocultamos los textos de tiempo que no necesitamos.
            if (tiempoTextObject != null) tiempoTextObject.SetActive(false);
            if (horasText != null) horasText.gameObject.SetActive(false);

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
    public void ResetStateInstantly()
    {
        StopAllCoroutines();
        navigationEnabled = false;

        isConfirmingDelete = false;
        isConfirmingSave = false;
        isConfirmingLoad = false;
        slotIndexToDelete = -1;
        slotIndexToSave = -1;
        slotIndexToLoad = -1;
        confirmationChoice = 0;

        if (confirmarBorradoPanel != null)
        {
            if (confirmarBorradoCanvasGroup != null) confirmarBorradoCanvasGroup.alpha = 0f;
            confirmarBorradoPanel.SetActive(false);
        }
        if (confirmarGuardadoPanel != null)
        {
            if (confirmarGuardadoCanvasGroup != null) confirmarGuardadoCanvasGroup.alpha = 0f;
            confirmarGuardadoPanel.SetActive(false);
        }
        if (confirmarProcesoPanel != null)
        {
            if (confirmarProcesoCanvasGroup != null) confirmarProcesoCanvasGroup.alpha = 0f;
            confirmarProcesoPanel.SetActive(false);
        }
        if (confirmarCargaPanel != null)
        {
            if (confirmarCargaCanvasGroup != null) confirmarCargaCanvasGroup.alpha = 0f;
            confirmarCargaPanel.SetActive(false);
        }

        if (GetComponent<CanvasGroup>() != null)
        {
            GetComponent<CanvasGroup>().alpha = 0f;
        }
    }
    private IEnumerator DeleteAndReturn()
    {
        Debug.Log("Confirmado. Borrando slot " + slotIndexToDelete);
        SaveLoadManager.Instance.DeleteSaveSlot(slotIndexToDelete);

        navigationEnabled = false;

        // Hacemos el fundido cruzado DE VUELTA al menú principal.
        StartCoroutine(FadeOut(confirmarBorradoCanvasGroup, 0.3f, () =>
        {
            confirmarBorradoPanel.SetActive(false);
        }));
        StartCoroutine(FadeIn(GetComponent<CanvasGroup>(), 0.3f, null));
        yield return new WaitForSeconds(0.3f);

        // 3. Una vez de vuelta, refrescamos la lista.
        RefrescarListaDeSlots();


        if (instantiatedSlots.Count > 0)
        {
            currentSelectionIndex = instantiatedSlots.Count - 1;
            lastSelectedSlotIndex = currentSelectionIndex;
        }
        else
        {
            currentSelectionIndex = 0; // El índice 0 ahora corresponde al primer botón.
            lastSelectedSlotIndex = 0;
        }

        // Forzamos una actualización inmediata del resaltado y el scroll.
        UpdateHighlightAndScroll();

        isConfirmingDelete = false;
        navigationEnabled = true;
        slotIndexToDelete = -1;
    }
    public CanvasGroup GetVisibleConfirmationCanvasGroup()
    {
        if (confirmarBorradoCanvasGroup != null && confirmarBorradoCanvasGroup.alpha > 0)
        {
            return confirmarBorradoCanvasGroup;
        }
        if (confirmarGuardadoCanvasGroup != null && confirmarGuardadoCanvasGroup.alpha > 0)
        {
            return confirmarGuardadoCanvasGroup;
        }
        if (confirmarProcesoCanvasGroup != null && confirmarProcesoCanvasGroup.alpha > 0)
        {
            return confirmarProcesoCanvasGroup;
        }
        if (confirmarCargaCanvasGroup != null && confirmarCargaCanvasGroup.alpha > 0)
        {
            return confirmarCargaCanvasGroup;
        }

        // Si llegamos aquí, ningún panel de confirmación está visible.
        return null;
    }
    private IEnumerator FadeIn(CanvasGroup canvasGroup, float duration, System.Action onComplete)
    {
        if (canvasGroup == null)
        {
            if (onComplete != null) onComplete();
            yield break;
        }

        canvasGroup.gameObject.SetActive(true);
        float startAlpha = 0f;
        canvasGroup.alpha = startAlpha;

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, time / duration);
            yield return null;
        }

        canvasGroup.alpha = 1f;

        if (onComplete != null)
        {
            onComplete();
        }
    }
    private IEnumerator FadeOut(CanvasGroup canvasGroup, float duration, System.Action onComplete)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, time / duration);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        if (onComplete != null)
        {
            onComplete();
        }
    }
    private IEnumerator OverwriteAndConfirmSequence(int slotIndex)
    {
        yield return StartCoroutine(SaveLoadManager.Instance.SaveGame(slotIndex));
        StartCoroutine(FadeOut(confirmarGuardadoCanvasGroup, 0.3f, () =>
        {
            confirmarGuardadoPanel.SetActive(false);
        }));

        StartCoroutine(FadeIn(confirmarProcesoCanvasGroup, 0.3f, null));

        // Esperamos a que los fundidos terminen antes de continuar.
        yield return new WaitForSeconds(0.3f);

        isConfirmingSave = false;
    }
    private IEnumerator CrossfadeMenus(CanvasGroup groupToFadeOut, CanvasGroup groupToFadeIn, float duration)
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

        if (groupToFadeOut != null)
        {
            groupToFadeOut.alpha = 0f;
        }
        if (groupToFadeIn != null)
        {
            groupToFadeIn.alpha = 1f;
        }
    }


    public void ResetAndClose()
    {
        StopAllCoroutines();
        navigationEnabled = false;

        isConfirmingDelete = false;
        isConfirmingSave = false;
        isConfirmingLoad = false;
        slotIndexToDelete = -1;
        slotIndexToSave = -1;
        slotIndexToLoad = -1;
        confirmationChoice = 0;

        if (confirmarBorradoPanel != null) confirmarBorradoPanel.SetActive(false);
        if (confirmarGuardadoPanel != null) confirmarGuardadoPanel.SetActive(false);
        if (confirmarProcesoPanel != null) confirmarProcesoPanel.SetActive(false);
        if (confirmarCargaPanel != null) confirmarCargaPanel.SetActive(false);

        if (menuController != null)
        {
            menuController.ShowSaveLoadScreen(false);
        }
    }
    
    //La firme Gemini hizo la mitad de esto XD
}