using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class DecisionManager : MonoBehaviour
{
    public GameManager gameManager;
    public RectTransform container;
    public CanvasGroup containerCG;
    public RectTransform[] optionRects; 
    public CanvasGroup[] optionCGs;
    public Text[] optionTexts;
    public float fadeInDuration = 0.2f;

    private List<DecisionOption> currentOptions = new List<DecisionOption>();
    private int currentIndex = -1;
    public bool isDecisionActive = false;

    void Awake()
    {
        container.gameObject.SetActive(false);
        if (containerCG == null) containerCG = container.GetComponent<CanvasGroup>();
    }

    public void StartDecision(DecisionNode node)
    {
        isDecisionActive = true;
        currentOptions.Clear();
        currentIndex = -1;
        if (!string.IsNullOrEmpty(node.opt1.text)) currentOptions.Add(node.opt1);
        if (!string.IsNullOrEmpty(node.opt2.text)) currentOptions.Add(node.opt2);
        if (!string.IsNullOrEmpty(node.opt3.text)) currentOptions.Add(node.opt3);
        if (!string.IsNullOrEmpty(node.opt4.text)) currentOptions.Add(node.opt4);
        if (!string.IsNullOrEmpty(node.opt5.text)) currentOptions.Add(node.opt5);
        SetupUI();
        containerCG.alpha = 0;
        container.gameObject.SetActive(true);
        StartCoroutine(UIAnimationHelper.Fade(containerCG, 0, 1, fadeInDuration));
    }

    public void RestoreDecision(DecisionNode node)
    {
        StopAllCoroutines();
        StartCoroutine(RestoreDecisionCoroutine(node));
    }

    public IEnumerator RestoreDecisionCoroutine(DecisionNode node)
    {
        isDecisionActive = true;
        currentOptions.Clear();
        currentIndex = -1;
        if (!string.IsNullOrEmpty(node.opt1.text)) currentOptions.Add(node.opt1);
        if (!string.IsNullOrEmpty(node.opt2.text)) currentOptions.Add(node.opt2);
        if (!string.IsNullOrEmpty(node.opt3.text)) currentOptions.Add(node.opt3);
        if (!string.IsNullOrEmpty(node.opt4.text)) currentOptions.Add(node.opt4);
        if (!string.IsNullOrEmpty(node.opt5.text)) currentOptions.Add(node.opt5);
        
        SetupUI();
        
        // Empezamos invisibles para no competir con la carga de AssetBundles
        containerCG.alpha = 0;
        container.gameObject.SetActive(true);
        
        // Esperamos un par de frames para que la Old 3DS se asiente tras el RestoreSnapshot
        yield return null;
        yield return null;
        
        // Fundido suave sincronizado con la salida del telón
        yield return StartCoroutine(UIAnimationHelper.Fade(containerCG, 0, 1, fadeInDuration));
    }

    public void HideDecision()
    {
        StopAllCoroutines();
        isDecisionActive = false;
        container.gameObject.SetActive(false);
    }

    private void SetupUI()
    {
        int count = currentOptions.Count;
        for (int i = 0; i < optionRects.Length; i++) 
        {
            if (i < count) 
            {
                optionRects[i].gameObject.SetActive(true);
                optionTexts[i].text = currentOptions[i].text;
                optionCGs[i].alpha = 0.5f;
                
                if (count == 2) 
                    optionRects[i].anchoredPosition = new Vector2(0, i == 0 ? 15 : -15);
                else if (count == 3) 
                    optionRects[i].anchoredPosition = new Vector2(0, i == 0 ? 30 : (i == 1 ? 0 : -30));
            } 
            else 
            {
                optionRects[i].gameObject.SetActive(false);
            }
        }
    }

    void Update()
    {
        if (!isDecisionActive) return;
        if (gameManager.IsGamePaused || gameManager.inputLock) return;

        bool up = Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.DownArrow);
        bool a = Input.GetKeyDown(KeyCode.A);

        #if UNITY_N3DS && !UNITY_EDITOR
        up = UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Up);
        down = UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.Down);
        a = UnityEngine.N3DS.GamePad.GetButtonTrigger(N3dsButton.A);
        #endif

        if (up) ChangeSelection(-1);
        else if (down) ChangeSelection(1);
        else if (a) ConfirmSelection();
    }
    
    private void ChangeSelection(int direction)
    {
        if (currentIndex == -1) 
        {
            currentIndex = (direction > 0) ? 0 : currentOptions.Count - 1;
        }
        else 
        { 
            currentIndex += direction; 
            
            if (currentIndex >= currentOptions.Count) 
                currentIndex = 0; 
            
            if (currentIndex < 0) 
                currentIndex = currentOptions.Count - 1; 
        }
        
        for (int i = 0; i < currentOptions.Count; i++) 
        {
            optionCGs[i].alpha = (i == currentIndex) ? 1.0f : 0.5f;
        }
    }

    private void ConfirmSelection()
    {
        if (currentIndex == -1) return;
        GlobalVariablesManager.Instance.ApplyAction(currentOptions[currentIndex].actionCode);
        isDecisionActive = false;
        container.gameObject.SetActive(false);
        gameManager.OnDecisionMade(currentOptions[currentIndex].nextIdHash);
    }

    public void ClearReferences()
    {
        currentOptions.Clear();
        if (optionTexts != null)
        {
            for (int i = 0; i < optionTexts.Length; i++)
                if (optionTexts[i] != null) optionTexts[i].text = "";
        }
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }
}