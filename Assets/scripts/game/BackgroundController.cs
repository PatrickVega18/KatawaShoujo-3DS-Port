using UnityEngine;
using UnityEngine.UI; 
using System.Collections;
using System.Globalization;

public class BackgroundController : MonoBehaviour
{
    private bool rolesInvertidos = false;
    [Header("Referencias UI de Fondos")]
    public Image uiFondoPrincipal;
    public Image uiFondoSecundario;
    private Image activePrincipal;
    private Image activeSecundario;

    [Header("Configuración de Animaciones (Valores por defecto)")]
    public float shakeAmplitude = 10f;
    public int shakeVibrations = 5;

    [Header("Configuración de Fondos")]
    public Vector2 tamanoEstandarFondo = new Vector2(400, 240);

    private string currentFondoPrincipalPath = null;
    private Vector2 currentFondoPrincipalSizeDelta;
    private string currentFondoSecundarioPath = null;

    private Coroutine activeFondoPrincipalAnimation = null;
    private Vector3 originalFondoPrincipalPosition;

    // para rastrear el tipo de animación actual del FondoPrincipal
    private string currentFPAnimationType = null;

    // Flags de estado para el fondo principal
    public bool FondoAnimacionCompletada { get; private set; }
    public bool IsFondoAnimando { get { return activeFondoPrincipalAnimation != null; } }

    // Variables para almacenar el estado final deseado por una animación interrumpida
    private Vector3 targetPositionForCompletion;
    private Vector2 targetSizeDeltaForCompletion;
    private float targetAlphaForCompletion;
    private bool isAnimatingPosition; // Para saber si la animación actual es de posición o de alfa
    private bool isAnimatingAlpha;
    private bool isAnimatingSize;

    // Getter para GameController
    public string GetCurrentFPAnimationType() { return currentFPAnimationType; }

    void Start()
    {
        activePrincipal = uiFondoPrincipal;
        activeSecundario = uiFondoSecundario;

        if (uiFondoPrincipal == null)
        {
            Debug.LogError("BackgroundController: 'uiFondoPrincipal' no está asignado en el Inspector.", gameObject);
            this.enabled = false;
            return;
        }
        if (uiFondoSecundario == null)
        {
            Debug.LogError("BackgroundController: 'uiFondoSecundario' no está asignado en el Inspector.", gameObject);
            this.enabled = false;
            return;
        }

        originalFondoPrincipalPosition = uiFondoPrincipal.rectTransform.anchoredPosition3D;
        currentFondoPrincipalSizeDelta = tamanoEstandarFondo;
        uiFondoPrincipal.rectTransform.sizeDelta = currentFondoPrincipalSizeDelta;
        SetImageAlpha(uiFondoPrincipal, 0f);
        uiFondoPrincipal.gameObject.SetActive(false);

        SetImageAlpha(uiFondoSecundario, 1f);
        uiFondoSecundario.sprite = null;
        uiFondoSecundario.rectTransform.sizeDelta = tamanoEstandarFondo;
        uiFondoSecundario.gameObject.SetActive(true);
        FondoAnimacionCompletada = true;
    }

    //==== PROCESA EL COMANDO PARA EL FONDO SECUNDARIO (LLAMADO DESDE GAMECONTROLLER) ====
    public void ProcessFondoSecundarioCommand(string pathFromCsv, string tamanoCsv, string coordenadasCsv)
    {
        if (string.IsNullOrEmpty(pathFromCsv)) return;
        string pathWithoutExtension = pathFromCsv;
        int dotIndex = pathFromCsv.LastIndexOf('.');
        if (dotIndex > 0 && dotIndex > pathFromCsv.LastIndexOf('/'))
        {
            pathWithoutExtension = pathFromCsv.Substring(0, dotIndex);
        }
        if (pathWithoutExtension == currentFondoSecundarioPath && activeSecundario.sprite != null) return;

        Sprite newSprite = Resources.Load<Sprite>("images/" + pathWithoutExtension);
        if (newSprite != null)
        {
            activeSecundario.sprite = newSprite;
            currentFondoSecundarioPath = pathWithoutExtension;
            activeSecundario.gameObject.SetActive(true);
            SetImageAlpha(activeSecundario, 1f);
        }
        else
        {
            Debug.LogError("BC: No se pudo cargar Sprite para FondoSecundario: images/" + pathWithoutExtension);
            activeSecundario.sprite = null; currentFondoSecundarioPath = null;
            activeSecundario.gameObject.SetActive(false); return;
        }
        activeSecundario.rectTransform.sizeDelta = ParseTamano(tamanoCsv, tamanoEstandarFondo);
        activeSecundario.rectTransform.anchoredPosition3D = ParseCoordenadas(coordenadasCsv, Vector3.zero);
    }

    //==== PROCESA TODOS LOS COMANDOS PARA EL FONDO PRINCIPAL (LLAMADO DESDE GAMECONTROLLER) ====
    public void ProcessFondoPrincipalCommand(string pathFromCsv, string accionConParametros, float tiempo, string coordenadasPosicionCsv, string tamanoPrincipalCsv, string switch_newImagePath, string switch_newImageSize, string switch_newImageCoords)
    {
        if (string.IsNullOrEmpty(accionConParametros) && string.IsNullOrEmpty(pathFromCsv) && string.IsNullOrEmpty(tamanoPrincipalCsv) && string.IsNullOrEmpty(coordenadasPosicionCsv))
        {
            FondoAnimacionCompletada = true;
            if (activeFondoPrincipalAnimation != null) { StopCoroutine(activeFondoPrincipalAnimation); activeFondoPrincipalAnimation = null; }
            currentFPAnimationType = null;
            isAnimatingAlpha = false; isAnimatingPosition = false; isAnimatingSize = false;
            return;
        }

        if (activeFondoPrincipalAnimation != null)
        {
            StopCoroutine(activeFondoPrincipalAnimation);
        }
        FondoAnimacionCompletada = false;
        isAnimatingPosition = false;
        isAnimatingAlpha = false;
        isAnimatingSize = false;
        currentFPAnimationType = null; // Limpiar por defecto

        string[] accionParts = accionConParametros.Split('-');
        string accionBase = accionParts[0].ToLowerInvariant();
        currentFPAnimationType = accionBase;

        // --- MANEJO DE TAMAÑO Y CARGA DE SPRITE ANTES DE LA ACCIÓN ---
        bool spriteHaCambiado = false;
        if (!string.IsNullOrEmpty(pathFromCsv))
        {
            string pathWithoutExtension = pathFromCsv;
            int dotIndex = pathFromCsv.LastIndexOf('.');
            if (dotIndex > 0 && dotIndex > pathFromCsv.LastIndexOf('/')) pathWithoutExtension = pathFromCsv.Substring(0, dotIndex);
            if (pathWithoutExtension != currentFondoPrincipalPath || activePrincipal.sprite == null)
            {
                Sprite loadedSprite = Resources.Load<Sprite>("images/" + pathWithoutExtension);
                if (loadedSprite != null)
                {
                    activePrincipal.sprite = loadedSprite; currentFondoPrincipalPath = pathWithoutExtension; spriteHaCambiado = true;
                }
                else
                {
                    Debug.LogError("BC: No se pudo cargar Sprite para FP: images/" + pathWithoutExtension + " para acción " + accionBase);
                    if (accionBase == "punch" || accionBase == "fadein" || accionBase.StartsWith("zoomin")) { FondoAnimacionCompletada = true; currentFPAnimationType = null; return; }
                }
            }
        }

        if (!string.IsNullOrEmpty(tamanoPrincipalCsv))
        {
            currentFondoPrincipalSizeDelta = ParseTamano(tamanoPrincipalCsv, tamanoEstandarFondo);
            activePrincipal.rectTransform.sizeDelta = currentFondoPrincipalSizeDelta;
        }
        else if (spriteHaCambiado)
        {
            currentFondoPrincipalSizeDelta = tamanoEstandarFondo;
            activePrincipal.rectTransform.sizeDelta = currentFondoPrincipalSizeDelta;
        }

        if (accionBase == "punch" || accionBase == "fadein")
        {
            if (!string.IsNullOrEmpty(coordenadasPosicionCsv))
            {
                Vector3 newPosition = ParseCoordenadas(coordenadasPosicionCsv, originalFondoPrincipalPosition);
                activePrincipal.rectTransform.anchoredPosition3D = newPosition;
                originalFondoPrincipalPosition = newPosition;
            }
            else
            {
                activePrincipal.rectTransform.anchoredPosition3D = originalFondoPrincipalPosition;
            }
        }

        switch (accionBase)
        {
            case "punch":
                ExecutePunch(pathFromCsv, coordenadasPosicionCsv);
                // Punch es instantáneo, así que se completa de inmediato
                FondoAnimacionCompletada = true;
                activeFondoPrincipalAnimation = null; // Asegurar que no quede corrutina activa
                break;

            case "fadein":
                ExecuteFadeIn(pathFromCsv, tiempo, coordenadasPosicionCsv);
                break;

            case "fadeout":
                ExecuteFadeOut(tiempo);
                break;

            case "shakex":
                ExecuteShake(tiempo, true); // true para shakeX
                break;

            case "shakey":
                ExecuteShake(tiempo, false); // false para shakeY
                break;

            case "slide":
                ExecuteSlide(tiempo, coordenadasPosicionCsv);
                break;

            case "zoomin":
                ParseAndExecuteZoomIn(accionParts, pathFromCsv, tiempo, coordenadasPosicionCsv);
                break;

            case "zoomout":
                ParseAndExecuteZoomOut(accionParts, pathFromCsv, tiempo, coordenadasPosicionCsv);
                break;

            case "switch":
                activeFondoPrincipalAnimation = StartCoroutine(ExecuteSwitchBackground(
                    switch_newImagePath, 
                    tiempo, 
                    switch_newImageSize, 
                    switch_newImageCoords
                ));
                break;

            default:
                Debug.LogWarning("BackgroundController: Acción desconocida para FondoPrincipal: '" + accionConParametros + "'");
                FondoAnimacionCompletada = true;
                activeFondoPrincipalAnimation = null;
                break;
        }
        if (activeFondoPrincipalAnimation == null && !FondoAnimacionCompletada &&
            (accionBase == "zoomin" || accionBase == "zoomout" || accionBase == "slide" || accionBase == "fadein" || accionBase == "fadeout" || accionBase == "shakex" || accionBase == "shakey"))
        {
            FondoAnimacionCompletada = true;
            currentFPAnimationType = null; // Limpiar si la acción falló en iniciar corrutina
        }
    }
    //==== COMPLETA INSTANTÁNEAMENTE LA ANIMACIÓN ACTUAL DEL FONDO PRINCIPAL (LLAMADO DESDE GAMECONTROLLER) ====
    public void CompleteFondoAnimacion()
    {
        if (currentFPAnimationType == "zoomin" ||
            currentFPAnimationType == "zoomout" ||
            currentFPAnimationType == "slide")
        {
            Debug.Log("BackgroundController: Solicitud de completar animación, pero es zoom/slide ('" + currentFPAnimationType + "'). No se fuerza, continuará.");
            return;
        }

        if (currentFPAnimationType == "switch")
        {
            if (activeFondoPrincipalAnimation != null)
            {
                StopCoroutine(activeFondoPrincipalAnimation);
            }

            SetImageAlpha(activePrincipal, 0f);
            activePrincipal.gameObject.SetActive(false);

            Image tempImage = activePrincipal;
            activePrincipal = activeSecundario;
            activeSecundario = tempImage;
            rolesInvertidos = !rolesInvertidos;

            if (uiFondoPrincipal != null && uiFondoSecundario != null)
            {
                int principalIndex = uiFondoPrincipal.transform.GetSiblingIndex();
                int secundarioIndex = uiFondoSecundario.transform.GetSiblingIndex();
                uiFondoPrincipal.transform.SetSiblingIndex(secundarioIndex);
                uiFondoSecundario.transform.SetSiblingIndex(principalIndex);
            }

            // Asumimos que el path del nuevo principal ya se actualizó en activeSecundario
            // ASUMIMOS DIJE >:v
            currentFondoPrincipalPath = currentFondoSecundarioPath;
            currentFondoSecundarioPath = null;
            activeSecundario.sprite = null;
            originalFondoPrincipalPosition = activePrincipal.rectTransform.anchoredPosition3D;
            currentFondoPrincipalSizeDelta = activePrincipal.rectTransform.sizeDelta;

        }
        else if (activeFondoPrincipalAnimation != null)
        {
            StopCoroutine(activeFondoPrincipalAnimation);
            activeFondoPrincipalAnimation = null;
            if (isAnimatingAlpha)
            {
                SetImageAlpha(activePrincipal, targetAlphaForCompletion);
                if (targetAlphaForCompletion == 0f) activePrincipal.gameObject.SetActive(false);
                else activePrincipal.gameObject.SetActive(true);
            }
            if (isAnimatingPosition)
            {
                activePrincipal.rectTransform.anchoredPosition3D = targetPositionForCompletion;
                if (Vector3.Distance(originalFondoPrincipalPosition, targetPositionForCompletion) > 0.01f)
                {
                    originalFondoPrincipalPosition = targetPositionForCompletion;
                }
            }
            if (isAnimatingSize)
            {
                activePrincipal.rectTransform.sizeDelta = targetSizeDeltaForCompletion;
            }
        }
        FondoAnimacionCompletada = true;
        isAnimatingAlpha = false;
        isAnimatingPosition = false;
        isAnimatingSize = false;
        currentFPAnimationType = null;
        activeFondoPrincipalAnimation = null;
    }

    public void StopCurrentVisualAnimationOnly()
    {
        if (activeFondoPrincipalAnimation != null)
        {
            StopCoroutine(activeFondoPrincipalAnimation);
            activeFondoPrincipalAnimation = null;
        }
        isAnimatingAlpha = false; isAnimatingPosition = false; isAnimatingSize = false;
        FondoAnimacionCompletada = true;
        currentFPAnimationType = null; 
    }

    // ===== EJECUTA LA ACCIÓN 'PUNCH' (CAMBIO INSTANTÁNEO DE IMAGEN Y POSICIÓN) =====
    private void ExecutePunch(string path, string coords)
    {
        if (activePrincipal.sprite != null)
        {
            SetImageAlpha(activePrincipal, 1f); activePrincipal.gameObject.SetActive(true);
        }
        else
        {
            SetImageAlpha(activePrincipal, 0f); activePrincipal.gameObject.SetActive(false);
        }
    }

    // ===== EJECUTA LA ACCIÓN 'FADEIN' =====
    private void ExecuteFadeIn(string path, float dur, string coords)
    {
        if (activePrincipal.sprite == null)
        {
            activePrincipal.gameObject.SetActive(false);
            FondoAnimacionCompletada = true;
            currentFPAnimationType = null;
            return;
        }
        activePrincipal.gameObject.SetActive(true); targetAlphaForCompletion = 1f;
        targetPositionForCompletion = activePrincipal.rectTransform.anchoredPosition3D;
        targetSizeDeltaForCompletion = activePrincipal.rectTransform.sizeDelta;

        isAnimatingAlpha = true;
        isAnimatingPosition = false;
        isAnimatingSize = false;

        activeFondoPrincipalAnimation = StartCoroutine(AnimateFade(activePrincipal, 1f, dur));
    }
    // ===== EJECUTA LA ACCIÓN 'FADEOUT' =====
    private void ExecuteFadeOut(float dur)
    {
        if (activePrincipal.sprite == null && activePrincipal.color.a == 0f)
        {
            activePrincipal.gameObject.SetActive(false);
            FondoAnimacionCompletada = true;
            currentFPAnimationType = null;
            return;
        }
        activePrincipal.gameObject.SetActive(true);
        targetAlphaForCompletion = 0f;
        targetPositionForCompletion = activePrincipal.rectTransform.anchoredPosition3D;

        isAnimatingAlpha = true;
        isAnimatingPosition = false;
        isAnimatingSize = false;

        activeFondoPrincipalAnimation = StartCoroutine(AnimateFade(activePrincipal, 0f, dur));
    }
    // ===== EJECUTA LA ACCIÓN 'SHAKE' (HORIZONTAL O VERTICAL) =====
    private void ExecuteShake(float duration, bool isShakeX)
    {
        if (activePrincipal.sprite == null || !activePrincipal.gameObject.activeSelf)
        {
            Debug.LogWarning("BackgroundController (Shake): No hay FondoPrincipal visible para aplicar shake.");
            return;
        }

        targetPositionForCompletion = originalFondoPrincipalPosition; // El shake debe terminar en la posición original
        targetAlphaForCompletion = activePrincipal.color.a; // El alfa no cambia en shake
        isAnimatingPosition = true;
        isAnimatingAlpha = false;

        activeFondoPrincipalAnimation = StartCoroutine(AnimateShake(activePrincipal, duration, isShakeX, originalFondoPrincipalPosition));
        Debug.Log("BackgroundController: Acción 'shake" + (isShakeX ? "X" : "Y") + "' iniciada para FondoPrincipal.");
    }
    // ===== EJECUTA LA ACCIÓN 'SLIDE' =====
    private void ExecuteSlide(float dur, string coordsDest)
    {
        if (activePrincipal.sprite == null || !activePrincipal.gameObject.activeSelf)
        {
            FondoAnimacionCompletada = true;
            currentFPAnimationType = null;
            return;
        }
        if (string.IsNullOrEmpty(coordsDest))
        {
            FondoAnimacionCompletada = true;
            currentFPAnimationType = null;
            return;
        }
        Vector3 destPos = ParseCoordenadas(coordsDest, activePrincipal.rectTransform.anchoredPosition3D);
        if (Vector3.Distance(activePrincipal.rectTransform.anchoredPosition3D, destPos) < 0.01f)
        {
            originalFondoPrincipalPosition = destPos;
            FondoAnimacionCompletada = true;
            currentFPAnimationType = null;
            return;
        }
        targetPositionForCompletion = destPos;
        targetAlphaForCompletion = activePrincipal.color.a;

        isAnimatingPosition = true;
        isAnimatingAlpha = false;
        isAnimatingSize = false;

        activeFondoPrincipalAnimation = StartCoroutine(AnimateSlide(activePrincipal, destPos, dur));
    }
    // ===== MÉTODOS DE PARSEO Y EJECUCIÓN PARA ZOOM =====
    private void ParseAndExecuteZoomIn(string[] accionParts, string pathFromCsv, float duration, string coordenadasPosicionCsv)
    {
        if (accionParts.Length == 3)
        {
            NumberFormatInfo nfi = new NumberFormatInfo { NumberDecimalSeparator = "," };
            float finalWidth, finalHeight;

            // CORRECCIÓN: Usar NumberStyles.Any en lugar de NSR.Any
            // ¿Qué es eso? buena pregunta xd
            if (float.TryParse(accionParts[1], NumberStyles.Any, nfi, out finalWidth) &&
                float.TryParse(accionParts[2], NumberStyles.Any, nfi, out finalHeight))
            {
                ExecuteZoom(pathFromCsv, duration, coordenadasPosicionCsv, activePrincipal.rectTransform.sizeDelta, new Vector2(finalWidth, finalHeight), true);
            }
            else { Debug.LogError("BC (ZoomIn): Error parseo dims: " + string.Join("-", accionParts)); FondoAnimacionCompletada = true; currentFPAnimationType = null; }
        }
        else { Debug.LogError("BC (ZoomIn): Formato incorrecto: " + string.Join("-", accionParts)); FondoAnimacionCompletada = true; currentFPAnimationType = null; }
    }
    private void ParseAndExecuteZoomOut(string[] accionParts, string pathFromCsv, float duration, string coordenadasPosicionCsv)
    {
        if (accionParts.Length == 5)
        {
            NumberFormatInfo nfi = new NumberFormatInfo { NumberDecimalSeparator = "," };
            float initialWidth, initialHeight, finalWidth, finalHeight;
            if (float.TryParse(accionParts[1], NumberStyles.Any, nfi, out initialWidth) &&
                float.TryParse(accionParts[2], NumberStyles.Any, nfi, out initialHeight) &&
                float.TryParse(accionParts[3], NumberStyles.Any, nfi, out finalWidth) &&
                float.TryParse(accionParts[4], NumberStyles.Any, nfi, out finalHeight))
            {
                ExecuteZoom(pathFromCsv, duration, coordenadasPosicionCsv, new Vector2(initialWidth, initialHeight), new Vector2(finalWidth, finalHeight), false);
            }
            else { Debug.LogError("BC (ZoomOut): Error parseo dims: " + string.Join("-", accionParts)); FondoAnimacionCompletada = true; currentFPAnimationType = null; }
        }
        else { Debug.LogError("BC (ZoomOut): Formato incorrecto: " + string.Join("-", accionParts)); FondoAnimacionCompletada = true; currentFPAnimationType = null; }
    }

    // ===== EJECUTA LA ACCIÓN 'ZOOM' (MODIFICANDO SIZEDELTA Y OPCIONALMENTE POSICIÓN) =====
    private void ExecuteZoom(string path, float dur, string coords, Vector2 sizeIni, Vector2 sizeFin, bool isZoomIn)
    {
        if (activePrincipal.sprite == null || !activePrincipal.gameObject.activeSelf) { FondoAnimacionCompletada = true; currentFPAnimationType = null; return; }
        Vector2 actualStartSize = activePrincipal.rectTransform.sizeDelta;
        if (!isZoomIn) { activePrincipal.rectTransform.sizeDelta = sizeIni; actualStartSize = sizeIni; }
        Vector3 startPos = activePrincipal.rectTransform.anchoredPosition3D;
        Vector3 targetPos = startPos; if (!string.IsNullOrEmpty(coords)) targetPos = ParseCoordenadas(coords, startPos);
        if (Vector2.Distance(actualStartSize, sizeFin) < 0.01f && Vector3.Distance(startPos, targetPos) < 0.01f)
        {
            currentFondoPrincipalSizeDelta = sizeFin; if (startPos != targetPos) originalFondoPrincipalPosition = targetPos;
            FondoAnimacionCompletada = true; currentFPAnimationType = null; return;
        }
        activePrincipal.gameObject.SetActive(true); targetSizeDeltaForCompletion = sizeFin; targetPositionForCompletion = targetPos;
        targetAlphaForCompletion = activePrincipal.color.a; isAnimatingSize = true;
        isAnimatingPosition = (startPos != targetPos); isAnimatingAlpha = false;
        activeFondoPrincipalAnimation = StartCoroutine(AnimateSizeAndPosition(activePrincipal, actualStartSize, sizeFin, startPos, targetPos, dur));
    }




    // Helper para cambiar el alfa de una imagen (útil para fades)
    private void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color currentColor = image.color;
        currentColor.a = Mathf.Clamp01(alpha);
        image.color = currentColor;
    }
    // ===== CORRUTINA PARA ANIMAR EL FADE (ALFA) DE UNA IMAGEN =====
    private IEnumerator AnimateFade(Image targetImage, float finalAlpha, float duration)
    {
        if (targetImage == null)
        {
            Debug.LogWarning("AnimateFade (Background): targetImage es null.");
            FondoAnimacionCompletada = true;
            activeFondoPrincipalAnimation = null;
            isAnimatingAlpha = false;
            yield break;
        }
        // Asegurar el estado activo ANTES de leer el alfa inicial si vamos a hacer fadeIn
        if (finalAlpha > 0 && !targetImage.gameObject.activeSelf)
        {
            SetImageAlpha(targetImage, 0f); // PONER A ALFA 0 ANTES DE ACTIVAR
            targetImage.gameObject.SetActive(true);
            Debug.Log(string.Format("[BG_AnimateFade] Activando {0} y poniendo alfa a 0 para fadeIn.", targetImage.gameObject.name));
        }
        else if (finalAlpha == 0 && !targetImage.gameObject.activeSelf)
        {
            // Si vamos a hacer fadeOut y ya está inactivo, no hay nada que hacer.
            Debug.Log(string.Format("[BG_AnimateFade] {0} ya está inactivo, no se requiere fadeOut.", targetImage.gameObject.name));
            FondoAnimacionCompletada = true;
            activeFondoPrincipalAnimation = null;
            isAnimatingAlpha = false;
            yield break;
        }

        Debug.Log(string.Format("[BG_AnimateFade] Para {0}: TargetAlpha={1}, Duration={2}. CurrentActive={3}, CurrentAlpha={4}",
                        targetImage.gameObject.name, finalAlpha, duration, targetImage.gameObject.activeSelf, targetImage.color.a));

        float startAlpha = targetImage.color.a; // Leer alfa DESPUÉS de posible SetActive y SetImageAlpha(0)
        float elapsedTime = 0f;

        if (duration <= 0f)
        {
            SetImageAlpha(targetImage, finalAlpha);
            if (finalAlpha == 0f && targetImage.gameObject.activeSelf)
            {
                targetImage.gameObject.SetActive(false);
            }
            else if (finalAlpha > 0f && !targetImage.gameObject.activeSelf) // Asegurar que esté activo si el alfa final es >0
            {
                targetImage.gameObject.SetActive(true);
            }
            FondoAnimacionCompletada = true;
            activeFondoPrincipalAnimation = null;
            isAnimatingAlpha = false;
            Debug.Log(string.Format("[BG_AnimateFade] {0} TERMINADO (duración cero). FinalAlpha={1}, FinalActive={2}",
                                    targetImage.gameObject.name, targetImage.color.a, targetImage.gameObject.activeSelf));
            yield break;
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, finalAlpha, elapsedTime / duration);
            SetImageAlpha(targetImage, newAlpha);
            yield return null;
        }

        SetImageAlpha(targetImage, finalAlpha);

        if (finalAlpha == 0f)
        {
            if (targetImage.gameObject.activeSelf)
            {
                targetImage.gameObject.SetActive(false);
            }
        }
        else
        {
            if (!targetImage.gameObject.activeSelf)
            {
                targetImage.gameObject.SetActive(true);
            }
        }
        Debug.Log(string.Format("[BG_AnimateFade] {0} TERMINADO. FinalAlpha={1}, FinalActive={2}",
                                targetImage.gameObject.name, targetImage.color.a, targetImage.gameObject.activeSelf));

        FondoAnimacionCompletada = true;
        activeFondoPrincipalAnimation = null;
        isAnimatingAlpha = false;
        currentFPAnimationType = null;
    }
    // ===== CORRUTINA PARA ANIMAR EL SHAKE DE UNA IMAGEN =====
    private IEnumerator AnimateShake(Image targetImage, float duration, bool shakeOnXAxis, Vector3 centerPosition)
    {
        if (duration <= 0f)
        {
            targetImage.rectTransform.anchoredPosition3D = centerPosition; // Asegurar posición final
            FondoAnimacionCompletada = true;
            activeFondoPrincipalAnimation = null;
            isAnimatingPosition = false;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float percentComplete = elapsedTime / duration;

            // Multiplicamos por (1 - percentComplete) para que el shake disminuya su intensidad hacia el final.
            float currentShakeAmplitude = shakeAmplitude * (1f - percentComplete);
            float offset = Mathf.Sin(elapsedTime * Mathf.PI * 2f * (shakeVibrations / duration)) * currentShakeAmplitude;

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

        targetImage.rectTransform.anchoredPosition3D = centerPosition;
        FondoAnimacionCompletada = true;
        activeFondoPrincipalAnimation = null;
        isAnimatingPosition = false;
    }
    // ===== CORRUTINA PARA ANIMAR EL SLIDE (MOVIMIENTO) DE UNA IMAGEN =====
    private IEnumerator AnimateSlide(Image targetImage, Vector3 destinationPosition, float duration)
    {
        Vector3 startPosition = targetImage.rectTransform.anchoredPosition3D;
        float elapsedTime = 0f;

        if (duration <= 0f)
        {
            targetImage.rectTransform.anchoredPosition3D = destinationPosition;
            originalFondoPrincipalPosition = destinationPosition; // Actualizar la posición base
            activeFondoPrincipalAnimation = null;
            yield break;
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            targetImage.rectTransform.anchoredPosition3D = Vector3.Lerp(startPosition, destinationPosition, elapsedTime / duration);
            yield return null;
        }

        targetImage.rectTransform.anchoredPosition3D = destinationPosition; // Asegurar posición final
        originalFondoPrincipalPosition = destinationPosition; // Actualizar la posición "original" o base después del slide
        FondoAnimacionCompletada = true;
        activeFondoPrincipalAnimation = null;
        isAnimatingPosition = false;
    }
    // ===== CORRUTINA PARA ANIMAR EL SIZEDELTA Y OPCIONALMENTE LA POSICIÓN (ZOOM + SLIDE) =====
    private IEnumerator AnimateSizeAndPosition(Image targetImage, Vector2 initialSize, Vector2 destinationSize, Vector3 initialPosition, Vector3 destinationPosition, float duration)
    {
        float elapsedTime = 0f;

        if (duration <= 0f)
        {
            targetImage.rectTransform.sizeDelta = destinationSize;
            targetImage.rectTransform.anchoredPosition3D = destinationPosition;
            if (isAnimatingPosition) originalFondoPrincipalPosition = destinationPosition; // Actualizar la posición base si se movió
            FondoAnimacionCompletada = true;
            activeFondoPrincipalAnimation = null;
            isAnimatingSize = false;
            isAnimatingPosition = false;
            yield break;
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            targetImage.rectTransform.sizeDelta = Vector2.Lerp(initialSize, destinationSize, progress);
            if (isAnimatingPosition) // Solo animar posición si se especificó un cambio
            {
                targetImage.rectTransform.anchoredPosition3D = Vector3.Lerp(initialPosition, destinationPosition, progress);
            }
            yield return null;
        }

        targetImage.rectTransform.sizeDelta = destinationSize;
        targetImage.rectTransform.anchoredPosition3D = destinationPosition;
        currentFondoPrincipalSizeDelta = destinationSize;
        if (isAnimatingPosition) originalFondoPrincipalPosition = destinationPosition;

        FondoAnimacionCompletada = true;
        activeFondoPrincipalAnimation = null;
        isAnimatingSize = false;
        isAnimatingPosition = false;
    }
    private void ExecuteInstantSwitch(string newPrincipalPath, string newPrincipalSizeCsv, string newPrincipalCoordsCsv)
    {
        // --- 1. Cargar y preparar el nuevo fondo (sin animación) ---
        if (string.IsNullOrEmpty(newPrincipalPath)) return; // Salida segura

        string pathWithoutExtension = newPrincipalPath;
        int dotIndex = newPrincipalPath.LastIndexOf('.');
        if (dotIndex > 0) pathWithoutExtension = newPrincipalPath.Substring(0, dotIndex);

        Sprite newPrincipalSprite = Resources.Load<Sprite>("images/" + pathWithoutExtension);
        if (newPrincipalSprite == null) return; // Salida segura

        activeSecundario.sprite = newPrincipalSprite;
        currentFondoSecundarioPath = pathWithoutExtension;
        activeSecundario.rectTransform.sizeDelta = ParseTamano(newPrincipalSizeCsv, tamanoEstandarFondo);
        activeSecundario.rectTransform.anchoredPosition3D = ParseCoordenadas(newPrincipalCoordsCsv, originalFondoPrincipalPosition);
        SetImageAlpha(activeSecundario, 1f);
        activeSecundario.gameObject.SetActive(true);

        // --- 2. Ocultar el fondo principal antiguo ---
        SetImageAlpha(activePrincipal, 0f);
        activePrincipal.gameObject.SetActive(false);

        // --- 3. Intercambiar roles y jerarquía ---
        Image tempImage = activePrincipal;
        activePrincipal = activeSecundario;
        activeSecundario = tempImage;
        rolesInvertidos = !rolesInvertidos;

        if (uiFondoPrincipal != null && uiFondoSecundario != null)
        {
            int principalIndex = uiFondoPrincipal.transform.GetSiblingIndex();
            int secundarioIndex = uiFondoSecundario.transform.GetSiblingIndex();
            uiFondoPrincipal.transform.SetSiblingIndex(secundarioIndex);
            uiFondoSecundario.transform.SetSiblingIndex(principalIndex);
        }

        // --- 4. Actualizar estado interno ---
        currentFondoPrincipalPath = pathWithoutExtension;
        currentFondoPrincipalSizeDelta = activePrincipal.rectTransform.sizeDelta;
        originalFondoPrincipalPosition = activePrincipal.rectTransform.anchoredPosition3D;
        currentFondoSecundarioPath = null;
        activeSecundario.sprite = null;

        Debug.Log("BC (Switch): Intercambio de fondos instantáneo completado.");
    }
    private IEnumerator ExecuteSwitchBackground(string newPrincipalPath, float fadeOutDuration, string newPrincipalSizeCsv, string newPrincipalCoordsCsv)
    {
        if (string.IsNullOrEmpty(newPrincipalPath))
        {
            Debug.LogError("BC (Switch): No se especificó una nueva imagen para el fondo principal.");
            FondoAnimacionCompletada = true;
            activeFondoPrincipalAnimation = null;
            yield break;
        }

        // Cargamos la nueva imagen en el Fondo Secundario
        string pathWithoutExtension = newPrincipalPath;
        int dotIndex = newPrincipalPath.LastIndexOf('.');
        if (dotIndex > 0 && dotIndex > newPrincipalPath.LastIndexOf('/'))
        {
            pathWithoutExtension = newPrincipalPath.Substring(0, dotIndex);
        }

        Sprite newPrincipalSprite = Resources.Load<Sprite>("images/" + pathWithoutExtension);
        if (newPrincipalSprite == null)
        {
            Debug.LogError("BC (Switch): No se pudo cargar el nuevo sprite para el fondo principal desde: images/" + pathWithoutExtension);
            FondoAnimacionCompletada = true;
            activeFondoPrincipalAnimation = null;
            yield break;
        }

        // Preparamos el Fondo Secundario para ser el nuevo Fondo Principal
        activeSecundario.sprite = newPrincipalSprite;
        currentFondoSecundarioPath = pathWithoutExtension; // Actualizamos el path del que es ahora el secundario (futuro principal)

        // Aplicamos tamaño y coordenadas al Fondo Secundario
        activeSecundario.rectTransform.sizeDelta = ParseTamano(newPrincipalSizeCsv, tamanoEstandarFondo);
        activeSecundario.rectTransform.anchoredPosition3D = ParseCoordenadas(newPrincipalCoordsCsv, originalFondoPrincipalPosition);

        // Hacemos que el Fondo Secundario sea instantáneamente visible (detrás del Principal)
        SetImageAlpha(activeSecundario, 1f);
        activeSecundario.gameObject.SetActive(true);

        // Animamos el FadeOut del Fondo Principal (el que está delante)
        float startAlpha = activePrincipal.color.a;
        float elapsedTime = 0f;

        if (fadeOutDuration <= 0f)
        {
            SetImageAlpha(activePrincipal, 0f);
            activePrincipal.gameObject.SetActive(false);
        }
        else
        {
            while (elapsedTime < fadeOutDuration)
            {
                elapsedTime += Time.deltaTime;
                SetImageAlpha(activePrincipal, Mathf.Lerp(startAlpha, 0f, elapsedTime / fadeOutDuration));
                yield return null;
            }
            SetImageAlpha(activePrincipal, 0f); // Asegurar el alfa final
            activePrincipal.gameObject.SetActive(false);
        }

        // Intercambiamos los roles de los fondos
        Image tempImage = activePrincipal;
        activePrincipal = activeSecundario;
        activeSecundario = tempImage;

        rolesInvertidos = !rolesInvertidos;

        if (uiFondoPrincipal != null && uiFondoSecundario != null)
        {
            // Obtenemos la posición actual de cada uno en la jerarquía del Canvas
            int principalIndex = uiFondoPrincipal.transform.GetSiblingIndex();
            int secundarioIndex = uiFondoSecundario.transform.GetSiblingIndex();

            // Establecemos el nuevo orden
            uiFondoPrincipal.transform.SetSiblingIndex(secundarioIndex);
            uiFondoSecundario.transform.SetSiblingIndex(principalIndex);

        }

        currentFondoPrincipalPath = pathWithoutExtension;
        currentFondoPrincipalSizeDelta = activePrincipal.rectTransform.sizeDelta;
        originalFondoPrincipalPosition = activePrincipal.rectTransform.anchoredPosition3D;

        currentFondoSecundarioPath = null;
        activeSecundario.sprite = null;

        // Actualizamos las propiedades internas para el nuevo Fondo Principal
        originalFondoPrincipalPosition = activePrincipal.rectTransform.anchoredPosition3D;
        currentFondoPrincipalSizeDelta = activePrincipal.rectTransform.sizeDelta;

        Debug.Log("BC (Switch): Intercambio de fondos completado. Nuevo Fondo Principal: " + activePrincipal.name);

        FondoAnimacionCompletada = true;
        activeFondoPrincipalAnimation = null;
        currentFPAnimationType = null; // Limpiamos el tipo de animación
    }







    // ===== PARSEA EL STRING DE COORDENADAS (EJ: "X Y Z" O "X Y") A VECTOR3 =====
    private Vector3 ParseCoordenadas(string coordenadasCsv, Vector3 defaultValue)
    {
        if (string.IsNullOrEmpty(coordenadasCsv))
        {
            return defaultValue;
        }

        // Definir un NumberFormatInfo que use la coma como separador decimal
        NumberFormatInfo nfi = new NumberFormatInfo();
        nfi.NumberDecimalSeparator = ",";
        nfi.NumberGroupSeparator = "."; // O lo que sea apropiado si tus números grandes usan separador de miles

        // Separar el string por espacios para obtener las partes X, Y, Z
        string[] parts = coordenadasCsv.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

        try
        {
            float x = defaultValue.x;
            float y = defaultValue.y;
            float z = defaultValue.z; // Para UI, Z usualmente es para orden de renderizado, no tanto posición espacial directa

            if (parts.Length >= 1)
            {
                x = float.Parse(parts[0].Trim(), nfi);
            }
            if (parts.Length >= 2)
            {
                y = float.Parse(parts[1].Trim(), nfi);
            }
            if (parts.Length >= 3)
            {
                // Para UI en un Canvas, Z de anchoredPosition3D puede afectar el orden de algunos elementos
                // o ser usado por shaders, pero no es una profundidad "real" como en 3D.
                z = float.Parse(parts[2].Trim(), nfi);
            }
            return new Vector3(x, y, z);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("BackgroundController: Error al parsear coordenadas '" + coordenadasCsv + "'. Error: " + e.Message + ". Usando valor por defecto: " + defaultValue);
            return defaultValue;
        }
    }
    // ===== PARSEA EL STRING DE TAMAÑO (EJ: "AnchoXAlto") A VECTOR2 =====
    private Vector2 ParseTamano(string tamanoCsv, Vector2 defaultValue)
    {
        if (string.IsNullOrEmpty(tamanoCsv))
        {
            return defaultValue;
        }

        string[] parts = tamanoCsv.ToLowerInvariant().Split('x'); // Separar por 'x'
        try
        {
            if (parts.Length == 2)
            {
                NumberFormatInfo nfi = new NumberFormatInfo { NumberDecimalSeparator = "," };
                float ancho = float.Parse(parts[0].Trim(), nfi);
                float alto = float.Parse(parts[1].Trim(), nfi);
                return new Vector2(ancho, alto);
            }
            else
            {
                Debug.LogWarning("BackgroundController: Formato de tamaño incorrecto '" + tamanoCsv + "'. Esperado: 'AnchoXAlto'. Usando valor por defecto: " + defaultValue);
                return defaultValue;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("BackgroundController: Error al parsear tamaño '" + tamanoCsv + "'. Error: " + e.Message + ". Usando valor por defecto: " + defaultValue);
            return defaultValue;
        }
    }

    // NUEVO MÉTODO:
    public void SnapToState(Vector2 finalSize, Vector3 finalPosition, bool applySize, bool applyPosition, float finalAlpha = 1f)
    {
        StopCurrentVisualAnimationOnly(); // Primero, detener cualquier animación en curso

        if (activePrincipal != null)
        {
            if (applySize)
            {
                // Debug.LogFormat("BGController.SnapToState: Aplicando Size {0}", finalSize);
                activePrincipal.rectTransform.sizeDelta = finalSize;
                currentFondoPrincipalSizeDelta = finalSize; // Actualizar estado interno
            }
            if (applyPosition)
            {
                // Debug.LogFormat("BGController.SnapToState: Aplicando Position {0}", finalPosition);
                activePrincipal.rectTransform.anchoredPosition3D = finalPosition;
                originalFondoPrincipalPosition = finalPosition; // Actualizar estado interno
            }

            SetImageAlpha(activePrincipal, finalAlpha);
            activePrincipal.gameObject.SetActive(finalAlpha > 0f); // Activar si es visible
                                                                    // Debug.Log("BGController.SnapToState: Estado final aplicado.");
        }
        FondoAnimacionCompletada = true; // La acción de snap es instantánea y completa.
    }

    // ===== CAMBIO: El método ahora es "inteligente" sobre las animaciones =====
    public BackgroundSaveData GetCurrentState()
    {
        BackgroundSaveData data = new BackgroundSaveData();
        data.rolesEstanInvertidos = this.rolesInvertidos;

        data.principalSiblingIndex = uiFondoPrincipal.transform.GetSiblingIndex();
        data.secundarioSiblingIndex = uiFondoSecundario.transform.GetSiblingIndex();

        // --- Guardamos estado del Fondo Principal ---
        if (activePrincipal != null && activePrincipal.gameObject.activeInHierarchy && activePrincipal.sprite != null)
        {
            data.principalIsActive = true;
            data.principalPath = currentFondoPrincipalPath;
            data.principalAlpha = activePrincipal.color.a;

            // --- LÓGICA DE GUARDADO CORREGIDA Y SEPARADA ---

            // Primero, decidimos qué POSICIÓN guardar.
            if (IsFondoAnimando && isAnimatingPosition)
            {
                // Si se está moviendo (slide o zoom con movimiento), guardamos la posición de destino.
                data.principalPosX = targetPositionForCompletion.x;
                data.principalPosY = targetPositionForCompletion.y;
                data.principalPosZ = targetPositionForCompletion.z;
            }
            else
            {
                // Si no, guardamos la posición actual.
                data.principalPosX = activePrincipal.rectTransform.anchoredPosition3D.x;
                data.principalPosY = activePrincipal.rectTransform.anchoredPosition3D.y;
                data.principalPosZ = activePrincipal.rectTransform.anchoredPosition3D.z;
            }

            // Segundo, decidimos qué TAMAÑO guardar.
            if (IsFondoAnimando && isAnimatingSize)
            {
                // Si está cambiando de tamaño (zoom), guardamos el tamaño de destino.
                data.principalSizeX = targetSizeDeltaForCompletion.x;
                data.principalSizeY = targetSizeDeltaForCompletion.y;
            }
            else
            {
                // Si no está cambiando de tamaño (incluso durante un slide), guardamos el tamaño actual.
                data.principalSizeX = activePrincipal.rectTransform.sizeDelta.x;
                data.principalSizeY = activePrincipal.rectTransform.sizeDelta.y;
            }
        }
        else
        {
            data.principalIsActive = false;
        }

        // --- Guardamos estado del Fondo Secundario (este no cambia) ---
        if (activeSecundario != null && activeSecundario.gameObject.activeInHierarchy && activeSecundario.sprite != null)
        {
            data.secundarioIsActive = true;
            data.secundarioPath = currentFondoSecundarioPath;
            data.secundarioPosX = activeSecundario.rectTransform.anchoredPosition3D.x;
            data.secundarioPosY = activeSecundario.rectTransform.anchoredPosition3D.y;
            data.secundarioPosZ = activeSecundario.rectTransform.anchoredPosition3D.z;
            data.secundarioSizeX = activeSecundario.rectTransform.sizeDelta.x;
            data.secundarioSizeY = activeSecundario.rectTransform.sizeDelta.y;
        }
        else
        {
            data.secundarioIsActive = false;
        }

        return data;
    }

    // ===== CAMBIO: El método ahora es una CORRUTINA para forzar el refresco visual =====
    public IEnumerator RestoreState(BackgroundSaveData data)
    {
        StopCurrentVisualAnimationOnly(); 

        this.rolesInvertidos = data.rolesEstanInvertidos;
        
        if (rolesInvertidos)
        {
            activePrincipal = uiFondoSecundario;
            activeSecundario = uiFondoPrincipal;
            Debug.Log("RestoreState: Roles de fondo estaban invertidos. Restaurando intercambio.");
        }
        else
        {
            // Si no, usamos la configuración por defecto.
            activePrincipal = uiFondoPrincipal;
            activeSecundario = uiFondoSecundario;
            Debug.Log("RestoreState: Roles de fondo estaban en estado normal.");
        }

        uiFondoPrincipal.transform.SetSiblingIndex(data.principalSiblingIndex);
        uiFondoSecundario.transform.SetSiblingIndex(data.secundarioSiblingIndex);
        
        if (data.principalIsActive)
        {
            currentFondoPrincipalPath = data.principalPath;
            Sprite loadedSprite = Resources.Load<Sprite>("images/" + data.principalPath);
            if (loadedSprite != null)
            {
                activePrincipal.sprite = loadedSprite;
                Vector3 newPos = new Vector3(data.principalPosX, data.principalPosY, data.principalPosZ);
                Vector2 newSize = new Vector2(data.principalSizeX, data.principalSizeY);
                // SnapToState lo pone todo en su sitio, pero aún no lo redibuja correctamente.
                SnapToState(newSize, newPos, true, true, data.principalAlpha);
            }
            else
            {
                activePrincipal.gameObject.SetActive(false);
            }
        }
        else
        {
            activePrincipal.sprite = null;
            activePrincipal.gameObject.SetActive(false);
        }

        // --- Restauramos Fondo Secundario ---
        if (data.secundarioIsActive)
        {
            currentFondoSecundarioPath = data.secundarioPath;
            Sprite loadedSprite = Resources.Load<Sprite>("images/" + data.secundarioPath);
            if (loadedSprite != null)
            {
                activeSecundario.sprite = loadedSprite;
                activeSecundario.rectTransform.anchoredPosition3D = new Vector3(data.secundarioPosX, data.secundarioPosY, data.secundarioPosZ);
                activeSecundario.rectTransform.sizeDelta = new Vector2(data.secundarioSizeX, data.secundarioSizeY);
                SetImageAlpha(activeSecundario, 1f);
                activeSecundario.gameObject.SetActive(true);
            }
             else
            {
                activeSecundario.gameObject.SetActive(false);
            }
        }
        else
        {
            activeSecundario.sprite = null;
            activeSecundario.gameObject.SetActive(false);
        }
        yield return null;

    }
}