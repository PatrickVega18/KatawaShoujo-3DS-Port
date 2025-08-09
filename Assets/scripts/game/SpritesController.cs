using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

public class SpritesController : MonoBehaviour
{
	[System.Serializable]
	public class SpriteSlot
	{
		public string slotName = "SpriteSlot";
		public Image mainImage;
		public Image switchImage;

		[HideInInspector] public Vector2 currentSize;
		[HideInInspector] public Image currentActiveImage;
		[HideInInspector] public string currentSpritePath = null;
		[HideInInspector] public Vector3 currentPosition;
		[HideInInspector] public Vector3 currentScale = Vector3.one;
		[HideInInspector] public Coroutine activeAnimation = null;
		[HideInInspector] public Vector3 initialPosition;

		[HideInInspector] public string pendingSwitchSpritePath;
		[HideInInspector] public float targetAlphaForCompletion;
		[HideInInspector] public Vector3 targetPositionForCompletion;
		[HideInInspector] public Vector3 targetScaleForCompletion;
		[HideInInspector] public bool isFading;
		[HideInInspector] public bool isMoving; //Tmb lo usaremos para el Shake
		[HideInInspector] public bool isScaling; // Si implementamos zoom de escala para sprites
		[HideInInspector] public bool isSwitching;

		public void Initialize(Vector3 basePosition, Vector2 baseSize)
		{
			initialPosition = basePosition;
			if (mainImage != null)
			{
				mainImage.sprite = null;
				SpritesController.SetImageAlpha(mainImage, 0f); // Usar el método estático
				mainImage.gameObject.SetActive(false);
				mainImage.rectTransform.anchoredPosition3D = initialPosition;
				mainImage.rectTransform.localScale = Vector3.one;
			}
			if (switchImage != null)
			{
				switchImage.sprite = null;
				SpritesController.SetImageAlpha(switchImage, 0f); // Usar el método estático
				switchImage.gameObject.SetActive(false);
				switchImage.rectTransform.anchoredPosition3D = initialPosition;
				switchImage.rectTransform.localScale = Vector3.one;
			}
			currentPosition = initialPosition;
			currentScale = Vector3.one;
			currentSize = Vector2.zero;
			currentActiveImage = mainImage;
		}
	}

	[Header("Referencias UI de Sprites")]
	public SpriteSlot spriteSlot1;
	public SpriteSlot spriteSlot2;
	public SpriteSlot spriteSlot3;

	public List<SpriteSlot> allSlots; // Para acceder a los slots por índice fácilmente

	[Header("Configuración de Animaciones (Valores por defecto)")]
	public float slideOffset = 50f;
	public float spriteShakeAmplitude = 5f;
	public int spriteShakeVibrations = 4;

	[Tooltip("Factor de velocidad para el fade-in durante un 'switch'. Ej: 0.5 para que dure la mitad del tiempo total del switch.")]
	[Range(0.1f, 1f)] // Rango para asegurar que sea un valor útil
	public float switchFadeInSpeedFactor = 0.5f;

	// Flags de estado (uno por slot para más precisión)
	private bool[] slotAnimacionCompletada = new bool[3] { true, true, true };

	// Propiedad general que chequea si ALGÚN slot está animando
	public bool IsSpriteAnimando
	{
		get
		{
			if (allSlots == null) return false;
			foreach (var slot in allSlots)
			{
				if (slot != null && slot.activeAnimation != null) return true;
			}
			return false;
		}
	}
	// Propiedad general que chequea si TODAS las animaciones de sprites están completadas
	public bool SpriteAnimacionCompletada
	{
		get
		{
			if (allSlots == null) return true; // Protección
			for (int i = 0; i < allSlots.Count; i++)
			{
				if (!slotAnimacionCompletada[i]) return false;
			}
			return true;
		}
	}



	// ===== INICIALIZA Y VALIDACIÓN DE REFERENCIAS =====
	void Start()
	{
		bool initializationError = false;
		if (spriteSlot1 == null || spriteSlot1.mainImage == null || spriteSlot1.switchImage == null)
		{
			Debug.LogError("SpritesController: SpriteSlot1 o sus imágenes no están asignados.", gameObject);
			initializationError = true;
		}
		if (spriteSlot2 == null || spriteSlot2.mainImage == null || spriteSlot2.switchImage == null)
		{
			Debug.LogError("SpritesController: SpriteSlot2 o sus imágenes no están asignados.", gameObject);
			initializationError = true;
		}
		if (spriteSlot3 == null || spriteSlot3.mainImage == null || spriteSlot3.switchImage == null)
		{
			Debug.LogError("SpritesController: SpriteSlot3 o sus imágenes no están asignados.", gameObject);
			initializationError = true;
		}

		if (initializationError)
		{
			this.enabled = false;
			return;
		}

		// Crear una lista para fácil acceso por índice
		allSlots = new List<SpriteSlot> { spriteSlot1, spriteSlot2, spriteSlot3 };

		for (int i = 0; i < allSlots.Count; i++)
		{
			if (allSlots[i].mainImage != null) // Asegurar que mainImage exista para obtener la pos/tamaño inicial
			{
				allSlots[i].Initialize(allSlots[i].mainImage.rectTransform.anchoredPosition3D, allSlots[i].mainImage.rectTransform.sizeDelta);
			}
			else
			{
				Debug.LogError("SpritesController: MainImage para el slot " + (i + 1) + " no está asignado. No se pudo inicializar correctamente.");
				this.enabled = false; return;
			}
			slotAnimacionCompletada[i] = true; // Cada slot individual está completado al inicio
		}
	}

	//==== PROCESA EL COMANDO PARA UN SLOT DE SPRITE ESPECÍFICO (LLAMADO DESDE GAMECONTROLLER) ====
	public void ProcessSpriteCommand(int slotIndex, string pathFromCsv, string accionConParametros, string switchToPathFromCsv, string coordenadasPosicionCsv, float tiempo, string tamanoCsv)
	{
		if (slotIndex < 0 || slotIndex >= allSlots.Count)
		{
			Debug.LogError("SpritesController: Índice de slot inválido: " + slotIndex);
			return;
		}

		SpriteSlot currentSlot = allSlots[slotIndex];

		// Si no hay acción y no hay nuevo path, no hacer nada para este slot.
		if (string.IsNullOrEmpty(accionConParametros) && string.IsNullOrEmpty(pathFromCsv) && string.IsNullOrEmpty(switchToPathFromCsv))
		{
			slotAnimacionCompletada[slotIndex] = true;
			if (currentSlot.activeAnimation != null)
			{ // Detener si había algo y se llama con comando vacío
				StopCoroutine(currentSlot.activeAnimation);
				currentSlot.activeAnimation = null;
			}
			return;
		}


		// Detener animación anterior del slot específico
		if (currentSlot.activeAnimation != null)
		{
			StopCoroutine(currentSlot.activeAnimation);
			// Restaurar al estado actual conocido antes de la nueva animación
			if (currentSlot.currentActiveImage != null)
			{
				currentSlot.currentActiveImage.rectTransform.anchoredPosition3D = currentSlot.currentPosition;
				currentSlot.currentActiveImage.rectTransform.localScale = currentSlot.currentScale;
				SetImageAlpha(currentSlot.currentActiveImage, currentSlot.currentActiveImage.sprite != null ? currentSlot.currentActiveImage.color.a : 0f);
			}
		}
		slotAnimacionCompletada[slotIndex] = false;
		currentSlot.isFading = false; // Resetear flags de tipo de animación
		currentSlot.isMoving = false; // Tmb para Shake
		currentSlot.isScaling = false;
		currentSlot.isSwitching = false;

		string accionBase = accionConParametros;
		if (!string.IsNullOrEmpty(accionBase)) accionBase = accionBase.ToLowerInvariant();

		switch (accionBase)
		{
			case "punch":
				ExecuteSpritePunch(currentSlot, pathFromCsv, coordenadasPosicionCsv, tamanoCsv);
				slotAnimacionCompletada[slotIndex] = true;
				currentSlot.activeAnimation = null;
				break;

			case "fadein":
				ExecuteSpriteFadeIn(currentSlot, pathFromCsv, tiempo, coordenadasPosicionCsv, tamanoCsv);
				break;
			case "fadeout":
				ExecuteSpriteFadeOut(currentSlot, tiempo);
				break;
			case "move":
				ExecuteSpriteMove(currentSlot, tiempo, coordenadasPosicionCsv);
				break;
			case "shakex":
				ExecuteSpriteShake(currentSlot, tiempo, true);
				break;
			case "shakey":
				ExecuteSpriteShake(currentSlot, tiempo, false);
				break;
			case "switch":
				ExecuteSpriteSwitch(currentSlot, switchToPathFromCsv, tiempo, coordenadasPosicionCsv, tamanoCsv);
				break;
			case "fadeinslideleft":
				ExecuteFadeInSlide(currentSlot, pathFromCsv, tiempo, coordenadasPosicionCsv, -1); // -1 para desde la izquierda
				break;
			case "fadeinslideright":
				ExecuteFadeInSlide(currentSlot, pathFromCsv, tiempo, coordenadasPosicionCsv, 1); // 1 para desde la derecha
				break;
			case "fadeoutslide":
				ExecuteFadeOutSlide(currentSlot, tiempo, coordenadasPosicionCsv);
				break;

			// Añadiremos casos para zoomIn/zoomOut si los sprites también los necesitan
			// No recuerdo si hay o no hay XD

			default:
				if (!string.IsNullOrEmpty(accionBase)) // Solo advertir si se especificó una acción desconocida
				{
					Debug.LogWarning("SpritesController: Acción desconocida '" + accionBase + "' para Slot " + (slotIndex + 1));
				}
				slotAnimacionCompletada[slotIndex] = true; // Considerar completado si no hay acción válida
				currentSlot.activeAnimation = null;
				break;
		}
	}

	// ===== EJECUTA LA ACCIÓN 'PUNCH' PARA UN SLOT DE SPRITE =====
    private void ExecuteSpritePunch(SpriteSlot slot, string pathFromCsv, string coordenadasPosicionCsv, string tamanoCsv)
    {
        Image imageToUse = slot.currentActiveImage;
        if (imageToUse == null) imageToUse = slot.mainImage;

        if (imageToUse == null)
        {
            Debug.LogError(this.GetType().Name + " (Punch): No hay Image asignada para el slot " + slot.slotName);
            return;
        }

        Sprite spriteToSet = imageToUse.sprite;
        if (!string.IsNullOrEmpty(pathFromCsv))
        {
            string pathWithoutExtension = pathFromCsv;
            int dotIndex = pathFromCsv.LastIndexOf('.');
            if (dotIndex > 0 && dotIndex > pathFromCsv.LastIndexOf('/'))
            {
                pathWithoutExtension = pathFromCsv.Substring(0, dotIndex);
            }

            if (pathWithoutExtension != slot.currentSpritePath || imageToUse.sprite == null)
            {
                string resourcePath = "images/" + pathWithoutExtension;
                Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);
                if (loadedSprite != null)
                {
                    spriteToSet = loadedSprite;
                    slot.currentSpritePath = pathWithoutExtension;
                }
                else
                {
                    Debug.LogError(this.GetType().Name + " (Punch): No se pudo cargar Sprite desde: " + resourcePath + " para slot " + slot.slotName);
                }
            }
        }

        imageToUse.sprite = spriteToSet;

        // Aplicamos el nuevo tamaño. Si tamanoCsv es nulo/vacío, usará el tamaño actual.
        Vector2 nuevoTamano = ParseTamano(tamanoCsv, slot.currentSize);
        imageToUse.rectTransform.sizeDelta = nuevoTamano;
		slot.currentSize = nuevoTamano;

        // Aplicamos las coordenadas
		if (!string.IsNullOrEmpty(coordenadasPosicionCsv))
		{
			Vector3 newPosition = ParseSpriteCoordenadas(coordenadasPosicionCsv, slot.initialPosition);
			imageToUse.rectTransform.anchoredPosition3D = newPosition;
			slot.currentPosition = newPosition;
		}
		else
		{
			imageToUse.rectTransform.anchoredPosition3D = slot.currentPosition;
		}

        if (imageToUse.sprite != null)
        {
            SetImageAlpha(imageToUse, 1f);
            imageToUse.gameObject.SetActive(true);
        }
        else
        {
            SetImageAlpha(imageToUse, 0f);
            imageToUse.gameObject.SetActive(false);
        }
        
        Image otherImage = (imageToUse == slot.mainImage) ? slot.switchImage : slot.mainImage;
        if (otherImage != null)
        {
            otherImage.gameObject.SetActive(false);
            SetImageAlpha(otherImage, 0f);
        }

        slot.currentActiveImage = imageToUse;
    }
	// ===== Método para ejecutar las animaciones de fadeIn y slide =====
	private void ExecuteFadeInSlide(SpriteSlot slot, string pathFromCsv, float duration, string coordsCsv, int direction)
	{
		Image imageToUse = slot.currentActiveImage;
		if (!LoadSpriteForAnimation(slot, imageToUse, pathFromCsv))
		{
			slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
			return;
		}

		// La coordenada del CSV es el DESTINO FINAL
		Vector3 destinationPos = ParseSpriteCoordenadas(coordsCsv, slot.initialPosition);
		// La posición INICIAL se calcula con el offset
		Vector3 startPos = new Vector3(destinationPos.x + (slideOffset * direction), destinationPos.y, destinationPos.z);

		// Guardar estado final para el "skip"
		slot.targetAlphaForCompletion = 1f;
		slot.targetPositionForCompletion = destinationPos;
		slot.isFading = true;
		slot.isMoving = true;

		slot.activeAnimation = StartCoroutine(AnimateFadeAndSlide(slot, imageToUse, startPos, destinationPos, 0f, 1f, duration));
	}

	// ===== Método para ejecutar la animación de fadeOut y slide =====
	private void ExecuteFadeOutSlide(SpriteSlot slot, float duration, string coordsCsv)
	{
		Image imageToUse = slot.currentActiveImage;
		if (imageToUse == null || imageToUse.sprite == null || !imageToUse.gameObject.activeSelf)
		{
			slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
			return; // No hay nada que animar
		}

		// La posición INICIAL es la actual
		Vector3 startPos = imageToUse.rectTransform.anchoredPosition3D;
		// La coordenada del CSV es el DESTINO FINAL
		Vector3 destinationPos = ParseSpriteCoordenadas(coordsCsv, startPos);

		// Guardar estado final para el "skip"
		slot.targetAlphaForCompletion = 0f;
		slot.targetPositionForCompletion = destinationPos;
		slot.isFading = true;
		slot.isMoving = true;

		slot.activeAnimation = StartCoroutine(AnimateFadeAndSlide(slot, imageToUse, startPos, destinationPos, imageToUse.color.a, 0f, duration));
	}

	// ===== Corrutina genérica que anima alfa y posición a la vez =====
	private IEnumerator AnimateFadeAndSlide(SpriteSlot slot, Image targetImage, Vector3 startPos, Vector3 endPos, float startAlpha, float endAlpha, float duration)
	{
		int slotIndex = allSlots.IndexOf(slot);
		if (targetImage == null)
		{
			if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
			yield break;
		}

		// Preparamos el estado inicial
		targetImage.gameObject.SetActive(true);
		targetImage.rectTransform.anchoredPosition3D = startPos;
		SetImageAlpha(targetImage, startAlpha);

		float elapsedTime = 0f;
		while (elapsedTime < duration)
		{
			elapsedTime += Time.deltaTime;
			float progress = Mathf.Clamp01(elapsedTime / duration);

			// Animar posición
			targetImage.rectTransform.anchoredPosition3D = Vector3.Lerp(startPos, endPos, progress);

			// Animar alfa
			SetImageAlpha(targetImage, Mathf.Lerp(startAlpha, endAlpha, progress));

			yield return null;
		}

		// Aseguramos el estado final
		targetImage.rectTransform.anchoredPosition3D = endPos;
		SetImageAlpha(targetImage, endAlpha);

		slot.currentPosition = endPos; // Actualizamos la posición final del slot

		// Si es un fadeOut completo, desactivamos el objeto
		if (endAlpha == 0f)
		{
			targetImage.gameObject.SetActive(false);
		}

		// Limpiamos los flags
		slot.activeAnimation = null;
		slot.isFading = false;
		slot.isMoving = false;
		if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
	}
	// ===== EJECUTA LA ACCIÓN 'FADEIN' PARA UN SLOT DE SPRITE =====
    private void ExecuteSpriteFadeIn(SpriteSlot slot, string pathFromCsv, float duration, string coordenadasPosicionCsv, string tamanoCsv)
    {
        Image imageToUse = slot.currentActiveImage;
        if (imageToUse == null) imageToUse = slot.mainImage;
        if (imageToUse == null)
        {
            Debug.LogError(this.GetType().Name + " (FadeIn): No hay Image asignada para el slot " + slot.slotName);
            slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
            return;
        }

        Sprite spriteToSet = imageToUse.sprite;
        bool spriteChangedOrWasNull = false;

        if (!string.IsNullOrEmpty(pathFromCsv))
        {
            string pathWithoutExtension = pathFromCsv;
            int dotIndex = pathFromCsv.LastIndexOf('.');
            if (dotIndex > 0 && dotIndex > pathFromCsv.LastIndexOf('/'))
            {
                pathWithoutExtension = pathFromCsv.Substring(0, dotIndex);
            }

            if (pathWithoutExtension != slot.currentSpritePath || imageToUse.sprite == null)
            {
                string resourcePath = "images/" + pathWithoutExtension;
                Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);
                if (loadedSprite != null)
                {
                    spriteToSet = loadedSprite;
                    slot.currentSpritePath = pathWithoutExtension;
                    spriteChangedOrWasNull = true;
                }
                else
                {
                    Debug.LogError(this.GetType().Name + " (FadeIn): No se pudo cargar Sprite desde: " + resourcePath + " para slot " + slot.slotName);
                    if (imageToUse.sprite == null)
                    {
                        slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
                        return;
                    }
                }
            }
        }
        else if (imageToUse.sprite == null)
        {
            Debug.LogWarning(this.GetType().Name + " (FadeIn): No se especificó path y no hay sprite actual en slot " + slot.slotName + ". No se puede hacer FadeIn.");
            slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
            return;
        }

        if (spriteChangedOrWasNull)
        {
            imageToUse.sprite = spriteToSet;
        }

        // Aplicamos el nuevo tamaño.
        Vector2 nuevoTamano = ParseTamano(tamanoCsv, slot.currentSize);
        imageToUse.rectTransform.sizeDelta = nuevoTamano;
		slot.currentSize = nuevoTamano;

        // Aplicamos las coordenadas
        if (!string.IsNullOrEmpty(coordenadasPosicionCsv))
        {
            Vector3 newPosition = ParseSpriteCoordenadas(coordenadasPosicionCsv, slot.initialPosition);
            imageToUse.rectTransform.anchoredPosition3D = newPosition;
            slot.currentPosition = newPosition;
        }
        else
        {
            imageToUse.rectTransform.anchoredPosition3D = slot.currentPosition;
        }

        imageToUse.gameObject.SetActive(true);

        slot.targetAlphaForCompletion = 1f;
        slot.targetPositionForCompletion = slot.currentPosition;
        slot.targetScaleForCompletion = slot.currentScale;
        slot.isFading = true;
        slot.isMoving = false;
        slot.isScaling = false;

        slot.activeAnimation = StartCoroutine(AnimateSpriteFade(imageToUse, 1f, duration, slot));
    }
	// ===== EJECUTA LA ACCIÓN 'FADEOUT' PARA UN SLOT DE SPRITE =====
	private void ExecuteSpriteFadeOut(SpriteSlot slot, float duration)
	{
		Image imageToUse = slot.currentActiveImage;
		if (imageToUse == null) imageToUse = slot.mainImage; // Fallback

		if (imageToUse == null || imageToUse.sprite == null || !imageToUse.gameObject.activeSelf || imageToUse.color.a == 0f)
		{
			Debug.Log("SpritesController (FadeOut): Slot " + slot.slotName + " ya está oculto o sin sprite. No se realiza fadeOut.");
			slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
			// Asegurarse de que esté inactivo si no tiene sprite o es transparente
			if (imageToUse != null) imageToUse.gameObject.SetActive(imageToUse.sprite != null && imageToUse.color.a > 0f);
			return;
		}

		// El sprite y las coordenadas no cambian para un fadeOut, solo el alfa.
		imageToUse.gameObject.SetActive(true); // Debe estar activo para que la corrutina pueda modificar el alfa

		// Guardar estado final para CompleteAllSpriteAnimations
		slot.targetAlphaForCompletion = 0f;
		slot.targetPositionForCompletion = slot.currentPosition; // La posición no cambia
		slot.targetScaleForCompletion = slot.currentScale;         // La escala no cambia
		slot.isFading = true;
		slot.isMoving = false;
		slot.isScaling = false;

		slot.activeAnimation = StartCoroutine(AnimateSpriteFade(imageToUse, 0f, duration, slot));
	}
	// ===== EJECUTA LA ACCIÓN 'MOVE' PARA UN SLOT DE SPRITE =====
	private void ExecuteSpriteMove(SpriteSlot slot, float duration, string coordenadasDestinoCsv)
	{
		Image imageToMove = slot.currentActiveImage;
		if (imageToMove == null) imageToMove = slot.mainImage;

		if (imageToMove == null || imageToMove.sprite == null || !imageToMove.gameObject.activeSelf)
		{
			Debug.LogWarning("SpritesController (Move): Slot " + slot.slotName + " no tiene sprite visible para mover.");
			slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
			return;
		}

		if (string.IsNullOrEmpty(coordenadasDestinoCsv))
		{
			Debug.LogWarning("SpritesController (Move): No se especificaron coordenadas de destino para el 'move' del slot " + slot.slotName);
			slotAnimacionCompletada[allSlots.IndexOf(slot)] = true; // Considerar completado si no hay destino
			return;
		}

		Vector3 targetPosition = ParseSpriteCoordenadas(coordenadasDestinoCsv, imageToMove.rectTransform.anchoredPosition3D);

		// Si ya está en la posición destino, no hacer nada.
		if (Vector3.Distance(imageToMove.rectTransform.anchoredPosition3D, targetPosition) < 0.01f)
		{
			Debug.Log("SpritesController (Move): Slot " + slot.slotName + " ya está en la posición de destino.");
			slot.currentPosition = targetPosition; // Asegurar que currentPosition esté actualizada
			slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
			return;
		}

		// Guardar estado final para CompleteAllSpriteAnimations
		slot.targetPositionForCompletion = targetPosition;
		slot.targetAlphaForCompletion = imageToMove.color.a; // El alfa no cambia
		slot.targetScaleForCompletion = slot.currentScale;   // La escala no cambia
		slot.isMoving = true;
		slot.isFading = false;
		slot.isScaling = false;

		slot.activeAnimation = StartCoroutine(AnimateSpriteMove(imageToMove, targetPosition, duration, slot));
		Debug.Log("SpritesController: Acción 'move' iniciada para " + slot.slotName + " hacia " + targetPosition);
	}
	// ===== EJECUTA LA ACCIÓN 'SHAKE' PARA UN SLOT DE SPRITE =====
	private void ExecuteSpriteShake(SpriteSlot slot, float duration, bool isShakeX)
	{
		Image imageToShake = slot.currentActiveImage;
		if (imageToShake == null) imageToShake = slot.mainImage;

		if (imageToShake == null || imageToShake.sprite == null || !imageToShake.gameObject.activeSelf)
		{
			Debug.LogWarning("SpritesController (Shake): Slot " + slot.slotName + " no tiene sprite visible para aplicar shake.");
			slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
			return;
		}

		slot.targetPositionForCompletion = slot.currentPosition;
		slot.targetAlphaForCompletion = imageToShake.color.a; // Alfa no cambia
		slot.targetScaleForCompletion = imageToShake.rectTransform.localScale; // Escala no cambia
		slot.isMoving = true; // Shake es un tipo de movimiento
		slot.isFading = false;
		slot.isScaling = false;

		slot.activeAnimation = StartCoroutine(AnimateSpriteShake(imageToShake, duration, isShakeX, slot.currentPosition, slot));
		Debug.Log("SpritesController: Acción 'shake" + (isShakeX ? "X" : "Y") + "' iniciada para " + slot.slotName);
	}
	// ===== EJECUTA LA ACCIÓN 'SWITCH' PARA UN SLOT DE SPRITE =====
    private void ExecuteSpriteSwitch(SpriteSlot slot, string newSpritePathCsv, float duration, string coordenadasPosicionCsv, string tamanoCsv)
    {
        if (string.IsNullOrEmpty(newSpritePathCsv))
        {
            Debug.LogError(this.GetType().Name + " (Switch): No se especificó 'switchToPathFromCsv' para el slot " + slot.slotName);
            slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
            return;
        }

        Image imageToFadeOut = slot.currentActiveImage;
        Image imageToFadeIn = (imageToFadeOut == slot.mainImage) ? slot.switchImage : slot.mainImage;

        if (imageToFadeOut == null || imageToFadeIn == null)
        {
            Debug.LogError(this.GetType().Name + " (Switch): Las imágenes mainImage o switchImage no están asignadas para el slot " + slot.slotName);
            slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
            return;
        }

        string pathWithoutExtension = newSpritePathCsv;
        int dotIndex = newSpritePathCsv.LastIndexOf('.');
        if (dotIndex > 0 && dotIndex > newSpritePathCsv.LastIndexOf('/'))
        {
            pathWithoutExtension = newSpritePathCsv.Substring(0, dotIndex);
        }
        string resourcePath = "images/" + pathWithoutExtension;
        Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);

        if (loadedSprite == null)
        {
            Debug.LogError(this.GetType().Name + " (Switch): No se pudo cargar el nuevo Sprite desde: " + resourcePath + " para slot " + slot.slotName);
            slotAnimacionCompletada[allSlots.IndexOf(slot)] = true;
            return;
        }
        
        imageToFadeIn.sprite = loadedSprite;

        slot.pendingSwitchSpritePath = pathWithoutExtension;

        Vector2 nuevoTamano = ParseTamano(tamanoCsv, slot.currentSize);
        imageToFadeIn.rectTransform.sizeDelta = nuevoTamano;
		slot.currentSize = nuevoTamano;

        Vector3 fadeInPosition;
        if (!string.IsNullOrEmpty(coordenadasPosicionCsv)) {
            fadeInPosition = ParseSpriteCoordenadas(coordenadasPosicionCsv, slot.initialPosition);
        } else {
            fadeInPosition = slot.currentPosition;
        }
        imageToFadeIn.rectTransform.anchoredPosition3D = fadeInPosition;
        imageToFadeIn.rectTransform.localScale = slot.currentScale;

        slot.targetAlphaForCompletion = 1f;
        slot.targetPositionForCompletion = fadeInPosition;
        slot.targetScaleForCompletion = slot.currentScale;
        slot.isSwitching = true;
        slot.isFading = false;
        slot.isMoving = false;
        slot.isScaling = false;

        slot.activeAnimation = StartCoroutine(AnimateSpriteSwitch(slot, imageToFadeOut, imageToFadeIn, fadeInPosition, duration));
    }







	// ===== CORRUTINA PARA ANIMAR EL FADE (ALFA) DE UNA IMAGEN DE SPRITE =====
	private IEnumerator AnimateSpriteFade(Image targetImage, float targetAlpha, float duration, SpriteSlot slot)
	{
		int slotIndex = allSlots.IndexOf(slot);

		if (targetImage == null)
		{
			if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
			yield break;
		}

		// Asegurar que la imagen esté activa para ver el fade si el targetAlpha es > 0
		if (targetAlpha > 0 && !targetImage.gameObject.activeSelf)
		{
			SetImageAlpha(targetImage, 0f); // Para que el fade in comience desde transparente
			targetImage.gameObject.SetActive(true);
		}

		float startAlpha = targetImage.color.a;
		float elapsedTime = 0f;

		if (duration <= 0f)
		{
			SetImageAlpha(targetImage, targetAlpha);
			if (targetAlpha == 0f && targetImage.gameObject.activeSelf) targetImage.gameObject.SetActive(false);
			else if (targetAlpha > 0 && !targetImage.gameObject.activeSelf) targetImage.gameObject.SetActive(true);

			if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
			slot.activeAnimation = null;
			slot.isFading = false;
			yield break;
		}

		while (elapsedTime < duration)
		{
			elapsedTime += Time.deltaTime;
			float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
			SetImageAlpha(targetImage, newAlpha);
			yield return null;
		}

		SetImageAlpha(targetImage, targetAlpha);

		if (targetAlpha == 0f && targetImage.gameObject.activeSelf) targetImage.gameObject.SetActive(false);
		else if (targetAlpha > 0 && !targetImage.gameObject.activeSelf) targetImage.gameObject.SetActive(true);

		if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
		slot.activeAnimation = null;
		slot.isFading = false;
	}
	// ===== CORRUTINA PARA ANIMAR EL MOVIMIENTO DE UNA IMAGEN DE SPRITE =====
	private IEnumerator AnimateSpriteMove(Image targetImage, Vector3 destinationPosition, float duration, SpriteSlot slot)
	{
		int slotIndex = allSlots.IndexOf(slot);
		if (targetImage == null)
		{
			if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
			slot.activeAnimation = null; // Asegurar que se limpie
			slot.isMoving = false;
			yield break;
		}

		Vector3 startPosition = targetImage.rectTransform.anchoredPosition3D;
		float elapsedTime = 0f;

		if (duration <= 0f)
		{
			targetImage.rectTransform.anchoredPosition3D = destinationPosition;
			slot.currentPosition = destinationPosition; // Actualizar la posición actual del slot
			if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
			slot.activeAnimation = null;
			slot.isMoving = false;
			yield break;
		}

		while (elapsedTime < duration)
		{
			elapsedTime += Time.deltaTime;
			targetImage.rectTransform.anchoredPosition3D = Vector3.Lerp(startPosition, destinationPosition, elapsedTime / duration);
			yield return null;
		}

		targetImage.rectTransform.anchoredPosition3D = destinationPosition;
		slot.currentPosition = destinationPosition; // Actualizar la posición actual del slot al finalizar

		if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
		slot.activeAnimation = null;
		slot.isMoving = false;
	}
	// ===== CORRUTINA PARA ANIMAR EL SHAKE DE UNA IMAGEN DE SPRITE =====
	private IEnumerator AnimateSpriteShake(Image targetImage, float duration, bool shakeOnXAxis, Vector3 centerPosition, SpriteSlot slot)
	{
		int slotIndex = allSlots.IndexOf(slot);
		if (targetImage == null)
		{
			if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
			slot.activeAnimation = null;
			slot.isMoving = false;
			yield break;
		}

		if (duration <= 0f)
		{
			targetImage.rectTransform.anchoredPosition3D = centerPosition;
			if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
			slot.activeAnimation = null;
			slot.isMoving = false;
			yield break;
		}

		float elapsedTime = 0f;

		while (elapsedTime < duration)
		{
			elapsedTime += Time.deltaTime;
			float percentComplete = elapsedTime / duration;

			// El shake disminuye su intensidad hacia el final
			float currentShakeAmplitude = spriteShakeAmplitude * (1f - percentComplete);
			float offset = Mathf.Sin(elapsedTime * Mathf.PI * 2f * (spriteShakeVibrations / duration)) * currentShakeAmplitude;

			if (shakeOnXAxis)
			{
				targetImage.rectTransform.anchoredPosition3D = new Vector3(centerPosition.x + offset, centerPosition.y, centerPosition.z);
			}
			else // shakeOnYAxis
			{
				targetImage.rectTransform.anchoredPosition3D = new Vector3(centerPosition.x, centerPosition.y + offset, centerPosition.z);
			}
			yield return null;
		}

		targetImage.rectTransform.anchoredPosition3D = centerPosition; // Volver a la posición central

		if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
		slot.activeAnimation = null;
		slot.isMoving = false;
	}
	// ===== CORRUTINA PARA ANIMAR EL INTERCAMBIO (SWITCH) DE SPRITES =====
	private IEnumerator AnimateSpriteSwitch(SpriteSlot slot, Image imgOut, Image imgIn, Vector3 fadeInPosition, float totalDuration)
	{
		int slotIndex = allSlots.IndexOf(slot);
		if (imgOut == null || imgIn == null)
		{
			if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
			if (slot != null)
			{
				slot.activeAnimation = null;
				slot.isSwitching = false;
			}
			yield break;
		}

		float fadeInActualDuration = totalDuration * Mathf.Clamp01(switchFadeInSpeedFactor);
		fadeInActualDuration = Mathf.Max(0.01f, fadeInActualDuration);
		float fadeOutActualDuration = Mathf.Max(0.01f, totalDuration);

		// Preparar imagen de entrada
		SetImageAlpha(imgIn, 0f);
		imgIn.gameObject.SetActive(true);
		imgIn.rectTransform.anchoredPosition3D = fadeInPosition;
		imgIn.rectTransform.localScale = slot.currentScale;

		float startAlphaOut = imgOut.color.a;
		float elapsedTime = 0f;

		// Si la duración es 0, hacer el switch instantáneo
		if (totalDuration <= 0f)
		{
			SetImageAlpha(imgOut, 0f);
			if (imgOut.gameObject.activeSelf) imgOut.gameObject.SetActive(false);
			SetImageAlpha(imgIn, 1f);
			if (!imgIn.gameObject.activeSelf) imgIn.gameObject.SetActive(true);

			slot.currentActiveImage = imgIn;
			slot.currentSpritePath = slot.pendingSwitchSpritePath;
			slot.currentPosition = fadeInPosition;

			// Intercambiar SiblingIndex
			if (slot.mainImage != null && slot.switchImage != null)
			{
				int mainIndex = slot.mainImage.transform.GetSiblingIndex();
				int switchIndex = slot.switchImage.transform.GetSiblingIndex();
				slot.mainImage.transform.SetSiblingIndex(switchIndex);
				slot.switchImage.transform.SetSiblingIndex(mainIndex);
			}

			if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
			slot.activeAnimation = null;
			slot.isSwitching = false;
			yield break;
		}

		// El bucle principal se ejecuta durante la duración más larga (la del fadeOut)
		while (elapsedTime < fadeOutActualDuration)
		{
			elapsedTime += Time.deltaTime;

			// Fade Out para imgOut (usa fadeOutActualDuration, que es totalDuration)
			float progressOut = Mathf.Clamp01(elapsedTime / fadeOutActualDuration);
			SetImageAlpha(imgOut, Mathf.Lerp(startAlphaOut, 0f, progressOut));

			// Fade In para imgIn (usa fadeInActualDuration)
			if (elapsedTime <= fadeInActualDuration)
			{
				float progressIn = Mathf.Clamp01(elapsedTime / fadeInActualDuration);
				SetImageAlpha(imgIn, Mathf.Lerp(0f, 1f, progressIn));
			}
			else
			{
				SetImageAlpha(imgIn, 1f);
			}
			yield return null;
		}

		// Asegurar estados finales
		SetImageAlpha(imgOut, 0f);
		if (imgOut.gameObject.activeSelf) imgOut.gameObject.SetActive(false);
		SetImageAlpha(imgIn, 1f);
		if (!imgIn.gameObject.activeSelf) imgIn.gameObject.SetActive(true);

		// Actualizar el estado del slot
		slot.currentActiveImage = imgIn;
		slot.currentSpritePath = slot.pendingSwitchSpritePath;
		slot.currentPosition = fadeInPosition;

		// Intercambiar SiblingIndex al final natural
		if (slot.mainImage != null && slot.switchImage != null)
		{
			int mainIndex = slot.mainImage.transform.GetSiblingIndex();
			int switchIndex = slot.switchImage.transform.GetSiblingIndex();
			slot.mainImage.transform.SetSiblingIndex(switchIndex);
			slot.switchImage.transform.SetSiblingIndex(mainIndex);
			// Debug.Log(slot.slotName + ": Sibling indices intercambiados. " + slot.mainImage.name + " ahora en " + slot.mainImage.transform.GetSiblingIndex() + ", " + slot.switchImage.name + " ahora en " + slot.switchImage.transform.GetSiblingIndex());
		}

		if (slotIndex != -1) slotAnimacionCompletada[slotIndex] = true;
		slot.activeAnimation = null;
		slot.isSwitching = false;
	}

	// ===== PARSEA EL STRING DE COORDENADAS (SOLO X e Y) =====
	private Vector3 ParseSpriteCoordenadas(string coordenadasCsv, Vector3 defaultValue)
	{
		// Si no nos dan coordenadas, devolvemos el valor por defecto.
		if (string.IsNullOrEmpty(coordenadasCsv))
		{
			return defaultValue;
		}

		string processedCoords = coordenadasCsv.Replace('_', ' ');
		NumberFormatInfo nfi = new NumberFormatInfo { NumberDecimalSeparator = "," };
		string[] parts = processedCoords.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

		try
		{
			// Mantenemos los valores por defecto por si el CSV viene incompleto.
			float x = defaultValue.x;
			float y = defaultValue.y;

			// Leemos solo los dos primeros valores para X e Y.
			if (parts.Length >= 1) x = float.Parse(parts[0].Trim(), nfi);
			if (parts.Length >= 2) y = float.Parse(parts[1].Trim(), nfi);

			// Creamos el vector de posición. La Z se queda con el valor por defecto (usualmente 0).
			return new Vector3(x, y, defaultValue.z);
		}
		catch (System.Exception e)
		{
			Debug.LogWarning(this.GetType().Name + ": Error al parsear coordenadas. Original: '" + coordenadasCsv + "', Procesado: '" + processedCoords + "'. Error: " + e.Message + ". Usando valor por defecto: " + defaultValue);
			return defaultValue;
		}
	}

	// Pequeña función de ayuda para no duplicar el código de carga de sprites
	private bool LoadSpriteForAnimation(SpriteSlot slot, Image imageToUse, string pathFromCsv)
	{
		if (!string.IsNullOrEmpty(pathFromCsv))
		{
			string resourcePath = "images/" + pathFromCsv.Split('.')[0];
			Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);
			if (loadedSprite != null)
			{
				imageToUse.sprite = loadedSprite;
				slot.currentSpritePath = pathFromCsv.Split('.')[0];
			}
			else
			{
				Debug.LogError("No se pudo cargar Sprite desde: " + resourcePath);
				return false;
			}
		}
		else if (imageToUse.sprite == null)
		{
			Debug.LogWarning("No se especificó path y no hay sprite actual. No se puede animar.");
			return false;
		}
		return true;
	}
	// Helper para cambiar el alfa de una imagen
	private static void SetImageAlpha(Image image, float alpha)
	{
		if (image == null) return;
		Color currentColor = image.color;
		currentColor.a = Mathf.Clamp01(alpha);
		image.color = currentColor;
	}

	public void CompleteAllSpriteAnimations() // O CompleteSpriteAnimation(int slotIndex)
	{
		for (int i = 0; i < allSlots.Count; i++)
		{
			SpriteSlot slot = allSlots[i];
			if (slot.activeAnimation != null)
			{
				StopCoroutine(slot.activeAnimation);

				// Aplicar estado final instantáneamente basado en qué tipo de animación era
				if (slot.isFading)
				{
					if (slot.currentActiveImage != null)
					{ // Asegurarse de que currentActiveImage no sea nulo
						SetImageAlpha(slot.currentActiveImage, slot.targetAlphaForCompletion);
						if (slot.targetAlphaForCompletion == 0f && slot.currentActiveImage.gameObject.activeSelf)
							slot.currentActiveImage.gameObject.SetActive(false);
						else if (slot.targetAlphaForCompletion > 0f && !slot.currentActiveImage.gameObject.activeSelf)
							slot.currentActiveImage.gameObject.SetActive(true);
					}
				}
				if (slot.isMoving)
				{
					if (slot.currentActiveImage != null)
					{ // Asegurarse de que currentActiveImage no sea nulo
						slot.currentActiveImage.rectTransform.anchoredPosition3D = slot.targetPositionForCompletion;
						slot.currentPosition = slot.targetPositionForCompletion; // Actualizar el estado guardado del slot
					}
				}
				// if (slot.isScaling) 
				// { 
				//     if(slot.currentActiveImage != null) {
				//         slot.currentActiveImage.rectTransform.localScale = slot.targetScaleForCompletion;
				//         slot.currentScale = slot.targetScaleForCompletion; // Actualizar estado
				//     }
				// }
				if (slot.isSwitching)
				{
					Image imgToBecomeActive = (slot.currentActiveImage == slot.mainImage) ? slot.switchImage : slot.mainImage;
					Image imgToBecomeInactive = slot.currentActiveImage;

					if (imgToBecomeInactive != null)
					{
						SetImageAlpha(imgToBecomeInactive, 0f);
						imgToBecomeInactive.gameObject.SetActive(false);
					}
					if (imgToBecomeActive != null)
					{
						// El sprite ya debería estar cargado en imgToBecomeActive desde ExecuteSpriteSwitch
						SetImageAlpha(imgToBecomeActive, 1f);
						imgToBecomeActive.gameObject.SetActive(true);
						imgToBecomeActive.rectTransform.anchoredPosition3D = slot.targetPositionForCompletion;
						imgToBecomeActive.rectTransform.localScale = slot.targetScaleForCompletion;

						slot.currentActiveImage = imgToBecomeActive;
						slot.currentPosition = slot.targetPositionForCompletion;
						slot.currentScale = slot.targetScaleForCompletion;
						slot.currentSpritePath = slot.pendingSwitchSpritePath;

						// Intercambiar SiblingIndex al completar forzadamente
						if (slot.mainImage != null && slot.switchImage != null)
						{
							int mainIndex = slot.mainImage.transform.GetSiblingIndex();
							int switchIndex = slot.switchImage.transform.GetSiblingIndex();
							slot.mainImage.transform.SetSiblingIndex(switchIndex);
							slot.switchImage.transform.SetSiblingIndex(mainIndex);
							// Debug.Log(slot.slotName + " (Completed): Sibling indices intercambiados.");
						}

					}
				}

				slot.activeAnimation = null;
				slot.isFading = false;
				slot.isMoving = false;
				slot.isScaling = false;
			}
			slotAnimacionCompletada[i] = true;
			slot.isSwitching = false;
		}
		Debug.Log("SpritesController: Todas las animaciones de sprite completadas por solicitud.");
	}

	// ===== MÉTODOS PARA EL SISTEMA DE GUARDADO Y CARGA =====
	public List<SpriteSaveData> GetCurrentStates()
	{
		List<SpriteSaveData> allStates = new List<SpriteSaveData>();

		foreach (var slot in allSlots)
		{
			SpriteSaveData state = new SpriteSaveData();
			// Comprobamos la imagen activa del slot
			if (slot.currentActiveImage != null && slot.currentActiveImage.gameObject.activeInHierarchy && slot.currentActiveImage.sprite != null)
			{
				state.isActive = true;
				state.spritePath = slot.currentSpritePath;
				state.alpha = slot.currentActiveImage.color.a;
				state.siblingIndex = slot.currentActiveImage.transform.GetSiblingIndex();
				state.posX = slot.currentPosition.x;
				state.posY = slot.currentPosition.y;
				state.posZ = slot.currentPosition.z;
				state.sizeX = slot.currentActiveImage.rectTransform.sizeDelta.x;
				state.sizeY = slot.currentActiveImage.rectTransform.sizeDelta.y;

				// Guardamos cuál de las dos imágenes está activa
				state.isSwitchImageActive = (slot.currentActiveImage == slot.switchImage);
			}
			else
			{
				state.isActive = false;
				state.isSwitchImageActive = false; // Por defecto, si está inactivo, asumimos que no es la de switch
			}
			allStates.Add(state);
		}
		return allStates;
	}

	public IEnumerator RestoreStates(List<SpriteSaveData> savedStates)
	{
		CompleteAllSpriteAnimations();

		for (int i = 0; i < allSlots.Count; i++)
		{
			if (i >= savedStates.Count) break;

			SpriteSaveData state = savedStates[i];
			SpriteSlot slot = allSlots[i];

			Image imageToRestore = state.isSwitchImageActive ? slot.switchImage : slot.mainImage;
			Image otherImage = state.isSwitchImageActive ? slot.mainImage : slot.switchImage;

			if (state.isActive)
			{
				// El slot está activo, configuramos la imagen principal.
				slot.currentActiveImage = imageToRestore;
				
				string resourcePath = "images/" + state.spritePath;
				Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);

				if (loadedSprite != null && imageToRestore != null)
				{
					imageToRestore.gameObject.SetActive(true);
					imageToRestore.sprite = loadedSprite;

					Vector3 newPos = new Vector3(state.posX, state.posY, state.posZ);
					imageToRestore.rectTransform.anchoredPosition3D = newPos;
					imageToRestore.transform.SetSiblingIndex(state.siblingIndex);
					SetImageAlpha(imageToRestore, state.alpha);
					imageToRestore.rectTransform.sizeDelta = new Vector2(state.sizeX, state.sizeY);
					slot.currentSize = new Vector2(state.sizeX, state.sizeY);
					imageToRestore.rectTransform.localScale = Vector3.one;

					slot.currentSpritePath = state.spritePath;
					slot.currentPosition = newPos;
					slot.currentScale = Vector3.one;
				}

				// Limpiamos la otra imagen del par para evitar residuos.
				if (otherImage != null)
				{
					otherImage.gameObject.SetActive(false);
					otherImage.sprite = null;
					SetImageAlpha(otherImage, 0f);
				}
			}
			else
			{
				// El slot completo está inactivo, limpiamos AMBAS imágenes.
				slot.currentActiveImage = slot.mainImage; // Reset al default
				slot.currentSpritePath = null;
				slot.currentSize = Vector2.zero;

				if (imageToRestore != null)
				{
					imageToRestore.gameObject.SetActive(false);
					imageToRestore.sprite = null; 
					SetImageAlpha(imageToRestore, 0f);
				}
				if (otherImage != null)
				{
					otherImage.gameObject.SetActive(false);
					otherImage.sprite = null;
					SetImageAlpha(otherImage, 0f);
				}
			}
		}

		yield return null;

		Debug.Log(this.GetType().Name + ": Estado restaurado y refrescado.");
	}

	// ===== Corrutina para forzar el refresco visual de un componente Image =====
	private IEnumerator ForceRedrawCoroutine(Image imageToRefresh)
	{
		// Solo proceder si la imagen es válida y se supone que debe estar activa
		if (imageToRefresh == null || !imageToRefresh.gameObject.activeInHierarchy)
		{
			yield break;
		}

		// Desactivamos el GameObject
		imageToRefresh.gameObject.SetActive(false);

		// ESPERAMOS UN FOTOGRAMA. Esta es la parte más importante del truco.
		yield return null;

		// Lo reactivamos en el siguiente fotograma.
		imageToRefresh.gameObject.SetActive(true);
	}
	
	private Vector2 ParseTamano(string tamanoCsv, Vector2 defaultValue)
	{
		// Si el campo del CSV está vacío...
		if (string.IsNullOrEmpty(tamanoCsv))
		{
			// Usamos una pequeña tolerancia (0.1f) en lugar de una comparación directa con cero.
			if (defaultValue.x < 0.1f && defaultValue.y < 0.1f)
			{
				// Si no hay un tamaño previo válido, usamos el valor por defecto de FÁBRICA para este controlador.
				return new Vector2(240, 240); 
			}
			else
			{
				// Si hay un tamaño previo válido, lo mantenemos (persistencia).
				return defaultValue;
			}
		}

		// Separamos el string por la 'x' (ej. "400x240")
		string[] parts = tamanoCsv.ToLowerInvariant().Split('x');
		if (parts.Length == 2)
		{
			try
			{
				// Convertimos las partes a números
				NumberFormatInfo nfi = new NumberFormatInfo { NumberDecimalSeparator = "," };
				float ancho = float.Parse(parts[0].Trim(), nfi);
				float alto = float.Parse(parts[1].Trim(), nfi);
				return new Vector2(ancho, alto);
			}
			catch (System.Exception e)
			{
				Debug.LogWarning(this.GetType().Name + ": Error al parsear tamaño '" + tamanoCsv + "': " + e.Message);
				// Si el parseo falla, aplicamos la misma lógica que si estuviera vacío.
				if (defaultValue.x < 0.1f && defaultValue.y < 0.1f) { return new Vector2(240, 240); }
				else { return defaultValue; }
			}
		}

		// Si el formato no es correcto, usamos el valor por defecto.
		if (defaultValue.x < 0.1f && defaultValue.y < 0.1f) { return new Vector2(240, 240); }
		return defaultValue;
	}
}