using UnityEngine;
using System.Collections; 

public class ControlsGameController : MonoBehaviour
{
    [Header("Referencias a todos los Controladores")]
    public GameController gameController;
    public DialogueUIManager dialogueManager;
    public BigDialogueController bigDialogueController;
    public BackgroundController backgroundController;

    // ========= SPRITES =========
    public SpritesController spritesController;
    public FacesController facesController;

    public HeadsController headsController;
    public LeftArmController leftArmController;
    public RightArmController rightArmController;

    public EventsController eventsController;
    private bool advanceButtonPressed = false;
	
    public void OnAdvanceButtonPressed()
    {
        if (!advanceButtonPressed)
        {
            StartCoroutine(SetAdvanceFlagForOneFrame());
        }
    }
    private IEnumerator SetAdvanceFlagForOneFrame()
    {
        // Activamos la bandera
        advanceButtonPressed = true;
        
        // Esperamos hasta el final del fotograma actual
        yield return new WaitForEndOfFrame();
        
        // Desactivamos la bandera, dejándola lista para el siguiente clic
        advanceButtonPressed = false;
    }

	void Update()
    {
        if (gameController == null || eventsController == null || dialogueManager == null) return;
        
        bool advancedThisFrame = false;

        if (gameController.currentGuionNode != null && eventsController != null)
        {
            string tipoNodoActual = gameController.currentGuionNode.ContainsKey("TipoNodo") ? gameController.currentGuionNode["TipoNodo"] : "";
            if (string.IsNullOrEmpty(tipoNodoActual)) tipoNodoActual = "Dialogo";

            if (tipoNodoActual.Equals("Evento", System.StringComparison.OrdinalIgnoreCase))
            {
                string eventoDefinidoEnEventsCSV = eventsController.GetCurrentEventTypeFromEventsCSV();
                bool autoFlagEvento = eventsController.GetCurrentEventAuto();
                bool inicialFlagEvento = eventsController.GetCurrentEventInicial();

                if (autoFlagEvento && !inicialFlagEvento)
                {
                    if (gameController.AreAllTasksCompleted())
                    {
                        gameController.AdvanceToNextNode();
                        advancedThisFrame = true;
                    }
                }
                else if (eventoDefinidoEnEventsCSV == "auto")
                {
                    if (gameController.autoAdvanceEnabledForCurrentNode && gameController.AreAllTasksCompleted())
                    {
                        gameController.AdvanceToNextNode();
                        advancedThisFrame = true;
                    }
                }
            }
        }

        if (!advancedThisFrame && (Input.GetKeyDown(KeyCode.A) || advanceButtonPressed))
        {
            if (eventsController != null)
            {
                eventsController.CompleteAllForzablePersistentEvents();
            }
            bool PudoCompletarAlgoConEstaPulsacion = false;
            bool seCompletoUnEventoForzableDelNodoActualEsteTurno = false;

            string tipoNodoActual = gameController.currentGuionNode != null && gameController.currentGuionNode.ContainsKey("TipoNodo") ? gameController.currentGuionNode["TipoNodo"] : "";
            if (string.IsNullOrEmpty(tipoNodoActual)) tipoNodoActual = "Dialogo";

            bool esNodoTipoEvento = tipoNodoActual.Equals("Evento", System.StringComparison.OrdinalIgnoreCase);
            bool eventoActualDelNodoEsForzable = false;
            bool eventoActualDelNodoEsAuto = false;

            if (bigDialogueController != null && bigDialogueController.IsFadingBox)
            {
                bigDialogueController.CompleteBoxFade();
                PudoCompletarAlgoConEstaPulsacion = true;
            }

            if (esNodoTipoEvento && eventsController != null)
            {
                eventoActualDelNodoEsForzable = eventsController.GetCurrentEventForzado();
                eventoActualDelNodoEsAuto = eventsController.GetCurrentEventAuto();
            }

            if (esNodoTipoEvento && eventsController != null && eventsController.IsEventoAnimando && eventoActualDelNodoEsForzable)
            {
                eventsController.CompleteCurrentEvent();
                PudoCompletarAlgoConEstaPulsacion = true;
                seCompletoUnEventoForzableDelNodoActualEsteTurno = true;
                if (eventoActualDelNodoEsAuto) { gameController.AdvanceToNextNode(); return; }
            }

            if (bigDialogueController != null && bigDialogueController.IsBigBoxModeActive && bigDialogueController.IsAnimating)
            {
                bigDialogueController.CompleteAnimation();
                PudoCompletarAlgoConEstaPulsacion = true;
            }
            else if (dialogueManager != null && dialogueManager.IsDisplayingSomething)
            {
                dialogueManager.CompleteTypewriter();
                PudoCompletarAlgoConEstaPulsacion = true;
            }

            if (backgroundController != null && backgroundController.IsFondoAnimando)
            {
                string currentBgAnimType = backgroundController.GetCurrentFPAnimationType();
                backgroundController.CompleteFondoAnimacion();
                if (currentBgAnimType != "zoomin" && currentBgAnimType != "zoomout" && currentBgAnimType != "slide")
                {
                    PudoCompletarAlgoConEstaPulsacion = true;
                }
            }
            if (spritesController != null && spritesController.IsSpriteAnimando)
            {
                spritesController.CompleteAllSpriteAnimations();
                PudoCompletarAlgoConEstaPulsacion = true;
            }
            if (facesController != null && facesController.IsSpriteAnimando)
            {
                facesController.CompleteAllSpriteAnimations();
                PudoCompletarAlgoConEstaPulsacion = true;
            }
            if (headsController != null && headsController.IsSpriteAnimando)
            {
                headsController.CompleteAllSpriteAnimations();
                PudoCompletarAlgoConEstaPulsacion = true;
            }
            if (leftArmController != null && leftArmController.IsSpriteAnimando)
            {
                leftArmController.CompleteAllSpriteAnimations();
                PudoCompletarAlgoConEstaPulsacion = true;
            }
            if (rightArmController != null && rightArmController.IsSpriteAnimando)
            {
                rightArmController.CompleteAllSpriteAnimations();
                PudoCompletarAlgoConEstaPulsacion = true;
            }

            if (!PudoCompletarAlgoConEstaPulsacion || (seCompletoUnEventoForzableDelNodoActualEsteTurno && !eventoActualDelNodoEsAuto))
            {
                bool puedeAvanzar = false;
                if (esNodoTipoEvento)
                {
                    if (!eventoActualDelNodoEsAuto)
                    {
                        if (gameController.AreAllTasksCompleted()) puedeAvanzar = true;
                    }
                    else if (eventoActualDelNodoEsAuto && gameController.AreAllTasksCompleted())
                    {
                        puedeAvanzar = true;
                    }
                }
                else
                {
                    if (gameController.AreAllTasksCompleted()) puedeAvanzar = true;
                }
                if (puedeAvanzar) gameController.AdvanceToNextNode();
            }
            //Menos mal no existe QA aquí x,d
        }
    }
}