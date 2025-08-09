using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Text;

public class TypewriterDialogueUI : MonoBehaviour
{
    [Header("Elementos de la UI")]
    [Tooltip("El GameObject padre que contiene toda la caja de diálogo.")]
    public GameObject dialogueBoxContainer;
    [Tooltip("El componente Text para el nombre del personaje.")]
    public Text characterNameText;
    [Tooltip("El ÚNICO componente Text para mostrar el diálogo completo.")]
    public Text dialogueText;

    [Header("Componentes Adicionales")]
    [Tooltip("Referencia opcional al script que cambia el color del nombre.")]
    public NameColorChanger nameColorChanger;

    public bool IsTyping { get; private set; }
    public bool TextoCompletado { get { return !IsTyping; } }
    public bool IsDisplayingSomething { get; private set; }


    private Coroutine _typewriterCoroutine;
    private string _fullTextForCompletion;
    private string _nameForCompletion;

    void Start()
    {
        if (dialogueBoxContainer == null || characterNameText == null || dialogueText == null)
        {
            Debug.LogError("TypewriterDialogueUI: Faltan referencias a elementos de la UI en el Inspector.", gameObject);
            this.enabled = false;
            return;
        }
        if (nameColorChanger == null)
        {
            nameColorChanger = characterNameText.GetComponent<NameColorChanger>();
        }
        
        IsTyping = false;
        IsDisplayingSomething = false;
        dialogueBoxContainer.SetActive(false);
    }

    public void ShowDialogue(string characterName, string fullText, float characterDelay)
    {
		fullText = fullText.Replace(" \\n ", "\n");

        if (_typewriterCoroutine != null)
		{
			StopCoroutine(_typewriterCoroutine);
		}
        
        _fullTextForCompletion = fullText;
        _nameForCompletion = characterName;

        if (string.IsNullOrEmpty(characterName) && string.IsNullOrEmpty(fullText))
        {
            HideDialogueBox();
            return;
        }

        IsDisplayingSomething = true;
        dialogueBoxContainer.SetActive(true);
        characterNameText.text = characterName;
        dialogueText.text = ""; 

        if (nameColorChanger != null)
        {
            nameColorChanger.UpdateCharacterColor(characterName);
        }

        _typewriterCoroutine = StartCoroutine(TypeTextCoroutine("\n" + fullText, characterDelay));
    }

    public void HideDialogueBox()
    {
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }
        IsTyping = false;
        IsDisplayingSomething = false;
        dialogueBoxContainer.SetActive(false);
    }

    public void CompleteTypewriter()
    {
        if (!IsTyping) return;

        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        dialogueText.text = "\n" + _fullTextForCompletion;
        IsTyping = false;
    }
    

    private IEnumerator TypeTextCoroutine(string textToType, float delay)
    {
        IsTyping = true;
        if (delay <= 0)
        {
            dialogueText.text = textToType;
            IsTyping = false;
            yield break;
        }
        
        StringBuilder stringBuilder = new StringBuilder();

        foreach (char c in textToType)
        {
            stringBuilder.Append(c);
            dialogueText.text = stringBuilder.ToString();
            yield return new WaitForSeconds(delay);
        }
        
        _typewriterCoroutine = null;
        IsTyping = false;
    }
}